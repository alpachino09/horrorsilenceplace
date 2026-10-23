"""Round-trip the actual exported FBX, not just the source .blend mesh."""
import bpy,pathlib,json
ROOT=pathlib.Path(__file__).resolve().parents[2];ART=ROOT/'Assets/_Silence/Art/VisualRebuild'
manifest=json.loads((ART/'model-manifest.json').read_text())['models'];checks=[]
for item in manifest:
 bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
 bpy.ops.import_scene.fbx(filepath=str(ART/'Models'/item['file']),use_custom_normals=True)
 objects=[o for o in bpy.context.scene.objects if o.type=='MESH'];verts=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
 bounds=[[min(v[i] for v in verts),max(v[i] for v in verts)] for i in range(3)]
 error=max(abs(bounds[i][j]-item['blender_bounds'][i][j]) for i in range(3) for j in range(2))
 checks.append({'asset':item['name'],'metre_bounds_error':error,'uv_present':all(len(o.data.uv_layers)>0 for o in objects),'passed':error<.025 and all(len(o.data.uv_layers)>0 for o in objects)})
report={'kind':'Blender FBX re-import, not Unity ModelImporter verification','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
(ROOT/'Docs/Validation/VisualRebuild/fbx-import.json').write_text(json.dumps(report,indent=2)+'\n')
print('SILENCE_FBX_ROUNDTRIP',report['passed'],report['failed'])
if report['failed']:raise RuntimeError('FBX export/import mismatch')
