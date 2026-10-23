"""Validate delivered technical art data; does not render Unity or guarantee art quality."""
from pathlib import Path
import hashlib,json,struct
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];ART=ROOT/'Assets/_Silence/Art/VisualRebuild';checks=[]
def check(value,name):checks.append({'name':name,'passed':bool(value)})
manifest=json.loads((ART/'asset-manifest.json').read_text())
for name,record in manifest['files'].items():
 p=ART/'Textures'/name;check(p.exists() and hashlib.sha256(p.read_bytes()).hexdigest()==record['sha256'],'Delivered data hash '+name)
for name in manifest['materials']:
 base=Image.open(ART/'Textures'/f'{name}_BaseColor.png');normal=np.asarray(Image.open(ART/'Textures'/f'{name}_Normal.png')).astype(np.float32)/255*2-1
 mask=Image.open(ART/'Textures'/f'{name}_Mask.png')
 check(base.size==(1024,1024) and mask.mode=='RGBA','PBR resolution and packed channels '+name)
 check(np.max(np.abs(np.linalg.norm(normal,axis=-1)-1))<.025,'Unit tangent normals '+name)
raw=(ART/'Textures/FarmTerrain.bytes').read_bytes();magic,n,seed=struct.unpack_from('<4sii',raw)
h=np.frombuffer(raw,dtype='<f4',count=n*n,offset=12).reshape(n,n)*10-3
weights=np.frombuffer(raw,dtype='<f4',offset=12+n*n*4).reshape(n,n,4)
check(magic==b'SVR2' and n==129 and seed==27092026,'Terrain format, size, seed')
check(np.isfinite(h).all() and h.min()>-.1 and h.max()<5,'Finite bounded terrain heights')
check(np.allclose(weights.sum(axis=-1),1,atol=1e-6) and np.min(weights)>=0,'Terrain weights normalized and nonnegative')
check(np.max(np.abs(h[60:69,50:59]))<1e-5,'Existing farmhouse footprint remains flat')
check(np.max(np.abs(h[65:76,69:78]))<1e-5,'Existing barn footprint remains flat')
check(np.max(np.abs(h[53,62:67]))<.04,'Existing player spawns remain at the expected ground height')
# Alpha cards need genuine transparent silhouettes and opaque foliage pixels.
for name in ['PineNeedles','GrassAtlas','FernAtlas']:
 alpha=np.asarray(Image.open(ART/'Textures'/f'{name}.png'))[:,:,3]
 check((alpha==0).mean()>.45 and (alpha>200).mean()>.01,'Real cutout coverage '+name)
models=json.loads((ART/'model-manifest.json').read_text())['models']
check(len(models)==28 and all((ART/'Models'/m['file']).read_bytes().startswith(b'Kaydara FBX Binary') for m in models),'28 real binary FBX files')
report={'kind':'Texture, mask, terrain and FBX file checks only','checks':checks,'passed':sum(c['passed'] for c in checks),'failed':sum(not c['passed'] for c in checks)}
(ROOT/'Docs/Validation/VisualRebuild/assets.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({k:v for k,v in report.items() if k!='checks'},indent=2))
for c in checks:
 if not c['passed']:print('FAIL',c['name'])
raise SystemExit(bool(report['failed']))
