"""Blender-side mesh checks. Does not assert Unity collision or navigation behaviour."""
import bpy,json,pathlib
from mathutils import Vector
from mathutils.bvhtree import BVHTree
ROOT=pathlib.Path(__file__).resolve().parents[2];out=ROOT/'Docs/Validation/VisualRebuild/geometry.json';checks=[]
def check(value,name):checks.append({'name':name,'passed':bool(value)})
def V(x,y,z):return Vector((x,-z,y))
for lod in range(3):
 obj=bpy.data.objects[f'Farmhouse_LOD{lod}'];mesh=obj.data
 polygons=[list(face.vertices) for face in mesh.polygons if obj.data.materials[face.material_index].name!='Glass']
 tree=BVHTree.FromPolygons([obj.matrix_world@v.co for v in mesh.vertices],polygons)
 def hit(x,y):return tree.ray_cast(V(x,y,-4.8),V(0,0,1),1.6)[0]
 check(hit(0,1.2) is None,f'House LOD{lod}: actual door opening is clear')
 check(hit(-2.3,1.3) is None and hit(2.8,1.3) is None,f'House LOD{lod}: window apertures exist behind glass')
 check(hit(-3.65,1.2) is not None,f'House LOD{lod}: wall exists outside apertures')
manifest=json.loads((ROOT/'Assets/_Silence/Art/VisualRebuild/model-manifest.json').read_text())['models'];byname={m['name']:m for m in manifest}
for group in ['Farmhouse','Barn','Pine_31','Pine_47','Pine_83']:
 sizes=[byname[f'{group}_LOD{i}']['triangles'] for i in range(3)];check(sizes[0]>sizes[1]>sizes[2],group+' has strictly decreasing mesh LOD triangle counts')
check(len(manifest)==28,'All 28 authored meshes exported')
report={'kind':'Blender 4.3.2 mesh ray and topology checks, not Unity physics','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
out.write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
if report['failed']:raise RuntimeError('Geometry checks failed')
