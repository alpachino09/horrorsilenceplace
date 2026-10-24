"""Authored cloth surfaces, panels and footwear. Contains no weapon geometry."""
import math
import numpy as np
import bpy
from common import *

def grid(name,rows,material,wrap=False):
    count=len(rows[0]);faces=[]
    for i in range(len(rows)-1):
        for j in range(count if wrap else count-1):
            k=(j+1)%count;a=i*count+j;b=i*count+k;faces.append((a,b,b+count,a+count))
    obj=mesh(name,[v for row in rows for v in row],faces,material)
    uv=obj.data.uv_layers.new(name='UVMap')
    for p in obj.data.polygons:
        for li in p.loop_indices:
            index=obj.data.loops[li].vertex_index;uv.data[li].uv=((index%count)/(count-1),(index//count)/(len(rows)-1))
    recalc(obj,True);return obj

def hood(materials):
    rows=[];left=[];right=[]
    qnodes=[0,.20,.48,.70,.87,.92,.96,1]
    rxnodes=[.145,.145,.127,.119,.095,.078,.049,0]
    edgenodes=[.085,.112,.103,.068,.020,0,0,0]
    for q in np.linspace(0,1,34):
        rx=float(np.interp(q,qnodes,rxnodes));edge=float(np.interp(q,qnodes,edgenodes))
        alpha=math.asin(min(.98,edge/max(.001,rx)));extent=math.pi-alpha
        back=.125*math.sin(q*math.pi*.87)+.025
        front=float(np.interp(q,[0,.3,.7,.88,.94,.97,1],[-.13,-.147,-.148,-.15,-.13,-.09,.035]))
        row=[]
        for u in np.linspace(-1,1,48):
            theta=u*extent;fold=.004*(math.sin(theta*5+q*9)+.4*math.sin(theta*11-q*13))*math.sin(q*math.pi)
            x=(rx+fold)*math.sin(theta);y=back+(front-back)*(1-math.cos(theta))/max(.001,1-math.cos(extent))
            y+=fold*math.cos(theta);z=1.337+.413*q+.004*math.sin(theta*3+q*11)*math.sin(q*math.pi)
            row.append((x,y,z) if q<.999 else (0,.035,1.75))
        rows.append(row)
        if q<=.925:left.append(row[0]);right.append(row[-1])
    obj=grid('Hood',rows,materials['Hood']);obj.data.materials.append(materials['Scarf'])
    weld(obj);apply(obj,'SUBSURF',levels=1,render_levels=1)
    head_bvh=surface_bvh(bpy.data.objects['Face'])
    for vertex in obj.data.vertices:
        if vertex.co.z<1.50:continue
        nearest,normal,_,_=head_bvh.find_nearest(vertex.co)
        if nearest is not None and (vertex.co-nearest).dot(normal)<.010:vertex.co=nearest+normal*.010
    obj.data.update();apply(obj,'SOLIDIFY',thickness=.0016,offset=-1,material_offset=1)
    trims=[]
    for side,points in [('L',left),('R',right)]:
        trims.append(curve_mesh('Hood binding '+side,points,[.0015]*len(points),materials['Leather'],6))
        stitch=[Vector(p)+Vector((0,-.0014,0)) for p in points]
        trims.append(curve_mesh('Hood seam '+side,stitch,[.00035]*len(stitch),materials['Panel'],4))
    return obj,combine(trims,'Hood_Trim')

def scarf(materials):
    rows=[]
    for q in np.linspace(0,1,45):
        row=[]
        for theta in np.linspace(0,math.tau,56,endpoint=False):
            fold=.005*math.tanh(math.sin(q*math.pi*8+theta*1.55)*2)+.003*math.sin(theta*5-q*12)
            rx=.112-.043*q+fold;ry=.175-.080*q+fold
            z=1.36+q*.164+.004*math.sin(theta*2+q*10)
            z-=.019*q*q*max(0,-math.cos(theta))
            row.append((rx*math.sin(theta),.004+ry*math.cos(theta),z))
        rows.append(row)
    obj=grid('Neck_Wrap',rows,materials['Scarf'],True)
    apply(obj,'SOLIDIFY',thickness=.001,offset=-1);return obj

def hair(materials):
    size=2048;u,v=np.meshgrid(np.linspace(0,1,size,dtype=np.float32),np.linspace(0,1,size,dtype=np.float32))
    phase=u*math.pi*68+np.sin(v*19)*.34
    strand=.5+.5*np.sin(phase);edge=np.clip(np.minimum(u,1-u)*35,0,1)
    alpha=np.clip((strand-.12)*6,0,1)*edge*np.clip((1-v)*45,0,1)
    rgb=np.empty((size,size,4),np.float32);rgb[:,:,:3]=np.array((.052,.034,.023))+(strand*.012)[:,:,None];rgb[:,:,3]=alpha
    texture=image_file('Hair_BaseColorAlpha',rgb)
    mat=shader('Hair_Cards',(1,1,1),.53,base=texture);bs=next(n for n in mat.node_tree.nodes if n.bl_idname=='ShaderNodeBsdfPrincipled')
    if 'Specular IOR Level' in bs.inputs:bs.inputs['Specular IOR Level'].default_value=.18
    node=next(n for n in mat.node_tree.nodes if n.bl_idname=='ShaderNodeTexImage');mat.node_tree.links.new(node.outputs['Alpha'],bs.inputs['Alpha'])
    if 'Anisotropic IOR Level' in bs.inputs:bs.inputs['Anisotropic IOR Level'].default_value=.45
    if hasattr(mat,'surface_render_method'):mat.surface_render_method='DITHERED'
    rows=[];objects=[];rng=np.random.default_rng(4317)
    for side in [-1,1]:
        for k in range(30):
            back=k/29;length=.30+.035*rng.random();width=.014+.006*rng.random()
            startx=side*(.020+.058*back);endx=side*(.071+.024*back)
            rows=[]
            for t in np.linspace(0,1,30):
                z=1.718-length*t
                x=startx*(1-t)**2+endx*(1-(1-t)**2)+side*.002*math.sin(t*5+k)
                y=-.114+.073*back+.012*math.sin(t*math.pi)-.008*t+.025*(1-t)**4
                row=[]
                for s in np.linspace(-.5,.5,4):
                    taper=(.15+.85*min(1,t/.13))*(1-.85*t**4)
                    row.append((x+s*width*taper,y+.0005*math.cos(s*math.tau)+.001*math.sin(t*11+k),z-.007*abs(s)*t))
                rows.append(row)
            obj=grid('Hair card',rows,mat);obj['rigid_bone']='Head';objects.append(obj)
        for k in range(9):
            x=side*(.060+.003*k);points=[]
            for t in np.linspace(0,1,18):points.append((x+side*.007*math.sin(t*3),-.128+.001*k+.003*math.sin(t*8)+.04*(1-t)**4,1.684-.28*t))
            objects.append(curve_mesh('Fine hair',points,[.00012*(1-.8*i/17) for i in range(18)],materials['Hair'],4))
    obj=combine(objects,'Hair');obj['rigid_bone']='Head';return obj

def hand_weight(human,index):
    return sum(w for name,w in human.weights[index].items() if name.startswith(('finger','wrist','metacarpal')))

def chest_envelope(obj):
    # A textile envelope bridges anatomical skin detail beneath the fitted jacket.
    for vertex in obj.data.vertices:
        p=vertex.co;x=p.x;z=p.z
        front=max(0,min(1,(-p.y-.05)/.04))
        weight=math.exp(-((z-1.285)/.075)**4)*max(0,1-(abs(x)/.17)**8)*front
        target=-.112-.078*math.exp(-((z-1.29)/.058)**2)*math.exp(-(abs(x)/.14)**4)
        p.y=p.y*(1-.96*weight)+target*.96*weight
    obj.data.update()
    obj['envelope_refined']=True

def cloth_from_body(human,name,predicate,material,kind):
    obj,ids=human.part(name,predicate,material);obj.data.update()
    if kind=='shirt':
        group=obj.vertex_groups.new(name='Cloth chest smoothing')
        for v in obj.data.vertices:
            amount=math.exp(-((v.co.z-1.275)/.085)**2)*max(0,min(1,(-v.co.y-.04)/.08))
            if amount>.01:group.add([v.index],amount,'REPLACE')
        apply(obj,'SMOOTH',factor=.8,iterations=14,vertex_group=group.name)
    for vertex,index in zip(obj.data.vertices,ids):
        p=vertex.co;normal=vertex.normal.copy();z=p.z;x=p.x
        if kind=='shirt':
            offset=.0055+.004*math.exp(-((z-1.04)/.07)**2)
            offset+=.0032*math.sin(z*105+x*36)*math.exp(-((z-1.16)/.15)**2)*(abs(x)>.13)
            offset+=.0025*math.sin(z*102+x*20)*math.exp(-((z-1.01)/.055)**2)
        elif kind=='pants':
            offset=.014+.012*math.exp(-((z-.73)/.18)**2)
            offset+=.008*math.sin(z*100+x*29)*math.exp(-((z-.49)/.075)**2)
            offset+=.004*math.sin(z*160+x*15)*math.exp(-((z-.19)/.05)**2)
            offset+=.003*math.sin(z*93+x*27)*math.exp(-((z-.89)/.06)**2)
        else:offset=.0022
        vertex.co+=normal*offset
    if kind=='shirt':chest_envelope(obj)
    apply(obj,'SUBSURF',levels=1,render_levels=1)
    # World-oriented UV keeps all new cloth surfaces at a consistent texel scale.
    planar_uv(obj,2.3);return obj

def seam_on(bvh,name,points,material,radius=.00065):
    projected=[]
    for p in points:
        direction=Vector((0,1,0)) if p[1]<-.3 else Vector((-1 if p[0]>0 else 1,0,0)) if abs(p[0])>.3 else None
        nearest,normal,_,_=bvh.ray_cast(Vector(p),direction,1) if direction is not None else bvh.find_nearest(Vector(p))
        if nearest is None:nearest,normal,_,_=bvh.find_nearest(Vector(p))
        if nearest is not None:projected.append(nearest+normal*.001)
    return curve_mesh(name,projected,[radius]*len(projected),material,4)

def patch(bvh,name,center,uaxis,width,height,material,bulge=.008):
    center=Vector(center);center,normal,_,_=bvh.find_nearest(center)
    uaxis=Vector(uaxis);uaxis=(uaxis-normal*uaxis.dot(normal)).normalized();rows=[]
    for v in np.linspace(-1,1,11):
        row=[]
        for u in np.linspace(-1,1,11):
            # Rounded rectangle surface follows the actual garment underneath it.
            rounding=1-.10*abs(v)**6;point=center+uaxis*(u*width*.5*rounding)+Vector((0,0,v*height*.5))
            p,n,_,_=bvh.ray_cast(point+normal*.20,-normal,.40)
            if p is None:p,n,_,_=bvh.find_nearest(point)
            lift=.0015+bulge*math.sin((u+1)*math.pi/2)*math.sin((v+1)*math.pi/2)
            row.append(p+n*lift)
        rows.append(row)
    obj=grid(name,rows,material);apply(obj,'SOLIDIFY',thickness=.001,offset=-1)
    boundary=rows[0]+[r[-1] for r in rows[1:]]+list(reversed(rows[-1][:-1]))+[r[0] for r in list(reversed(rows))[1:-1]]
    boundary.append(boundary[0]);stitch=curve_mesh(name+' seam',boundary,[.00035]*len(boundary),bpy.data.materials['Fabric_Olive'],4)
    return combine([obj,stitch],name)

def garments(human,materials):
    torso=lambda f:all(hand_weight(human,i)<.65 for i in f) and .965<np.mean(human.co[f,2])<1.49
    pants=lambda f:.15<np.mean(human.co[f,2])<1.006 and max(abs(human.co[i,0]) for i in f)<.29
    glove=lambda f:np.mean([hand_weight(human,i) for i in f])>.25 and np.mean([sum(w for n,w in human.weights[i].items() if n.startswith('finger') and ('-2.' in n or '-3.' in n or '-4.' in n)) for i in f])<.45
    shirt=cloth_from_body(human,'Jacket',torso,materials['Fabric'],'shirt')
    trousers=cloth_from_body(human,'Cargo_Trousers',pants,materials['Fabric'],'pants')
    gloves=cloth_from_body(human,'Fingerless_Gloves',glove,materials['Leather'],'gloves')
    hands,ids=human.part('Hands',lambda f:any(hand_weight(human,i)>.3 for i in f),materials['Hands'])
    apply(hands,'SUBSURF',levels=1,render_levels=1)
    pbvh=surface_bvh(trousers);sbvh=surface_bvh(shirt);detail=[];seams=[]
    for side in [-1,1]:
        for x in [.065,.12]:seams.append(seam_on(sbvh,'Jacket panel seam',[(side*x,-.5,z) for z in np.linspace(1.00,1.37,34)],materials['Panel'],.00055))
        seams.append(seam_on(pbvh,'Outside leg seam',[(side*.33,.01,z) for z in np.linspace(.19,.97,58)],materials['Panel']))
        seams.append(seam_on(pbvh,'Inside leg seam',[(side*.04,.04,z) for z in np.linspace(.2,.80,50)],materials['Panel'],.00045))
        detail.append(patch(pbvh,'Cargo Pocket', (side*.19,-.020,.76),(side*.55,.84,0),.135,.17,materials['Panel'],.016))
        detail.append(patch(pbvh,'Cargo Flap',(side*.19,-.020,.832),(side*.55,.84,0),.145,.045,materials['Leather'],.007))
        detail.append(patch(pbvh,'Knee Reinforcement',(side*.151,-.3,.51),(1,0,0),.09,.13,materials['Panel'],.002))
        detail.append(patch(pbvh,'Thigh Panel',(side*.16,-.24,.86),(1,0,0),.10,.14,materials['Panel'],.002))
        ankle=human.joint('foot.L' if side==1 else 'foot.R');cuff_rows=[]
        for z in [.163,.190]:
            ring=[]
            for a in np.linspace(0,math.tau,48,endpoint=False):
                center=Vector((ankle[0],ankle[1],z));direction=Vector((math.sin(a),math.cos(a),0))
                p,n,_,_=pbvh.ray_cast(center+direction*.12,-direction,.24)
                if p is None:p,n,_,_=pbvh.find_nearest(center+direction*.04)
                ring.append(p+n*(.0015+.0005*math.sin(a*12)))
            cuff_rows.append(ring)
        detail.append(grid('Trouser cuff',cuff_rows,materials['Panel'],True))
    rows=[]
    for z in [.987,1.020]:
        row=[]
        for a in np.linspace(0,math.tau,96,endpoint=False):
            seed=Vector((math.sin(a)*.4,math.cos(a)*.4,z));p,n,_,_=sbvh.ray_cast(seed,Vector((-math.sin(a),-math.cos(a),0)),.8)
            if p is None:p,n,_,_=pbvh.find_nearest(seed);p.z=z
            row.append(p+n*.002)
        rows.append(row)
    belt=grid('Waist_Belt',rows,materials['Leather'],True);apply(belt,'SOLIDIFY',thickness=.002,offset=0)
    front=Vector((0,-.4,1.018));p,n,_,_=pbvh.find_nearest(front);by=p.y-.006
    points=[(-.020,by,.999),(.020,by,.999),(.020,by,1.034),(-.020,by,1.034),(-.020,by,.999)]
    detail.append(curve_mesh('Belt buckle',points,[.0018]*len(points),materials['Metal'],6))
    cords=[]
    for i in range(3):
        z=1.354+i*.012;cords.append(seam_on(sbvh,'Neck lacing',[(-.017,-.4,z),(.017,-.4,z+.012)],materials['Leather'],.0009))
        cords.append(seam_on(sbvh,'Neck lacing',[(.017,-.4,z),(-.017,-.4,z+.012)],materials['Leather'],.0009))
    detail.append(combine(cords,'Neck_Lacing'))
    return [shirt,trousers,gloves,hands,belt,combine(detail,'Clothing_Panels'),combine(seams,'Garment_Seams')]

def shoes(human,materials):
    objects=[]
    for side in ['L','R']:
        ankle=human.joint('foot.'+side);cx=ankle[0]
        ynodes=[.055,.035,-.005,-.055,-.115,-.175,-.210,-.226]
        widths=[.014,.031,.044,.047,.045,.037,.023,0]
        heights=[.11,.16,.175,.127,.095,.076,.060,.042]
        rows=[]
        for y,w,h in zip(ynodes,widths,heights):
            row=[]
            for theta in np.linspace(0,math.tau,48,endpoint=False):
                x=cx+w*math.sin(theta);z=.04+(h-.04)*(.5+.5*math.cos(theta))
                row.append((x,y,z))
            rows.append(row)
        obj=grid('Shoe Upper '+side,rows,materials['Leather'],True);weld(obj);apply(obj,'SUBSURF',levels=1,render_levels=1)
        obj['rigid_bone']='LeftFoot' if side=='L' else 'RightFoot';objects.append(obj)
        outline=[]
        for sign,ys,ws in [(1,ynodes,widths),(-1,list(reversed(ynodes)),list(reversed(widths)))]:
            outline.extend([(cx+sign*(w+.003),y,0) for y,w in zip(ys,ws)])
        smooth=[];count=len(outline)
        for i in range(count):
            p0=Vector(outline[(i-1)%count]);p1=Vector(outline[i]);p2=Vector(outline[(i+1)%count]);p3=Vector(outline[(i+2)%count])
            for t in np.linspace(0,1,7,endpoint=False):smooth.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
        rings=[[Vector((p.x,p.y,z)) for p in smooth] for z in [0,.007,.029]]
        sole=grid('Rubber sole '+side,rings,materials['Sole'],True)
        bm=bmesh.new();bm.from_mesh(sole.data);bmesh.ops.holes_fill(bm,edges=[e for e in bm.edges if e.is_boundary],sides=0);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(sole.data);bm.free();planar_uv(sole,3)
        sole['rigid_bone']=obj['rigid_bone'];objects.append(sole)
        for i,y in enumerate(np.linspace(-.100,.012,7)):
            z=.108+i*.009;points=[(cx-.026,y,z),(cx+.026,y+.008,z+.006)]
            lace=curve_mesh('Shoe lace '+side,points,[.0011,.0011],materials['Scarf'],5);lace['rigid_bone']=obj['rigid_bone'];objects.append(lace)
            for sign in [-1,1]:
                center=Vector((cx+sign*.028,y,z));ring=[center+Vector((math.cos(a)*.0027,0,math.sin(a)*.0027)) for a in np.linspace(0,math.tau,13)]
                eyelet=curve_mesh('Eyelet '+side,ring,[.0005]*len(ring),materials['Metal'],4);eyelet['rigid_bone']=obj['rigid_bone'];objects.append(eyelet)
        # Contrasting side panels are raised surfaces rather than a painted silhouette.
        shoe_bvh=surface_bvh(obj)
        for sign in [-1,1]:
            panel=patch(shoe_bvh,'Shoe panel '+side,(cx+sign*.06,-.025,.08),(0,1,0),.10,.058,materials['Sole'],.001)
            panel['rigid_bone']=obj['rigid_bone'];objects.append(panel)
    # Keep per-foot bones while merging draw calls within each side later during rigging.
    return objects

def build(human,materials):
    hood_obj,trim=hood(materials);wrap=scarf(materials);hair_obj=hair(materials)
    checkpoint('05_hood_hair_neckwrap')
    clothes=garments(human,materials);footwear=shoes(human,materials)
    checkpoint('06_clothing_gloves_footwear')
    for obj in [hood_obj,trim,wrap,hair_obj]+clothes+footwear:
        if not obj.data.uv_layers:planar_uv(obj,3)
        for p in obj.data.polygons:p.use_smooth=True
    checkpoint('07_geometry');checkpoint('08_uv_pbr')
    return [hood_obj,trim,wrap,hair_obj]+clothes+footwear
