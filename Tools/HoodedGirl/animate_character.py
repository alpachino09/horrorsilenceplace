import sys,math,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *
from face_shapes import expressions

def reset(rig):
    for p in rig.pose.bones:p.matrix_basis=Matrix.Identity(4);p.rotation_mode='QUATERNION'

def offset_world(p,offset):
    m=p.bone.matrix_local.copy();m.translation+=Vector(offset);p.matrix=m

def rotation_world(p,rotation):
    base=p.bone.matrix_local.copy()
    if p.parent:base=p.parent.matrix@p.parent.bone.matrix_local.inverted()@base
    anchor=base.translation.copy();p.matrix=Matrix.Translation(anchor)@rotation.to_4x4()@Matrix.Translation(-anchor)@base

def foot_control(rig,side,position):
    p=rig.pose.bones[side+'FootIK'];m=p.bone.matrix_local.copy();m.translation=position;p.matrix=m

def orient_foot(rig,side,pitch):
    p=rig.pose.bones[side+'Foot'];m=p.bone.matrix_local.copy();m.translation=rig.pose.bones[side+'LowerLeg'].tail.copy()
    pivot=m.translation.copy();p.matrix=Matrix.Translation(pivot)@Matrix.Rotation(pitch,4,'X')@Matrix.Translation(-pivot)@m
    bpy.context.view_layer.update()
    toe=rig.pose.bones[side+'Toes'];rotation_world(toe,Matrix.Rotation(-max(0,pitch)*.9,3,'X'))

def skin_min_z(obj,rig):
    matrices={g.index:rig.pose.bones[g.name].matrix@rig.data.bones[g.name].matrix_local.inverted() for g in obj.vertex_groups if g.name in rig.data.bones}
    low=1e9
    for vertex in obj.data.vertices:
        point=Vector((0,0,0))
        for g in vertex.groups:
            if g.group in matrices:point+=(matrices[g.group]@vertex.co)*g.weight
        low=min(low,point.z)
    return low

def calibrate_poles(rig):
    reset(rig);offset_world(rig.pose.bones['Hips'],(0,0,-.09));bpy.context.view_layer.update();result={}
    for side in ['Left','Right']:
        constraint=rig.pose.bones[side+'LowerLeg'].constraints['Foot plant IK'];constraint.influence=1
        choices=[]
        for angle in [0,math.pi/2,-math.pi/2,math.pi]:
            constraint.pole_angle=angle;bpy.context.view_layer.update();knee=rig.pose.bones[side+'LowerLeg'].head.copy()
            choices.append((knee.y+abs(knee.x-rig.data.bones[side+'LowerLeg'].head_local.x)*.2,angle))
        constraint.pole_angle=min(choices)[1];result[side]=constraint.pole_angle
    reset(rig);bpy.context.view_layer.update();return result

def gait(q,period,speed,stance,lift):
    stride=speed*period*stance
    if q<stance:
        t=q/stance;y=-stride*.5+stride*t;z=0
        pitch=-.10*max(0,1-t/.18)**2+.16*max(0,(t-.8)/.2)**2
        return y,z,pitch,True
    t=(q-stance)/(1-stance);m=stride*(1-stance)/stance
    y=(2*t**3-3*t*t+1)*stride*.5+(t**3-2*t*t+t)*m+(-2*t**3+3*t*t)*(-stride*.5)+(t**3-t*t)*m
    return y,lift*math.sin(math.pi*t)**2,-.12*math.sin(math.pi*t),False

def pose(rig,kind,u,period):
    reset(rig);moving=kind in ['walk','back','strafe_left','strafe_right','run','crouch_walk','carry_walk','wounded_walk']
    run=kind=='run';crouch=kind in ['crouch_idle','crouch_walk'];carry=kind in ['carry_idle','carry_walk','use_item']
    down=.70 if crouch else .08 if run else .09 if moving else .024
    bob=(.015 if run else .006)*math.sin(u*math.tau*2) if moving else .002*math.sin(u*math.tau)
    lean=.11 if crouch else .08 if run else .025 if moving else 0
    if kind=='land':down+=.11*math.sin(u*math.pi)
    if kind=='jump':down-=.06*math.sin(u*math.pi)
    if kind=='fall':down-=.035;lean=.07
    if kind.startswith('wounded'):down+=.035;lean=.13
    if kind=='death':down=.024+.65*(u*u*(3-2*u));lean=1.35*(u*u*(3-2*u))
    offset_world(rig.pose.bones['Hips'],(.007*math.sin(u*math.tau) if moving else 0,0,-down+bob))
    bpy.context.view_layer.update()
    hips=rig.pose.bones['Hips'];origin=hips.matrix.translation.copy()
    yaw=(.45 if kind=='turn_left' else -.45)*math.sin(u*math.pi) if kind.startswith('turn_') else 0
    hips.matrix=Matrix.Translation(origin)@Matrix.Rotation(yaw,4,'Z')@Matrix.Rotation(lean,4,'X')@Matrix.Translation(-origin)@hips.matrix
    bpy.context.view_layer.update()
    breath=math.sin(u*math.tau)
    for n,amount in [('Chest',.003),('Spine',.0015)]:rig.pose.bones[n].scale=(1+amount*breath,1+amount*.5*breath,1+amount*.7*breath)
    for side,phase,sign in [('Left',0,1),('Right',.5,-1)]:
        swing=(.65 if run else .28)*math.cos((u+phase)*math.tau) if moving else .015*math.sin(u*math.tau)
        if crouch:swing=-.40+.10*math.cos((u+phase)*math.tau)
        if carry:swing=-.55
        if kind=='use_item':swing=-.55-.25*math.sin(u*math.pi)
        if kind.startswith('wounded') and side=='Left':swing=-.42
        rotation_world(rig.pose.bones[side+'UpperArm'],Matrix.Rotation(swing,3,'X')@Matrix.Rotation(sign*1.35,3,'Y'))
        bpy.context.view_layer.update()
        flex=.95 if kind.startswith('wounded') and side=='Left' else .70 if carry else .90 if run else .55 if crouch else .08
        rotation_world(rig.pose.bones[side+'LowerArm'],Matrix.Rotation(-flex,3,'X'))
        for label in ['Thumb','Index','Middle','Ring','Little']:
            for suffix,factor in [('Proximal',1),('Intermediate',.8),('Distal',.45)]:
                p=rig.pose.bones[side+label+suffix];p.rotation_quaternion=Matrix.Rotation((.35 if carry else .12)*factor,3,'X').to_quaternion()
    if kind=='look_around':rotation_world(rig.pose.bones['Head'],Matrix.Rotation(.45*math.sin(u*math.tau),3,'Z'))
    speed=5.5 if run else 1.4 if crouch else 1.1 if kind=='wounded_walk' else 1.8
    stance=.25 if run else .60 if crouch else .55
    pitches={};targets={};planted={}
    for side,phase in [('Left',0),('Right',.5)]:
        q=(u+phase)%1;off,lift,pitch,plant=gait(q,period,speed,stance,.13 if run else .07) if moving else (0,0,0,True)
        ankle=rig.data.bones[side+'Foot'].head_local.copy()
        if kind.startswith('strafe_'):ankle.x+=off*(1 if kind=='strafe_left' else -1)
        else:ankle.y+=off*(-1 if kind=='back' else 1)
        if kind in ['jump','fall']:lift+=.08*(math.sin(u*math.pi) if kind=='jump' else 1);plant=False
        ankle.z+=lift;targets[side]=ankle;pitches[side]=pitch;planted[side]=plant
        foot_control(rig,side,ankle);rig.pose.bones[side+'LowerLeg'].constraints['Foot plant IK'].influence=1
    bpy.context.view_layer.update()
    for side in ['Left','Right']:orient_foot(rig,side,pitches[side])
    bpy.context.view_layer.update()
    # Correct actual skinned sole support instead of assuming the ankle is the floor.
    for _ in range(2):
        for side,letter in [('Left','L'),('Right','R')]:
            sole=bpy.data.objects.get('Rubber sole '+letter)
            if sole is None or not planted[side]:continue
            z=skin_min_z(sole,rig);targets[side].z-=z;foot_control(rig,side,targets[side])
        bpy.context.view_layer.update()
        for side in ['Left','Right']:orient_foot(rig,side,pitches[side])
        bpy.context.view_layer.update()
    if kind=='breath':
        reset(rig)
        for side in ['Left','Right']:rig.pose.bones[side+'LowerLeg'].constraints['Foot plant IK'].influence=0
        rig.pose.bones['Chest'].scale=(1+.005*breath,1+.002*breath,1+.004*breath)
        rig.pose.bones['Spine'].rotation_quaternion=Matrix.Rotation(.004*breath,3,'X').to_quaternion()
    return planted

CLIPS=[('Idle',84,'idle',True),('Walk',22,'walk',True),('WalkBack',22,'back',True),('StrafeLeft',24,'strafe_left',True),('StrafeRight',24,'strafe_right',True),('Run',15,'run',True),
 ('CrouchIdle',84,'crouch_idle',True),('CrouchWalk',27,'crouch_walk',True),('TurnLeft',30,'turn_left',False),('TurnRight',30,'turn_right',False),('LookAround',80,'look_around',True),
 ('Jump',20,'jump',False),('Fall',30,'fall',True),('Land',12,'land',False),('CarryIdle',84,'carry_idle',True),('CarryWalk',22,'carry_walk',True),('WoundedIdle',84,'wounded_idle',True),
 ('WoundedWalk',28,'wounded_walk',True),('Death',50,'death',False),('UseItem',30,'use_item',False),('BreathGame',84,'breath',True)]

def make_actions(rig):
    scene=bpy.context.scene;scene.render.fps=30;rig.animation_data_create();mapping={};metrics=[]
    for name,frames,kind,loop in CLIPS:
        action=bpy.data.actions.new(name);action.use_fake_user=True;rig.animation_data.action=action;rig.animation_data.use_nla=False
        errors=[]
        for f in range(frames+1):
            rig.animation_data.action=None
            scene.frame_set(f+1);planted=pose(rig,kind,f/frames,frames/30)
            if kind in ['walk','run','crouch_walk']:
                for side,letter in [('Left','L'),('Right','R')]:
                    if planted[side]:errors.append(abs(skin_min_z(bpy.data.objects['Rubber sole '+letter],rig)))
            rig.animation_data.action=action
            for p in rig.pose.bones:
                p.keyframe_insert('location',frame=f+1,group=p.name);p.keyframe_insert('rotation_quaternion',frame=f+1,group=p.name);p.keyframe_insert('scale',frame=f+1,group=p.name)
            for side,letter in [('Left','L'),('Right','R')]:
                c=rig.pose.bones[side+'LowerLeg'].constraints['Foot plant IK'];c.keyframe_insert('influence',frame=f+1)
        mapping[name]=action.name
        metrics.append({'name':name,'frames':[1,frames+1],'seconds':frames/30,'loop':loop,'max_planted_sole_height_error':max(errors) if errors else None})
        print('HOODED_CLIP',name,flush=True)
    rig.animation_data.action=None
    track=rig.animation_data.nla_tracks.new();track.name='HoodedGirl body exports';start=1
    for name,action_name in mapping.items():
        action=bpy.data.actions[action_name];strip=track.strips.new(name,start,action);strip.name=name;strip.extrapolation='NOTHING';start=int(strip.frame_end)+2
    rig.animation_data.use_nla=False;rig.animation_data.action=bpy.data.actions[mapping['Idle']];scene.frame_set(1)
    rig['silence_actions']=json.dumps(mapping)
    return metrics

def make_face_demo(rig):
    poses=expressions(bpy.data.objects['Face']);actions={}
    for obj in [o for o in bpy.context.scene.objects if o.type=='MESH' and o.data.shape_keys is not None]:
        keys=obj.data.shape_keys;keys.animation_data_create();action=bpy.data.actions.new('FaceDemo_'+obj.name);action.use_fake_user=True;keys.animation_data.action=action
        for i,values in enumerate(poses.values()):
            for frame in [1+i*30,16+i*30]:
                for key in keys.key_blocks:
                    if key.name=='Basis':continue
                    key.value=values.get(key.name,0);key.keyframe_insert('value',frame=frame)
        for key in keys.key_blocks:
            if key.name!='Basis':key.value=0;key.keyframe_insert('value',frame=211)
        actions[obj.name]=action.name;keys.animation_data.action=None
    rig['silence_facial_actions']=json.dumps(actions)
    (ART/'Documentation'/'facial_expressions.json').write_text(json.dumps(poses,indent=2)+'\n')

if __name__=='__main__':
    bpy.ops.wm.open_mainfile(filepath=str(STAGES/'09_humanoid_rig.blend'),use_scripts=False)
    if bpy.data.objects.get('Head') is not None:bpy.data.objects['Head'].name='Face'
    rig=bpy.data.objects['HoodedGirl_Rig'];poles=calibrate_poles(rig);metrics=make_actions(rig);make_face_demo(rig)
    report={'blender':bpy.app.version_string,'clips':metrics,'pole_angles':poles,'native_unity_tested':False,'art_quality_approved':False}
    (ART/'Documentation'/'animation.json').write_text(json.dumps(report,indent=2)+'\n');checkpoint('10_animation_facial')
