"""Blender 5.2 helpers. Graphical MakeHuman data are CC0; this code is original."""
import bpy, bmesh, math, json, os
import numpy as np
from pathlib import Path
from mathutils import Vector, Matrix
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

ROOT = Path(os.environ.get('SILENCE_REPOSITORY', Path(__file__).resolve().parents[2]))
ART = ROOT/'Assets/Characters/HoodedGirl'
SOURCE = ROOT/'ArtSource/HoodedGirl'
STAGES = Path('/workspace/generated-art/HoodedGirl/Stages') if Path('/workspace').exists() else SOURCE/'Stages'
DATA = Path(__file__).parent/'MakeHuman'
for folder in [ART/'Models', ART/'Textures', ART/'Documentation', SOURCE, STAGES]: folder.mkdir(parents=True, exist_ok=True)

def parse_obj(path):
    verts=[]; uv=[]; groups={}; group='body'
    for line in Path(path).read_text().splitlines():
        a=line.split()
        if not a: continue
        if a[0]=='v': verts.append(tuple(map(float,a[1:4])))
        elif a[0]=='vt': uv.append(tuple(map(float,a[1:3])))
        elif a[0]=='g': group=' '.join(a[1:])
        elif a[0]=='f':
            face=[]; tex=[]
            for part in a[1:]:
                bits=part.split('/'); face.append(int(bits[0])-1); tex.append(int(bits[1])-1 if len(bits)>1 and bits[1] else 0)
            groups.setdefault(group,[]).append((face,tex))
    return np.array(verts,dtype=np.float64),np.array(uv),groups

def target(name, count):
    out=np.zeros((count,3),dtype=np.float64)
    for line in (DATA/'targets'/f'{name}.target').read_text().splitlines():
        a=line.split()
        if len(a)==4 and not a[0].startswith('#'): out[int(a[0])]=list(map(float,a[1:]))
    return out

class Human:
    def __init__(self, refined=True):
        raw,self.uv,self.groups=parse_obj(DATA/'3dobjs/base.obj'); self.count=len(raw)
        identity=json.loads((DATA/'SOURCE.json').read_text())['identity_weights']
        if not refined: identity={'macrodetails/caucasian-female-young':1.0}
        for name,weight in identity.items(): raw += target(name,self.count)*weight
        ids=sorted({v for f,_ in self.groups['body'] for v in f})
        self.body_ids=np.array(ids,dtype=np.int32)
        self.floor=float(raw[ids,1].min()); height=float(raw[ids,1].max()-self.floor)
        self.scale=1.72/height
        self.raw=raw; self.co=self.convert(raw)
        self.rig_data=json.loads((DATA/'rigs/default.mhskel').read_text())
        self.weights=[{} for _ in range(self.count)]
        for name,entries in json.loads((DATA/'rigs/default_weights.mhw').read_text())['weights'].items():
            for index,value in entries: self.weights[index][name]=value
    def convert(self, raw, delta=False):
        out=np.empty_like(raw); out[:,0]=raw[:,0]*self.scale; out[:,1]=-raw[:,2]*self.scale
        out[:,2]=(raw[:,1]-(0 if delta else self.floor))*self.scale+(0 if delta else .018)
        return out
    def delta(self,name): return self.convert(target('expression/units/caucasian/'+name,self.count),True)
    def joint(self,bone,end='head'):
        key=self.rig_data['bones'][bone][end]
        return self.co[self.rig_data['joints'][key]].mean(axis=0)
    def part(self,name,predicate,material):
        faces=[(f,uv) for f,uv in self.groups['body'] if predicate(f)]
        ids=sorted({i for f,_ in faces for i in f}); mapping={old:i for i,old in enumerate(ids)}
        obj=mesh(name,self.co[ids],[[mapping[i] for i in f] for f,_ in faces],material)
        layer=obj.data.uv_layers.new(name='UVMap')
        for polygon,(_,uv) in zip(obj.data.polygons,faces):
            for loop,index in zip(polygon.loop_indices,uv):layer.data[loop].uv=self.uv[index]
        obj['source_ids']=ids
        return obj,ids

def mesh(name, vertices, faces, material=None):
    data=bpy.data.meshes.new(name+' Mesh'); data.from_pydata(vertices,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.scene.collection.objects.link(obj)
    if material: data.materials.append(material)
    for p in data.polygons:p.use_smooth=True
    return obj

def recalc(obj,radial=False):
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.update()
    if radial and sum(p.normal.dot(Vector((p.center.x,p.center.y,0))) for p in obj.data.polygons)<0:
        for p in obj.data.polygons:p.flip()
        obj.data.update()

def apply(obj,kind,**attrs):
    bpy.context.view_layer.objects.active=obj
    mod=obj.modifiers.new(kind,kind)
    for key,value in attrs.items():setattr(mod,key,value)
    bpy.ops.object.modifier_apply(modifier=mod.name)

def planar_uv(obj,scale=1):
    uv=obj.data.uv_layers.active or obj.data.uv_layers.new(name='UVMap')
    for p in obj.data.polygons:
        axis=max(range(3),key=lambda i:abs(p.normal[i]));axes=[i for i in range(3) if i!=axis]
        for loop in p.loop_indices:
            co=obj.data.vertices[obj.data.loops[loop].vertex_index].co
            uv.data[loop].uv=(co[axes[0]]*scale,co[axes[1]]*scale)

def image_file(name,rgb,data=False):
    h,w=rgb.shape[:2]; img=bpy.data.images.new(name,width=w,height=h,alpha=True)
    img.colorspace_settings.name='Non-Color' if data else 'sRGB'
    rgba=np.ones((h,w,4),dtype=np.float32);rgba[:,:,:rgb.shape[2]]=np.clip(rgb,0,1)
    img.pixels.foreach_set(rgba.ravel());img.filepath_raw=str(ART/'Textures'/f'{name}.png');img.file_format='PNG';img.save()
    return img

def shader(name,color,roughness=.5,base=None,normal=None,mask=None,sss=0):
    m=bpy.data.materials.new(name);m.use_fake_user=True
    if m.node_tree is None:m.use_nodes=True
    tree=m.node_tree;tree.nodes.clear();bs=tree.nodes.new('ShaderNodeBsdfPrincipled');out=tree.nodes.new('ShaderNodeOutputMaterial');tree.links.new(bs.outputs['BSDF'],out.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*color,1);bs.inputs['Roughness'].default_value=roughness
    if sss and 'Subsurface Weight' in bs.inputs:
        bs.inputs['Subsurface Weight'].default_value=sss
        if 'Subsurface Scale' in bs.inputs:bs.inputs['Subsurface Scale'].default_value=.0025
        bs.inputs['Subsurface Radius'].default_value=(1,.45,.25)
    if name.startswith('Skin'):
        if 'IOR' in bs.inputs:bs.inputs['IOR'].default_value=1.42
        if 'Specular IOR Level' in bs.inputs:bs.inputs['Specular IOR Level'].default_value=.35
    def tex(image):
        node=tree.nodes.new('ShaderNodeTexImage');node.image=image;return node
    if base:
        node=tex(base);mix=tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*color,1)
        tree.links.new(node.outputs['Color'],mix.inputs[1]);tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
    if normal:
        node=tex(normal);n=tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35;tree.links.new(node.outputs['Color'],n.inputs['Color']);tree.links.new(n.outputs[0],bs.inputs['Normal'])
    if mask:
        node=tex(mask);inv=tree.nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1;tree.links.new(node.outputs['Alpha'],inv.inputs[1]);tree.links.new(inv.outputs[0],bs.inputs['Roughness'])
    return m

def weld(obj,distance=.000001):
    bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=distance)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free();obj.data.validate();obj.data.update()

def curve_mesh(name,points,radii,material,sides=5):
    pts=[Vector(p) for p in points];vertices=[];faces=[];last=None
    for i,p in enumerate(pts):
        tangent=pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]
        if tangent.length<1e-8:tangent=Vector((0,0,1))
        tangent.normalize();n=tangent.cross(Vector((0,1,0)))
        if n.length<.01:n=tangent.cross(Vector((1,0,0)))
        n.normalize()
        if last is not None and n.dot(last)<0:n=-n
        last=n;v=tangent.cross(n).normalized()
        for j in range(sides):vertices.append(p+(n*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))*radii[i])
    for i in range(len(pts)-1):
        for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;faces.append((a,b,b+sides,a+sides))
    faces.append(tuple(reversed(range(sides))));faces.append(tuple((len(pts)-1)*sides+j for j in range(sides)))
    obj=mesh(name,vertices,faces,material);planar_uv(obj,8);return obj

def combine(objects,name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join();obj=objects[0];obj.name=name;return obj

def checkpoint(label):
    bpy.ops.wm.save_as_mainfile(filepath=str(STAGES/f'{label}.blend'),compress=True)
    print('HOODED_STAGE',label,flush=True)

def surface_bvh(obj):
    return BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices) for p in obj.data.polygons])

def camera(name,position,target,lens=70):
    bpy.ops.object.camera_add(location=position);cam=bpy.context.object;cam.name=name;cam.data.lens=lens
    cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();return cam

def lighting():
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.threads_mode='FIXED';scene.render.threads=4;scene.cycles.max_bounces=6
    scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
    for n in scene.world.node_tree.nodes:
        if n.bl_idname=='ShaderNodeBackground':n.inputs['Color'].default_value=(.025,.028,.033,1);n.inputs['Strength'].default_value=.35
    for name,pos,power,color,size in [('Key',(-2,-3,3.2),260,(1,.9,.82),2),('Fill',(2,-2,2.1),150,(.80,.88,1),2),('Rim',(0,1,2.5),240,(1,.95,.87),1.4)]:
        d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=color;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,1.4))-o.location).to_track_quat('-Z','Y').to_euler()
    scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.resolution_percentage=100

def render(path,cam,width=768,height=960,samples=24):
    scene=bpy.context.scene;scene.camera=cam;scene.render.resolution_x=width;scene.render.resolution_y=height;scene.cycles.samples=samples;scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
