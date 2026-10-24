import sys,os,math,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy,numpy as np
from common import *
from textures import make_materials
from face_shapes import add_head_keys,subdivide_keys,ARKIT,transfer_keys

def eyes(human,materials):
    objects=[]
    for side in ['L','R']:
        center=Vector(human.joint('eye.'+side));center.y-=.001
        radius=.01215;verts=[];faces=[];uvs=[];rows=32;cols=64
        for i in range(rows+1):
            phi=.0001+(math.pi-.0002)*i/rows
            for j in range(cols):
                a=j*math.tau/cols;x=math.sin(phi)*math.cos(a);z=math.sin(phi)*math.sin(a)
                verts.append(center+Vector((x,-math.cos(phi),z))*radius);uvs.append((.5+x*.5,.5+z*.5))
        for i in range(rows):
            for j in range(cols):a=i*cols+j;b=i*cols+(j+1)%cols;faces.append((a,b,b+cols,a+cols))
        eye=mesh('Eye.'+side,verts,faces,materials['Eyes']);recalc(eye)
        uv=eye.data.uv_layers.new(name='UVMap')
        for p in eye.data.polygons:
            for loop in p.loop_indices:uv.data[loop].uv=uvs[eye.data.loops[loop].vertex_index]
        eye['rigid_bone']='LeftEye' if side=='L' else 'RightEye'
        eye.shape_key_add(name='Basis',from_mix=False)
        for name in ARKIT:
            if name.startswith('eyeLook') and name.endswith('Left' if side=='L' else 'Right'):
                key=eye.shape_key_add(name=name,from_mix=False);key.value=0
                direction=-1 if 'In' in name and side=='L' or 'Out' in name and side=='R' else 1
                axis='X' if 'Up' in name or 'Down' in name else 'Z'
                angle=.22*(-1 if 'Up' in name else 1) if axis=='X' else .22*direction
                rotation=Matrix.Rotation(angle,3,axis)
                for co,p in zip(verts,key.data):p.co=center+rotation@(co-center)
        eye['eye_look_vertical_corrected']=True;objects.append(eye)
    return objects

def facial_hair(head,human,materials):
    bvh=surface_bvh(head);rng=np.random.default_rng(2713);brows=[];lashes=[]
    def front(x,z):
        p,n,_,_=bvh.ray_cast(Vector((x,-.5,z)),Vector((0,1,0)),1)
        return p if p is not None else Vector((x,-.13,z))
    for side in [-1,1]:
        for i in range(360):
            t=(i+.5)/360;x=side*(.011+t*.047);z=1.644+.002*math.sin(t*math.pi)-.006*t+(rng.random()-.5)*.005
            p=front(x,z);p.y-=.0007
            length=.0045+rng.random()*.002;dx=side*(.0015+.003*t);dz=length*(.9-.7*t)
            points=[p+Vector((dx*f,-math.sin(f*math.pi)*.00035,dz*f)) for f in np.linspace(0,1,4)]
            brows.append(curve_mesh('Brow hair',points,[.00012,.00012,.000075,.00002],materials['Brows'],4))
        center=human.joint('eye.L' if side==1 else 'eye.R')
        for upper,count in [(True,34),(False,18)]:
            for i in range(count):
                t=(i+1)/(count+1);x=center[0]+(t-.5)*.024
                z=center[2]+(math.sin(t*math.pi)*(.0048 if upper else -.0036))
                p=front(x,z);p.y-=.0005;length=.0024 if upper else .0013
                direction=Vector(((t-.5)*.003,-length,length*(.65 if upper else -.3)))
                points=[p+direction*f+Vector((0,-.0004*math.sin(f*math.pi),.0003*math.sin(f*math.pi))) for f in np.linspace(0,1,4)]
                lashes.append(curve_mesh('Lash',points,[.000075,.00007,.00004,.000012],materials['Lashes'],4))
    brow=combine(brows,'Brows');lash=combine(lashes,'Lashes')
    kd=KDTree(len(head.data.vertices))
    for v in head.data.vertices:kd.insert(v.co,v.index)
    kd.balance()
    for obj in [brow,lash]:
        indices=[kd.find(v.co)[1] for v in obj.data.vertices];transfer_keys(obj,head,indices);obj['rigid_bone']='Head'
    return brow,lash

def build():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene=bpy.context.scene;scene.name='SILENCE_HoodedGirl';scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    materials=make_materials();human=Human()
    working,ids=human.part('Anatomy_Working',lambda f:True,materials['Hands']);working.hide_render=True
    checkpoint('03_anatomy_blockout')
    head,head_ids=human.part('Face',lambda f:min(human.co[i,2] for i in f)>1.475,materials['Skin'])
    uv=head.data.uv_layers.active
    for p in head.data.polygons:
        values=[];front=p.center.y<.005
        for loop in p.loop_indices:
            co=head.data.vertices[head.data.loops[loop].vertex_index].co
            values.append((co.x/.225+.5 if front else .94+.045*(co.x+.1)/.2,(co.z-1.475)/.27))
        for loop,value in zip(p.loop_indices,values):uv.data[loop].uv=value
    add_head_keys(head,human,head_ids);subdivide_keys(head)
    from refine_surface import face_details
    face_details(head)
    eye_objects=eyes(human,materials);brows,lashes=facial_hair(head,human,materials)
    bpy.context.view_layer.update()
    for eye in eye_objects:
        if max(eye.dimensions)>.04:raise RuntimeError('Invalid eye bounds before rendering/export: '+eye.name)
    for obj in [head,brows,lashes]+eye_objects:
        for key in obj.data.shape_keys.key_blocks:
            if key.name!='Basis' and key.value!=0:raise RuntimeError('Non-neutral saved expression: '+obj.name+'/'+key.name)
    lighting();cam=camera('Face_Review',(0,-1.1,1.62),(0,-.10,1.61),85);scene.camera=cam
    checkpoint('04_face_refined')
    reviews=SOURCE/'Previews';reviews.mkdir(exist_ok=True)
    render(reviews/'face_initial.png',cam,768,960,24)
    return human,materials,working,head,eye_objects,brows,lashes

if __name__=='__main__':build()
