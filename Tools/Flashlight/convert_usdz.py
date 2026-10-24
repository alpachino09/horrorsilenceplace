"""Blender 5.2: convert the supplied asset; never generates replacement geometry."""
import argparse,sys,json,hashlib,zipfile
from pathlib import Path
import bpy
from mathutils import Vector


def convert(source,output):
    source=Path(source).expanduser().resolve();output=Path(output).expanduser().resolve()
    if not source.is_file():raise FileNotFoundError('Supply the actual tactical_flashlight.usdz file; it was not present in the task attachments.')
    if source.suffix.lower() not in {'.usdz','.usd','.usdc','.usda'}:raise ValueError('Expected a USD / USDZ source file.')
    if output.exists() and any(output.iterdir()):raise FileExistsError('Use a new empty output directory to preserve previous results.')
    output.mkdir(parents=True,exist_ok=True);textures=output/'Textures';textures.mkdir()
    if source.suffix.lower()=='.usdz':
        with zipfile.ZipFile(source) as archive:
            if any(Path(n).is_absolute() or '..' in Path(n).parts for n in archive.namelist()):raise ValueError('Archive has paths outside its root.')
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    bpy.ops.wm.usd_import(filepath=str(source),import_cameras=False,import_lights=False)
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
    if not meshes:raise ValueError('The supplied file contains no imported mesh geometry.')
    for image in list(bpy.data.images):
        if image.size[0]<=0 or image.size[1]<=0:continue
        image.filepath_raw=str(textures/(bpy.path.clean_name(image.name)+'.png'));image.file_format='PNG';image.save()
    bpy.ops.object.select_all(action='DESELECT')
    for o in bpy.context.scene.objects:
        if o.type in {'MESH','ARMATURE','EMPTY'}:o.select_set(True)
    bpy.context.view_layer.objects.active=meshes[0]
    bpy.ops.export_scene.fbx(filepath=str(output/'TacticalFlashlight.fbx'),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
    bounds=[o.matrix_world@Vector(corner) for o in meshes for corner in o.bound_box]
    dimensions=[max(p[i] for p in bounds)-min(p[i] for p in bounds) for i in range(3)]
    record={'source':source.name,'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),'blender':bpy.app.version_string,'meshes':[{'name':o.name,'vertices':len(o.data.vertices),'polygons':len(o.data.polygons)} for o in meshes],
        'bounds_metres':dimensions,'materials':[m.name for m in bpy.data.materials],
        'textures':[p.name for p in sorted(textures.glob('*.png'))],'original_geometry_imported':True,'replacement_geometry_generated':False,
        'native_unity_tested':False,'pbr_shader_conversion_approved':False,'hand_contact_calibrated':False,
        'next':'Import FBX/textures into Unity; reproduce PBR materials with URP Lit; use menu 24 to create and calibrate the actual art rig. No meshes are split automatically.'}
    (output/'conversion.json').write_text(json.dumps(record,indent=2)+'\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'TacticalFlashlight.blend'),compress=True)
    for image in bpy.data.images:
        if image.filepath and Path(bpy.path.abspath(image.filepath)).exists():image.filepath=bpy.path.relpath(bpy.path.abspath(image.filepath),start=str(output))
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'TacticalFlashlight.blend'),compress=True)
    print('FLASHLIGHT_CONVERTED',len(meshes),'meshes;',len(record['textures']),'textures. Check conversion.json.',flush=True)

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sys.argv[1:]
    parser=argparse.ArgumentParser();parser.add_argument('source');parser.add_argument('output');options=parser.parse_args(args);convert(options.source,options.output)
