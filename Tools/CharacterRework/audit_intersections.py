"""Actual deformed triangle overlaps of selected hand/body surfaces in Blender.

Unlike a nearest-normal sign on an open torso patch this tests crossing surfaces.
It still does not prove complete self-collision freedom or Unity retarget quality.
"""
import argparse
import json
from pathlib import Path
import bpy
from mathutils.bvhtree import BVHTree

p=argparse.ArgumentParser()
p.add_argument('--blend',type=Path,required=True)
p.add_argument('--output',type=Path,required=True)
a=p.parse_args()
test=BVHTree.FromPolygons([(0,0,0),(1,0,0),(0,1,0)],[(0,1,2)])
separate=BVHTree.FromPolygons([(.6,.6,0),(1,.6,0),(.6,1,0)],[(0,1,2)])
crossing=BVHTree.FromPolygons([(.2,.2,-1),(.2,.2,1),(.2,.8,0)],[(0,1,2)])
assert not test.overlap(separate) and test.overlap(crossing)
bpy.ops.wm.open_mainfile(filepath=str(a.blend.resolve()),use_scripts=False)
rig=bpy.data.objects['HoodedGirl_Rig'];body=bpy.data.objects['Body_Clothed']
rig.animation_data.use_nla=False
groups={g.index:g.name for g in body.vertex_groups}
torso_names={'Hips','Spine','Chest','UpperChest','LeftUpperLeg','RightUpperLeg','LeftLowerLeg','RightLowerLeg'}
hand_names={b.name for b in rig.data.bones if 'Hand' in b.name or any(x in b.name for x in ['Index','Middle','Ring','Little','Thumb'])}
def selected(names):
 return {v.index for v in body.data.vertices if sum(g.weight for g in v.groups if groups[g.group] in names)>.65}
torso=selected(torso_names);hands=selected(hand_names)
torso_faces=[tuple(f.vertices) for f in body.data.polygons if all(i in torso for i in f.vertices)]
hand_faces=[tuple(f.vertices) for f in body.data.polygons if all(i in hands for i in f.vertices)]
assert torso_faces and hand_faces
records=[]
for name in ['Idle','Walk','WalkBack','Run','CrouchIdle','CrouchWalk','CarryIdle','CarryWalk','WoundedIdle','WoundedWalk','UseItem','TurnLeft','TurnRight','StrafeLeft','StrafeRight','Jump','Fall','Land','Death','LookAround']:
 action=bpy.data.actions[name];rig.animation_data.action=action
 if action.slots:rig.animation_data.action_slot=action.slots[0]
 samples=[]
 for i in range(13):
  frame=action.frame_range[0]+(action.frame_range[1]-action.frame_range[0])*i/12
  bpy.context.scene.frame_set(int(frame),subframe=frame-int(frame));bpy.context.view_layer.update()
  obj=body.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=obj.to_mesh()
  vertices=[obj.matrix_world@v.co for v in mesh.vertices]
  torso_tree=BVHTree.FromPolygons(vertices,torso_faces);hand_tree=BVHTree.FromPolygons(vertices,hand_faces)
  overlaps=torso_tree.overlap(hand_tree)
  samples.append({'frame':frame,'overlapping_triangle_pairs':len(overlaps)})
  obj.to_mesh_clear()
 records.append({'clip':name,'samples':samples,'maximum_triangle_pairs':max(s['overlapping_triangle_pairs'] for s in samples)})
 print(name,records[-1]['maximum_triangle_pairs'])
result={'kind':'Actual Blender evaluated-surface BVH overlaps; not Unity acceptance','hand_faces':len(hand_faces),'body_faces':len(torso_faces),
 'samples':260,'poses':records,'limitations':'Only selected >0.65 weighted hand/body garment surfaces in LOD0, 13 times per clip; not every vertex, time or mesh. No containment test for fully enclosed noncrossing surfaces.',
 'native_unity_tested':False}
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,indent=2)+'\n')
