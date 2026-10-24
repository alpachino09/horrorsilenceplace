"""Actual Blender USDZ/FBX pipeline test using an isolated non-delivery geometry fixture."""
import tempfile,json
from pathlib import Path
import bpy
from convert_usdz import convert

def run():
    with tempfile.TemporaryDirectory(prefix='silence-usdz-fixture-') as temporary:
        root=Path(temporary);bpy.ops.wm.read_factory_settings(use_empty=True)
        data=bpy.data.meshes.new('FixtureData');data.from_pydata([(0,0,0),(.1,0,0),(.1,.1,.04),(0,.1,0)],[],[(0,1,2),(0,2,3)]);data.update()
        obj=bpy.data.objects.new('ImportTestFixture_NOT_A_FLASHLIGHT',data);bpy.context.scene.collection.objects.link(obj)
        material=bpy.data.materials.new('FixtureMaterial');material.diffuse_color=(.12,.18,.2,1);data.materials.append(material)
        source=root/'fixture.usdz';bpy.ops.wm.usd_export(filepath=str(source))
        convert(source,root/'Converted')
        result=json.loads((root/'Converted/conversion.json').read_text())
        assert len(result['meshes'])==1 and result['meshes'][0]['vertices']==4 and result['meshes'][0]['polygons']==2
        assert result['replacement_geometry_generated'] is False
        bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(root/'Converted/TacticalFlashlight.fbx'))
        meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1 and len(meshes[0].data.vertices)==4
        try:convert(root/'missing.usdz',root/'Missing')
        except FileNotFoundError:pass
        else:raise AssertionError('Missing source was not rejected')
        record={'blender':bpy.app.version_string,'usd_import_export_fixture_passed':True,'fbx_roundtrip_fixture_passed':True,'missing_source_rejected':True,
                'fixture_is_actual_flashlight':False,'actual_user_usdz_available':False,'actual_art_import_tested':False,'native_unity_tested':False}
        destination=Path(__file__).resolve().parents[2]/'Docs/Validation/Flashlight';destination.mkdir(parents=True,exist_ok=True);(destination/'converter.json').write_text(json.dumps(record,indent=2)+'\n')
        print('FLASHLIGHT_CONVERTER_FIXTURE passed; actual user model not received.',flush=True)

if __name__=='__main__':run()
