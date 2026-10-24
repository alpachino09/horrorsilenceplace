import bpy,math
import numpy as np
from common import Vector

ARKIT = ['eyeBlinkLeft','eyeLookDownLeft','eyeLookInLeft','eyeLookOutLeft','eyeLookUpLeft','eyeSquintLeft','eyeWideLeft',
 'eyeBlinkRight','eyeLookDownRight','eyeLookInRight','eyeLookOutRight','eyeLookUpRight','eyeSquintRight','eyeWideRight',
 'jawForward','jawLeft','jawRight','jawOpen','mouthClose','mouthFunnel','mouthPucker','mouthLeft','mouthRight',
 'mouthSmileLeft','mouthSmileRight','mouthFrownLeft','mouthFrownRight','mouthDimpleLeft','mouthDimpleRight',
 'mouthStretchLeft','mouthStretchRight','mouthRollLower','mouthRollUpper','mouthShrugLower','mouthShrugUpper',
 'mouthPressLeft','mouthPressRight','mouthLowerDownLeft','mouthLowerDownRight','mouthUpperUpLeft','mouthUpperUpRight',
 'browDownLeft','browDownRight','browInnerUp','browOuterUpLeft','browOuterUpRight','cheekPuff','cheekSquintLeft','cheekSquintRight',
 'noseSneerLeft','noseSneerRight','tongueOut']

def deltas(human,co):
    x=co[:,0];y=co[:,1];z=co[:,2];n=len(co)
    left=np.clip((x+.004)/.008,0,1);right=1-left
    upper=np.clip((z-1.554)/.006,0,1);lower=1-upper
    native=lambda name:human.delta(name)
    def gaussian(cx,cz,rx,rz):return np.exp(-((x-cx)/rx)**2-((z-cz)/rz)**2)*(y<-.025)
    def custom(dx=0,dy=0,dz=0,mask=None):
        mask=np.ones(n) if mask is None else mask
        return np.stack((np.zeros(n)+dx,np.zeros(n)+dy,np.zeros(n)+dz),axis=-1)*mask[:,None]
    out={};mouth=gaussian(0,1.556,.045,.024);jaw=gaussian(0,1.526,.06,.047)
    for side,s,m in [('Left','left',left),('Right','right',right)]:
        cx=.031 if side=='Left' else -.031
        eye=gaussian(cx,1.622,.024,.019)
        cheek=gaussian(cx*1.35,1.598,.025,.022)
        out['eyeBlink'+side]=native('eye-'+s+'-closure')*.95
        out['eyeSquint'+side]=native('eye-'+s+'-slit')*.8
        out['eyeWide'+side]=native('eye-'+s+'-opened-up')*.8
        out['eyeLookDown'+side]=custom(dz=-.002,mask=eye)
        out['eyeLookUp'+side]=custom(dz=.0025,mask=eye)
        out['eyeLookIn'+side]=custom(dx=-.002 if side=='Left' else .002,mask=eye)
        out['eyeLookOut'+side]=custom(dx=.002 if side=='Left' else -.002,mask=eye)
        out['browDown'+side]=native('eyebrows-'+s+'-down')*.8
        out['browOuterUp'+side]=native('eyebrows-'+s+'-extern-up')*.85
        corner=gaussian(.024 if side=='Left' else -.024,1.556,.018,.014)
        out['mouthSmile'+side]=native('mouth-corner-puller')*m[:,None]*.75+custom(dz=.002,mask=corner)
        out['mouthFrown'+side]=native('mouth-depression')*m[:,None]*.65
        out['mouthDimple'+side]=native('mouth-retraction')*m[:,None]*.5+custom(dy=.002,mask=corner)
        out['mouthStretch'+side]=custom(dx=.008 if side=='Left' else -.008,mask=corner)
        out['mouthPress'+side]=native('mouth-compression')*m[:,None]*.7
        out['mouthLowerDown'+side]=native('mouth-depression-retraction')*(m*lower)[:,None]*.75
        out['mouthUpperUp'+side]=native('mouth-upward-retraction')*(m*upper)[:,None]*.75
        out['cheekSquint'+side]=custom(dy=-.0015,dz=.004,mask=cheek)+native('eye-'+s+'-slit')*.25
        out['noseSneer'+side]=native('nose-'+s+'-elevation')*.75
    out['browInnerUp']=(native('eyebrows-left-inner-up')+native('eyebrows-right-inner-up'))*.75
    out['jawOpen']=native('mouth-open')*.9
    out['jawForward']=custom(dy=-.008,mask=jaw)
    out['jawLeft']=custom(dx=.008,mask=jaw)
    out['jawRight']=custom(dx=-.008,mask=jaw)
    out['mouthClose']=native('mouth-compression')*.85+custom(dz=.001,mask=mouth*lower)
    out['mouthFunnel']=native('mouth-protusion')*.65+native('mouth-open')*.15+native('mouth-pursing')*.25
    out['mouthPucker']=native('mouth-pursing')*.8
    out['mouthLeft']=custom(dx=.007,mask=mouth)
    out['mouthRight']=custom(dx=-.007,mask=mouth)
    out['mouthRollLower']=custom(dy=.004,dz=.001,mask=mouth*lower)
    out['mouthRollUpper']=custom(dy=.004,dz=-.001,mask=mouth*upper)
    out['mouthShrugLower']=native('mouth-elevation')*lower[:,None]*.55+custom(dz=.001,mask=jaw)
    out['mouthShrugUpper']=native('mouth-elevation')*upper[:,None]*.55
    out['cheekPuff']=custom(dy=-.006,mask=gaussian(-.044,1.57,.027,.025)+gaussian(.044,1.57,.027,.025))
    # Oral-floor motion accompanies the separate, visible tongue mesh deformation.
    oral=gaussian(0,1.549,.018,.009)*np.clip((y+.145)/.025,0,1)
    out['tongueOut']=custom(dy=-.003,dz=.001,mask=oral)
    assert set(out)==set(ARKIT)
    return out

def add_head_keys(head,human,ids):
    base=np.array([v.co for v in head.data.vertices]); head.shape_key_add(name='Basis',from_mix=False)
    for name,delta in deltas(human,human.co).items():
        key=head.shape_key_add(name=name,from_mix=False);key.value=0;a=base+delta[np.array(ids)];key.data.foreach_set('co',a.astype(np.float32).ravel())
        if np.max(np.linalg.norm(a-base,axis=1))<1e-7:raise RuntimeError('Empty facial deformation: '+name)

def subdivide_keys(obj):
    """Evaluate the same subdivision for basis and every morph; retain identical topology."""
    modifier=obj.modifiers.new('Facial quad surface','SUBSURF');modifier.levels=1;modifier.render_levels=1
    deps=bpy.context.evaluated_depsgraph_get();keys=obj.data.shape_keys.key_blocks;names=[k.name for k in keys]
    for k in keys:k.value=0
    bpy.context.view_layer.update();evaluated=obj.evaluated_get(deps)
    basis=bpy.data.meshes.new_from_object(evaluated,depsgraph=deps,preserve_all_data_layers=True)
    coordinates={}
    for name in names[1:]:
        keys[name].value=1;bpy.context.view_layer.update();sample=obj.evaluated_get(deps).to_mesh()
        arr=np.empty(len(sample.vertices)*3,dtype=np.float32);sample.vertices.foreach_get('co',arr);coordinates[name]=arr
        obj.evaluated_get(deps).to_mesh_clear();keys[name].value=0
    old=obj.data;obj.modifiers.remove(modifier);obj.data=basis;obj.shape_key_add(name='Basis',from_mix=False)
    for name in names[1:]:
        key=obj.shape_key_add(name=name,from_mix=False);key.value=0;key.data.foreach_set('co',coordinates[name])
    if old.users==0:bpy.data.meshes.remove(old)
    for p in obj.data.polygons:p.use_smooth=True

def transfer_keys(obj,head,indices):
    base=np.array([v.co for v in obj.data.vertices]);head_base=np.array([v.co for v in head.data.vertices])
    obj.shape_key_add(name='Basis',from_mix=False)
    for source in head.data.shape_keys.key_blocks:
        if source.name=='Basis':continue
        d=np.array([p.co for p in source.data])-head_base
        offset=d[np.array(indices)]
        if np.max(np.linalg.norm(offset,axis=1))<1e-7:continue
        key=obj.shape_key_add(name=source.name,from_mix=False);key.value=0;key.data.foreach_set('co',(base+offset).astype(np.float32).ravel())

def expressions(head):
    return {
      'neutral':{},'blink':{'eyeBlinkLeft':1,'eyeBlinkRight':1},'mouth_open':{'jawOpen':.8},
      'fear':{'browInnerUp':.75,'browOuterUpLeft':.35,'browOuterUpRight':.35,'eyeWideLeft':.5,'eyeWideRight':.5,'jawOpen':.22,'mouthStretchLeft':.22,'mouthStretchRight':.22},
      'pain':{'browDownLeft':.7,'browDownRight':.7,'eyeSquintLeft':.6,'eyeSquintRight':.6,'noseSneerLeft':.35,'noseSneerRight':.35,'mouthFrownLeft':.35,'mouthFrownRight':.35,'jawOpen':.16},
      'surprise':{'browInnerUp':.8,'browOuterUpLeft':.7,'browOuterUpRight':.7,'eyeWideLeft':.65,'eyeWideRight':.65,'jawOpen':.5,'mouthFunnel':.25},
      'tension':{'browDownLeft':.25,'browDownRight':.25,'mouthPressLeft':.7,'mouthPressRight':.7,'mouthRollLower':.2},
    }
