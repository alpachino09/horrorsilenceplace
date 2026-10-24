import sys,json,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *
from face_shapes import add_head_keys,transfer_keys
from animate_character import reset,make_face_demo
from build_character import eyes
from refine_surface import face_details

def deduplicate_materials(obj):
    unique=[];mapping={}
    for index,material in enumerate(obj.data.materials):
        if material not in unique:unique.append(material)
        mapping[index]=unique.index(material)
    indices=[mapping[p.material_index] for p in obj.data.polygons]
    obj.data.materials.clear()
    for material in unique:obj.data.materials.append(material)
    for p,index in zip(obj.data.polygons,indices):p.material_index=index

def batch_game_meshes(rig):
    collection=bpy.data.collections.new('EDITABLE_SOURCE_PARTS');bpy.context.scene.collection.children.link(collection)
    collection.hide_render=True;collection.hide_viewport=True
    sets=[('Body_Clothed',[bpy.data.objects[n] for n in ['Jacket','Cargo_Trousers','Fingerless_Gloves','Hands','Waist_Belt','Clothing_Panels','Garment_Seams']])]
    for side in ['Left','Right']:
        sets.append(('Footwear_'+side,[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('rigid_bone')==side+'Foot']))
    for name,originals in sets:
        copies=[]
        for original in originals:
            copy=original.copy();copy.data=original.data.copy();bpy.context.scene.collection.objects.link(copy);copy.hide_set(False);copy.hide_render=False;copies.append(copy)
            original['game_mesh']=False;original['source_only']=True;original.name='Source_'+original.name
            for c in list(original.users_collection):c.objects.unlink(original)
            collection.objects.link(original)
        combined=combine(copies,name);combined['game_mesh']=True;combined.parent=rig;deduplicate_materials(combined)

def transfer_skin(source,target,rig):
    target.vertex_groups.clear();groups={g.name:target.vertex_groups.new(name=g.name) for g in source.vertex_groups}
    source_names={g.index:g.name for g in source.vertex_groups};kd=KDTree(len(source.data.vertices))
    for v in source.data.vertices:kd.insert(v.co,v.index)
    kd.balance()
    for v in target.data.vertices:
        entries={};total=0
        for _,index,d in kd.find_n(v.co,3):
            factor=1/(d+.0005)**2;total+=factor
            for g in source.data.vertices[index].groups:
                name=source_names[g.group];entries[name]=entries.get(name,0)+g.weight*factor
        top=sorted(entries.items(),key=lambda p:p[1],reverse=True)[:4];s=sum(w for _,w in top)
        for name,w in top:
            if w>0:groups[name].add([v.index],w/s,'REPLACE')
    target.parent=rig;mod=target.modifiers.new('Humanoid skin','ARMATURE');mod.object=rig

def low_head(human,source,rig,level):
    head,ids=human.part('Face_LOD'+str(level),lambda f:min(human.co[i,2] for i in f)>1.475,bpy.data.materials['Skin_Face'])
    uv=head.data.uv_layers.active
    for p in head.data.polygons:
        front=p.center.y<.005
        for loop in p.loop_indices:
            co=head.data.vertices[head.data.loops[loop].vertex_index].co
            uv.data[loop].uv=(co.x/.225+.5 if front else .94+.045*(co.x+.1)/.2,(co.z-1.475)/.27)
    add_head_keys(head,human,ids);face_details(head);transfer_skin(source,head,rig)
    head['game_mesh']=True;head['lod_level']=level;return head

def normalize(obj):
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bpy.ops.object.vertex_group_limit_total(limit=4);bpy.ops.object.vertex_group_normalize_all(lock_active=False)

def lods(rig):
    originals=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('game_mesh')]
    for obj in originals:obj['lod_level']=0
    human=Human();result={0:originals}
    for level,ratio in [(1,.60),(2,.35)]:
        meshes=[]
        for source in originals:
            if source.name=='Face':meshes.append(low_head(human,source,rig,level));continue
            if level==2 and source.name=='Lashes':continue
            obj=source.copy();obj.name=source.name+'_LOD'+str(level);bpy.context.scene.collection.objects.link(obj)
            obj.data=source.data if source.data.shape_keys is not None else source.data.copy()
            obj['lod_level']=level;obj['game_mesh']=True
            if obj.data.shape_keys is None and len(obj.data.polygons)>60:
                for modifier in list(obj.modifiers):
                    if modifier.type=='ARMATURE':obj.modifiers.remove(modifier)
                apply(obj,'DECIMATE',ratio=ratio,use_collapse_triangulate=True)
                normalize(obj);mod=obj.modifiers.new('Humanoid skin','ARMATURE');mod.object=rig
            meshes.append(obj)
        result[level]=meshes
    return result

def material_manifest():
    records=[]
    used={m for o in bpy.context.scene.objects if o.type=='MESH' and o.get('game_mesh') for m in o.data.materials if m is not None}
    for material in sorted(used,key=lambda m:m.name):
        bs=next((n for n in material.node_tree.nodes if n.bl_idname=='ShaderNodeBsdfPrincipled'),None)
        if bs is None:continue
        images={}
        for n in material.node_tree.nodes:
            if n.bl_idname!='ShaderNodeTexImage' or n.image is None:continue
            file=Path(bpy.path.abspath(n.image.filepath)).name
            if '_Normal' in file:images['normal']=file
            elif '_Mask' in file:images['mask']=file
            else:images['base']=file
        mix=next((n for n in material.node_tree.nodes if n.bl_idname=='ShaderNodeMixRGB' and n.blend_type=='MULTIPLY'),None)
        color=list(mix.inputs[2].default_value if mix else bs.inputs['Base Color'].default_value)
        records.append({'name':material.name,'color':color,'roughness':float(bs.inputs['Roughness'].default_value),
            'metallic':float(bs.inputs['Metallic'].default_value),'base':images.get('base',''),'normal':images.get('normal',''),'mask':images.get('mask',''),
            'alphaClip':material.name=='Hair_Cards','doubleSided':material.name=='Hair_Cards'})
    (ART/'Documentation'/'materials.json').write_text(json.dumps({'materials':records},indent=2)+'\n')

def export():
    bpy.ops.wm.open_mainfile(filepath=str(STAGES/'10_animation_facial.blend'),use_scripts=False)
    if bpy.data.objects.get('Head') is not None:bpy.data.objects['Head'].name='Face'
    rig=bpy.data.objects['HoodedGirl_Rig'];mapping=json.loads(rig['silence_actions'])
    rig.animation_data.action=None;rig.animation_data.use_nla=False;reset(rig)
    for side in ['Left','Right']:rig.pose.bones[side+'LowerLeg'].constraints['Foot plant IK'].influence=0
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.data.shape_keys is not None:
            obj.data.shape_keys.animation_data_clear()
            for key in obj.data.shape_keys.key_blocks:
                if key.name!='Basis':key.value=0
    bpy.context.view_layer.update();batch_game_meshes(rig);levels=lods(rig);make_face_demo(rig)
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.data.shape_keys is not None:obj.data.shape_keys.animation_data.action=None
    stats=[]
    for level,meshes in levels.items():
        bpy.ops.object.select_all(action='DESELECT');rig.hide_set(False);rig.select_set(True)
        for obj in meshes:obj.hide_set(False);obj.hide_render=False;obj.select_set(True)
        bpy.context.view_layer.objects.active=rig;rig.animation_data.action=None;rig.animation_data.use_nla=level==0;reset(rig);bpy.context.scene.frame_set(1)
        path=ART/'Models'/f'HoodedGirl_LOD{level}.fbx'
        bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',
            apply_unit_scale=True,global_scale=1,add_leaf_bones=False,use_armature_deform_only=True,use_mesh_modifiers=False,
            bake_anim=level==0,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=level==0,bake_anim_simplify_factor=0,bake_anim_step=1,path_mode='STRIP',mesh_smooth_type='FACE')
        triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in meshes)
        stats.append({'level':level,'objects':len(meshes),'triangles':triangles,'bytes':path.stat().st_size})
        for obj in meshes:obj.hide_set(level!=0);obj.hide_render=level!=0
        print('HOODED_FBX',level,triangles,'triangles',flush=True)
    material_manifest()
    rig.animation_data.use_nla=False;rig.animation_data.action=bpy.data.actions[mapping['Idle']]
    bpy.context.scene.frame_set(1);bpy.context.scene.frame_end=211
    facial=json.loads(rig['silence_facial_actions'])
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH' and obj.data.shape_keys is not None and obj.name in facial:
            obj.data.shape_keys.animation_data.action=bpy.data.actions[facial[obj.name]]
    bpy.context.scene.frame_set(1)
    source_only=bpy.data.objects.get('Anatomy_Working')
    if source_only is not None:source_only.hide_set(True);source_only.hide_render=True
    text=bpy.data.texts.new('START_HERE_RU')
    text.write('HoodedGirl — исходник SILENCE.\nМодель без оружия.\nFace: Basis + 52 ARKit shape keys.\nRig: 55 деформирующих костей, T-pose rest.\nАктивен Idle; остальные клипы в Action Editor.\nFaceDemo_*: кадры 1–211, семь выражений.\nLOD1/2 скрыты, переключаются через Outliner.\nТекстуры внешние, сохраняйте структуру репозитория.\nUnity import и художественная приёмка ещё требуют проверки.\n')
    report={'blender':bpy.app.version_string,'height_target_metres':[1.70,1.75],'deform_bones':sum(b.use_deform for b in rig.data.bones),
        'body_clip_count':len(mapping),'body_clips':list(mapping),'primary_head_blendshapes':len(bpy.data.objects['Face'].data.shape_keys.key_blocks)-1,
        'lods':stats,'weapons_included':False,'native_unity_tested':False,'exact_likeness_approved':False,'photorealistic_quality_approved':False}
    (ART/'Documentation'/'model_manifest.json').write_text(json.dumps(report,indent=2)+'\n')
    checkpoint('11_fbx_export')
    path=SOURCE/'HoodedGirl.blend';bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    for image in bpy.data.images:
        if image.filepath and Path(bpy.path.abspath(image.filepath)).exists():image.filepath=bpy.path.relpath(bpy.path.abspath(image.filepath),start=str(SOURCE))
    bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
    print('HOODED_SOURCE_SAVED',flush=True)

if __name__=='__main__':export()
