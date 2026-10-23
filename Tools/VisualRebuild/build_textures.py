"""Original, deterministic technical PBR maps and shared terrain data, not photographic scans."""
from pathlib import Path
import hashlib, json, math, struct
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy.ndimage import gaussian_filter

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/_Silence/Art/VisualRebuild/Textures'
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 1024
rng = np.random.default_rng(27092026)
y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32) / SIZE

def noise(sigma):
    field = gaussian_filter(rng.random((SIZE, SIZE)).astype(np.float32), sigma, mode='wrap')
    return (field-field.min()) / max(float(np.ptp(field)), 1e-6)

fine, medium, broad = noise(.7), noise(7), noise(55)

def save(name, color, height, roughness, metallic=0):
    color = np.clip(color, .015, .92)
    height = np.asarray(height, dtype=np.float32)
    gx = (np.roll(height, -1, 1)-np.roll(height, 1, 1)) * 12
    gy = (np.roll(height, -1, 0)-np.roll(height, 1, 0)) * 12
    normal = np.stack([-gx, -gy, np.ones_like(gx)], -1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    ao = np.clip(.73 + .27 * height + .12 * (height-gaussian_filter(height, 4, mode='wrap')), .45, 1)
    shape = height.shape
    metal = np.broadcast_to(metallic, shape)
    smooth = 1-np.broadcast_to(roughness, shape)
    mask = np.stack([metal, ao, np.clip(height, 0, 1), np.clip(smooth, .02, .85)], -1)
    for suffix, values in [('BaseColor', color), ('Normal', normal*.5+.5), ('Mask', mask)]:
        Image.fromarray(np.uint8(np.clip(values, 0, 1)*255)).save(OUT/f'{name}_{suffix}.png', optimize=True)

warp = noise(25)
grain = .5 + .5*np.sin((x*92 + warp*1.3 + np.sin(y*math.tau*4)*.12)*math.tau)
cracks = np.power(.5+.5*np.sin((x*31+warp*.3)*math.tau), 34)
wood_h = .42 + .08*grain + .035*fine - .09*cracks
wood_c = np.stack([.35+.07*grain, .31+.06*grain, .25+.05*grain], -1)
wood_c *= (.65+.35*broad)[...,None] * (1-.46*cracks)[...,None]
save('AgedWood', wood_c, wood_h, .70+.22*medium)
# Bark has interrupted longitudinal fissures, rather than uniformly stretched grain.
bark_h = np.clip(.35 + .48*grain + .12*medium - .35*cracks, 0, 1)
save('Bark', np.array([.22,.19,.15]) * (.55+.75*bark_h)[...,None], bark_h, .92)
plaster_h = .2 + .4*fine + .2*medium
plaster_c = np.array([.52,.49,.42])*(.72+.28*broad)[...,None]
plaster_c *= (1-.25*np.power(np.clip(1-medium,0,1),3))[...,None]
save('Plaster', plaster_c, plaster_h, .87)
# Real mortar geometry in the material, with staggered courses and irregular brick faces.
row = np.floor(y*14)
bx = (x*7 + (row%2)*.5)%1
by = (y*14)%1
mortar = (bx<.055)|(by<.09)
brick_h = np.where(mortar, .12, .58 + medium*.18 + fine*.08)
brick_c = np.array([.34,.17,.10]) * (.65+.55*medium)[...,None]
brick_c[mortar] = np.array([.25,.24,.21])
save('Brick', brick_c, brick_h, .86)
rust = np.clip((broad-.32)*3,0,1)
metal_c = np.array([.22,.24,.24])[None,None,:]*(1-rust[...,None]) + np.array([.33,.16,.065])*rust[...,None]
save('Rust', metal_c*(.7+.3*fine)[...,None], .2+.5*medium+.2*fine, .35+.5*rust, (1-rust)*.8)
roof_h = .3 + .3*medium + .15*fine + .1*np.sin(x*math.tau*18)
save('Roof', np.array([.19,.21,.21])*(.65+.5*medium)[...,None], roof_h, .78)
stone_h = .2+.4*medium+.15*broad+.15*fine
save('Stone', np.array([.34,.35,.32])*(.6+.6*medium)[...,None], stone_h, .9)
for name, base, wet in [('Forest', [.19,.16,.11], False),('Grass', [.25,.29,.14],False),('Mud',[.19,.16,.12],True),('Gravel',[.33,.32,.29],False)]:
    grains = noise(1 if name=='Gravel' else 2.5)
    h = np.clip(.2 + .2*broad + .3*medium + .3*grains,0,1)
    if name=='Forest':
        twigs=(np.sin((x*33+y*7)*math.tau+warp*7)>.97)&(medium>.45)
        h[twigs] += .1
    if name=='Grass':
        blades=np.power(.5+.5*np.sin((x*240+y*28+warp*2)*math.tau),8)
        h=np.clip(h+blades*.15,0,1)
    puddle=np.clip((.42-broad)*5,0,1) if wet else np.zeros_like(h)
    color=np.array(base)[None,None,:]*(.60+.7*h)[...,None] * (1-puddle*.25)[...,None]
    save(name,color,h,np.clip(.84-puddle*.6+(1-medium)*.12,.1,.98))
# Needle and grass cards have an explicit alpha silhouette. They are not opaque triangles/cones.
for name, grass in [('PineNeedles',False), ('GrassAtlas',True), ('FernAtlas',True)]:
    image=Image.new('RGBA',(512,512),(25,36,19,0)); draw=ImageDraw.Draw(image)
    local=np.random.default_rng(91 if name=='PineNeedles' else 712 if name=='FernAtlas' else 328)
    if name=='PineNeedles':
        draw.line((256,500,256,35),fill=(64,51,30,255),width=6)
        for i in range(52):
            cy=475-i*8; extent=(1-abs(cy-265)/270)*150+15
            for side in [-1,1]:
                tx=256+side*extent*local.uniform(.65,1); ty=cy-local.uniform(20,70)
                draw.line((256,cy,tx,ty),fill=(int(local.integers(42,80)),int(local.integers(65,100)),34,255),width=3)
    elif name=='FernAtlas':
        draw.line((256,505,256,20), fill=(44,70,23,255),width=5)
        for i in range(23):
            cy=480-i*19; extent=(math.sin((cy/520)*math.pi))**.65*190
            for side in [-1,1]:
                end=(256+side*extent,cy-25)
                draw.line((256,cy,*end),fill=(55,82,30,255),width=3)
                for j in range(9):
                    t=(j+1)/11; px=256+(end[0]-256)*t;py=cy-25*t
                    draw.polygon([(px,py+3),(px+side*20,py-12),(px+side*5,py-17),(px-side*2,py-4)], fill=(50+i%5*5,75+i%4*5,29,255))
    else:
        for i in range(45):
            bx=int(local.integers(80,430)); by=510
            tip=(int(bx+local.uniform(-100,100)),int(local.integers(35,240)))
            draw.polygon([(bx-4,by),(bx+5,by),(tip[0]+3,tip[1]+20),tip,(tip[0]-4,tip[1]+25)],fill=(int(local.integers(52,100)),int(local.integers(69,104)),36,255))
    image.save(OUT/f'{name}.png',optimize=True)
# Soft cookie, technical intensity map. It changes no gameplay flashlight state.
v=np.linspace(-1,1,256); yy,xx=np.meshgrid(v,v,indexing='ij');rad=np.sqrt(xx*xx+yy*yy)
cone=np.clip((1-rad)/.25,0,1)**1.8*(.7+.3*np.exp(-rad*rad*10))
Image.fromarray(np.uint8(cone*255)).save(OUT/'FlashlightCookie.png')

# Terrain: an authored bowl around an existing yard. Exact float samples are shared by Blender and Unity.
N=129
coords=np.linspace(-64,64,N); zz,xx=np.meshgrid(coords,coords,indexing='ij')
def smooth(a,b,v):
 t=np.clip((v-a)/(b-a),0,1);return t*t*(3-2*t)
def rect_dist(cx,cz,hx,hz): return np.maximum(np.abs(xx-cx)-hx,np.abs(zz-cz)-hz)
def segment(ax,az,bx,bz):
 dx=bx-ax; dz=bz-az;t=np.clip(((xx-ax)*dx+(zz-az)*dz)/(dx*dx+dz*dz),0,1)
 return np.sqrt((xx-ax-t*dx)**2+(zz-az-t*dz)**2)
road=np.minimum.reduce([segment(0,-22,0,23),segment(0,-4,-11,-4),segment(0,2,9,1)])
yard=smooth(17,38,np.sqrt(xx*xx+zz*zz))
hills=2.1+1.0*np.sin(xx*.055+np.sin(zz*.04))+.75*np.cos(zz*.065)
heights=yard*hills + (1-yard)*(.07*np.sin(xx*.35)*np.cos(zz*.22))
rut=(np.exp(-((np.abs(xx)-.68)/.17)**2)*.035)*np.exp(-np.maximum(np.abs(zz)-22,0))
heights-=rut*(1-smooth(1,2.5,road))
foundation=np.minimum(rect_dist(-10,0,4.8,4.8),rect_dist(9,6,4.8,5.8))
heights*=smooth(0,2,foundation)
heights*=smooth(0,.55,np.abs(zz+11))*.15+.85 # no abrupt high relief at spawn
height=(heights+3)/10
forest=smooth(16,27,np.sqrt(xx*xx+zz*zz))*(.8+.2*np.sin(xx*.23)*np.cos(zz*.2))
gravel=(1-smooth(1,2.1,road))*.9
mud=np.clip((1-smooth(.8,2.7,road))*.35 + np.exp(-((xx+4)**2+(zz-8)**2)/65)*.4,0,.7)*(1-gravel)
grass=np.maximum(.02,1-forest-gravel-mud)
weights=np.stack([forest,grass,mud,gravel],-1);weights/=weights.sum(-1,keepdims=True)
path=OUT/'FarmTerrain.bytes'
with path.open('wb') as f:
 f.write(struct.pack('<4sii',b'SVR2',N,27092026));f.write(np.asarray(height,dtype='<f4').tobytes());f.write(np.asarray(weights,dtype='<f4').tobytes())
Image.fromarray(np.uint16(np.clip(height,0,1)*65535)).save(OUT/'FarmHeightPreview.png')
manifest={'generator':'Tools/VisualRebuild/build_textures.py','seed':27092026,'resolution':SIZE,'provenance':'Original procedural artwork created for SILENCE; no external scans/models included.','materials':['AgedWood','Bark','Plaster','Brick','Rust','Roof','Stone','Forest','Grass','Mud','Gravel'],'terrain':{'resolution':N,'world_size':[128,10,128],'position':[-64,-3,-64],'layer_order':['Forest','Grass','Mud','Gravel']},'files':{p.name:{'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(OUT.iterdir()) if p.suffix in ['.png','.bytes']}}
(ROOT/'Assets/_Silence/Art/VisualRebuild/asset-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print('Created',len(manifest['files']),'technical images/data; terrain min/max',float(heights.min()),float(heights.max()))
