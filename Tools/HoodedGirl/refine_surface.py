import math
import numpy as np
from common import *

def face_details(head):
    if head.get('surface_refined_v2'):return
    base=np.array([v.co for v in head.data.vertices]);x=base[:,0];y=base[:,1];z=base[:,2]
    inset=np.zeros(len(base));front=np.clip((-y-.06)/.04,0,1)
    for cx in [-.031,.031]:
        q=(x-cx)/.018;line=1.632+.003*np.sqrt(np.maximum(0,1-q*q))
        inset+=.00042*np.exp(-((z-line)/.0009)**2)*np.clip(1-q*q,0,1)
        inset+=.00011*np.exp(-((z-(1.614-.001*q*q))/.0008)**2)*np.clip(1-q*q,0,1)
    naso_x=.016+.30*(1.59-z)
    inset+=.00023*np.exp(-((np.abs(x)-naso_x)/.0012)**2)*np.exp(-((z-1.579)/.022)**4)
    inset+=.00015*np.exp(-(x/.0015)**2)*np.exp(-((z-1.576)/.009)**4)
    inset-=.00013*(np.exp(-((x-.003)/.0015)**2)+np.exp(-((x+.003)/.0015)**2))*np.exp(-((z-1.576)/.009)**4)
    offset=np.zeros_like(base)
    if not head.get('surface_refined'):offset[:,1]=inset*front
    # Slightly taper the lower face; preserve the same topology for all 52 keys.
    jaw=np.exp(-((z-1.535)/.038)**4)*np.clip((np.abs(x)-.025)/.03,0,1)
    offset[:,0]=-np.sign(x)*jaw*.0025
    for key in head.data.shape_keys.key_blocks:
        co=np.array([p.co for p in key.data])+offset;key.data.foreach_set('co',co.astype(np.float32).ravel())
    head.data.update();head['surface_refined']=True;head['surface_refined_v2']=True

def materials():
    for material in bpy.data.materials:
        if material.node_tree is None:continue
        for bs in material.node_tree.nodes:
            if bs.bl_idname!='ShaderNodeBsdfPrincipled':continue
            if material.name.startswith('Skin'):
                if 'IOR' in bs.inputs:bs.inputs['IOR'].default_value=1.42
                if 'Specular IOR Level' in bs.inputs:bs.inputs['Specular IOR Level'].default_value=.35
                if 'Subsurface Scale' in bs.inputs:bs.inputs['Subsurface Scale'].default_value=.0025

def scene_lighting():
    scene=bpy.context.scene
    if scene.world is not None and scene.world.node_tree:
        for n in scene.world.node_tree.nodes:
            if n.bl_idname=='ShaderNodeBackground':n.inputs['Color'].default_value=(.025,.028,.033,1);n.inputs['Strength'].default_value=.35

def apply_all():
    head=bpy.data.objects.get('Face')
    if head is not None:face_details(head)
    jacket=bpy.data.objects.get('Jacket')
    if jacket is not None and not jacket.get('envelope_refined'):
        from wardrobe import chest_envelope
        chest_envelope(jacket);jacket['envelope_refined']=True
    materials();scene_lighting()

def regenerate_skin():
    from textures import skin_maps,iris_map
    existing=[bpy.data.images.get(n) for n in ['Skin_BaseColor','Skin_Normal','Skin_Mask','Eyes_BaseColor']]
    generated=skin_maps()+ (iris_map(),)
    for img in existing:
        if img is not None:img.source='FILE';img.reload()
    for img in generated:
        if img.users==0:bpy.data.images.remove(img)

if __name__=='__main__':
    bpy.ops.wm.open_mainfile(filepath=str(STAGES/'10_animation_facial.blend'),use_scripts=False)
    if bpy.data.objects.get('Head') is not None:bpy.data.objects['Head'].name='Face'
    apply_all();regenerate_skin();checkpoint('10_animation_facial')
