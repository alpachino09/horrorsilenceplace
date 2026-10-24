#!/usr/bin/env python3
"""Run with Blender's Python/bpy after preparing Unity meta as ordinary JSON.

Example metadata conversion (system Python + PyYAML):
  python -c "import yaml,json; json.dump(yaml.safe_load(open('model.fbx.meta'))['ModelImporter'],open('/tmp/rig.json','w'))"
Then Blender Python: inspect_skeleton.py --fbx model.fbx --rig-json /tmp/rig.json --report result.json
This does not validate a native Unity Avatar or retarget Unity muscle clips.
"""
import argparse
import json
from pathlib import Path
import bpy


def inspect(fbx, metadata, report):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx.resolve()), use_anim=False)
    arms = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    human = json.loads(metadata.read_text())['humanDescription']['human']
    names = {b.name for a in arms for b in a.data.bones}
    missing = [h['boneName'] for h in human if h['boneName'] not in names]
    if len(arms) != 1 or missing:
        raise ValueError('Skeleton/metadata mismatch: ' + str(missing))
    result = {'kind': 'Actual Blender FBX import and provided Unity metadata inspection; not a Unity Avatar validation',
              'blender': bpy.app.version_string, 'source_fbx_bytes': fbx.stat().st_size,
              'armature_count': len(arms), 'bone_count': len(names), 'human_mapping_count': len(human),
              'missing_mapped_bones': missing, 'human_labels': [h['humanName'] for h in human],
              'native_unity_avatar_tested': False}
    report.write_text(json.dumps(result, indent=2) + '\n')
    print('Actual FBX:', len(names), 'bones;', len(human), 'mapped; missing:', len(missing))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--fbx', required=True, type=Path)
    parser.add_argument('--rig-json', required=True, type=Path)
    parser.add_argument('--report', required=True, type=Path)
    args = parser.parse_args()
    inspect(args.fbx, args.rig_json, args.report)
