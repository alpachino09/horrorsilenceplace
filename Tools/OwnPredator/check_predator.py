import bpy, json, pathlib, os, math
import numpy as np
from mathutils.kdtree import KDTree
OUT=pathlib.Path(os.environ.get('SILENCE_PREDATOR_OUTPUT','/workspace/generated-art/SilencePredator'))
DEST=pathlib.Path(os.environ.get('SILENCE_PREDATOR_REPORT','/tmp/predator-checks.json'))
scene=bpy.context.scene;rig=next(o for o in scene.objects if o.type=='ARMATURE');mapping=json.loads(rig['silence_actions']);checks=[]
def check(ok,name,**info):checks.append({'check':name,'passed':bool(ok),**info})
def action(name):return bpy.data.actions[mapping[name]]
def pose(name,fraction):
 rig.animation_data.use_nla=False;rig.animation_data.action=action(name)
 a,b=action(name).frame_range;scene.frame_set(int(a+(b-a)*fraction));bpy.context.view_layer.update()
def evaluated(objects):
 dg=bpy.context.evaluated_depsgraph_get();result=[]
 for ob in objects:
  evalob=ob.evaluated_get(dg);me=evalob.to_mesh();mat=evalob.matrix_world
  result.extend(mat@v.co for v in me.vertices);evalob.to_mesh_clear()
 return result
check(len(mapping)==19 and all(n in bpy.data.actions for n in mapping.values()),'19 distinct actions survive saving/reopening .blend')
images={node.image for ob in scene.objects if ob.type=='MESH' and ob.name.endswith('_LOD0') for mat in ob.data.materials if mat and mat.use_nodes for node in mat.node_tree.nodes if node.type=='TEX_IMAGE' and node.image}
check(len(images)==12 and all(pathlib.Path(bpy.path.abspath(im.filepath)).is_file() for im in images),'All twelve saved material texture links resolve after reopening')
check(sum(b.use_deform for b in rig.data.bones)==55,'55 deform bones')
check(sum(b.name.startswith('Hearing_') for b in rig.data.bones)==6,'Six independent hearing valves')
check(sum(c.type=='IK' for p in rig.pose.bones for c in p.constraints)==4,'Four leg IK chains exist')
triangles=[]
for level in range(3):
 meshes=[o for o in scene.objects if o.type=='MESH' and o.name.endswith('_LOD'+str(level))]
 triangles.append(sum(sum(len(f.vertices)-2 for f in o.data.polygons) for o in meshes))
 good=all(len(o.data.uv_layers)>0 for o in meshes)
 norm=True;limit=True;finite=True
 for ob in meshes:
  for vertex in ob.data.vertices:
   vals=[g.weight for g in vertex.groups if g.weight>1e-6];norm=norm and abs(sum(vals)-1)<.002;limit=limit and len(vals)<=4;finite=finite and all(math.isfinite(v) for v in vertex.co)
 check(good and len(meshes)==4,'LOD'+str(level)+' four UV-mapped material meshes')
 check(norm and limit and finite,'LOD'+str(level)+' finite geometry, normalized <=4 bone influences')
check(triangles[0]>triangles[1]>triangles[2],'Strictly decreasing LOD counts',triangles=triangles)
if 'silence_morphology' in rig:
 morphology=json.loads(rig['silence_morphology'])
 check(all(low<=value<=high for value,(low,high) in zip(triangles,[(80000,150000),(35000,60000),(10000,20000)])),
       'All three LODs fit the requested triangle ranges',triangles=triangles)
 # Count actual disconnected fang surfaces after joining/decimation, not source object names.
 teeth=next(o for o in scene.objects if o.name=='Bone_LOD0');parents=list(range(len(teeth.data.vertices)))
 def find_root(index):
  while parents[index]!=index:parents[index]=parents[parents[index]];index=parents[index]
  return index
 for edge in teeth.data.edges:
  a,b=map(find_root,edge.vertices);parents[b]=a
 components={}
 for vertex in teeth.data.vertices:components.setdefault(find_root(vertex.index),[]).append(vertex.co.copy())
 fangs=[]
 for points in components.values():
  center=sum(points,points[0]*0)/len(points)
  if center.y<-1.60 and center.z>1.30:fangs.append(center)
 check(len(fangs)==morphology['fangs']==104,'Actual joined mesh retains 104 individual fangs',actual_fangs=len(fangs))
 sectors=[sum((p.x>=0)==right and (p.z>=2.28)==upper for p in fangs) for right in [False,True] for upper in [False,True]]
 check(min(sectors)>=20,'Fangs occupy upper, lower and both lateral mouth sectors',sector_counts=sectors)
for name in mapping:
 for fraction in [0,.5,1]:
  pose(name,fraction)
  check(all(math.isfinite(v) for pb in rig.pose.bones for row in pb.matrix for v in row),'Finite pose '+name+' '+str(fraction))
pose('Idle',0);closed={b:rig.pose.bones[b].matrix.copy() for b in rig.pose.bones.keys() if b.startswith('Hearing_')}
pose('ListenLoop',.5)
check(all(sum(abs(a-b) for row1,row2 in zip(m,rig.pose.bones[n].matrix) for a,b in zip(row1,row2))>.2 for n,m in closed.items()),'All six hearing valves actually change pose')
if 'silence_morphology' in rig:
 pose('Idle',0);closed_jaw=(rig.pose.bones['Head'].matrix.inverted()@rig.pose.bones['Jaw'].matrix).to_quaternion()
 pose('AttackBite',.5);open_jaw=(rig.pose.bones['Head'].matrix.inverted()@rig.pose.bones['Jaw'].matrix).to_quaternion()
 check(closed_jaw.rotation_difference(open_jaw).angle>.15,'Rebuilt mandible still articulates independently of the skull')
body=next(o for o in scene.objects if o.name=='Skin_LOD0');pose('Idle',0);coords=evaluated([body]);mn=[min(p[i] for p in coords) for i in range(3)];mx=[max(p[i] for p in coords) for i in range(3)]
check(2.4<mx[2]<3.3,'Creature height in metres',skin_bounds=[mn,mx])
for name in ['Skin','Armor','Tissue','Bone']:
 for suffix in ['BaseColor','Normal','Mask']:
  p=OUT/'Assets/Creatures/SilencePredator/Textures'/(name+'_'+suffix+'.png')
  im=bpy.data.images.load(str(p),check_existing=False);check(tuple(im.size)==(4096,4096),'4K map '+name+' '+suffix);bpy.data.images.remove(im)
# Inspect actual FBX, not only its binary header.
source_scene=scene;source_meshes=[o for o in scene.objects if o.name.endswith('_LOD0')]
importscene=bpy.data.scenes.new('FBX verification only');bpy.context.window.scene=importscene
oldactions=set(bpy.data.actions)
bpy.ops.import_scene.fbx(filepath=str(OUT/'Assets/Creatures/SilencePredator/Models/Vesper_LOD0.fbx'),use_image_search=False)
imported_actions=[a for a in bpy.data.actions if a not in oldactions];import_rig=next(o for o in importscene.objects if o.type=='ARMATURE')
check(len(import_rig.data.bones)==55,'Exported/reimported FBX contains 55 deform bones')
check(all(any(a.name==name or a.name.endswith('|'+name) for a in imported_actions) for name in mapping),'Exported/reimported FBX contains all 19 named takes',takes=[a.name for a in imported_actions])
check(len([o for o in importscene.objects if o.type=='MESH'])==4,'FBX re-import has four skinned material meshes')
# Geometric drift checks compare actual deformed vertices at sampled times.
for name in ['Idle','Walk','Run','ListenLoop','AttackBite']:
 target=next((a for a in imported_actions if a.name==name or a.name.endswith('|'+name)),None)
 if target is None:continue
 for fraction in [0,.5]:
  bpy.context.window.scene=source_scene;scene=source_scene;pose(name,fraction);reference=evaluated(source_meshes)
  bpy.context.window.scene=importscene;scene=importscene;import_rig.animation_data.action=target
  start,end=target.frame_range;scene.frame_set(int(start+(end-start)*fraction));bpy.context.view_layer.update()
  result=evaluated([o for o in importscene.objects if o.type=='MESH'])
  tree=KDTree(len(result))
  for i,p in enumerate(result):tree.insert(p,i)
  tree.balance();error=max(tree.find(p)[2] for p in reference[::9]);check(error<.025,'FBX deformed surface '+name+' '+str(fraction),max_sampled_distance_metres=error)
# Every lower LOD must use the same deform skeleton for Unity bone remapping.
base_bones={b.name:b.matrix_local.copy() for b in import_rig.data.bones}
for level in [1,2]:
 extra=bpy.data.scenes.new('FBX LOD'+str(level)+' verification');bpy.context.window.scene=extra
 bpy.ops.import_scene.fbx(filepath=str(OUT/'Assets/Creatures/SilencePredator/Models'/('Vesper_LOD'+str(level)+'.fbx')),use_image_search=False)
 other=next(o for o in extra.objects if o.type=='ARMATURE');skinned=[o for o in extra.objects if o.type=='MESH']
 check(set(base_bones)=={b.name for b in other.data.bones},'FBX LOD'+str(level)+' matches shared bone names')
 drift=max(abs(a-c) for b in other.data.bones for row1,row2 in zip(base_bones[b.name],b.matrix_local) for a,c in zip(row1,row2))
 check(drift<.001 and len(skinned)==4 and all(o.data.uv_layers for o in skinned),'FBX LOD'+str(level)+' rest transforms and four UV meshes',max_bone_matrix_error=drift)
report={'kind':'Actual Blender source, skin/pose and FBX roundtrip checks; not Unity validation','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
DEST.parent.mkdir(parents=True,exist_ok=True);DEST.write_text(json.dumps(report,indent=2)+'\n')
print('PREDATOR_CHECKS',report['passed'],report['failed'])
for c in checks:
 if not c['passed']:print('FAIL',c)
if report['failed']:raise RuntimeError('Predator verification failed')
