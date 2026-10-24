import math
import numpy as np
from common import *

def morph(obj,name,offset):
    if obj.data.shape_keys is None:obj.shape_key_add(name='Basis',from_mix=False)
    key=obj.shape_key_add(name=name,from_mix=False);key.value=0
    for v,p in zip(obj.data.vertices,key.data):p.co=v.co+Vector(offset(v.co))

def build(materials):
    rows=[];cols=56;head_bvh=surface_bvh(bpy.data.objects['Face'])
    for q in np.linspace(0,1,17):
        row=[]
        for a in np.linspace(0,math.tau,cols,endpoint=False):
            radius=math.cos(q*math.pi/2)
            x=.027*radius*math.cos(a);z=1.548+.029*radius*math.sin(a)
            surface,_,_,_=head_bvh.ray_cast(Vector((x,-.4,z)),Vector((0,1,0)),.6)
            front=surface.y+.007 if surface is not None and surface.y<-.09 else -.137
            row.append((x,front*(1-q)-.079*q,z))
        rows.append(row)
    verts=[p for row in rows for p in row];faces=[]
    for i in range(len(rows)-1):
        for j in range(cols):a=i*cols+j;b=i*cols+(j+1)%cols;faces.append((a,a+cols,b+cols,b))
    oral=mesh('Mouth_Interior',verts,faces,materials['Mouth']);weld(oral);planar_uv(oral,6);oral['rigid_bone']='Head'
    center=Vector((0,-.105,1.548))
    if sum(p.normal.dot(p.center-center) for p in oral.data.polygons)>0:
        for p in oral.data.polygons:p.flip()
    oral.data.update()
    teeth=[]
    for lower in [False,True]:
        row=[]
        for i in range(10):
            t=(i-4.5)/4.5;x=t*.022;y=-.135+.012*t*t
            width=.0043 if abs(t)<.7 else .0037;z=1.551 if lower else 1.562;h=.0058 if lower else .0065
            verts=[];faces=[]
            for zz,scale in [(z-h/2,.85),(z,.99),(z+h/2,.75)]:
                for a in np.linspace(0,math.tau,8,endpoint=False):verts.append((x+math.cos(a)*width*.5*scale,y+math.sin(a)*.0025*scale,zz))
            for r in range(2):
                for j in range(8):a=r*8+j;b=r*8+(j+1)%8;faces.append((a,b,b+8,a+8))
            faces.extend([tuple(reversed(range(8))),tuple(16+j for j in range(8))])
            tooth=mesh('Tooth',verts,faces,materials['Teeth']);recalc(tooth);planar_uv(tooth,10);row.append(tooth)
        obj=combine(row,'Teeth_Lower' if lower else 'Teeth_Upper');obj['rigid_bone']='Head'
        if lower:
            morph(obj,'jawOpen',lambda p:(0,.002,-.018));morph(obj,'jawForward',lambda p:(0,-.007,0));morph(obj,'jawLeft',lambda p:(.006,0,0));morph(obj,'jawRight',lambda p:(-.006,0,0))
        teeth.append(obj)
    rows=[]
    for q in np.linspace(0,1,16):
        row=[]
        for a in np.linspace(0,math.tau,20,endpoint=False):
            width=.0115*math.sin((q*.8+.1)*math.pi)
            row.append((width*math.cos(a),-.085-.040*q,1.548+.0035*math.sin(a)))
        rows.append(row)
    verts=[p for r in rows for p in r];faces=[]
    for i in range(len(rows)-1):
        for j in range(20):a=i*20+j;b=i*20+(j+1)%20;faces.append((a,b,b+20,a+20))
    faces.extend([tuple(reversed(range(20))),tuple((len(rows)-1)*20+j for j in range(20))])
    tongue=mesh('Tongue',verts,faces,materials['Mouth']);recalc(tongue);planar_uv(tongue,10);tongue['rigid_bone']='Head'
    morph(tongue,'tongueOut',lambda p:(0,-.032*max(0,min(1,(-p.y-.095)/.042)),.002))
    morph(tongue,'jawOpen',lambda p:(0,0,-.012))
    return [oral,tongue]+teeth
