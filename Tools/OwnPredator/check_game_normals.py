import bpy,os,json,pathlib
from mathutils import Vector
root=pathlib.Path(os.environ['SILENCE_REPOSITORY'])
bpy.ops.wm.open_mainfile(filepath=str(root/'ArtSource/VesperGame/Vesper_Game.blend'),use_scripts=False)
scene=next(s for s in bpy.data.scenes if s.get('silence_rig'));checks=[]
for obj in scene.objects:
    if obj.type!='MESH' or not obj.name.startswith(('Skin_LOD','Tissue_LOD')):continue
    mesh=obj.data;parents=list(range(len(mesh.vertices)))
    def find(index):
        while parents[index]!=index:parents[index]=parents[parents[index]];index=parents[index]
        return index
    for edge in mesh.edges:
        a,b=map(find,edge.vertices);parents[b]=a
    components={}
    for vertex in mesh.vertices:components.setdefault(find(vertex.index),[]).append(vertex.index)
    for ids in components.values():
        points=[mesh.vertices[i].co for i in ids];lo=[min(p[k] for p in points) for k in range(3)];hi=[max(p[k] for p in points) for k in range(3)]
        if not (lo[1]<-2.2 and -1.25<hi[1]<-.95 and hi[0]-lo[0]>.9 and hi[2]-lo[2]>1):continue
        ids=set(ids);score=area=0;inside=obj.name.startswith('Tissue')
        for face in mesh.polygons:
            if face.vertices[0] not in ids:continue
            c=face.center;u=(c.y+(2.29 if inside else 2.22))/(1.12 if inside else 1.20)
            radial=Vector((c.x,0,c.z-(2.28+(.14 if inside else .23)*u)))
            if radial.length>.15:score+=face.normal.dot(radial.normalized())*face.area;area+=face.area
        score/=max(area,1e-8)
        checks.append({'mesh':obj.name,'check':'Mouth cavity faces inward' if inside else 'Outer cranium faces outward','radial_normal_score':score,'passed':score<-.5 if inside else score>.5})
report={'kind':'Actual mesh normal orientation; not Unity render approval','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
if len(checks)!=6:report['failed']+=1
destination=root/'Docs/Validation/VesperGame/normals.json';destination.write_text(json.dumps(report,indent=2)+'\n')
print('NORMAL_CHECKS',report['passed'],report['failed'])
if report['failed']:raise RuntimeError('Mouth winding regression')
