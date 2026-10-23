import bpy,os,pathlib,json,sys
out=pathlib.Path(os.environ.get('SILENCE_PREDATOR_REVIEW','/tmp/silence-own-review'));out.mkdir(parents=True,exist_ok=True)
scene=bpy.context.scene;bpy.context.window.scene=scene
scene.render.resolution_x=1000;scene.render.resolution_y=800;scene.cycles.samples=32;scene.render.threads=6
rig=scene.objects[scene.get('silence_rig','Vesper_Rig')];cameras=json.loads(scene.get('silence_cameras','{}'))
shots=[('overall','Front','Idle',1),('head_closed','HeadClose','Idle',1),('head_open','HeadClose','ListenLoop',31),('side','Side','Idle',1),('back','Back','Idle',1),('horror','Front','Idle',1),('scale','Front','Idle',1)]
chosen=sys.argv[sys.argv.index('--')+1] if '--' in sys.argv else 'all'
for name,camera,action,frame in shots:
 if chosen!='all' and name!=chosen:continue
 for obj in scene.objects:
  if obj.type=='LIGHT':obj.data.energy={'Studio key':1600,'Studio fill':1000,'Studio rim':1800}.get(obj.name,obj.data.energy)
  if obj.name.startswith('Reference '):obj.hide_render=name!='scale'
 scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
 if name=='horror':
  scene.world.node_tree.nodes['Background'].inputs[1].default_value=.035
  for obj in scene.objects:
   if obj.type=='LIGHT':obj.data.energy*=.07 if 'fill' in obj.name else .2
 rig.animation_data.use_nla=False;mapping=json.loads(rig.get('silence_actions','{}'));rig.animation_data.action=bpy.data.actions[mapping.get(action,action)];scene.frame_set(frame);scene.camera=scene.objects[cameras.get(camera,'Camera_'+camera)];scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
print('REVIEW_IMAGES',str(out))
