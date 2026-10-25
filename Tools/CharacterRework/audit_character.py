"""Inspect delivered Blender geometry/skin/poses. Does not modify the source file.

Nearest torso signs are diagnostic samples on an open surface, not a guarantee
that an entire mesh is intersection-free in Unity after Humanoid retargeting.
"""
import argparse
import json
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

p = argparse.ArgumentParser()
p.add_argument('--blend', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
bpy.ops.wm.open_mainfile(filepath=str(a.blend.resolve()), use_scripts=False)
rig = bpy.data.objects['HoodedGirl_Rig']
rig.animation_data.use_nla = False
meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH' and o.get('game_mesh')]
torso_bones = {'Hips', 'Spine', 'Chest', 'UpperChest', 'Neck'}
records = []
for obj in sorted(meshes, key=lambda o: o.name):
    names = {g.index: g.name for g in obj.vertex_groups}
    sums = [sum(g.weight for g in v.groups) for v in obj.data.vertices]
    records.append({'name': obj.name, 'lod': obj.get('lod_level', 0), 'vertices': len(sums),
        'degenerate_faces': sum(f.area < 1e-12 for f in obj.data.polygons),
        'missing_armature_link': not any(m.type == 'ARMATURE' and m.object == rig for m in obj.modifiers),
        'unweighted': sum(s < .999 or s > 1.001 for s in sums),
        'over_four_influences': sum(sum(g.weight > 1e-6 for g in v.groups) > 4 for v in obj.data.vertices),
        'unknown_deform_groups': sorted({names[g.group] for v in obj.data.vertices for g in v.groups
            if g.weight > 1e-6 and (names[g.group] not in rig.data.bones or not rig.data.bones[names[g.group]].use_deform)}),
        'materials': [m.name if m else None for m in obj.data.materials]})
body = bpy.data.objects['Body_Clothed']
groups = {g.index: g.name for g in body.vertex_groups}
torso_vertices = {v.index for v in body.data.vertices if sum(g.weight for g in v.groups if groups[g.group] in torso_bones) > .72}
torso_faces = [tuple(f.vertices) for f in body.data.polygons if all(i in torso_vertices for i in f.vertices)]
pose_records = []
for name in ['Idle', 'Walk', 'WalkBack', 'Run', 'CrouchIdle', 'CrouchWalk', 'CarryIdle', 'CarryWalk', 'WoundedIdle', 'WoundedWalk', 'UseItem', 'TurnLeft', 'TurnRight', 'StrafeLeft', 'StrafeRight', 'Jump', 'Fall', 'Land', 'Death', 'LookAround']:
    action = bpy.data.actions.get(name)
    if action is None:
        continue
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    samples = []
    for i in range(13):
        frame = action.frame_range[0] + (action.frame_range[1] - action.frame_range[0]) * i / 12
        bpy.context.scene.frame_set(int(frame), subframe=frame-int(frame))
        bpy.context.view_layer.update()
        evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
        mesh = evaluated.to_mesh()
        tree = BVHTree.FromPolygons([evaluated.matrix_world @ v.co for v in mesh.vertices], torso_faces)
        positions = {}
        for side in ['Left', 'Right']:
            for suffix in ['Hand', 'LowerArm']:
                point = rig.matrix_world @ rig.pose.bones[side+suffix].head
                near, normal, face, distance = tree.find_nearest(point)
                positions[side+suffix] = {'position': list(point), 'torso_distance': distance,
                    'signed_torso_distance': (point-near).dot(normal) if near is not None else None}
        samples.append({'frame': frame, 'bones': positions})
        evaluated.to_mesh_clear()
    pose_records.append({'clip': name, 'samples': samples})
rest = {n: {'head': list(b.head_local), 'tail': list(b.tail_local), 'parent': b.parent.name if b.parent else None}
        for n,b in rig.data.bones.items() if any(k in n for k in ['UpperArm','LowerArm','Shoulder','Hand','Hips','Chest','Spine'])}
result = {'kind': 'Actual Blender source inspection; not Unity retarget/render acceptance', 'source': str(a.blend),
    'blender_version': bpy.app.version_string, 'meshes': records, 'bones': rest,
    'torso_face_count': len(torso_faces), 'torso_probe_limitations': 'Open weighted torso surface; nearest signed distance is a diagnostic indicator only.',
    'poses': pose_records, 'native_unity_tested': False}
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(result, indent=2)+'\n')
print('Meshes:', len(records), 'unweighted:', sum(r['unweighted'] for r in records))
for pose in pose_records:
    print(pose['clip'], {n: round(min(s['bones'][n]['signed_torso_distance'] for s in pose['samples']), 4)
          for n in ['LeftHand','RightHand','LeftLowerArm','RightLowerArm']})
