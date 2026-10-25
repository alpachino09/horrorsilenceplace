"""Capture real Blender-deformed vertices as an independent skinning oracle.

Run with bpy's Python. Never saves or modifies the source .blend on disk.
This is not a Unity Humanoid retarget test.
"""
import argparse
import hashlib
import json
from pathlib import Path
import bpy

p = argparse.ArgumentParser()
p.add_argument('--blend', type=Path, required=True)
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
bpy.ops.wm.open_mainfile(filepath=str(a.blend.resolve()), use_scripts=False)
rig = bpy.data.objects['HoodedGirl_Rig']
body = bpy.data.objects['Body_Clothed']
assert len(body.modifiers) == 1 and body.modifiers[0].type == 'ARMATURE'
assert not body.modifiers[0].use_deform_preserve_volume
assert not body.data.shape_keys
rig.animation_data.use_nla = False
bones = list(rig.data.bones)
indices = {b.name: i for i, b in enumerate(bones)}
groups = {g.index: g.name for g in body.vertex_groups}


def matrix(m):
    return [[m[row][col] for row in range(3)] for col in range(4)]


samples = []
for i in range(64):
    vertex = body.data.vertices[i * (len(body.data.vertices) - 1) // 63]
    weights = [[indices[groups[g.group]], g.weight] for g in vertex.groups if g.weight > 0]
    assert len(weights) <= 4
    total = sum(w[1] for w in weights)
    weights = [[j, w / total] for j, w in weights]
    weights += [[0, 0]] * (4 - len(weights))
    samples.append({'index': vertex.index, 'vertex': list(vertex.co), 'weights': weights})
bind = [matrix(b.matrix_local.inverted() @ rig.matrix_world.inverted() @ body.matrix_world) for b in bones]
poses = []
for name, phase in [('Idle', 0), ('Idle', .5), ('Walk', .25), ('Run', .5), ('CrouchIdle', 0), ('Jump', .25), ('Death', 1)]:
    action = bpy.data.actions[name]
    rig.animation_data.action = action
    if action.slots:
        rig.animation_data.action_slot = action.slots[0]
    frame = action.frame_range[0] + (action.frame_range[1] - action.frame_range[0]) * phase
    bpy.context.scene.frame_set(int(frame), subframe=frame-int(frame))
    bpy.context.view_layer.update()
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    assert len(mesh.vertices) == len(body.data.vertices)
    poses.append({'clip': name, 'phase': phase,
        'bones': [matrix(rig.matrix_world @ rig.pose.bones[b.name].matrix) for b in bones],
        'expected': [list(evaluated.matrix_world @ mesh.vertices[v['index']].co) for v in samples]})
    evaluated.to_mesh_clear()
result = {'kind': 'Actual Blender armature deformation oracle; not Unity retarget or Play acceptance',
    'blender': bpy.app.version_string, 'source_sha256': hashlib.sha256(a.blend.read_bytes()).hexdigest(),
    'renderer': body.name, 'skin_vertex_count': len(body.data.vertices),
    'bind': bind, 'vertices': samples, 'poses': poses}
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(result, indent=2)+'\n')
print('Captured', len(samples), 'real vertices across', len(poses), 'Blender-deformed poses')
