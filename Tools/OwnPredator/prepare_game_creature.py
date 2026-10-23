"""Restore the owner-selected Vesper 0.2 and prepare real game animation FBXs.
Run in bpy 5.2.3 with SILENCE_VESPER_SOURCE pointing at the verified source .blend.
The empty owner upload is documented, never passed off as a model.
"""
import bpy, json, math, os, pathlib, shutil
from mathutils import Vector

ROOT = pathlib.Path(os.environ['SILENCE_REPOSITORY']).resolve()
SOURCE = pathlib.Path(os.environ['SILENCE_VESPER_SOURCE']).resolve()
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
scene = next(s for s in bpy.data.scenes if s.get('silence_rig'))
bpy.context.window.scene = scene
rig = scene.objects[scene['silence_rig']]
mapping = json.loads(rig['silence_actions'])
if len(rig.data.bones) != 63 or 'silence_morphology' not in rig:
    raise RuntimeError('Expected the verified Vesper 0.2 rig; refusing to export an empty or unrelated file.')
art = ROOT / 'Assets/Creatures/SilencePredator'
for name in ['Models', 'Textures', 'Documentation']:
    (art/name).mkdir(parents=True, exist_ok=True)
normal_fixes=[]
for obj in scene.objects:
    if obj.type!='MESH' or not obj.name.startswith(('Skin_LOD','Tissue_LOD')):continue
    mesh=obj.data;parents=list(range(len(mesh.vertices)))
    def find(index):
        while parents[index]!=index:parents[index]=parents[parents[index]];index=parents[index]
        return index
    for edge in mesh.edges:
        a,b=map(find,edge.vertices);parents[b]=a
    components={}
    for vertex in mesh.vertices:components.setdefault(find(vertex.index),[]).append(vertex.index)
    for ids in components.values():
        positions=[mesh.vertices[i].co for i in ids];minimum=[min(v[k] for v in positions) for k in range(3)];maximum=[max(v[k] for v in positions) for k in range(3)]
        ids=set(ids);faces=[p for p in mesh.polygons if p.vertices[0] in ids];inside=obj.name.startswith('Tissue')
        mouth=minimum[1]<-2.2 and -1.25<maximum[1]<-.95 and maximum[0]-minimum[0]>.9 and maximum[2]-minimum[2]>1
        reverse=False;score=0
        if mouth:
            area=0
            for face in faces:
                c=face.center;u=(c.y+(2.29 if inside else 2.22))/(1.12 if inside else 1.20)
                radial=Vector((c.x,0,c.z-(2.28+(.14 if inside else .23)*u)))
                if radial.length>.15:score+=face.normal.dot(radial.normalized())*face.area;area+=face.area
            score/=max(area,1e-8);reverse=score>0 if inside else score<0
        elif inside and sum(p.x for p in positions)/len(positions)<-.10 and minimum[2]>2.4 and maximum[1]-minimum[1]<.6:
            score=sum(f.normal.z*f.area for f in faces)/max(sum(f.area for f in faces),1e-8)
            reverse=score<-.15
        if reverse:
            for face in faces:face.flip()
            normal_fixes.append({'mesh':obj.name,'faces':len(faces),'component':'mouth cavity' if inside and mouth else 'outer head' if mouth else 'mirrored acoustic membrane','before_score':score})
    mesh.update()
for image in bpy.data.images:
    if image.filepath and image.name.startswith('Vesper_'):
        original = pathlib.Path(bpy.path.abspath(image.filepath))
        if not original.is_file():
            raise FileNotFoundError(original)
        destination = art/'Textures'/original.name
        shutil.copyfile(original, destination)
        image.filepath = str(destination)
rig.animation_data.use_nla = False
rig.animation_data.action = bpy.data.actions[mapping['Idle']]
scene.frame_set(1)
rest = {p.name:p.matrix_basis.copy() for p in rig.pose.bones}
rig.animation_data.action = None
def reset():
    for name, matrix in rest.items():rig.pose.bones[name].matrix_basis = matrix
def rotate(name,x=0,y=0,z=0):
    bone=rig.pose.bones[name];bone.rotation_mode='XYZ';bone.rotation_euler=(x,y,z)
def offset(name,xyz):
    rig.pose.bones[name].location=rig.data.bones[name].matrix_local.to_3x3().inverted() @ Vector(xyz)
def curves(action):
    if hasattr(action,'fcurves'):return list(action.fcurves)
    return [curve for layer in action.layers for strip in layer.strips for bag in strip.channelbags for curve in bag.fcurves]
def clip(name,frames,mode):
    action=bpy.data.actions.new('VesperGame_'+name);action.use_fake_user=True;action['silence_clip']=name
    rig.animation_data.action=action;duration=frames/30
    for frame in range(frames+1):
        scene.frame_set(frame+1);reset();u=frame/frames;t=u*duration
        if mode=='breath':
            breath=math.sin(u*math.tau)
            rig.pose.bones['Chest'].scale=(1+.013*breath,1+.006*breath,1+.011*breath)
            rig.pose.bones['Spine'].scale=(1+.007*breath,1,1+.006*breath)
            rotate('Chest',.007*breath);rotate('Neck',.005*breath)
        else:
            run=mode=='run';stance=.40 if run else .64;stride=1.60 if run else 1.35;lift=.30 if run else .18
            phases=[0,.04,.50,.54] if run else [0,.5,.75,.25]
            for i,(kind,side) in enumerate([('Fore','L'),('Fore','R'),('Hind','L'),('Hind','R')]):
                phase=(u+phases[i])%1
                if phase<stance:y=stride*(phase/stance-.5);z=0
                else:
                    swing=(phase-stance)/(1-stance);s2=swing*swing;s3=s2*swing
                    velocity=stride*(1-stance)/stance
                    y=(2*s3-3*s2+1)*stride*.5+(s3-2*s2+swing)*velocity+(-2*s3+3*s2)*(-stride*.5)+(s3-s2)*velocity
                    z=math.sin(swing*math.pi)**2*lift
                offset('IK_'+kind+'.'+side,(0,y,z))
                for digit in range(3):rotate(f'{kind}Digit{digit}B.{side}',max(0,z)*.8)
            offset('Pelvis',(0,0,-.045+(.04 if run else .018)*math.sin(u*math.tau*2)))
            rotate('Chest',-.025+math.sin(u*math.tau)*(.05 if run else .028),0,math.sin(u*math.tau)*.022)
            rotate('Neck',-.09 if run else -.055,0,math.sin(u*math.tau)*.012)
            rotate('Jaw',.025+.025*math.sin(u*math.tau))
        for bone in rig.pose.bones:
            bone.keyframe_insert('location',frame=frame+1,group=bone.name)
            bone.keyframe_insert('rotation_euler',frame=frame+1,group=bone.name)
            bone.keyframe_insert('scale',frame=frame+1,group=bone.name)
    for curve in curves(action):
        for point in curve.keyframe_points:point.interpolation='LINEAR'
    mapping[name]=action.name
    return {'name':name,'seconds':duration,'frames':[1,frames+1]}

new_clips=[clip('GameWalk',23,'walk'),clip('GameRun',13,'run'),clip('BreathGame',138,'breath')]
reset();rig.animation_data.action=None
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
track=rig.animation_data.nla_tracks.new();track.name='Vesper game exports';start=1
for name,action_name in mapping.items():
    action=bpy.data.actions[action_name];strip=track.strips.new(name,start,action);strip.name=name
    strip.extrapolation='NOTHING';start=int(strip.frame_end)+2
rig['silence_actions']=json.dumps(mapping)
rig['silence_game_clips']=json.dumps({'count':22,'breathing':'BreathGame','walk':'GameWalk','run':'GameRun'})
rig.hide_set(False)
for level in range(3):
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
    meshes=[o for o in scene.objects if o.type=='MESH' and o.name.endswith('_LOD'+str(level))]
    if len(meshes)!=4:raise RuntimeError('Expected four material meshes for LOD '+str(level))
    for obj in meshes:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=rig
    rig.animation_data.action=None;rig.animation_data.use_nla=level==0;reset();scene.frame_set(1)
    bpy.ops.export_scene.fbx(filepath=str(art/'Models'/f'Vesper_LOD{level}.fbx'),use_selection=True,
        object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,global_scale=1,
        add_leaf_bones=False,use_armature_deform_only=True,bake_anim=level==0,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=level==0,bake_anim_simplify_factor=0,bake_anim_step=1)
    for obj in meshes:obj.hide_set(level!=0);obj.hide_render=level!=0
rig.animation_data.use_nla=False;rig.animation_data.action=bpy.data.actions[mapping['Idle']];scene.frame_set(1);rig.hide_set(True)
scene.frame_end=70
source_dir=ROOT/'ArtSource/VesperGame';source_dir.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source_dir/'Vesper_Game.blend'),compress=True)
for image in bpy.data.images:
    if image.filepath.startswith(str(art)):image.filepath=bpy.path.relpath(image.filepath,start=str(source_dir))
bpy.ops.wm.save_as_mainfile(filepath=str(source_dir/'Vesper_Game.blend'),compress=True)
report={'kind':'Actual Blender game exports from the owner-approved restored Vesper 0.2',
    'blender':bpy.app.version_string,'clips':list(mapping),'new_clips':new_clips,'clip_count':len(mapping),
    'walk_source_metres_per_second':1.35/((23/30)*.64),'run_source_metres_per_second':1.60/((13/30)*.40),
    'normal_orientation_fixes':normal_fixes,
    'native_unity_tested':False,'ultrarealistic_art_approved':False,'audio':'Procedural placeholder breathing and steps; not production recordings'}
(art/'Documentation/game-animation.json').write_text(json.dumps(report,indent=2)+'\n')
print('VESPER_GAME_EXPORTS',len(mapping),flush=True)
