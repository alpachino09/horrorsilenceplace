"""Render the delivered asset itself, without image generation or substitute geometry."""
import sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *

def main():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'HoodedGirl.blend'),use_scripts=False)
    scene=bpy.context.scene;rig=bpy.data.objects['HoodedGirl_Rig'];mapping=json.loads(rig['silence_actions'])
    destination=SOURCE/'Previews';destination.mkdir(exist_ok=True)
    for o in list(scene.objects):
        if o.type in {'LIGHT','CAMERA'}:bpy.data.objects.remove(o,do_unlink=True)
    lighting()
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.002));floor=bpy.context.object;floor.name='Review floor (not exported)';floor.data.materials.append(shader('Review floor',(.045,.047,.050),.85))
    for o in scene.objects:
        if o.type=='MESH' and o.data.shape_keys:o.data.shape_keys.animation_data.action=None
    rig.animation_data.use_nla=False;rig.animation_data.action=bpy.data.actions[mapping['Idle']];scene.frame_set(1)
    full=camera('Body front',(1.8,-4.3,1.15),(0,0,.88),65)
    render(destination/'body_front.png',full,640,880,24)
    for name,position in [('side',(4.6,0,1.15)),('back',(-1.8,4.3,1.15))]:
        full.location=position;full.rotation_euler=(Vector((0,0,.88))-full.location).to_track_quat('-Z','Y').to_euler();render(destination/f'body_{name}.png',full,640,880,20)
    face=camera('Face review',(.20,-1.3,1.60),(0,-.015,1.59),85)
    poses=json.loads((ART/'Documentation'/'facial_expressions.json').read_text())
    for name,values in poses.items():
        for o in scene.objects:
            if o.type=='MESH' and o.data.shape_keys:
                for key in o.data.shape_keys.key_blocks:
                    if key.name!='Basis':key.value=values.get(key.name,0)
        bpy.context.view_layer.update();render(destination/f'face_{name}.png',face,576,640,24)
    for o in scene.objects:
        if o.type=='MESH' and o.data.shape_keys:
            for key in o.data.shape_keys.key_blocks:
                if key.name!='Basis':key.value=0
    full.location=(1.8,-4.3,1.15);full.rotation_euler=(Vector((0,0,.88))-full.location).to_track_quat('-Z','Y').to_euler()
    for name,frame in [('Walk',6),('Run',5),('CrouchWalk',7),('UseItem',16)]:
        rig.animation_data.action=bpy.data.actions[mapping[name]];scene.frame_set(frame);render(destination/f'pose_{name}.png',full,640,880,20)
    print('HOODED_REVIEW_RENDERED',len(poses)+7,flush=True)

if __name__=='__main__':main()
