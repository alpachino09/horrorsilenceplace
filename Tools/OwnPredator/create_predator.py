"""SILENCE original creature generator. Blender 4.2+ Python console or background.
Original procedural artwork; not a film asset and not certified AAA art.
Keeps existing scenes. Writes editable .blend, rigged FBX LODs, maps and reports.
Environment: SILENCE_PREDATOR_OUTPUT, SILENCE_PREDATOR_TEXTURE_SIZE (default 4096).
"""
import bpy, math, os, json, pathlib, random, time, hashlib
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils import noise as mnoise

if bpy.app.version < (4,2,0):raise RuntimeError('Use Blender 4.2 or newer; this generator was tested in Blender 4.3.2')
START=time.time(); SEED=73819; RNG=random.Random(SEED)
OUT=pathlib.Path(os.environ.get('SILENCE_PREDATOR_OUTPUT', str(pathlib.Path.home()/'Desktop'/'SilencePredator'))).expanduser().resolve()
if 'SILENCE_PREDATOR_OUTPUT' not in os.environ and (OUT/'SilencePredator.blend').exists():OUT=OUT/('Run_'+time.strftime('%Y%m%d_%H%M%S'))
ART=OUT/'Assets'/'Creatures'/'SilencePredator'
for name in ['Models','Materials','Textures','Rigs','Animations','Prefabs','Scripts','Physics','Demo','Documentation','Editor']:(ART/name).mkdir(parents=True,exist_ok=True)
SIZE=int(os.environ.get('SILENCE_PREDATOR_TEXTURE_SIZE','4096'))
if SIZE not in (512,1024,2048,4096):raise ValueError('Texture size must be 512, 1024, 2048 or 4096')
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
SCENE=bpy.data.scenes.new('SILENCE_Vesper_Original')
if bpy.context.window:bpy.context.window.scene=SCENE
SCENE.unit_settings.system='METRIC';SCENE.unit_settings.scale_length=1
SCENE.render.fps=30
COL=bpy.data.collections.new('VESPER | original anatomy');SCENE.collection.children.link(COL)
SOURCE=bpy.data.collections.new('SCULPT_SOURCE | editable connected skin');SCENE.collection.children.link(SOURCE)
SOURCE.hide_render=True;SOURCE.hide_viewport=True
PARTS=[];BONES={};SEGMENTS=[];CLIPS=[];MATS={};ACTIONS={};REPORT={'generator':'SILENCE Vesper 0.1','seed':SEED,'blender':bpy.app.version_string,'native_unity_tested':False,'art_approved':False}
def say(s):print('SILENCE: '+s,flush=True)
def V(v):return Vector(v)
def blend(a,b,t):return V(a).lerp(V(b),t)
def smooth(t):return t*t*(3-2*t)
def link(obj,collection=COL):collection.objects.link(obj);return obj

def field(cells,seed):
 r=np.random.default_rng(seed);grid=r.random((cells,cells),dtype=np.float32)
 co=np.arange(SIZE,dtype=np.float32)*cells/SIZE;i=co.astype(np.int32);t=co-i;t=t*t*(3-2*t)
 a=grid[i[:,None]%cells,i[None,:]%cells];b=grid[i[:,None]%cells,(i[None,:]+1)%cells]
 a=a*(1-t[None,:])+b*t[None,:]
 b=grid[(i[:,None]+1)%cells,i[None,:]%cells]*(1-t[None,:])+grid[(i[:,None]+1)%cells,(i[None,:]+1)%cells]*t[None,:]
 return a*(1-t[:,None])+b*t[:,None]
def image_file(name,rgb,data=False,alpha=None):
 im=bpy.data.images.new('Vesper_'+name,width=SIZE,height=SIZE,alpha=True,float_buffer=False)
 pixels=np.empty((SIZE,SIZE,4),dtype=np.float32);pixels[...,:3]=rgb;pixels[...,3]=1 if alpha is None else alpha
 im.colorspace_settings.name='Non-Color' if data else 'sRGB';im.pixels.foreach_set(pixels.ravel());im.filepath_raw=str(ART/'Textures'/(name+'.png'));im.file_format='PNG';im.save();return im

def maps(kind):
 say('Creating '+kind+' PBR maps '+str(SIZE)+' px')
 a=field(7,SEED+1);b=field(43,SEED+2);c=field(237,SEED+3);f=field(761,SEED+4)
 u=np.arange(SIZE,dtype=np.float32)[None,:]/SIZE;v=np.arange(SIZE,dtype=np.float32)[:,None]/SIZE
 if kind=='Skin':
  crease=np.exp(-((np.sin((u*23+.25*b)*math.tau))/.16)**2)*(.25+.75*a)
  h=.45+.10*b+.045*c+.018*f-.07*crease
  color=np.array([.13,.105,.092],dtype=np.float32)*( .60+.32*a+.13*b)[...,None]
  color*= (1-.18*crease)[...,None];rough=.66+.20*c-.10*a
 elif kind=='Armor':
  lamella=(.5+.5*np.sin((u*28+.08*np.sin(v*math.tau*4)+.12*b)*math.tau))**7
  crack=np.exp(-(np.sin((v*13+.12*a)*math.tau)/.065)**2)*(.3+.7*b)
  h=.43+.035*lamella+.025*c-.065*crack+.012*f
  edge=np.abs(v-.5)*2
  color=np.array([.125,.115,.105],dtype=np.float32)*(.53+.30*a+.16*edge)[...,None]
  color*= (1-.24*crack)[...,None];rough=.46+.24*b+.13*crack
 else:
  folds=(.5+.5*np.sin((u*17+.22*b)*math.tau))**3
  h=.4+.12*folds+.035*c+.012*f
  color=np.array([.115,.018,.024],dtype=np.float32)*(.55+.38*a+.12*c)[...,None];rough=.22+.15*b
 base=image_file(kind+'_BaseColor',np.clip(color,0,1))
 gx=(np.roll(h,-1,1)-np.roll(h,1,1))*8;gy=(np.roll(h,-1,0)-np.roll(h,1,0))*8
 normal=np.stack((-gx,-gy,np.ones_like(h)),axis=-1);normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
 norm=image_file(kind+'_Normal',normal*.5+.5,True)
 rough=np.clip(rough,.14,.92);ao=np.clip(.85+.15*h-.10*(.5-b),.6,1)
 packed=np.stack((np.zeros_like(h),ao,np.clip(h,0,1)),axis=-1)
 mask=image_file(kind+'_Mask',packed,True,1-rough)
 return base,norm,mask

def material(name):
 m=bpy.data.materials.new('Vesper_'+name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;p=n.get('Principled BSDF')
 if name=='Bone':
  p.inputs['Base Color'].default_value=(.24,.205,.155,1);p.inputs['Roughness'].default_value=.43
 else:
  base,norm,mask=maps(name)
  for im,label in [(base,'Base'),(norm,'Normal'),(mask,'Mask')]:
   t=n.new('ShaderNodeTexImage');t.image=im;t.label=label
   if label=='Base':l.new(t.outputs['Color'],p.inputs['Base Color'])
   elif label=='Normal':
    bump=n.new('ShaderNodeNormalMap');bump.inputs['Strength'].default_value=.55;l.new(t.outputs['Color'],bump.inputs['Color']);l.new(bump.outputs[0],p.inputs['Normal'])
   else:
    inv=n.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;l.new(t.outputs['Alpha'],inv.inputs[1]);l.new(inv.outputs[0],p.inputs['Roughness'])
  if 'Subsurface Weight' in p.inputs:p.inputs['Subsurface Weight'].default_value=.08 if name=='Skin' else .22 if name=='Tissue' else 0
  if name=='Tissue' and 'Coat Weight' in p.inputs:p.inputs['Coat Weight'].default_value=.25
 MATS[name]=m;return m
for name in ['Skin','Armor','Tissue','Bone']:material(name)

def mesh(name,verts,faces,mat,uv=None,bone=None,weights=None,record=True):
 data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();obj=link(bpy.data.objects.new(name,data));data.materials.append(MATS[mat])
 layer=data.uv_layers.new(name='UVMap')
 for poly in data.polygons:
  poly.use_smooth=True
  for loop in poly.loop_indices:
   vi=data.loops[loop].vertex_index
   layer.data[loop].uv=uv[vi] if uv else (verts[vi][0]*.6,verts[vi][1]*.6)
 if bone:
  obj.vertex_groups.new(name=bone).add(list(range(len(verts))),1,'REPLACE')
 if weights:
  for i,items in enumerate(weights):
   for group,w in items:
    g=obj.vertex_groups.get(group) or obj.vertex_groups.new(name=group);g.add([i],w,'REPLACE')
 if record:PARTS.append(obj)
 return obj

def tube(name,points,radii,mat='Skin',sides=12,steps=4,bone=None,weights=None,detail=.018,record=True):
 points=[V(p) for p in points];verts=[];uv=[];boneweights=[];centers=[];rs=[]
 for k in range(len(points)-1):
  p0=points[max(0,k-1)];p1=points[k];p2=points[k+1];p3=points[min(len(points)-1,k+2)]
  for j in range(steps):
   t=j/steps;t2=t*t;t3=t2*t
   centers.append((2*p1+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t2+(-p0+3*p1-3*p2+p3)*t3)*.5)
   ra=np.array(radii[k] if isinstance(radii[k],(tuple,list)) else [radii[k],radii[k]])
   rb=np.array(radii[k+1] if isinstance(radii[k+1],(tuple,list)) else [radii[k+1],radii[k+1]])
   rs.append(ra*(1-t)+rb*t)
 centers.append(points[-1]);r=radii[-1];rs.append(r if isinstance(r,(tuple,list)) else [r,r])
 previous=None
 for k,(p,r) in enumerate(zip(centers,rs)):
  tangent=(centers[min(len(centers)-1,k+1)]-centers[max(0,k-1)]).normalized()
  normal=tangent.cross(V((0,1,0)))
  if normal.length<.1:normal=tangent.cross(V((1,0,0)))
  normal.normalize()
  if previous is not None and normal.dot(previous)<0:normal=-normal
  previous=normal;binormal=tangent.cross(normal).normalized()
  for j in range(sides):
   a=j*math.tau/sides;mod=1+detail*math.sin(a*3+k*.7)+detail*.5*math.sin(a*7+k*.23)
   co=p+(normal*math.cos(a)*r[0]+binormal*math.sin(a)*r[1])*mod;verts.append(tuple(co));uv.append((k/max(1,len(centers)-1),j/sides))
   if weights:boneweights.append(weights(k/(len(centers)-1)))
 faces=[]
 for k in range(len(centers)-1):
  for j in range(sides):faces.append((k*sides+j,k*sides+(j+1)%sides,(k+1)*sides+(j+1)%sides,(k+1)*sides+j))
 faces.extend([tuple(reversed(range(sides))),tuple((len(centers)-1)*sides+j for j in range(sides))])
 return mesh(name,verts,faces,mat,uv,bone,boneweights if weights else None,record)

def add_bone(name,a,b,parent=None,deform=True,radius=.25):
 BONES[name]=(V(a),V(b),parent,deform)
 if deform:SEGMENTS.append((name,V(a),V(b),radius))

def plate(name,points,widths,height=.13,bone='Chest',mat='Armor',n=16,m=10):
 # Closed organic carapace shell: irregular rim, growth ridges and thickness.
 points=[V(p) for p in points];verts=[];uv=[]
 for underside in (False,True):
  for i in range(n+1):
   u=i/n;k=min(len(points)-2,int(u*(len(points)-1)));t=u*(len(points)-1)-k;c=points[k].lerp(points[k+1],smooth(t))
   w=widths[k]*(1-t)+widths[k+1]*t
   tangent=points[k+1]-points[k];right=tangent.cross(V((0,0,1))).normalized()
   if right.length<.1:right=V((1,0,0))
   for j in range(m+1):
    v=j/m*2-1
    ridge=(1-v*v)*height*(.75+.25*math.sin(u*math.pi))
    flutes=.014*math.cos(v*math.pi*6)*(1-v*v)*math.sin(u*math.pi)
    co=c+right*(v*w*(1+.035*math.sin(u*19+v*3)))+V((0,0,-.035 if underside else ridge+flutes))
    verts.append(tuple(co));uv.append((u,j/m))
 layer=(n+1)*(m+1);faces=[]
 for offset in [0,layer]:
  for i in range(n):
   for j in range(m):
    q=(offset+i*(m+1)+j,offset+i*(m+1)+j+1,offset+(i+1)*(m+1)+j+1,offset+(i+1)*(m+1)+j)
    faces.append(tuple(reversed(q)) if offset else q)
 for i in range(n):
  for j in [0,m]:
   a=i*(m+1)+j;b=(i+1)*(m+1)+j;faces.append((a,b,b+layer,a+layer))
 for i in [0,n]:
  for j in range(m):
   a=i*(m+1)+j;faces.append((a,a+1,a+1+layer,a+layer))
 return mesh(name,verts,faces,mat,uv,bone)

say('Building connected anatomical skin')
add_bone('Root',(0,0,0),(0,0,.4),None,True,.25)
add_bone('Pelvis',(0,1.12,1.78),(0,.55,1.95),'Root',radius=.45)
add_bone('Spine',(0,.55,1.95),(0,-.08,2.22),'Pelvis',radius=.42)
add_bone('Chest',(0,-.08,2.22),(0,-.64,2.48),'Spine',radius=.6)
add_bone('Neck',(0,-.64,2.48),(0,-1.12,2.56),'Chest',radius=.37)
add_bone('Head',(0,-1.12,2.56),(0,-1.9,2.46),'Neck',radius=.43)
add_bone('Jaw',(0,-1.35,2.2),(0,-2.0,2.12),'Head',radius=.2)
SKIN=[]
SKIN.append(tube('Anatomical trunk',[(0,1.55,1.63),(0,1.16,1.86),(0,.64,1.82),(0,.12,2.12),(0,-.35,2.34),(0,-.72,2.45),(0,-1.10,2.5)],[.16,(.48,.48),(.31,.38),(.48,.48),(.70,.59),(.53,.42),(.25,.28)],sides=40,steps=7,record=False))
LIMBS={}
for side,sign in [('L',1),('R',-1)]:
 shoulder=(sign*.57,-.53,2.40);elbow=(sign*1.0,-.22,1.39);wrist=(sign*.96,-1.52,.29);palm=(sign*.99,-1.87,.18)
 hip=(sign*.39,1.12,1.82);knee=(sign*.76,.57,1.05);hock=(sign*.76,1.67,.42);paw=(sign*.75,1.28,.15)
 for prefix,a,b,parent,radius in [('ForeUpper',shoulder,elbow,'Chest',.28),('ForeLower',elbow,wrist,'ForeUpper.'+side,.16),('Hand',wrist,palm,'ForeLower.'+side,.18),('HindUpper',hip,knee,'Pelvis',.30),('HindLower',knee,hock,'HindUpper.'+side,.17),('Foot',hock,paw,'HindLower.'+side,.15)]:add_bone(prefix+'.'+side,a,b,parent,radius=radius)
 add_bone('IK_Fore.'+side,wrist,palm,'Root',False);add_bone('IK_Hind.'+side,hock,paw,'Root',False)
 for kind,a,b,c in [('Fore',shoulder,elbow,wrist),('Hind',hip,knee,hock)]:
  pole=V(b)+(V(b)-(V(a)+V(c))*.5).normalized()*1.6
  add_bone('Pole_'+kind+'.'+side,pole,pole+V((0,0,.25)),'Root',False)
 LIMBS[side]={'fore':(shoulder,elbow,wrist,palm),'hind':(hip,knee,hock,paw)}
 SKIN.append(tube('Fore anatomy '+side,[shoulder,blend(shoulder,elbow,.32),elbow,blend(elbow,wrist,.43),wrist,palm],[(.33,.32),(.29,.34),(.17,.19),(.105,.16),(.085,.10),(.15,.085)],sides=22,steps=5,record=False))
 SKIN.append(tube('Hind anatomy '+side,[hip,blend(hip,knee,.4),knee,blend(knee,hock,.45),hock,paw],[(.32,.30),(.31,.28),(.17,.17),(.135,.18),(.095,.09),(.17,.10)],sides=22,steps=5,record=False))
 for limb,base,parent,spread,reach in [('Fore',V(palm),'Hand.'+side,.18,.44),('Hind',V(paw),'Foot.'+side,.14,.33)]:
  for digit in range(3):
   x=(digit-1)*spread
   a=base+V((x,.06,0));b=a+V((x*.25,-reach*.47,-.035));c=a+V((x*.4,-reach,-.075));c.z=max(.075,c.z)
   n1=f'{limb}Digit{digit}A.{side}';n2=f'{limb}Digit{digit}B.{side}'
   add_bone(n1,a,b,parent,radius=.075);add_bone(n2,b,c,n1,radius=.055)
   SKIN.append(tube('Digit flesh',[a,b,c],[.065,.055,.025],sides=10,steps=4,record=False))
   tip=c+V((sign*.025,-.17 if limb=='Fore' else -.12,-.06));tip.z=.018
   tube('Curved keratin talon',[c,c+V((0,-.07,.015)),tip],[.044,.030,.002],'Bone',10,5,n2,detail=.05)

# Union flesh before weights: the body and limbs are one connected editable surface.
bpy.ops.object.select_all(action='DESELECT')
for o in SKIN:o.select_set(True)
bpy.context.view_layer.objects.active=SKIN[0];bpy.ops.object.join();skin=SKIN[0];skin.name='Dermis_LOD0'
rem=skin.modifiers.new('Anatomical surface union','REMESH');rem.mode='VOXEL';rem.voxel_size=.043;rem.use_smooth_shade=True
bpy.ops.object.modifier_apply(modifier=rem.name)
sm=skin.modifiers.new('Organic transitions','SMOOTH');sm.factor=.55;sm.iterations=3;bpy.ops.object.modifier_apply(modifier=sm.name)
# Macroscopic scar/fold displacement is masked to joints, not a uniform bumpy surface.
for vertex in skin.data.vertices:
 p=vertex.co;crease=0
 for side,sign in [('L',1),('R',-1)]:
  for joint in [LIMBS[side]['fore'][1],LIMBS[side]['hind'][1]]:
   d=(p-V(joint)).length;crease+=math.exp(-d*d/.07)*math.sin((p.z+p.y*.5)*72)*.012
 vertex.co+=vertex.normal*(crease+.004*mnoise.noise_vector(p*7)[0])
source=skin.copy();source.data=skin.data.copy();source.name='EDITABLE_connected_high_surface';SOURCE.objects.link(source)
PARTS.append(skin)
# Source head is an open upper cranium, with an independently articulated lower jaw.
def cranium(name,lower=False):
 verts=[];uv=[];faces=[];rings=18;sides=28
 for i in range(rings+1):
  u=i/rings;y=-1.02-u*1.10;w=(.28+.23*math.sin(math.pi*u))*(1-.25*u);cz=2.48-.20*u
  if lower:cz=2.245-.025*math.sin(u*math.pi)
  for j in range(sides+1):
   a=j/sides*math.pi+(math.pi if lower else 0);z=cz+(.17 if lower else .29)*math.sin(a)
   verts.append((math.cos(a)*w,y,z));uv.append((u,j/sides))
 for i in range(rings):
  for j in range(sides):a=i*(sides+1)+j;faces.append((a,a+1,a+sides+2,a+sides+1))
 o=mesh(name,verts,faces,'Tissue' if lower else 'Skin',uv,'Jaw' if lower else 'Head')
 mod=o.modifiers.new('Skull tissue thickness','SOLIDIFY');mod.thickness=.045;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o
cranium('Eyeless upper cranium');cranium('Articulated mandible',True)
# Close the upper snout; an open tube here resembles a nose rather than an eyeless skull.
cap=[(0,-2.12,2.28)]+[(math.cos(j/28*math.pi)*.21,-2.12,2.28+.29*math.sin(j/28*math.pi)) for j in range(29)]
mesh('Sealed rostral bone',cap,[(0,j+1,j+2) for j in range(28)],'Armor',bone='Head')
plate('Long faceless rostral shield',[(0,-.98,2.91),(0,-1.70,2.88),(0,-2.30,2.31)],[.12,.39,.018],.065,'Head',n=24,m=14)
# Jaw lining and palate overlap into the throat, avoiding an empty hollow shell.
for lower in [False,True]:
 bone='Jaw' if lower else 'Head';z=2.245 if lower else 2.30
 verts=[];uv=[];faces=[]
 for i in range(17):
  u=i/16;y=-1.22-u*.87;w=.30*math.sin(math.pi*(u*.84+.08))
  for j in range(13):
   t=j/12*2-1;verts.append((t*w,y,z+.018*math.cos(u*40)*(1-t*t)+(.045 if lower else -.045)*(1-t*t)));uv.append((u,j/12))
 for i in range(16):
  for j in range(12):a=i*13+j;faces.append((a,a+1,a+14,a+13))
 mesh('Corrugated oral floor' if lower else 'Ridged palate',verts,faces,'Tissue',uv,bone)
 ring=[(.30*math.sin(i*math.tau/36),-1.65-.44*math.cos(i*math.tau/36),z) for i in range(37)]
 tube('Gingival ridge',ring,[.025]*37,'Tissue',10,2,bone,detail=.04)
 for row,count in [(0,24),(1,14)]:
  for i in range(count):
   angle=(i+.25*RNG.random())*math.tau/count
   x=(.30-row*.075)*math.sin(angle);y=-1.65-(.44-row*.07)*math.cos(angle)
   length=RNG.uniform(.055,.14)*(1 if row==0 else .6)*(1.7 if i in (3,8,15) else 1);direction=1 if lower else -1
   a=(x,y,z);b=(x*.94,y+.018,z+direction*length*.6);c=(x*.87,y+.04,z+direction*length)
   tube('Individual tooth',[a,b,c],[.017 if row==0 else .011,.015,.0018],'Bone',8,3,bone,detail=.06)
for sign in [-1,1]:
 verts=[];weights=[];uv=[]
 for j in range(9):
  t=j/8
  for i in range(7):
   u=i/6;verts.append((sign*(.28+.02*math.sin(t*math.pi)),-1.25-u*.43,2.3-t*.055));uv.append((u,t));weights.append([('Head',1-t),('Jaw',t)])
 faces=[(j*7+i,j*7+i+1,(j+1)*7+i+1,(j+1)*7+i) for j in range(8) for i in range(6)]
 mesh('Flexible oral commissure',verts,faces,'Tissue',uv,weights=weights)
# Layered dorsal and shoulder armor, matched to skeletal body regions.
for i in range(10):
 y=1.42-i*.245;z=1.99+(1.42-y)*.31
 plate('Vertebral shield '+str(i),[(0,y+.16,z),(0,y,z+.12),(0,y-.16,z+.05)],[.06,.29 if i<5 else .40,.025],.075,'Pelvis' if i<3 else 'Spine' if i<6 else 'Chest',n=9,m=10)
for side,sign in [('L',1),('R',-1)]:
 for i in range(5):
  y=-.25+i*.28;z=2.31-i*.09
  plate('Rib shield',[(sign*.12,y+.18,z+.10),(sign*.46,y,z),(sign*.58,y-.14,z-.19)],[.03,.17,.015],.075,'Chest' if i<2 else 'Spine',n=9,m=8)
 for prefix,points in [('Fore',LIMBS[side]['fore']),('Hind',LIMBS[side]['hind'])]:
  a,b,c,d=map(V,points)
  plate('Limb upper shield',[a+V((0,-.14,.09)),a.lerp(b,.45)+V((0,-.15,.07)),b+V((0,-.12,.05))],[.05,.22 if prefix=='Fore' else .19,.035],.08,('ForeUpper.' if prefix=='Fore' else 'HindUpper.')+side,n=14,m=9)
  for k in range(3):
   t=.16+k*.24;p=b.lerp(c,t)
   plate('Overlapping forearm scute',[p+V((0,-.12,.10)),p+V((0,-.13,0)),p+V((0,-.09,-.17))],[.02,.13,.02],.07,('ForeLower.' if prefix=='Fore' else 'HindLower.')+side,n=8,m=8)
 # Real longitudinal tendon ridges and folds over the flexor side.
 a,b,c,d=map(V,LIMBS[side]['fore'])
 def tendon_weights(t,s=side):
  a=smooth(max(0,min(1,(t-.28)/.4)))
  return [('ForeUpper.'+s,1-a),('ForeLower.'+s,a)]
 for k in [-1,1]:tube('Flexor tendon',[a.lerp(b,.28)+V((sign*.12,k*.10,0)),b+V((sign*.10,k*.08,0)),b.lerp(c,.7)+V((sign*.08,k*.04,0))],[.035,.025,.012],'Skin',9,5,weights=tendon_weights)

# Six independent outer valves over six folded acoustic chambers; no ordinary eyes.
for side,sign in [('L',1),('R',-1)]:
 for k in range(3):
  y=-1.13-k*.30;hinge=(sign*(.24+.015*k),y,2.92-.04*k)
  name=f'Hearing_{side}{k}';inner=f'Membrane_{side}{k}'
  add_bone(name,hinge,V(hinge)+V((sign*.35,-.10,.04)),'Head',radius=.1)
  add_bone(inner,hinge,V(hinge)+V((sign*.22,-.07,-.12)),'Head',radius=.08)
  plate('Independent acoustic valve',[(sign*.05,y+.20,2.86-k*.04),(sign*.34,y,2.90-k*.04),(sign*.53,y-.23,2.61-k*.04)],[.04,.25,.025],.095,name,n=16,m=13)
  # A concave ridged organ under the outer valve, with geometry down inside the chamber.
  verts=[];uv=[];faces=[]
  for i in range(17):
   u=i/16
   for j in range(21):
    a=j/20*math.tau;r=.035+u*.22
    p=V(hinge)+V((sign*(.07+r*math.sin(a)),r*math.cos(a),-.17+.11*u+.018*math.sin(u*math.pi*12)))
    verts.append(tuple(p));uv.append((j/20,u))
  for i in range(16):
   for j in range(20):a=i*21+j;faces.append((a,a+1,a+22,a+21))
  mesh('Folded acoustic concha',verts,faces,'Tissue',uv,inner)
  for j in range(7):
   a=j*math.tau/7;r=.21
   tube('Acoustic radial ligament',[V(hinge)+V((sign*.07,0,-.16)),V(hinge)+V((sign*(.07+r*.55*math.sin(a)),r*.55*math.cos(a),-.08)),V(hinge)+V((sign*(.07+r*math.sin(a)),r*math.cos(a),-.06))],[.014,.012,.005],'Tissue',7,3,inner)

say('Creating skeleton and deform weights')
armdata=bpy.data.armatures.new('Vesper export skeleton');RIG=link(bpy.data.objects.new('Vesper_Rig',armdata));bpy.context.view_layer.objects.active=RIG;RIG.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
for name,(a,b,parent,deform) in BONES.items():
 eb=armdata.edit_bones.new(name);eb.head=a;eb.tail=b;eb.use_deform=deform
 if parent:eb.parent=armdata.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT');RIG.show_in_front=True
for side in ['L','R']:
 for kind,owner,chain in [('Fore','ForeLower.',LIMBS[side]['fore']),('Hind','HindLower.',LIMBS[side]['hind'])]:
  pb=RIG.pose.bones[owner+side];ik=pb.constraints.new('IK');ik.target=RIG;ik.subtarget='IK_'+kind+'.'+side;ik.chain_count=2;ik.pole_target=RIG;ik.pole_subtarget='Pole_'+kind+'.'+side
  best=(1e9,0)
  for angle in [i*math.pi/16 for i in range(-16,17)]:
   ik.pole_angle=angle;bpy.context.view_layer.update();error=(pb.head-V(chain[1])).length
   if error<best[0]:best=(error,angle)
  ik.pole_angle=best[1]
  bone=RIG.pose.bones[('Hand.' if kind=='Fore' else 'Foot.')+side]
  copy=bone.constraints.new('COPY_ROTATION');copy.target=RIG;copy.subtarget='IK_'+kind+'.'+side;copy.target_space='WORLD';copy.owner_space='WORLD'
# Vertex influences use anatomical segment distances; final skin is normalized to <=4 bones.
coords=np.array([v.co[:] for v in skin.data.vertices],dtype=np.float32);cost=[]
for name,a,b,radius in SEGMENTS:
 if name.startswith(('Hearing','Membrane','Jaw','Head','Root')):continue
 aa=np.array(a);d=np.array(b-a);t=np.clip(((coords-aa)*d).sum(1)/max(float(d@d),1e-8),0,1)
 distance=np.linalg.norm(coords-aa-t[:,None]*d,axis=1);cost.append((name,distance/max(.08,radius)))
values=np.stack([x[1] for x in cost],axis=1);idx=np.argsort(values,axis=1)[:,:4];dist=np.take_along_axis(values,idx,axis=1)
weights=np.exp(-(dist-dist[:,:1])*5);weights/=weights.sum(1,keepdims=True)
for k,(name,_) in enumerate(cost):
 group=skin.vertex_groups.new(name=name)
 for v in np.where((idx==k).any(1))[0]:
  slot=int(np.where(idx[v]==k)[0][0]);w=float(weights[v,slot])
  if w>.001:group.add([int(v)],w,'REPLACE')
# Keep the high voxel surface as source; decimate the game skin only.
bpy.ops.object.select_all(action='DESELECT');skin.select_set(True);bpy.context.view_layer.objects.active=skin
tri=sum(len(p.vertices)-2 for p in skin.data.polygons)
if tri>85000:
 dec=skin.modifiers.new('Game surface budget','DECIMATE');dec.ratio=85000/tri;bpy.ops.object.modifier_apply(modifier=dec.name)
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=1.05,island_margin=.006);bpy.ops.object.mode_set(mode='OBJECT')
# Join by surface material to keep four draw surfaces, preserving bone groups and UVs.
GAME=[]
groups={mat:[o for o in PARTS if o.data.materials[0]==MATS[mat]] for mat in ['Skin','Armor','Tissue','Bone']}
for mat in ['Skin','Armor','Tissue','Bone']:
 items=groups[mat]
 if not items:continue
 bpy.ops.object.select_all(action='DESELECT')
 for o in items:o.select_set(True)
 bpy.context.view_layer.objects.active=items[0];bpy.ops.object.join();o=items[0];o.name=mat+'_LOD0';o.parent=RIG
 for vertex in o.data.vertices:
  total=sum(g.weight for g in vertex.groups)
  if total>0:
   for g in vertex.groups:o.vertex_groups[g.group].add([vertex.index],g.weight/total,'REPLACE')
 mod=o.modifiers.new('Deform | export rig','ARMATURE');mod.object=RIG;mod.use_deform_preserve_volume=False
 GAME.append(o)
LODS={0:GAME}
for level,ratio in [(1,.43),(2,.12)]:
 collection=bpy.data.collections.new('LOD'+str(level)+' | reduced skinned meshes');SCENE.collection.children.link(collection);items=[]
 for original in GAME:
  o=original.copy();o.data=original.data.copy();o.name=original.name.replace('LOD0','LOD'+str(level));collection.objects.link(o)
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
  dec=o.modifiers.new('LOD reduction','DECIMATE');dec.ratio=ratio;bpy.ops.object.modifier_move_up(modifier=dec.name);bpy.ops.object.modifier_apply(modifier=dec.name)
  for vertex in o.data.vertices:
   pairs=sorted([(g.group,g.weight) for g in vertex.groups],key=lambda x:x[1],reverse=True)
   for group,w in pairs[4:]:o.vertex_groups[group].remove([vertex.index])
   total=sum(w for group,w in pairs[:4])
   for group,w in pairs[:4]:
    if total:o.vertex_groups[group].add([vertex.index],w/total,'REPLACE')
  o.hide_render=True;o.hide_set(True);items.append(o)
 LODS[level]=items

say('Authoring distinct motion and sequential hearing valves')
REST={n:p.matrix_basis.copy() for n,p in RIG.pose.bones.items()}
def reset_pose():
 for n,p in RIG.pose.bones.items():p.matrix_basis=REST[n]
def rotate(name,x=0,y=0,z=0):
 p=RIG.pose.bones[name];p.rotation_mode='XYZ';p.rotation_euler=(x,y,z)
def offset_world(name,value):
 pb=RIG.pose.bones[name];rot=armdata.bones[name].matrix_local.to_3x3();pb.location=rot.inverted()@V(value)
def hearing(amount,time):
 for side,sign in [('L',1),('R',-1)]:
  for k in range(3):
   a=smooth(max(0,min(1,(amount-k*.13)/.72)))
   name=f'Hearing_{side}{k}';axis=armdata.bones[name].matrix_local.to_3x3().inverted()@V((0,1,0))
   pb=RIG.pose.bones[name];pb.rotation_mode='XYZ';pb.rotation_euler=Quaternion(axis,-sign*a*1.3).to_euler('XYZ')
   rotate(f'Membrane_{side}{k}',a*.16,sign*a*.35,math.sin(time*4+k)*a*.04)
def gait(t,cycle,stride,lift):
 for side in ['L','R']:
  for kind in ['Fore','Hind']:
   phase=(t/cycle+(.5 if side=='R' else 0)+(.5 if kind=='Hind' else 0))%1
   if phase<.64:y=stride*(phase/.64-.5);z=0
   else:
    u=(phase-.64)/.36;y=stride*(.5-smooth(u));z=math.sin(math.pi*u)*lift
   offset_world('IK_'+kind+'.'+side,(0,y,z))
 offset_world('Pelvis',(0,0,-.025+.025*math.cos(t/cycle*math.tau*2)))
 rotate('Chest',math.sin(t/cycle*math.tau)*.025,0,math.sin(t/cycle*math.tau)*.025)
 rotate('Neck',-.04+math.sin(t/cycle*math.tau)*.025,0,0)

def make_action(name,seconds,mode):
 reset_pose();action=bpy.data.actions.new('Vesper_'+SCENE.name+'_'+name);action['silence_clip']=name;action.use_fake_user=True;ACTIONS[name]=action;RIG.animation_data_create();RIG.animation_data.action=action
 count=int(seconds*30);SCENE.frame_start=1;SCENE.frame_end=count+1
 frames=list(range(1,count+2,2))
 if frames[-1]!=count+1:frames.append(count+1)
 for frame in frames:
  SCENE.frame_set(frame);reset_pose();t=(frame-1)/30;u=min(1,t/seconds);breath=math.sin(t*math.tau/seconds)
  rotate('Chest',.008*breath);rotate('Neck',.012*breath)
  if mode in ['Walk','Stalk','Run','Sprint','Investigate','Search']:
   cycle={'Walk':1.4,'Stalk':1.8,'Run':.7,'Sprint':.5,'Investigate':1.6,'Search':1.7}[mode]
   gait(t,cycle,{'Walk':.60,'Stalk':.38,'Run':1.0,'Sprint':1.18,'Investigate':.42,'Search':.36}[mode],.12 if mode in ['Walk','Stalk','Investigate','Search'] else .25)
   if mode in ['Search','Investigate']:hearing(.6,t);rotate('Head',0,.08,math.sin(t*2)*.20)
  elif mode=='IdleBreathing':rotate('Chest',breath*.025);rotate('Neck',breath*.025);offset_world('IK_Fore.L',(0,0,max(0,breath)*.008))
  elif mode.startswith('Listen'):
   amount=smooth(u) if mode=='ListenStart' else 1-smooth(u) if mode=='ListenEnd' else .92+.06*math.sin(t*math.tau/seconds)
   hearing(amount,t);rotate('Neck',-.12*amount);rotate('Head',0,0,math.sin(t*math.tau/seconds)*.12*amount)
  elif mode=='Alert':
   a=math.sin(math.pi*u)**.6;hearing(a,t);rotate('Neck',-.22*a);rotate('Jaw',.23*a)
  elif mode.startswith('Turn'):
   sign=1 if mode=='TurnLeft' else -1;gait(t,1.3,.18,.08);rotate('Chest',0,0,sign*.17*math.sin(math.pi*u));rotate('Head',0,0,sign*.35*math.sin(math.pi*u))
  elif mode=='AttackBite':
   a=math.sin(math.pi*u);rotate('Jaw',.72*max(0,math.sin(math.pi*u*1.7)));rotate('Neck',.22*a);rotate('Head',.24*a)
  elif mode=='AttackClaw':
   a=max(0,math.sin(math.pi*u));offset_world('IK_Fore.L',(.13*a,-.9*a,.7*a));rotate('Chest',-.08*a,0,-.13*a);rotate('Jaw',.25*a)
  elif mode=='Lunge':
   a=math.sin(math.pi*u);offset_world('IK_Fore.L',(0,-.6*a,.15*a));offset_world('IK_Fore.R',(0,-.6*a,.15*a));rotate('Chest',.10*a);rotate('Neck',.2*a);rotate('Jaw',.45*a)
  elif mode=='Stunned':
   hearing(.7,t);rotate('Head',.08*math.sin(t*29),.04*math.sin(t*21),.07*math.sin(t*25));rotate('Chest',.06,0,.04*math.sin(t*13));rotate('Jaw',.25)
  elif mode=='Recover':
   a=1-smooth(u);hearing(a*.5,t);rotate('Chest',.08*a);rotate('Neck',.20*a);rotate('Jaw',.28*a)
  for pb in RIG.pose.bones:
   if pb.name.startswith(('IK_','Pole_')) or pb.name=='Pelvis':pb.keyframe_insert('location',frame=frame,group=pb.name)
   else:
    pb.keyframe_insert('rotation_euler',frame=frame,group=pb.name)
 if hasattr(action,'fcurves'):
  for curve in action.fcurves:
   for key in curve.keyframe_points:key.interpolation='LINEAR'
 CLIPS.append({'name':name,'seconds':seconds,'frames':[1,count+1],'status':'Procedural authored first pass; contacts/animation quality require visual and native review'})
 return action
for name,seconds in [('Idle',2.3),('IdleBreathing',2.3),('Walk',2.8),('Stalk',3.6),('Run',1.4),('Sprint',1.0),('ListenStart',1.1),('ListenLoop',2.4),('ListenEnd',.9),('Alert',.8),('TurnLeft',1.3),('TurnRight',1.3),('AttackBite',.9),('AttackClaw',1.1),('Lunge',.8),('Search',3.4),('Investigate',3.2),('Stunned',1.6),('Recover',1.8)]:make_action(name,seconds,name)
RIG.animation_data.action=ACTIONS['Idle'];SCENE.frame_set(1)
# A simple mannequin is demonstration infrastructure, not creature anatomy.
refcol=bpy.data.collections.new('REFERENCE | 1.75 metre human');SCENE.collection.children.link(refcol)
refmat=bpy.data.materials.new('Human scale neutral');refmat.diffuse_color=(.18,.20,.22,1)
refparts=[]
for label,points,radii in [('Torso',[(2.3,.8,.85),(2.3,.8,1.25),(2.3,.8,1.47)],[.15,.21,.13]),('Head',[(2.3,.8,1.49),(2.3,.8,1.62),(2.3,.8,1.75)],[.075,.12,.012])]:
 refparts.append(tube('Reference '+label,points,radii,'Skin',12,4,record=False))
for sign in [-1,1]:
 refparts.append(tube('Reference leg',[(2.3+sign*.10,.8,.88),(2.3+sign*.13,.8,.43),(2.3+sign*.14,.72,.03)],[.09,.065,.05],'Skin',10,3,record=False))
 refparts.append(tube('Reference arm',[(2.3+sign*.19,.8,1.40),(2.3+sign*.28,.8,1.03),(2.3+sign*.31,.8,.82)],[.065,.05,.035],'Skin',10,3,record=False))
for obj in refparts:
 COL.objects.unlink(obj);refcol.objects.link(obj);obj.data.materials.clear();obj.data.materials.append(refmat);obj.hide_render=True
# Render studio and dark setup are editable; no existing user's scene is cleared.
def area(name,position,target,power,color,size):
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.color=color;data.shape='DISK';data.size=size
 obj=link(bpy.data.objects.new(name,data));obj.location=position;obj.rotation_euler=(V(target)-obj.location).to_track_quat('-Z','Y').to_euler();return obj
world=bpy.data.worlds.new('Vesper neutral studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.18,.20,.24,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;SCENE.world=world
area('Studio key',(4,-5,6),(0,-.4,1.7),1600,(1,.87,.74),4)
area('Studio fill',(-4,-2,4),(0,-.4,1.6),1000,(.64,.77,1),4)
area('Studio rim',(0,4,6),(0,0,1.8),1800,(.76,.86,1),3)
# Plain floor is demo infrastructure, not creature anatomy.
plane=mesh('Demo floor',[(-100,-100,0),(100,-100,0),(100,100,0),(-100,100,0)],[(0,1,2,3)],'Skin',record=False)
floormaterial=bpy.data.materials.new('Studio floor');floormaterial.diffuse_color=(.035,.04,.045,1);floormaterial.use_nodes=True;floormaterial.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.035,.04,.045,1);plane.data.materials.clear();plane.data.materials.append(floormaterial)
CAMERAS={}
for name,position,target,lens in [('Front',(4.4,-7,3.7),(0,-.15,1.5),46),('Side',(6.5,0,2.8),(0,-.2,1.5),46),('Back',(4.3,5.5,3.5),(0,.0,1.5),46),('HeadClose',(2.1,-4.4,3.4),(0,-1.55,2.52),62)]:
 data=bpy.data.cameras.new(name);data.lens=lens;cam=link(bpy.data.objects.new('Camera_'+name,data));cam.location=position;cam.rotation_euler=(V(target)-cam.location).to_track_quat('-Z','Y').to_euler();CAMERAS[name]=cam
SCENE.camera=CAMERAS['Front'];SCENE.render.engine='CYCLES';SCENE.cycles.samples=48;SCENE.cycles.use_denoising=False;SCENE.cycles.max_bounces=5
SCENE.render.resolution_x=1200;SCENE.render.resolution_y=900;SCENE.render.resolution_percentage=100
SCENE.view_settings.view_transform='AgX';SCENE.view_settings.look='AgX - Medium High Contrast'
SCENE.render.threads_mode='FIXED';SCENE.render.threads=min(8,os.cpu_count() or 4)
# Isolate our 19 takes in NLA: other user actions/scenes are never exported or deleted.
RIG['silence_actions']=json.dumps({n:a.name for n,a in ACTIONS.items()})
track=RIG.animation_data.nla_tracks.new();track.name='VESPER export takes';start=1
for name,action in ACTIONS.items():
 strip=track.strips.new(name,start,action);strip.name=name;strip.extrapolation='NOTHING';start=int(strip.frame_end)+2
RIG.animation_data.use_nla=False
# Export all three skinned levels with one shared deform hierarchy. IK is baked by the FBX exporter.
say('Exporting real skinned FBX and animation takes')
for level,objects in LODS.items():
 bpy.ops.object.select_all(action='DESELECT');RIG.select_set(True)
 for o in objects:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=RIG
 RIG.animation_data.action=None;RIG.animation_data.use_nla=level==0;reset_pose();SCENE.frame_set(1)
 bpy.ops.export_scene.fbx(filepath=str(ART/'Models'/f'Vesper_LOD{level}.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,add_leaf_bones=False,use_armature_deform_only=True,bake_anim=level==0,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=level==0,bake_anim_simplify_factor=.5,bake_anim_step=1,path_mode='STRIP',mesh_smooth_type='FACE')
 for o in objects:
  o.hide_set(level!=0);o.hide_render=level!=0
RIG.animation_data.use_nla=False;RIG.animation_data.action=ACTIONS['Idle'];SCENE.frame_set(1);SCENE.frame_end=70
REPORT['clips']=CLIPS;REPORT['bones']=len(BONES);REPORT['deform_bones']=sum(b[3] for b in BONES.values());REPORT['texture_resolution']=SIZE
REPORT['lods']={str(level):sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in items) for level,items in LODS.items()}
REPORT['limitations']=['Not accepted as AAA or photorealistic final art','Voxel/decimate surface is not artist-retopologized production edge flow','Procedural animation contacts, foot sliding and clipping need further review','Unity Editor API compilation, FBX rig/clip import, native performance and live multiplayer not run','Death excluded: existing game does not allow killing this predator']
REPORT['seconds']=time.time()-START
(ART/'Documentation'/'generation.json').write_text(json.dumps(REPORT,indent=2)+'\n')
bpy.ops.object.select_all(action='DESELECT');RIG.hide_set(True);GAME[0].select_set(True);bpy.context.view_layer.objects.active=GAME[0]
SCENE['silence_rig']=RIG.name;SCENE['silence_cameras']=json.dumps({n:o.name for n,o in CAMERAS.items()})
if not bpy.app.background and bpy.context.area:
 area=bpy.context.area;area.type='VIEW_3D';space=area.spaces.active
 space.region_3d.view_location=V((0,-.1,1.5));space.region_3d.view_rotation=CAMERAS['Front'].rotation_euler.to_quaternion();space.region_3d.view_distance=7
 space.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SilencePredator.blend'),compress=True)
# Now that the .blend location is known, make only our maps relative. Existing user assets
# were remapped by Blender's Save As and keep their original resolved locations.
for image in bpy.data.images:
 if image.filepath_raw.startswith(str(ART)):image.filepath_raw='//Assets/Creatures/SilencePredator/Textures/'+pathlib.Path(image.filepath_raw).name
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'SilencePredator.blend'),compress=True)
say('Saved '+str(OUT/'SilencePredator.blend')+' | '+str(REPORT['lods']))

UNITY_RUNTIME = 'using UnityEngine;\nnamespace Silence.Predator\n{\n    [DisallowMultipleComponent]\n    public sealed class MonsterAnimationController : MonoBehaviour\n    {\n        public Animator animator;\n        public bool demoControls;\n        private static readonly string[] DemoClips = {"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenStart","ListenEnd","AttackBite","AttackClaw","Lunge","Alert","Search","Investigate","Stunned","Recover"};\n        private string active = "Idle";\n        private void Awake() { if (animator != null) animator.applyRootMotion = false; }\n        public bool Play(string clip, float playback = 1)\n        {\n            if (animator == null || animator.runtimeAnimatorController == null ||\n                !animator.HasState(0, Animator.StringToHash("Base Layer." + clip))) return false;\n            animator.speed = Mathf.Clamp(playback, .1f, 3);\n            animator.CrossFadeInFixedTime("Base Layer." + clip, .12f, 0); active = clip; return true;\n        }\n        // Pass only an already perceived sound from the authoritative hearing system.\n        // This class never finds players, applies damage or moves the network root.\n        public void SetHearing(float perceived) { if (perceived > .05f) Play("ListenStart"); else Play("ListenEnd"); }\n        public void SetMotion(float metresPerSecond, bool chasing)\n        {\n            string clip = metresPerSecond < .1f ? "Idle" : chasing ? "Run" : "Walk";\n            if (clip != active) Play(clip);\n            if (animator != null) animator.speed = metresPerSecond < .1f ? 1 : Mathf.Clamp(metresPerSecond / (chasing ? 2.23f : .67f), .5f, 3);\n        }\n        private void OnGUI()\n        {\n            if (!demoControls) return;\n            GUILayout.BeginArea(new Rect(16,16,225,Screen.height-32), GUI.skin.box);\n            GUILayout.Label("SILENCE / Vesper — preview");\n            foreach (string clip in DemoClips) if (GUILayout.Button(clip)) Play(clip);\n            GUILayout.EndArea();\n        }\n    }\n}\n'
(ART/'Scripts/MonsterAnimationController.cs').write_text(UNITY_RUNTIME,encoding='utf-8')

UNITY_EDITOR = '#if UNITY_EDITOR\nusing System;\nusing System.IO;\nusing System.Linq;\nusing System.Collections.Generic;\nusing UnityEditor;\nusing UnityEditor.Animations;\nusing UnityEditor.SceneManagement;\nusing UnityEngine;\nusing UnityEngine.Rendering;\nusing Silence.Predator;\nusing Object = UnityEngine.Object;\nnamespace Silence.Predator.Editor\n{\n    internal sealed class PredatorImporter : AssetPostprocessor\n    {\n        private const string Root="Assets/Creatures/SilencePredator/";\n        private void OnPreprocessModel()\n        {\n            if(!assetPath.StartsWith(Root+"Models/",StringComparison.Ordinal))return;\n            var m=(ModelImporter)assetImporter;m.animationType=ModelImporterAnimationType.Generic;\n            m.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;m.optimizeGameObjects=false;\n            m.importAnimation=assetPath.EndsWith("LOD0.fbx",StringComparison.Ordinal);m.addCollider=false;\n            m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.CalculateMikk;\n        }\n        private void OnPreprocessTexture()\n        {\n            if(!assetPath.StartsWith(Root+"Textures/",StringComparison.Ordinal))return;\n            var t=(TextureImporter)assetImporter;t.maxTextureSize=4096;t.mipmapEnabled=true;t.anisoLevel=8;\n            t.sRGBTexture=assetPath.Contains("_BaseColor");\n            if(assetPath.Contains("_Normal"))t.textureType=TextureImporterType.NormalMap;\n            t.textureCompression=TextureImporterCompression.CompressedHQ;\n        }\n    }\n    public static class SilencePredatorSetup\n    {\n        private const string Root="Assets/Creatures/SilencePredator";\n        [MenuItem("SILENCE/17 Create Original Predator Demo")]\n        public static void Generate()\n        {\n            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");\n            string scenePath=Root+"/Demo/Monster_Demo.unity";\n            if(File.Exists(scenePath)){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(scenePath);return;}\n            if(!File.Exists(Root+"/Models/Vesper_LOD0.fbx"))throw new InvalidOperationException("Run the Blender generator, then copy its Assets/Creatures/SilencePredator folder here.");\n            if(Shader.Find("Universal Render Pipeline/Lit")==null)throw new InvalidOperationException("This demo requires URP Lit.");\n            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;\n            foreach(string folder in new[]{"Materials","Animations","Prefabs","Demo"})Folder(Root+"/"+folder);\n            for(int i=0;i<3;i++)AssetDatabase.ImportAsset(Root+"/Models/Vesper_LOD"+i+".fbx",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);\n            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/Vesper_LOD0.fbx");\n            var takes=importer.defaultClipAnimations;\n            string[] loops={"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenLoop","Search","Investigate"};\n            foreach(var take in takes){take.name=take.name.Split(\'|\').Last();take.loopTime=loops.Contains(take.name);take.lockRootRotation=true;take.lockRootHeightY=true;take.lockRootPositionXZ=true;}\n            if(takes.Length!=19)throw new InvalidOperationException("Expected 19 imported takes; inspect the FBX Animation tab.");\n            importer.clipAnimations=takes;importer.SaveAndReimport();\n            var mats=new Dictionary<string,Material>();foreach(string name in new[]{"Skin","Armor","Tissue","Bone"})mats[name]=Material(name);\n            var controller=Controller();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);\n            var root=new GameObject("Monster_Creature");\n            var main=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Vesper_LOD0.fbx"));main.transform.SetParent(root.transform,false);\n            var animator=main.GetComponentInChildren<Animator>(true);if(animator==null)throw new InvalidOperationException("Generic Animator missing; inspect FBX rig import.");\n            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;\n            var driver=root.AddComponent<MonsterAnimationController>();driver.animator=animator;\n            var bones=main.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());\n            var lods=new LOD[3];var first=main.GetComponentsInChildren<SkinnedMeshRenderer>(true);Assign(first,mats);lods[0]=new LOD(.45f,first);\n            for(int level=1;level<=2;level++)\n            {\n                var bucket=new GameObject("LOD"+level);bucket.transform.SetParent(root.transform,false);\n                var temp=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Vesper_LOD"+level+".fbx"));\n                var renderers=new List<SkinnedMeshRenderer>();\n                foreach(var source in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))\n                {\n                    var go=new GameObject(source.name);go.transform.SetParent(bucket.transform,false);\n                    var matrix=temp.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;\n                    go.transform.localPosition=matrix.GetColumn(3);go.transform.localRotation=matrix.rotation;go.transform.localScale=matrix.lossyScale;\n                    var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=source.sharedMesh;\n                    skin.bones=source.bones.Select(b=>bones.TryGetValue(b.name,out var mapped)?mapped:throw new InvalidOperationException("Bone missing: "+b.name)).ToArray();\n                    skin.rootBone=source.rootBone!=null&&bones.TryGetValue(source.rootBone.name,out var rb)?rb:animator.transform;\n                    skin.localBounds=source.localBounds;renderers.Add(skin);\n                }\n                Object.DestroyImmediate(temp);Assign(renderers.ToArray(),mats);lods[level]=new LOD(level==1?.16f:.03f,renderers.ToArray());\n            }\n            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();\n            var body=root.AddComponent<CapsuleCollider>();body.height=2.9f;body.center=Vector3.up*1.45f;body.radius=.5f;\n            foreach(string name in new[]{"Head","Chest","Pelvis"})if(bones.TryGetValue(name,out var bone))\n            {var trigger=bone.gameObject.AddComponent<SphereCollider>();trigger.radius=.3f;trigger.isTrigger=true;}\n            foreach(var pair in new[]{("Audio_Mouth","Jaw"),("Audio_Hearing","Head"),("VFX_Breath","Head")})if(bones.TryGetValue(pair.Item2,out var parent))new GameObject(pair.Item1).transform.SetParent(parent,false);\n            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Monster_Creature.prefab");driver.demoControls=true;\n            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Demo floor";floor.transform.position=Vector3.down*.05f;floor.transform.localScale=new Vector3(16,.1f,16);\n            var scale=GameObject.CreatePrimitive(PrimitiveType.Capsule);scale.name="Human height reference 1.75m";scale.transform.position=new Vector3(2.4f,.875f,0);scale.transform.localScale=new Vector3(.5f,.875f,.5f);\n            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4.4f,3.7f,7);camera.transform.LookAt(new Vector3(0,1.5f,0));camera.fieldOfView=45;camera.gameObject.AddComponent<AudioListener>();\n            Light("Key",new Vector3(4,6,5),new Color(1,.85f,.7f),120,root.transform);Light("Fill",new Vector3(-4,4,2),new Color(.65f,.75f,1),70,root.transform);Light("Rim",new Vector3(0,5,-5),Color.white,130,root.transform);\n            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.17f,.19f,.23f);\n            AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene,scenePath))throw new IOException("Cannot save monster demo.");\n            Debug.Log("SILENCE: prefab and demo created. Check deformation, clips, hearing plates, LOD and materials in Play. This is not native visual/performance approval or a replacement of the authoritative game creature.");\n        }\n        private static void Assign(SkinnedMeshRenderer[] renderers,Dictionary<string,Material> materials)\n        {\n            foreach(var r in renderers){string key=materials.Keys.First(k=>r.name.StartsWith(k,StringComparison.Ordinal));r.sharedMaterials=new[]{materials[key]};r.localBounds=new Bounds(new Vector3(0,1.45f,0),new Vector3(6,5,7));}\n        }\n        private static Material Material(string name)\n        {\n            string path=Root+"/Materials/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null)return old;\n            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};\n            if(name=="Bone"){mat.SetColor("_BaseColor",new Color(.24f,.205f,.155f));mat.SetFloat("_Smoothness",.57f);}\n            else\n            {\n                Texture2D Map(string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+suffix+".png")??throw new InvalidOperationException("Missing map "+name+suffix);\n                mat.SetTexture("_BaseMap",Map("_BaseColor"));mat.SetTexture("_BumpMap",Map("_Normal"));mat.SetTexture("_MetallicGlossMap",Map("_Mask"));mat.SetTexture("_OcclusionMap",Map("_Mask"));\n                mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.EnableKeyword("_OCCLUSIONMAP");\n            }\n            AssetDatabase.CreateAsset(mat,path);return mat;\n        }\n        private static AnimatorController Controller()\n        {\n            string path=Root+"/Animations/Monster.controller";var old=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(old!=null)return old;\n            var clips=AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/Vesper_LOD0.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToArray();\n            string[] names={"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenStart","ListenLoop","ListenEnd","Alert","TurnLeft","TurnRight","AttackBite","AttackClaw","Lunge","Search","Investigate","Stunned","Recover"};\n            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();\n            foreach(string name in names){var clip=clips.FirstOrDefault(c=>c.name==name||c.name.EndsWith("|"+name,StringComparison.Ordinal));if(clip==null)throw new InvalidOperationException("Imported clip missing: "+name);var state=machine.AddState(name);state.motion=clip;states[name]=state;}\n            machine.defaultState=states["Idle"];\n            foreach(string name in new[]{"ListenStart","ListenEnd","Alert","AttackBite","AttackClaw","Lunge","Stunned","Recover"}){var t=states[name].AddTransition(states[name=="ListenStart"?"ListenLoop":"Idle"]);t.hasExitTime=true;t.exitTime=1;t.duration=.08f;}\n            return controller;\n        }\n        private static void Light(string name,Vector3 position,Color color,float intensity,Transform subject){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Spot;l.transform.position=position;l.transform.LookAt(subject.position+Vector3.up*1.5f);l.range=20;l.spotAngle=80;l.color=color;l.intensity=intensity;l.shadows=LightShadows.Soft;}\n        private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf(\'/\');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}\n    }\n}\n#endif\n'
(ART/'Editor/SilencePredatorSetup.cs').write_text(UNITY_EDITOR,encoding='utf-8')
