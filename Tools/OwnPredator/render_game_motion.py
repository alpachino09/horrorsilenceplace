"""Render real DCC game motions for review, without claiming Unity footage."""
import bpy,json,os,pathlib,math
from mathutils import Vector
root=pathlib.Path(os.environ['SILENCE_REPOSITORY'])
out=pathlib.Path(os.environ.get('SILENCE_GAME_REVIEW','/tmp/silence-game-review'));out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/VesperGame/Vesper_Game.blend'),use_scripts=False)
scene=next(s for s in bpy.data.scenes if s.get('silence_rig'));bpy.context.window.scene=scene
rig=scene.objects[scene['silence_rig']];mapping=json.loads(rig['silence_actions']);cameras=json.loads(scene['silence_cameras'])
scene.render.resolution_x=600;scene.render.resolution_y=480;scene.render.resolution_percentage=100
scene.cycles.samples=12;scene.render.threads_mode='FIXED';scene.render.threads=6
scene.camera=scene.objects[cameras['Front']];camera_start=scene.camera.location.copy()
floor=scene.objects['Demo floor'];material=floor.data.materials[0]
shader=next(n for n in material.node_tree.nodes if n.bl_idname=='ShaderNodeBsdfPrincipled')
checker=material.node_tree.nodes.new('ShaderNodeTexChecker');checker.inputs['Scale'].default_value=2
checker.inputs['Color1'].default_value=(.028,.033,.038,1);checker.inputs['Color2'].default_value=(.055,.060,.065,1)
geometry=material.node_tree.nodes.new('ShaderNodeNewGeometry');material.node_tree.links.new(geometry.outputs['Position'],checker.inputs['Vector'])
material.node_tree.links.new(checker.outputs['Color'],shader.inputs['Base Color'])
rig.animation_data.use_nla=False
for name,count in [('GameWalk',12),('ListenStart',8),('BreathGame',4)]:
    rig.animation_data.action=bpy.data.actions[mapping[name]]
    start,end=rig.animation_data.action.frame_range
    for i in range(count):
        fraction=i/count;scene.frame_set(round(start+(end-start)*fraction))
        # A tracking camera and stationary metre grid expose foot sliding in the walk.
        shift=Vector((0,-1.35/((23/30)*.64)*(scene.frame_current-1)/30,0)) if name=='GameWalk' else Vector((0,0,0))
        rig.location=shift;scene.camera=scene.objects[cameras['HeadClose' if name=='ListenStart' else 'Front']]
        if name=='GameWalk':scene.camera.location=camera_start+shift
        elif name=='BreathGame':scene.camera.location=camera_start
        scene.render.filepath=str(out/f'{name}_{i:02d}.png');bpy.ops.render.render(write_still=True)
    rig.location=(0,0,0)
# A real visibility check with back-facing surfaces made transparent, approximating culling.
for material in bpy.data.materials:
    if not material.use_nodes:continue
    nodes=material.node_tree.nodes;links=material.node_tree.links
    output=next((n for n in nodes if n.bl_idname=='ShaderNodeOutputMaterial'),None)
    if output is None or not output.inputs['Surface'].is_linked:continue
    previous=output.inputs['Surface'].links[0].from_socket
    geometry=nodes.new('ShaderNodeNewGeometry');transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader')
    links.new(geometry.outputs['Backfacing'],mix.inputs[0]);links.new(previous,mix.inputs[1]);links.new(transparent.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],output.inputs['Surface'])
rig.animation_data.action=bpy.data.actions[mapping['Idle']];scene.frame_set(1)
scene.camera=scene.objects[cameras['MawFront']];scene.render.filepath=str(out/'front_culling_check.png');bpy.ops.render.render(write_still=True)
print('GAME_MOTION_RENDERS',str(out),flush=True)
