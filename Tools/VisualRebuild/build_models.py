"""Run with Blender 4.3: original architectural meshes with real openings, UVs, bevels and LODs."""
import bpy, math, random, json, pathlib, sys, hashlib
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2]
ART=ROOT/'Assets/_Silence/Art/VisualRebuild'; OUT=ART/'Models';OUT.mkdir(exist_ok=True)
TEX=ART/'Textures'
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
random.seed(27092026)
materials={}
map_names={'Wood':'AgedWood','Plaster':'Plaster','Brick':'Brick','Roof':'Roof','Rust':'Rust','Stone':'Stone','Bark':'Bark','Leaf':'PineNeedles','GrassCard':'GrassAtlas','Fern':'FernAtlas','Metal':'Rust','Glass':None,'Fabric':'Plaster','Bulb':None}
for name, texture in map_names.items():
 m=bpy.data.materials.new(name);m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links;p=nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.8
 if name=='Bulb':
  p.inputs['Base Color'].default_value=(.6,.38,.17,1);p.inputs['Emission Color'].default_value=(1,.5,.18,1);p.inputs['Emission Strength'].default_value=2
 elif name=='Glass':
  p.inputs['Base Color'].default_value=(.14,.2,.19,1);p.inputs['Transmission Weight'].default_value=.75;p.inputs['Roughness'].default_value=.18;p.inputs['IOR'].default_value=1.46
 elif name in ['Leaf','GrassCard','Fern']:
  image=bpy.data.images.load(str(TEX/f'{texture}.png'));node=nodes.new('ShaderNodeTexImage');node.image=image
  links.new(node.outputs['Color'],p.inputs['Base Color']);links.new(node.outputs['Alpha'],p.inputs['Alpha']);m.surface_render_method='DITHERED';m.use_transparent_shadow=True
 else:
  for suffix in ['BaseColor','Normal','Mask']:
   image=bpy.data.images.load(str(TEX/f'{texture}_{suffix}.png'),check_existing=True)
   if suffix!='BaseColor':image.colorspace_settings.name='Non-Color'
   node=nodes.new('ShaderNodeTexImage');node.image=image
   if suffix=='BaseColor':links.new(node.outputs['Color'],p.inputs['Base Color'])
   elif suffix=='Normal':
    normal=nodes.new('ShaderNodeNormalMap');normal.inputs['Strength'].default_value=.45;links.new(node.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],p.inputs['Normal'])
   else:
    invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1;links.new(node.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],p.inputs['Roughness'])
 materials[name]=m

def V(p): return Vector((p[0],-p[2],p[1]))
current=[]
def uv_mesh(obj, material):
 obj.data.materials.append(materials[material]);uv=obj.data.uv_layers.new(name='UVMap')
 for poly in obj.data.polygons:
  dominant=max(range(3),key=lambda i:abs(poly.normal[i]))
  for loop in poly.loop_indices:
   co=obj.data.vertices[obj.data.loops[loop].vertex_index].co+obj.location
   if dominant==2: value=(co.y,co.x)
   elif dominant==1:value=(co.z,co.x)
   else:value=(co.z,co.y)
   uv.data[loop].uv=value

def box(name,p,size,mat='Wood', bevel=.006,rot=(0,0,0)):
 # Build vertices directly so exported meshes have applied metric transforms.
 x,y,z=(s/2 for s in size);verts=[V((a,b,c)) for a,b,c in [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]]
 faces=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);obj.location=V(p)
 obj.rotation_euler=(math.radians(rot[0]),-math.radians(rot[2]),math.radians(rot[1]));uv_mesh(obj,mat)
 if bevel:
  mod=obj.modifiers.new('Worn edge radius','BEVEL');mod.width=bevel;mod.segments=2
 current.append(obj);return obj

def rod(name,a,b,r,mat='Rust',sides=12):
 a,b=V(a),V(b);d=b-a;t=d.normalized();u=t.cross(Vector((0,0,1)))
 if u.length<.1:u=t.cross(Vector((0,1,0)))
 u.normalize();v=t.cross(u);verts=[]
 for p in [a,b]:
  for i in range(sides):verts.append(p+(math.cos(i*math.tau/sides)*u+math.sin(i*math.tau/sides)*v)*r)
 faces=[]
 for i in range(sides):faces.append((i,(i+1)%sides,(i+1)%sides+sides,i+sides))
 faces.extend([tuple(reversed(range(sides))),tuple(range(sides,sides*2))]);mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);uv_mesh(obj,mat)
 for face in mesh.polygons:face.use_smooth=len(face.vertices)==4
 current.append(obj);return obj

def curved(name,points,r,mat='Rust'):
 for a,b in zip(points,points[1:]):rod(name,a,b,r,mat)

def merge(name,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for obj in objects:obj.select_set(True)
 bpy.context.view_layer.objects.active=objects[0]
 for obj in objects:
  bpy.context.view_layer.objects.active=obj
  for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();obj=objects[0];obj.name=name
 # Put all authored asset pivots at origin, independent of the first mesh piece.
 bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR');return obj

def wall(name,axis,plane,length,height,openings,wood=True,lod=0):
 # Grid subtraction gives actual window/door apertures; no painted fake windows.
 boundaries=sorted(set([-length/2,length/2]+[v for op in openings for v in op[:2]]))
 levels=sorted(set([0,height]+[v for op in openings for v in op[2:]]))
 for a,b in zip(boundaries,boundaries[1:]):
  for c,d in zip(levels,levels[1:]):
   if any(op[0]<(a+b)/2<op[1] and op[2]<(c+d)/2<op[3] for op in openings):continue
   p=((a+b)/2,(c+d)/2,plane) if axis=='X' else (plane,(c+d)/2,(a+b)/2)
   size=(b-a,d-c,.22) if axis=='X' else (.22,d-c,b-a)
   box(name+' structural wall',p,size,'Wood' if wood and lod==2 else 'Plaster',.004 if lod==0 else 0)
 # Individual overlapping weatherboards have thickness, seams and irregular ends.
 if wood and lod<2:
  rows=int(height/.19)
  for row in range(rows):
   low=row*.19;high=min(height,low+.205)
   segments=[(-length/2,length/2)]
   for op in openings:
    if low>=op[3] or high<=op[2]:continue
    segments=[seg for start,end in segments for seg in [(start,min(end,op[0])),(max(start,op[1]),end)] if seg[1]-seg[0]>.01]
   for a,b in segments:
    p=((a+b)/2,(low+high)/2,plane-math.copysign(.14,-plane)) if axis=='X' else (plane-math.copysign(.14,-plane),(low+high)/2,(a+b)/2)
    size=(b-a-.006,high-low,.045) if axis=='X' else (.045,high-low,b-a-.006)
    box(name+' weatherboard',p,size,'Wood',.003 if lod==0 else 0)

def window(cx,cz,width=1.4,bottom=.9,height=1.4,lod=0):
 cy=bottom+height/2
 for dx in [-width/2,width/2]:box('Window jamb',(cx+dx,cy,cz-.07),(.10,height+.2,.3),'Wood')
 for dy in [-height/2,height/2]:box('Window head sill',(cx,cy+dy,cz-.08),(width+.2,.11,.34),'Wood')
 box('Window central mullion',(cx,cy,cz-.1),(.045,height,.08),'Wood')
 box('Window crossbar',(cx,cy,cz-.1),(width,.04,.08),'Wood')
 if lod<2:
  box('Glass pane',(cx,cy,cz-.05),(width-.07,height-.07,.009),'Glass',0)
  for side in [-1,1]:
   box('Storm shutter',(cx+side*(width/2+.25),cy,cz-.1),(.32,height+.15,.07),'Wood',.006)
   for h in [-.42,.42]:box('Shutter brace',(cx+side*(width/2+.25),cy+h,cz-.15),(.36,.07,.04),'Rust',.003)
  box('Drip edge',(cx,cy-height/2-.10,cz-.2),(width+.22,.028,.38),'Rust',.003,(-6,0,0))

def roof(width,depth,eave=4.4,rise=1.55,lod=0):
 pitch=math.degrees(math.atan2(rise,width/2));slant=math.hypot(width/2,rise)
 for side in [-1,1]:
  box('Roof deck',(side*width/4,eave+rise/2,0),(slant,.13,depth),'Roof',.003, (0,0,-side*pitch))
  if lod==0:
   rows=9;cols=int(depth/.46)
   for r in range(rows):
    t=(r+.5)/rows;xx=side*(width/2)*(1-t); yy=eave+rise*t+.08
    for c in range(cols):
     zz=-depth/2+(c+.5)*depth/cols+(.08 if r%2 else 0)
     box('Offset roof shingle',(xx,yy,zz),(slant/rows+.07,.024,depth/cols-.013),'Roof',.004,(0,0,-side*pitch))
  for z in [-depth/2,depth/2]:
   rod('Rake flashing',(side*width/2,eave+.03,z),(0,eave+rise+.03,z),.045,'Rust',8)
  box('Fascia',(side*width/2,eave-.06,0),(.12,.25,depth),'Wood')
  curved('Gutter',[(side*(width/2+.04),eave-.16,-depth/2),(side*(width/2+.04),eave-.16,depth/2)],.055)
  if lod<2:curved('Downspout',[(side*(width/2+.04),eave-.16,depth/2-.15),(side*(width/2+.04),.35,depth/2-.15),(side*(width/2+.3),.17,depth/2-.15)],.045)
 box('Ridge cap',(0,eave+rise+.11,0),(.24,.12,depth+.08),'Rust',.02)
 # Gables follow a triangle; they are not giant uncut wall cubes.
 for z in [-depth/2+.2,depth/2-.2]:
  for i in range(14):
   xx=-width/2+(i+.5)*width/14;h=rise*(1-abs(xx)/(width/2))
   if h>.05:box('Gable board',(xx,eave+h/2,z),(width/14-.009,h,.08),'Wood',.004 if lod==0 else 0)

def farmhouse(lod):
 global current;current=[]
 # Rim foundation and timber floor. Front doorstep remains at the original playable height.
 for z in [-3.9,3.9]:box('Brick foundation',(0,-.12,z),(8,.35,.30),'Brick')
 for xx in [-3.9,3.9]:box('Brick foundation',(xx,-.12,0),(.30,.35,8),'Brick')
 for i in range(21):box('Floorboard',(-3.8+i*.38,.025,0),(.365,.05,7.75),'Wood',.003 if lod==0 else 0)
 front=[(-.8,.8,0,2.3),(-3.25,-1.85,.9,2.3),(1.85,3.25,.9,2.3)]
 wall('Front','X',-4,8,4.4,front,lod=lod);wall('Back','X',4,8,4.4,[(-.7,.7,1.05,2.35)],lod=lod)
 for side in [-1,1]:wall('Side','Z',side*4,8,4.4,[],lod=lod)
 for cx in [-2.55,2.55]:window(cx,-4,lod=lod)
 window(0,4,lod=lod)
 for xx in [-4.08,4.08]:
  for z in [-4.08,4.08]:box('Corner trim',(xx,2.2,z),(.16,4.4,.16),'Wood')
 for xx in [-.86,.86]:box('Door frame',(xx,1.16,-4.12),(.12,2.4,.3),'Wood')
 box('Door header',(0,2.36,-4.12),(1.86,.13,.32),'Wood')
 box('Threshold',(0,.06,-4.15),(1.7,.06,.35),'Wood')
 roof(8.8,8.9,lod=lod)
 box('Chimney',(2.5,5.1,2.2),(.6,2.5,.65),'Brick',.012)
 box('Chimney cap',(2.5,6.37,2.2),(.8,.14,.85),'Stone',.02)
 if lod<2:
  for i in range(10):
   h=(i+1)*.18;box('Stair nosing',(2,h-.02,-2+i*.4),(1.8,.065,.40),'Wood')
  # Existing loft geometry is respected so navigation/physics stays at the known baseline.
  box('Loft boards',(0,1.7,2.8),(7.5,.16,2),'Wood')
  for xx in [-3.6,3.6]:box('Ceiling beam',(xx,4.15,0),(.18,.24,7.8),'Wood')
  for z in [-3,0,3]:box('Cross beam',(0,4.14,z),(8,.25,.18),'Wood')
  # Motivated practical lamp, cable and hardware, not an invisible point light.
  box('Lamp wall plate',(-1.2,2.2,-3.84),(.18,.25,.04),'Rust')
  rod('Lamp bracket',(-1.2,2.25,-3.82),(-1.2,2.25,-3.5),.025)
  box('Lamp housing',(-1.2,2.05,-3.5),(.25,.35,.21),'Rust',.025)
  box('Lamp lens',(-1.2,2.05,-3.38),(.18,.25,.018),'Bulb',0)
  curved('Electrical conduit',[(-1.2,2.2,-3.82),(-1.2,3.8,-3.82),(3.6,3.8,-3.82),(3.6,.5,-3.82)],.014)
  for z in [-3.6,-1.5,.5,2.5]:box('Interior baseboard',(0,.12,z if abs(z)>3 else 3.86),(7.6,.12,.05),'Wood')
 return merge(f'Farmhouse_LOD{lod}',current)

def barn(lod):
 global current;current=[]
 wall('Barn front','X',-5,8,4.8,[(-1.4,1.4,0,3)],lod=lod)
 wall('Barn back','X',5,8,4.8,[],lod=lod)
 for side in [-1,1]:wall('Barn side','Z',side*4,10,4.8,[],lod=lod)
 roof(8.8,10.8,4.8,1.8,lod)
 for xx in [-1.5,1.5]:box('Barn jamb',(xx,1.5,-5.1),(.18,3.2,.24),'Wood')
 if lod<2:
  for z in [-4,0,4]:
   box('Barn truss',(0,4.4,z),(8,.22,.2),'Wood')
   for side in [-1,1]:rod('Diagonal brace',(side*3.6,3,z),(side*2.4,4.4,z),.09,'Wood',8)
 return merge(f'Barn_LOD{lod}',current)

def door():
 global current;current=[]
 for i in range(8):box('Door plank',((i+.5)*.2,1.11,0),(.194,2.22,.10),'Wood',.007)
 for y in [.26,1.96]:box('Door rail',(.8,y,-.07),(1.6,.12,.06),'Wood')
 brace=box('Diagonal door brace',(.8,1.11,-.075),(.12,2.04,.06),'Wood',.006,(0,0,-36))
 for y in [.25,1.96]:
  box('Strap hinge',(.23,y,-.12),(.56,.08,.03),'Rust')
  rod('Hinge pin',(.015,y-.10,-.12),(.015,y+.1,-.12),.025)
 box('Lock plate',(1.4,1.12,-.08),(.12,.25,.025),'Rust')
 rod('Latch handle',(1.4,1.16,-.11),(1.24,1.16,-.19),.022)
 return merge('DoorLeaf',current)

def table():
 global current;current=[]
 for i in range(5):box('Table top board',(0,.46,-.4+i*.2),(2,.08,.192),'Wood')
 for xx in [-.84,.84]:
  for zz in [-.34,.34]:box('Tapered table leg',(xx,-.04,zz),(.085,.9,.085),'Wood')
 for zz in [-.38,.38]:box('Apron',(0,.27,zz),(1.84,.22,.065),'Wood')
 for xx in [-.88,.88]:box('Apron',(xx,.27,0),(.065,.22,.8),'Wood')
 return merge('Table',current)

def shelf():
 global current;current=[]
 for xx in [-.8,.8]:box('Cabinet side',(xx,.7,0),(.06,1.4,.4),'Wood')
 box('Cabinet back',(0,.7,.18),(1.6,1.4,.035),'Wood')
 for y in [.03,.45,.9,1.4]:box('Shelf',(0,y,0),(1.6,.06,.4),'Wood')
 for i in range(12):
  x=-.65+i*.1;h=.15+random.random()*.13
  box('Aged book',(x,.48+h/2,0),(.065,h,.22),'Fabric',.008, (0,0,random.uniform(-6,6)))
 return merge('WallCabinet',current)

def fence():
 global current;current=[]
 for x in [-1,1]:box('Fence post',(x,.9,0),(.14,1.8,.16),'Wood',.018)
 for y in [.65,1.35]:
  for k in range(4):box('Uneven fence rail',(-.75+k*.5,y+random.uniform(-.015,.015),0),(.51,.15,.11),'Wood',.009,(0,0,random.uniform(-1,1)))
 return merge('FencePanel',current)

def crate():
 global current;current=[]
 for y in [-.45,.45]:
  for i in range(5):box('Crate slat',(-.4+i*.2,y,0),(.18,.1,.98),'Wood')
 for z in [-.45,.45]:
  for i in range(5):box('Crate side slat',(-.4+i*.2,0,z),(.18,.84,.1),'Wood')
 for x in [-.45,.45]:
  for z in [-.39,.39]:box('Crate corner batten',(x,0,z),(.11,1,.11),'Wood')
 return merge('Crate',current)

def can():
 global current;current=[]
 rod('Ribbed metal can',(0,-.5,0),(0,.5,0),.48,'Rust',32)
 for y in [-.47,-.3,.3,.47]:
  for i in range(32):
   a=i*math.tau/32;b=(i+1)*math.tau/32
   rod('Rolled can seam',(math.cos(a)*.49,y,math.sin(a)*.49),(math.cos(b)*.49,y,math.sin(b)*.49),.018,'Metal',6)
 return merge('MetalCan',current)

def radio():
 global current;current=[]
 box('Radio shell',(0,0,0),(1,.65,.6),'Rust',.035)
 box('Speaker surround',(-.17,0,-.32),(.56,.48,.025),'Metal',.02)
 for i in range(12):box('Speaker grille',(-.17,-.2+i*.035,-.34),(.5,.009,.012),'Rust',.001)
 for x in [.21,.36]:rod('Tuning knob',(x,-.16,-.32),(x,-.16,-.39),.052,'Metal',16)
 box('Frequency dial',(.27,.15,-.32),(.32,.12,.022),'Glass',0)
 rod('Antenna',(.4,.31,.1),(.46,1.0,.1),.008,'Metal',8)
 return merge('Radio',current)

def ceiling_lamp():
 global current;current=[]
 rod('Lamp suspension',(0,.14,0),(0,.65,0),.012,'Metal',8)
 box('Ceiling rose',(0,.65,0),(.2,.025,.2),'Metal',.012)
 rod('Bulb',(0,-.08,0),(0,.12,0),.055,'Bulb',16)
 for side in range(6):
  a=side*math.tau/6;x=math.cos(a)*.16;z=math.sin(a)*.16
  rod('Protective cage',(x,-.16,z),(x,.14,z),.009,'Metal',6)
 for i in range(18):
  a=i*math.tau/18;b=(i+1)*math.tau/18
  for y in [-.16,.14]:rod('Cage hoop',(math.cos(a)*.16,y,math.sin(a)*.16),(math.cos(b)*.16,y,math.sin(b)*.16),.009,'Metal',6)
 return merge('CeilingLamp',current)

assets=[]
for build,name in [(farmhouse,'Farmhouse'),(barn,'Barn')]:
 for level in range(3):assets.append((name+f'_LOD{level}',build(level)))
for name,build in [('DoorLeaf',door),('Table',table),('WallCabinet',shelf),('FencePanel',fence),('Crate',crate),('MetalCan',can),('Radio',radio),('CeilingLamp',ceiling_lamp)]:assets.append((name,build()))

# Tree geometry: irregular tapered trunk + authored whorls with alpha needle shoots. No spheres/cones.
def tube(name,points,radii,mat='Bark',sides=9):
 global current
 verts=[];uvs=[]
 for j,p in enumerate(points):
  p=V(p)
  for k in range(sides):
   a=k*math.tau/sides;verts.append(p+Vector((math.cos(a)*radii[j],math.sin(a)*radii[j],0)))
 faces=[]
 for j in range(len(points)-1):
  for k in range(sides):faces.append((j*sides+k,j*sides+(k+1)%sides,(j+1)*sides+(k+1)%sides,(j+1)*sides+k))
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);uv_mesh(obj,mat)
 for p in mesh.polygons:p.use_smooth=True
 current.append(obj)

def card(name,p,width,length,angle,tilt,mat='Leaf'):
 global current
 c=V(p);right=Vector((math.cos(angle),math.sin(angle),0));up=Vector((-math.sin(angle)*math.sin(tilt),math.cos(angle)*math.sin(tilt),math.cos(tilt)))
 verts=[c-right*width/2,c+right*width/2,c+right*width/2+up*length,c-right*width/2+up*length]
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],[(0,1,2,3)]);mesh.update();mesh.materials.append(materials[mat]);uv=mesh.uv_layers.new()
 for i,v in enumerate([(0,0),(1,0),(1,1),(0,1)]):uv.data[i].uv=v
 obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);current.append(obj)

def pine(seed,lod):
 global current;current=[];r=random.Random(seed);height=8+r.random()*3
 points=[(.10*math.sin(j*.7+seed),height*j/12,.06*math.cos(j*.8)) for j in range(13)]
 tube('Irregular pine trunk',points,[.24*(1-j/13)**1.1+.018 for j in range(13)],sides=10 if lod<2 else 6)
 levels=9 if lod==0 else 6 if lod==1 else 4
 for level in range(levels):
  y=2+level*(height-2.7)/levels;extent=(1-(y/height))**.7*3
  count=6 if lod==0 else 4
  for b in range(count):
   a=b*math.tau/count+level*.9+r.uniform(-.3,.3);length=extent*r.uniform(.7,1.15)
   tip=(math.cos(a)*length,y-.18,math.sin(a)*length)
   if lod<2:tube('Crooked branch',[(0,y,0),(tip[0]*.5,y-.25,tip[2]*.5),tip],[.05,.028,.006],sides=6)
   shoots=5 if lod==0 else 3 if lod==1 else 2
   for j in range(shoots):
    t=(j+.4)/shoots
    for side in [-1,1]:
     px=tip[0]*t+math.cos(a+side*.9)*.3; pz=tip[2]*t+math.sin(a+side*.9)*.3
     card('Pine needle shoot',(px,y-.15+ r.uniform(-.1,.1),pz),.7 if lod<2 else 1.0,.9 if lod<2 else 1.35,a+side*.7,.9,'Leaf')
 return merge(f'Pine_{seed}_LOD{lod}',current)
for seed in [31,47,83]:
 for lod in range(3):assets.append((f'Pine_{seed}_LOD{lod}',pine(seed,lod)))
for kind in ['Fern','GrassCard']:
 current=[]
 for i in range(7 if kind=='Fern' else 3):card('Bent frond',(0,0,0),.65,.9 if kind=='Fern' else .6,i*math.tau/7,1.0 if kind=='Fern' else .2,kind)
 assets.append((kind,merge(kind,current)))
# Nonuniform rock meshes use displaced icosphere faces and a full rough PBR material.
for seed in [5,12,29]:
 current=[];bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=3,radius=1)
 obj=bpy.context.object;obj.name=f'Rock_{seed}';r=random.Random(seed)
 for v in obj.data.vertices:
  factor=1+r.uniform(-.10,.10)+.08*math.sin(v.co.x*8+seed);v.co*=factor;v.co.z*=.6
  if v.co.z<-.25:v.co.z=-.25+(v.co.z+.25)*.15
 uv_mesh(obj,'Stone');current.append(obj);assets.append((obj.name,merge(obj.name,current)))
manifest=[]
for name,obj in assets:
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 bpy.ops.export_scene.fbx(filepath=str(OUT/f'{name}.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,global_scale=1,bake_space_transform=True,axis_forward='-Z',axis_up='Y',mesh_smooth_type='FACE',use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP',bake_anim=False)
 obj.data.calc_loop_triangles();coords=[obj.matrix_world@v.co for v in obj.data.vertices];bounds=[[min(v[i] for v in coords),max(v[i] for v in coords)] for i in range(3)]
 manifest.append({'name':name,'vertices':len(obj.data.vertices),'triangles':len(obj.data.loop_triangles),'materials':[m.name for m in obj.data.materials],'blender_bounds':bounds,'file':f'{name}.fbx','sha256':hashlib.sha256((OUT/f'{name}.fbx').read_bytes()).hexdigest()})
 obj.hide_render=True;obj.hide_set(True)
(ART/'model-manifest.json').write_text(json.dumps({'generator':'Tools/VisualRebuild/build_models.py','units':'metres','seed':27092026,'models':manifest},indent=2)+'\n')
bpy.ops.wm.save_as_mainfile(filepath='/tmp/Silence_Visual_Assets.blend')
print('SILENCE_MODELS_EXPORTED',len(manifest),'triangles',sum(m['triangles'] for m in manifest))
