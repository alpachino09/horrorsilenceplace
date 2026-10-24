import sys,json,hashlib,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *
from face_shapes import ARKIT

CHECKS=[]
def check(value,label,**metrics):
    CHECKS.append(dict(check=label,passed=bool(value),**metrics))

def shape_checks(obj,label,primary=False):
    keys=obj.data.shape_keys
    if keys is None:
        check(not primary,label+' has facial data');return
    names=set(k.name for k in keys.key_blocks if k.name!='Basis')
    if primary:check(names==set(ARKIT),label+' exact 52 ARKit channel names',count=len(names))
    basis=np.array([p.co for p in keys.key_blocks['Basis'].data]);seen=set();empty=[];duplicates=[];maximum=0
    for key in keys.key_blocks:
        if key.name=='Basis':continue
        delta=np.array([p.co for p in key.data])-basis;peak=float(np.linalg.norm(delta,axis=1).max());maximum=max(maximum,peak)
        if peak<1e-7:empty.append(key.name)
        digest=hashlib.sha256(np.round(delta,7).tobytes()).hexdigest()
        if digest in seen:duplicates.append(key.name)
        seen.add(digest)
    check(not empty,label+' no empty morphs',empty=empty)
    check(not duplicates,label+' no duplicate morphs',duplicates=duplicates)
    check(maximum<.12,label+' bounded facial displacement',max_metres=maximum)

def skin_world(obj,rig):
    vertices=np.array([v.co for v in obj.data.vertices]);out=np.zeros_like(vertices)
    prefix=rig.matrix_world;suffix=rig.matrix_world.inverted()@obj.matrix_world
    matrices={g.index:prefix@rig.pose.bones[g.name].matrix@rig.data.bones[g.name].matrix_local.inverted()@suffix for g in obj.vertex_groups if g.name in rig.data.bones}
    for v in obj.data.vertices:
        for g in v.groups:
            if g.group in matrices:out[v.index]+=np.array(matrices[g.group]@v.co)*g.weight
    return out

def geometry(obj):
    data=obj.data;name=obj.name
    check(len(data.vertices)>0 and len(data.polygons)>0,name+' populated mesh')
    check(all(math.isfinite(x) for v in data.vertices for x in v.co),name+' finite vertices')
    bad=sum(p.area<1e-12 for p in data.polygons)
    check(bad==0,name+' no degenerate faces',count=bad)
    check(bool(data.uv_layers),name+' UV layer exists')
    if data.uv_layers:check(all(math.isfinite(x) for loop in data.uv_layers.active.data for x in loop.uv),name+' finite UVs')
    check(len(data.materials)>0 and all(m is not None for m in data.materials),name+' material references')
    check(all(p.material_index<len(data.materials) for p in data.polygons),name+' material indices')
    counts=[len(v.groups) for v in data.vertices];sums=[sum(g.weight for g in v.groups) for v in data.vertices]
    check(min(counts)>=1 and max(counts)<=4,name+' one to four skin influences',maximum=max(counts))
    check(max(abs(s-1) for s in sums)<1e-4,name+' normalized skin weights')

def source_checks():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'HoodedGirl.blend'),use_scripts=False)
    scene=bpy.context.scene;rig=bpy.data.objects['HoodedGirl_Rig'];scene.frame_set(1);bpy.context.view_layer.update()
    meshes=[o for o in scene.objects if o.type=='MESH' and o.get('game_mesh')]
    check(sum(b.use_deform for b in rig.data.bones)==55,'55 deform bones')
    check(all(o.name not in rig.data.bones for o in meshes),'mesh names do not collide with skeleton bone names')
    check(not any(word in o.name.lower() for o in meshes for word in ['crossbow','quiver','arrow','weapon','bolt']),'no weapon objects')
    for level in range(3):
        group=[o for o in meshes if o.get('lod_level')==level];check(len(group)>0,'LOD '+str(level)+' exists')
        co=np.concatenate([np.array([v.co for v in o.data.vertices]) for o in group]);height=float(co[:,2].max()-co[:,2].min())
        check(1.70<=height<=1.751,'LOD '+str(level)+' height in metres',height=height)
        for obj in group:geometry(obj)
        face=bpy.data.objects['Face' if level==0 else 'Face_LOD'+str(level)];shape_checks(face,'LOD '+str(level)+' face',True)
    for obj in meshes:
        if obj.data.shape_keys is not None:
            check(all(k.value==0 for k in obj.data.shape_keys.key_blocks if k.name!='Basis'),obj.name+' saved neutral expression')
    for side in ['Left','Right']:
        for part in ['UpperArm','LowerArm']:
            bone=rig.data.bones[side+part];direction=(bone.tail_local-bone.head_local).normalized()
            check(abs(direction.x)>.99 and abs(direction.z)<.01,side+part+' T pose rest direction')
    used={n.image for o in meshes for m in o.data.materials if m and m.node_tree for n in m.node_tree.nodes if n.bl_idname=='ShaderNodeTexImage' and n.image is not None}
    for image in sorted(used,key=lambda i:i.name):
        path=Path(bpy.path.abspath(image.filepath));check(path.is_file(),'texture file '+image.name)
        check(image.size[0] in [2048,4096] and image.size[1] in [2048,4096],'texture dimensions '+image.name,width=image.size[0],height=image.size[1])
        if '_Normal' in image.name or '_Mask' in image.name:check(image.colorspace_settings.name=='Non-Color','data color space '+image.name)
    mapping=json.loads(rig['silence_actions']);check(len(mapping)==21,'21 body actions')
    for obj in meshes:
        if obj.data.shape_keys and obj.data.shape_keys.animation_data:obj.data.shape_keys.animation_data.action=None
    for name,frames,stance in [('Walk',22,.55),('Run',15,.25),('CrouchWalk',27,.60)]:
        rig.animation_data.action=bpy.data.actions[mapping[name]];rig.animation_data.use_nla=False;errors=[]
        for frame in sorted(set([1,frames+1]+[1+round(frames*t) for t in [.1,.25,.4,.5,.65,.8,.9]])):
            scene.frame_set(frame);bpy.context.view_layer.update()
            for side,phase in [('Left',0),('Right',.5)]:
                if ((frame-1)/frames+phase)%1<stance:errors.append(abs(float(skin_world(bpy.data.objects['Footwear_'+side],rig)[:,2].min())))
        check(max(errors)<.003,'actual saved action sole contact '+name,max_metres=max(errors))
    return mapping

def import_checks():
    for level in range(3):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        path=ART/'Models'/f'HoodedGirl_LOD{level}.fbx';bpy.ops.import_scene.fbx(filepath=str(path),use_anim=level==0)
        rigs=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'];check(len(rigs)==1,'FBX '+str(level)+' one skeleton')
        if not rigs:continue
        rig=rigs[0];check(len(rig.data.bones)>=55,'FBX '+str(level)+' skeleton bone count',count=len(rig.data.bones))
        faces=[o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Face')];check(len(faces)==1,'FBX '+str(level)+' face present')
        if faces:shape_checks(faces[0],'FBX '+str(level)+' face',True)
        if level==0:
            names=[a.name.split('|')[-1] for a in bpy.data.actions];required=['Idle','Walk','Run','CrouchIdle','CrouchWalk','LookAround','Jump','Fall','Land','Death','UseItem','BreathGame']
            check(all(any(n==r or n.endswith(r) for n in names) for r in required),'FBX body actions imported',count=len(names))
            # Replay exported bone tracks (not the source IK) after importing FBX.
            for name,frames,stance in [('Walk',22,.55),('Run',15,.25),('CrouchWalk',27,.60)]:
                actions=[a for a in bpy.data.actions if a.name.split('|')[-1]==name]
                if not actions:continue
                action=actions[0];rig.animation_data_create();rig.animation_data.action=action;rig.animation_data.use_nla=False;errors=[]
                for frame in [1+round(frames*t) for t in [0,.1,.25,.4,.5,.65,.8,.9,1]]:
                    bpy.context.scene.frame_set(frame);bpy.context.view_layer.update()
                    for side,phase in [('Left',0),('Right',.5)]:
                        shoe=next((o for o in bpy.context.scene.objects if o.type=='MESH' and o.name.startswith('Footwear_'+side)),None)
                        if shoe is not None and ((frame-1)/frames+phase)%1<stance:errors.append(abs(float(skin_world(shoe,rig)[:,2].min())))
                check(bool(errors) and max(errors)<.004,'FBX replay sole contact '+name,max_metres=max(errors) if errors else None)

def main():
    source_checks();import_checks()
    result={'kind':'Actual Blender source and FBX roundtrip checks; no Unity execution','blender':bpy.app.version_string,'checks':CHECKS,'passed':sum(c['passed'] for c in CHECKS),'failed':sum(not c['passed'] for c in CHECKS)}
    destination=ROOT/'Docs/Validation/HoodedGirl';destination.mkdir(parents=True,exist_ok=True);(destination/'blender.json').write_text(json.dumps(result,indent=2)+'\n')
    print('HOODED_CHECKS',result['passed'],'passed',result['failed'],'failed',flush=True)
    for c in CHECKS:
        if not c['passed']:print('FAIL',c['check'],flush=True)
    if result['failed']:raise SystemExit(1)

if __name__=='__main__':main()
