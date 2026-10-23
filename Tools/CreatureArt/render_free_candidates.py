import bpy, math, pathlib, shlex, json, argparse, sys
from mathutils import Vector, Quaternion, Matrix
parser=argparse.ArgumentParser(description='Render actual free Unvanquished creature assets, not concept images.')
parser.add_argument('--source',type=pathlib.Path,required=True)
parser.add_argument('--output',type=pathlib.Path,required=True)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
BASE=args.source;OUT=args.output;OUT.mkdir(parents=True,exist_ok=True)
NAMES={0:'Dretch',1:'Mantis',2:'Marauder',3:'Dragoon',4:'Tyrant'}

def matrix(values):
 loc=Vector(values[:3]);q=Quaternion((values[6],values[3],values[4],values[5]));q.normalize()
 scale=values[7:10] if len(values)>=10 else [1,1,1]
 return Matrix.Translation(loc)@q.to_matrix().to_4x4()@Matrix.Diagonal((*scale,1))

def read_iqe(path):
 joints=[];bind=[];parts=[];current=None;pose=[];frame=False;animations=[]
 for line in path.open():
  if not line.strip() or line.startswith('#'):continue
  if line.startswith('joint '):
   bits=shlex.split(line);joints.append((bits[1],int(bits[2])))
  elif line.startswith('pq '):
   v=list(map(float,line.split()[1:]));(pose if frame else bind).append(v)
  elif line.startswith('mesh '):
   current={'name':shlex.split(line)[1],'v':[],'uv':[],'weights':[],'faces':[],'material':None};parts.append(current)
  elif line.startswith('material '):current['material']=shlex.split(line)[1]
  elif line.startswith('vp '):current['v'].append(list(map(float,line.split()[1:4])))
  elif line.startswith('vt '):current['uv'].append(list(map(float,line.split()[1:3])))
  elif line.startswith('vb '):
   bits=line.split()[1:];current['weights'].append([(int(bits[i]),float(bits[i+1])) for i in range(0,len(bits),2)])
  elif line.startswith('fm '):current['faces'].append(tuple(map(int,line.split()[1:])))
  elif line.startswith('animation '):
   animations.append(shlex.split(line)[1])
   if frame and pose:break
  elif line.startswith('frame '):
   if frame:break
   frame=True
 def world(values):
  out=[]
  for i,(name,parent) in enumerate(joints):
   m=matrix(values[i]);out.append(out[parent]@m if parent>=0 else m)
  return out
 rest=world(bind);stand=world(pose) if len(pose)==len(bind) else rest
 skin=[p@b.inverted_safe() for p,b in zip(stand,rest)]
 for part in parts:
  part['v']=[sum((skin[j]@Vector(v)*w for j,w in weights),Vector()) for v,weights in zip(part['v'],part['weights'])]
 return parts,joints

def material(path):
 m=bpy.data.materials.new(path);m.use_nodes=True;nodes=m.node_tree.nodes;links=m.node_tree.links;p=nodes.get('Principled BSDF')
 # Asset source uses diffuse/normal/specular. Preview uses a neutral roughness approximation.
 p.inputs['Roughness'].default_value=.48;p.inputs['Specular IOR Level'].default_value=.35
 texfile=BASE/(path+'.png')
 if not texfile.exists():texfile=BASE/(path+'.jpg')
 if texfile.exists():
  t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(texfile),check_existing=True);links.new(t.outputs['Color'],p.inputs['Base Color'])
 normal=BASE/(path+'_n.png')
 if normal.exists():
  t=nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(normal),check_existing=True);t.image.colorspace_settings.name='Non-Color'
  n=nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.7;links.new(t.outputs['Color'],n.inputs['Color']);links.new(n.outputs[0],p.inputs['Normal'])
 return m

def lamp(name,p,energy,size,color,target):
 l=bpy.data.lights.new(name,'AREA');l.energy=energy;l.shape='DISK';l.size=size;l.color=color
 o=bpy.data.objects.new(name,l);bpy.context.collection.objects.link(o);o.location=p;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()

report=[]
for level,name in NAMES.items():
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 source=BASE/f'models/players/level{level}/level{level}.iqe';parts,joints=read_iqe(source);objects=[]
 for part in parts:
  mesh=bpy.data.meshes.new(part['name']);mesh.from_pydata(part['v'],[],part['faces']);mesh.update();uv=mesh.uv_layers.new()
  for loop in mesh.loops:
   u,v=part['uv'][loop.vertex_index];uv.data[loop.index].uv=(u,1-v)
  for face in mesh.polygons:face.use_smooth=True
  obj=bpy.data.objects.new(part['name'],mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(material(part['material']));objects.append(obj)
 points=[obj.matrix_world@v.co for obj in objects for v in obj.data.vertices]
 mn=Vector([min(v[i] for v in points) for i in range(3)]);mx=Vector([max(v[i] for v in points) for i in range(3)])
 span=max(mx-mn);scale=3/span;center=(mn+mx)/2
 for obj in objects:
  obj.location=(-center.x*scale,-center.y*scale,-mn.z*scale);obj.scale=(scale,)*3
 height=(mx.z-mn.z)*scale;target=Vector((0,0,height*.48))
 bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.012));ground=bpy.context.object
 m=bpy.data.materials.new('Floor');m.diffuse_color=(.035,.045,.055,1);m.use_nodes=True;m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=m.diffuse_color;m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.8;ground.data.materials.append(m)
 world=bpy.data.worlds.new('Neutral studio');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.19,.23,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;bpy.context.scene.world=world
 lamp('Key',(3,-4,5),550,4,(.83,.9,1),target);lamp('Fill',(-4,-2,3),300,4,(.65,.75,1),target);lamp('Rim',(0,3,5),650,3,(1,.79,.62),target)
 camdata=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camdata);bpy.context.collection.objects.link(cam)
 cam.location=(3.7,-5.8,max(2.5,height*.7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();camdata.type='ORTHO';camdata.ortho_scale=3.85;bpy.context.scene.camera=cam
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=False;scene.cycles.max_bounces=4;scene.render.threads_mode='FIXED';scene.render.threads=6
 scene.render.resolution_x=560;scene.render.resolution_y=480;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(OUT/f'{level}-{name}.png')
 scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast'
 bpy.ops.render.render(write_still=True)
 allanimations=[]
 for line in source.open():
  if line.startswith('animation '):allanimations.append(shlex.split(line)[1])
 triangles=sum(sum(len(f)-2 for f in part['faces']) for part in parts)
 report.append({'name':name,'level':level,'source':str(source.relative_to(BASE)),'joints':len(joints),'triangles':triangles,'animations':allanimations,'preview':str(OUT/f'{level}-{name}.png'),'render':'Blender Cycles neutral studio; original mesh posed at first stand frame; approximate roughness, not native Unity','unity_import_tested':False})
(OUT/'inventory.json').write_text(json.dumps(report,indent=2)+'\n')
