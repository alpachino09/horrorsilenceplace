"""Blender asset review, not Unity screenshots. Before/after uses identical camera and illumination."""
import bpy,math,pathlib,struct,json,sys
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[2];ART=ROOT/'Assets/_Silence/Art/VisualRebuild';OUT=ROOT/'Docs/VisualRebuild/Previews';OUT.mkdir(exist_ok=True)

def V(p):return Vector((p[0],-p[2],p[1]))
# An emissive bulb must not shadow a point light located inside its physical mesh.
for name in ['Bulb','Glass']:
 mat=bpy.data.materials.get(name)
 if mat:
  nodes=mat.node_tree.nodes;links=mat.node_tree.links;output=nodes.get('Material Output');principled=nodes.get('Principled BSDF')
  lightpath=nodes.new('ShaderNodeLightPath');transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader')
  links.new(lightpath.outputs['Is Shadow Ray'],mix.inputs[0]);links.new(principled.outputs['BSDF'],mix.inputs[1]);links.new(transparent.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])

def instance(name,p,scale=1,rotation=0):
 source=bpy.data.objects[name];obj=source.copy();obj.data=source.data;bpy.context.collection.objects.link(obj)
 obj.hide_render=False;obj.hide_set(False);obj.location=V(p);obj.scale=(scale,)*3;obj.rotation_euler.z=rotation;return obj

def simple_mat(name,color):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.9;return m

def cube(p,size,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=V(p));obj=bpy.context.object;obj.scale=(size[0],size[2],size[1]);obj.data.materials.append(mat);return obj

def light(kind,name,p,color,energy,size=1,target=None):
 data=bpy.data.lights.new(name,kind);data.energy=energy;data.color=color
 if kind=='AREA':data.shape='DISK';data.size=size
 if kind=='POINT':data.shadow_soft_size=size
 obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj);obj.location=V(p)
 if target:obj.rotation_euler=(V(target)-obj.location).to_track_quat('-Z','Y').to_euler()
 return obj

def ground():
 raw=(ART/'Textures/FarmTerrain.bytes').read_bytes();_,n,seed=struct.unpack_from('<4sii',raw);h=struct.unpack_from('<'+'f'*(n*n),raw,12);weights=struct.unpack_from('<'+'f'*(n*n*4),raw,12+n*n*4)
 verts=[V((-64+x, h[z*n+x]*10-3,-64+z)) for z in range(n) for x in range(n)]
 faces=[(z*n+x,(z+1)*n+x,(z+1)*n+x+1,z*n+x+1) for z in range(n-1) for x in range(n-1)]
 mesh=bpy.data.meshes.new('Shared terrain samples');mesh.from_pydata(verts,[],faces);mesh.update();obj=bpy.data.objects.new('Shared authored terrain',mesh);bpy.context.collection.objects.link(obj)
 mesh.uv_layers.new(name='UVMap')
 for i in range(4):mesh.attributes.new(name=f'GroundWeight{i}',type='FLOAT',domain='CORNER')
 # Re-fetch RNA attributes after allocation: adding custom data invalidates earlier layer references.
 loop_vertices=[loop.vertex_index for loop in mesh.loops]
 uvs=[component for vi in loop_vertices for component in ((verts[vi].x+64)/4,(-verts[vi].y+64)/4)]
 mesh.uv_layers['UVMap'].data.foreach_set('uv',uvs)
 for i in range(4):mesh.attributes[f'GroundWeight{i}'].data.foreach_set('value',[weights[vi*4+i] for vi in loop_vertices])
 mesh.update()
 for poly in mesh.polygons:poly.use_smooth=True
 assert len(mesh.polygons)==128*128 and len(mesh.vertices)==129*129
 print('TERRAIN_REVIEW',len(mesh.vertices),len(mesh.polygons),obj.hide_render,flush=True)
 mat=bpy.data.materials.new('Ground PBR layer review');mat.use_nodes=True;nodes=mat.node_tree.nodes;links=mat.node_tree.links;p=nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.9; p.inputs['Specular IOR Level'].default_value=.15
 # Four scalar attributes avoid the vertex-color alpha unpremultiplication of numeric weights.
 result=None
 for i,name in enumerate(['Forest','Grass','Mud','Gravel']):
  tex=nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ART/f'Textures/{name}_BaseColor.png'),check_existing=True)
  attribute=nodes.new('ShaderNodeAttribute');attribute.attribute_name=f'GroundWeight{i}';factor=attribute.outputs['Fac']
  mult=nodes.new('ShaderNodeVectorMath');mult.operation='SCALE';links.new(tex.outputs['Color'],mult.inputs[0]);links.new(factor,mult.inputs[3])
  if result is None:result=mult.outputs[0]
  else:
   add=nodes.new('ShaderNodeVectorMath');add.operation='ADD';links.new(result,add.inputs[0]);links.new(mult.outputs[0],add.inputs[1]);result=add.outputs[0]
 links.new(result,p.inputs['Base Color']);mesh.materials.append(mat)
 return h

def setup(after=True):
 for obj in list(bpy.context.scene.objects):
  if obj.name not in bpy.data.objects or obj.type in ['CAMERA','LIGHT'] or not obj.hide_render:bpy.data.objects.remove(obj,do_unlink=True)
 # Source asset objects are all hidden, and remain available as linked mesh data.
 if after:
  h=ground();instance('Farmhouse_LOD0',(-10,0,0));instance('Barn_LOD0',(9,0,6));instance('DoorLeaf',(-10.8,0,-4),1,0)
  instance('Table',(-11,.5,-1));instance('Radio',(-12.5,.8,1),.45);instance('WallCabinet',(-10,0,3.45));instance('CeilingLamp',(-10,2.8,-.3))
  for i in range(-19,20,2):instance('FencePanel',(i,0,-18))
  for z in range(-17,18,2):
   for x in [-20,20]:instance('FencePanel',(x,0,z),1,math.pi/2)
  for i in range(25):
   a=i*2.39996;radius=24+i%5*3;x=math.cos(a)*radius;z=math.sin(a)*radius
   ix=max(0,min(128,round(x+64)));iz=max(0,min(128,round(z+64)));y=h[iz*129+ix]*10-3
   instance(f'Pine_{[31,47,83][i%3]}_LOD0',(x,y,z),.8+i%4*.1,i*.87)
  for i in range(40):
   a=i*2.399;radius=17+i%5*3;x=math.cos(a)*radius;z=math.sin(a)*radius
   if abs(x)<14 and abs(z)<11:continue
   ix=round(x+64);iz=round(z+64);y=h[iz*129+ix]*10-3
   instance('Fern' if i%3 else 'Rock_5',(x,y,z),.4+i%4*.15,i)
 else:
  grey=simple_mat('Legacy plaster',(.48,.45,.35));wood=simple_mat('Legacy wood',(.3,.2,.12));grass=simple_mat('Legacy grass',(.18,.23,.13))
  cube((0,-.25,0),(44,.5,44),grass)
  cube((-10,.025,0),(8,.05,8),grey);cube((-10,2.2,4),(8,4.4,.2),grey)
  for side in [-1,1]:
   cube((-10+side*4,2.2,0),(.2,4.4,8),grey);cube((-10+side*2.4,2.2,-4),(3.2,4.4,.2),grey)
  cube((-10,3.35,-4),(1.6,2.1,.2),grey);cube((-10,4.5,0),(8.5,.2,8.5),wood)
  cube((-10,1.11,-4),(1.6,2.22,.12),wood)
  for i in range(-20,21,2):cube((i,.9,-18),(.18,1.8,.18),wood)
  for y in [.65,1.35]:cube((0,y,-18),(40,.16,.14),wood)
 world=bpy.data.worlds.new('Muted overcast evening');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.26,.32,.40,1);world.node_tree.nodes['Background'].inputs[1].default_value=.4;bpy.context.scene.world=world
 sun=light('SUN','Evening sunlight',(0,10,-10),(1,.81,.64),1.3,target=(-10,0,0));sun.data.angle=.10
 light('AREA','Sky from open yard',(-5,9,-5),(.55,.68,1),450,size=12,target=(-10,0,0))
 light('POINT','Ceiling practical',(-10,2.8,-.3),(1,.72,.43),180,size=.07)
 light('POINT','Practical house lamp',(-11.2,2.05,-3.35),(1,.63,.30),35,size=.06)
 data=bpy.data.cameras.new('Reference camera');cam=bpy.data.objects.new('Reference camera',data);bpy.context.collection.objects.link(cam);bpy.context.scene.camera=cam;data.lens=28
 return cam

def render(name,position,target,after=True):
 cam=setup(after);cam.location=V(position);cam.rotation_euler=(V(target)-cam.location).to_track_quat('-Z','Y').to_euler()
 if 'interior' in name:
  lamp=light('SPOT','Camera-mounted flashlight',position,(1,.91,.76),85,target=target);lamp.data.spot_size=math.radians(58);lamp.data.spot_blend=.45;lamp.data.shadow_soft_size=.04
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=64;scene.cycles.use_denoising=False;scene.cycles.max_bounces=5;scene.cycles.transparent_max_bounces=8
 scene.render.resolution_x=1152;scene.render.resolution_y=720;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/name)
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.view_settings.exposure=.35
 scene.render.threads_mode='FIXED';scene.render.threads=6
 bpy.ops.render.render(write_still=True)
shot='all'
if '--' in sys.argv:shot=sys.argv[sys.argv.index('--')+1]
shots=[('before-exterior.png',(0,1.7,-11),(-10,2,0),False),('after-exterior.png',(0,1.7,-11),(-10,2,0),True),('after-interior.png',(-10,1.55,-3.05),(-11.1,1.2,1.6),True)]
for index,s in enumerate(shots):
 if shot=='all' or shot==str(index):render(*s)
(OUT/'render-settings.json').write_text(json.dumps({'engine':'Blender 4.3.2 Cycles CPU','samples':64,'denoising':False,'unity_screenshot':False,'baseline':'Recreated from existing Unity generator dimensions; not a captured Unity scene','resolution':[1152,720],'lens_mm':28,'exterior_camera_unity_coordinates':[0,1.7,-11],'exterior_look_target':[-10,2,0],'world_strength':.4,'sun_energy':1.3,'sun_color':[1,.81,.64]},indent=2)+'\n')
(OUT/'README.md').write_text('# Blender asset previews\n\nThese are Blender Cycles renders of authored meshes/textures, not Unity screenshots.\nBefore is a geometric reconstruction from the existing Unity generator, not a Unity capture. Before/after exterior uses the exact same camera, world and lights.\nUnity integration, final art acceptance and GPU performance remain unverified.\n')
