import sys,math,re,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *

def make_rig(human):
    data=bpy.data.armatures.new('HoodedGirl_Humanoid');rig=bpy.data.objects.new('HoodedGirl_Rig',data);bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    def bone(name,head,tail,parent=None,deform=True):
        b=data.edit_bones.new(name);b.head=head;b.tail=tail
        if (b.tail-b.head).length<.002:b.tail=b.head+Vector((0,0,.01))
        if parent:b.parent=data.edit_bones[parent]
        b.use_deform=deform;b.align_roll(Vector((0,-1,0)));return b
    j=human.joint
    bone('Root',(0,0,0),(0,0,.15),deform=False)
    bone('Hips',j('spine05'),j('spine04'),'Root')
    bone('Spine',j('spine04'),j('spine02'),'Hips')
    bone('Chest',j('spine02'),j('spine01'),'Spine')
    bone('UpperChest',j('spine01'),j('neck01'),'Chest')
    bone('Neck',j('neck01'),j('head'),'UpperChest')
    bone('Head',j('head'),j('head','tail'),'Neck')
    bone('Jaw',j('jaw'),j('jaw','tail'),'Head')
    for side,prefix in [('L','Left'),('R','Right')]:
        bone(prefix+'Shoulder',j('clavicle.'+side),j('upperarm01.'+side),'UpperChest')
        bone(prefix+'UpperArm',j('upperarm01.'+side),j('lowerarm01.'+side),prefix+'Shoulder')
        bone(prefix+'LowerArm',j('lowerarm01.'+side),j('wrist.'+side),prefix+'UpperArm')
        bone(prefix+'Hand',j('wrist.'+side),j('finger3-1.'+side),prefix+'LowerArm')
        for finger,label in [(1,'Thumb'),(2,'Index'),(3,'Middle'),(4,'Ring'),(5,'Little')]:
            for segment,suffix in [(1,'Proximal'),(2,'Intermediate'),(3,'Distal')]:
                source=f'finger{finger}-{segment}.{side}'
                parent=prefix+'Hand' if segment==1 else prefix+label+('Proximal' if segment==2 else 'Intermediate')
                bone(prefix+label+suffix,j(source),j(source,'tail'),parent)
        bone(prefix+'UpperLeg',j('upperleg01.'+side),j('lowerleg01.'+side),'Hips')
        bone(prefix+'LowerLeg',j('lowerleg01.'+side),j('foot.'+side),prefix+'UpperLeg')
        toe=np.mean([j(f'toe{i}-1.{side}') for i in [2,3,4]],axis=0)
        bone(prefix+'Foot',j('foot.'+side),toe,prefix+'LowerLeg')
        bone(prefix+'Toes',toe,j('toe3-3.'+side,'tail'),prefix+'Foot')
        bone(prefix+'Eye',j('eye.'+side),j('eye.'+side,'tail'),'Head')
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
    return rig

def mapped_bone(name):
    prefix='Left' if name.endswith('.L') else 'Right'
    if name.startswith('finger'):
        m=re.match(r'finger([1-5])-([1-4])',name)
        if m:return prefix+['Thumb','Index','Middle','Ring','Little'][int(m[1])-1]+['Proximal','Intermediate','Distal'][min(2,int(m[2])-1)]
    for begin,dest in [('clavicle','Shoulder'),('shoulder','Shoulder'),('upperarm','UpperArm'),('lowerarm','LowerArm'),('wrist','Hand'),('metacarpal','Hand'),('upperleg','UpperLeg'),('lowerleg','LowerLeg'),('foot','Foot'),('toe','Toes'),('eye.','Eye')]:
        if name.startswith(begin):return prefix+dest
    if name.startswith('pelvis') or name in ['root','spine05']:return 'Hips'
    if name in ['spine04','spine03']:return 'Spine'
    if name=='spine02' or name.startswith('breast'):return 'Chest'
    if name=='spine01':return 'UpperChest'
    if name.startswith('neck'):return 'Neck'
    if name=='jaw':return 'Jaw'
    return 'Head'

def bind_all(human,rig):
    weights=[]
    for entries in human.weights:
        dest={}
        for name,w in entries.items():
            mapped=mapped_bone(name);dest[mapped]=dest.get(mapped,0)+w
        total=sum(dest.values())
        weights.append({n:w/total for n,w in dest.items()} if total else {'Head':1})
    kd=KDTree(len(human.body_ids))
    for index in human.body_ids:kd.insert(Vector(human.co[index]),int(index))
    kd.balance()
    objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
    for obj in objects:
        obj.vertex_groups.clear()
        groups={b.name:obj.vertex_groups.new(name=b.name) for b in rig.data.bones if b.use_deform}
        fixed=obj.get('rigid_bone')
        for vertex in obj.data.vertices:
            if fixed:
                dest={fixed:1}
                if fixed.endswith('Foot'):
                    t=max(0,min(1,(-vertex.co.y-.13)/.055));t=t*t*(3-2*t)
                    dest={fixed:1-t,fixed.replace('Foot','Toes'):t}
            else:
                nearby=kd.find_n(vertex.co,3);dest={};total=0
                for _,index,d in nearby:
                    blend=1/(d+.0015)**2;total+=blend
                    for name,value in weights[index].items():dest[name]=dest.get(name,0)+value*blend
                dest={name:value/total for name,value in dest.items()}
            dest=dict(sorted(dest.items(),key=lambda p:p[1],reverse=True)[:4]);total=sum(dest.values())
            for name,value in dest.items():
                if value>0:groups[name].add([vertex.index],value/total,'REPLACE')
        modifier=obj.modifiers.new('Humanoid skin','ARMATURE');modifier.object=rig;obj.parent=rig
        obj['game_mesh']=obj.name!='Anatomy_Working'
    return objects

def tpose_rest(rig,objects):
    for name in ['LeftUpperArm','RightUpperArm']:
        direction=rig.data.bones[name].tail_local-rig.data.bones[name].head_local
        angle=math.atan(direction.z/direction.x)
        p=rig.pose.bones[name];base=p.bone.matrix_local.copy();anchor=base.translation
        p.matrix=Matrix.Translation(anchor)@Matrix.Rotation(angle,4,'Y')@Matrix.Translation(-anchor)@base
    bpy.context.view_layer.update()
    for side,sign in [('Left',1),('Right',-1)]:
        p=rig.pose.bones[side+'LowerArm'];m=p.matrix.copy();direction=m.to_3x3()@Vector((0,1,0))
        rotation=direction.rotation_difference(Vector((sign,0,0))).to_matrix().to_4x4();anchor=m.translation.copy()
        p.matrix=Matrix.Translation(anchor)@rotation@Matrix.Translation(-anchor)@m
    bpy.context.view_layer.update()
    matrices={b.name:rig.pose.bones[b.name].matrix@b.matrix_local.inverted() for b in rig.data.bones}
    for obj in objects:
        active={g.index:g.name for g in obj.vertex_groups if g.name in matrices}
        base=np.array([v.co for v in obj.data.vertices],dtype=np.float64);result=np.zeros_like(base)
        for vertex in obj.data.vertices:
            for entry in vertex.groups:
                name=active.get(entry.group)
                if name:result[vertex.index]+=np.array(matrices[name]@vertex.co)*entry.weight
        offset=result-base
        if np.max(np.abs(offset))<1e-6:continue
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                co=np.array([v.co for v in key.data])+offset;key.data.foreach_set('co',co.astype(np.float32).ravel())
        else:obj.data.vertices.foreach_set('co',result.astype(np.float32).ravel())
        obj.data.update()
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='POSE');bpy.ops.pose.armature_apply(selected=False);bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()

def controls(rig):
    bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
    for side in ['Left','Right']:
        ankle=rig.data.edit_bones[side+'Foot'].head.copy();knee=rig.data.edit_bones[side+'LowerLeg'].head.copy()
        for name,position in [(side+'FootIK',ankle),(side+'KneePole',knee+Vector((0,-.7,0)))]:
            b=rig.data.edit_bones.new(name);b.head=position;b.tail=position+Vector((0,0,.06));b.parent=rig.data.edit_bones['Root'];b.use_deform=False
    bpy.ops.object.mode_set(mode='OBJECT')
    for side in ['Left','Right']:
        p=rig.pose.bones[side+'LowerLeg'];c=p.constraints.new('IK');c.name='Foot plant IK';c.target=rig;c.subtarget=side+'FootIK';c.chain_count=2;c.use_stretch=False;c.influence=0
        # Knee lies in front of the leg in the anatomical rest pose; preserve that plane.
        c.pole_target=rig;c.pole_subtarget=side+'KneePole';c.pole_angle=-math.pi/2

if __name__=='__main__':
    bpy.ops.wm.open_mainfile(filepath=str(STAGES/'08_uv_pbr.blend'),use_scripts=False)
    for obj in [o for o in bpy.context.scene.objects if o.name.startswith('Eye.') and not o.get('eye_look_vertical_corrected')]:
        side='Left' if obj.name.endswith('L') else 'Right';keys=obj.data.shape_keys.key_blocks
        up=keys['eyeLookUp'+side];down=keys['eyeLookDown'+side]
        a=np.array([v.co for v in up.data],dtype=np.float32);b=np.array([v.co for v in down.data],dtype=np.float32)
        up.data.foreach_set('co',b.ravel());down.data.foreach_set('co',a.ravel());obj['eye_look_vertical_corrected']=True
    human=Human();rig=make_rig(human);objects=bind_all(human,rig);tpose_rest(rig,objects);controls(rig)
    mapping={name:name for name in rig.data.bones.keys() if name not in ['Root'] and not name.endswith(('FootIK','KneePole'))}
    (ART/'Documentation'/'humanoid_mapping.json').write_text(json.dumps(mapping,indent=2)+'\n')
    checkpoint('09_humanoid_rig')
    print('HOODED_RIG',sum(b.use_deform for b in rig.data.bones),'deform bones',flush=True)
