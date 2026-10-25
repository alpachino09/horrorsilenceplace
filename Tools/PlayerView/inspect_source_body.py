"""Blender-only geometry evidence, not a Unity camera/retarget test."""
import argparse
import json
import math
from pathlib import Path
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

parser = argparse.ArgumentParser()
parser.add_argument('--blend', required=True, type=Path)
parser.add_argument('--report', required=True, type=Path)
args = parser.parse_args()
bpy.ops.wm.open_mainfile(filepath=str(args.blend.resolve()), use_scripts=False)
rig = bpy.data.objects['HoodedGirl_Rig']
rig.animation_data.action = bpy.data.actions['Idle']
rig.animation_data.use_nla = False
if rig.animation_data.action.slots:
    rig.animation_data.action_slot = rig.animation_data.action.slots[0]
bpy.context.scene.frame_set(1)
bpy.context.view_layer.update()
left = rig.matrix_world @ rig.pose.bones['LeftEye'].head
right = rig.matrix_world @ rig.pose.bones['RightEye'].head
natural = (left + right) * .5 + Vector((0, -.04, 0))
obj = bpy.data.objects['Body_Clothed']
evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
mesh = evaluated.to_mesh()
vertices = [evaluated.matrix_world @ v.co for v in mesh.vertices]
polygons = [tuple(p.vertices) for p in mesh.polygons]
bvh = BVHTree.FromPolygons(vertices, polygons)
direction = Vector((0, -math.cos(math.radians(85)), -math.sin(math.radians(85))))
def probe(origin):
    point, normal, face, distance = bvh.ray_cast(origin, direction, 4)
    return {'origin': list(origin), 'hit': face is not None,
            'position': list(point) if point is not None else None,
            'front_facing': normal.dot(direction) < 0 if normal is not None else None,
            'distance': distance}
result = {'kind': 'Actual saved Blender source, skinned Idle, downward ray into Body_Clothed; not Unity rendering',
          'old_eye': probe(Vector((0, 0, 1.64))), 'natural_eye': probe(natural),
          'source_idle_hands': {side: list(rig.matrix_world @ rig.pose.bones[side+'Hand'].head) for side in ['Left','Right']},
          'native_unity_tested': False}
evaluated.to_mesh_clear()
args.report.parent.mkdir(parents=True, exist_ok=True)
args.report.write_text(json.dumps(result, indent=2)+'\n')
print('Source geometry:', result['old_eye'], result['natural_eye'])
