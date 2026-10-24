import math
import numpy as np
from common import image_file,shader

def normals(height,world_span):
    dx=(np.roll(height,-1,1)-np.roll(height,1,1))*height.shape[1]/(2*world_span)
    dy=(np.roll(height,-1,0)-np.roll(height,1,0))*height.shape[0]/(2*world_span)
    n=np.stack((-dx,-dy,np.ones_like(height)),axis=-1);n/=np.linalg.norm(n,axis=2,keepdims=True)
    return n*.5+.5

def skin_maps(size=4096):
    u,v=np.meshgrid(np.linspace(0,1,size,dtype=np.float32),np.linspace(0,1,size,dtype=np.float32))
    z=1.475+v*.27;x=(u-.5)*.225;front=(u<.93).astype(np.float32)
    def spot(cx,cz,rx,rz):return np.exp(-((x-cx)/rx)**2-((z-cz)/rz)**2)*front
    base=np.empty((size,size,3),np.float32);base[:]=(.66,.485,.415)
    broad=(np.sin(u*71+np.sin(v*15))*np.sin(v*91+np.cos(u*30)))*.003
    base+=broad[:,:,None]
    cheek=spot(-.044,1.565,.026,.025)+spot(.044,1.565,.026,.025)
    base+=cheek[:,:,None]*np.array((.035,-.016,-.013),np.float32)
    nose=spot(0,1.579,.016,.021);base+=nose[:,:,None]*np.array((.025,-.012,-.009),np.float32)
    eye_shadow=spot(-.031,1.612,.025,.014)+spot(.031,1.612,.025,.014)
    base+=eye_shadow[:,:,None]*np.array((-.025,-.024,-.023),np.float32)
    lip_arc=np.sqrt(np.maximum(0,1-(x/.026)**2))
    top=1.556+.009*lip_arc-.0025*np.exp(-(x/.006)**2)
    bottom=1.556-.008*lip_arc
    lip=np.clip((top-z)/.0007,0,1)*np.clip((z-bottom)/.0007,0,1)*(np.abs(x)<.026)*front
    lip_color=np.array((.40,.185,.175),np.float32)
    base=base*(1-lip[:,:,None])+lip_color*lip[:,:,None]
    # Fine pores and subtle freckles; large anatomical form remains in the mesh.
    gu=u*660;gv=v*660;cellx=np.floor(gu);celly=np.floor(gv)
    jitter=np.mod(np.sin(cellx*127.1+celly*311.7)*43758.5453,1)
    px=gu-cellx-.22-.56*jitter;py=gv-celly-.22-.56*np.mod(jitter*7.31,1)
    height=-.000010*np.exp(-(px*px+py*py)/(.08+.02*jitter))
    height+=lip*.000004*np.sin(u*math.tau*1400+np.sin(v*180)*2)
    freckles=np.exp(-(px*px+py*py)/.01)*(jitter>.988)*(cheek+spot(0,1.586,.03,.008))
    base-=freckles[:,:,None]*np.array((.04,.035,.03),np.float32)
    for cx,cz,r in [(.039,1.553,.00065),(-.022,1.681,.00045),(-.048,1.580,.00035)]:
        mole=spot(cx,cz,r,r);base-=mole[:,:,None]*np.array((.28,.24,.21),np.float32)
    rough=.58-.09*lip-.035*nose+.01*broad
    mask=np.ones((size,size,4),np.float32);mask[:,:,0]=0;mask[:,:,1]=1;mask[:,:,2]=0;mask[:,:,3]=1-rough
    maps=(image_file('Skin_BaseColor',base),image_file('Skin_Normal',normals(height,.225),True),image_file('Skin_Mask',mask,True))
    return maps

def cloth_maps(size=2048):
    u,v=np.meshgrid(np.linspace(0,1,size,dtype=np.float32),np.linspace(0,1,size,dtype=np.float32))
    warp=np.sin(u*math.tau*430);weft=np.sin(v*math.tau*380)
    twill=np.sin((u*430+v*380)*math.tau*.5)
    thread=.55*warp+.35*weft+.1*twill
    age=np.sin(u*41+np.sin(v*27))*np.sin(v*53)*.008
    base=np.empty((size,size,3),np.float32);base[:]=(.22,.205,.17);base+=thread[:,:,None]*.005+age[:,:,None]*.4
    height=thread*.000014;mask=np.ones((size,size,4),np.float32);mask[:,:,0]=0;mask[:,:,2]=0;mask[:,:,3]=.22+thread*.015
    return image_file('Fabric_BaseColor',base),image_file('Fabric_Normal',normals(height,.45),True),image_file('Fabric_Mask',mask,True)

def leather_maps(size=2048):
    u,v=np.meshgrid(np.linspace(0,1,size,dtype=np.float32),np.linspace(0,1,size,dtype=np.float32))
    grain=np.sin(u*850+np.sin(v*740)*1.4)*np.sin(v*790+np.cos(u*500))
    base=np.empty((size,size,3),np.float32);base[:]=(.10,.09,.075);base+=grain[:,:,None]*.004
    mask=np.ones((size,size,4),np.float32);mask[:,:,0]=0;mask[:,:,2]=0;mask[:,:,3]=.43+grain*.025
    return image_file('Leather_BaseColor',base),image_file('Leather_Normal',normals(grain*.000018,.35),True),image_file('Leather_Mask',mask,True)

def iris_map(size=2048):
    u,v=np.meshgrid(np.linspace(0,1,size,dtype=np.float32),np.linspace(0,1,size,dtype=np.float32));x=u-.5;y=v-.5;r=np.sqrt(x*x+y*y);a=np.arctan2(y,x)
    rgba=np.ones((size,size,3),np.float32);rgba[:]=(.68,.655,.61)
    fibers=np.sin(a*197+np.sin(a*41)*2+r*145)*.028+np.sin(a*311-r*60)*.018
    iris=np.stack((.22+fibers,.285+fibers,.25+fibers),axis=-1)
    iris+=np.exp(-((r-.105)/.04)**2)[:,:,None]*np.array((.055,.025,-.015),np.float32)
    iris*=np.clip((.275-r)*24,.28,1)[:,:,None]
    mask=np.clip((.255-r)*350,0,1);rgba=rgba*(1-mask[:,:,None])+iris*mask[:,:,None]
    pupil=np.clip((.104-r)*350,0,1);rgba=rgba*(1-pupil[:,:,None])+.007*pupil[:,:,None]
    veins=np.maximum(0,np.sin(a*29+r*150+np.sin(a*13))-.97)*np.clip((r-.24)*3,0,1)
    rgba+=veins[:,:,None]*np.array((.12,-.08,-.08),np.float32)
    return image_file('Eyes_BaseColor',rgba)

def make_materials():
    skin=skin_maps();cloth=cloth_maps();leather=leather_maps()
    return {
        'Skin':shader('Skin_Face',(1,1,1),base=skin[0],normal=skin[1],mask=skin[2],sss=.1),
        'Hands':shader('Skin_Hands',(.57,.36,.28),.48,normal=skin[1],sss=.07),
        'Fabric':shader('Fabric_Olive',(1,1,1),base=cloth[0],normal=cloth[1],mask=cloth[2]),
        'Hood':shader('Fabric_Hood',(.91,.94,.90),base=cloth[0],normal=cloth[1],mask=cloth[2]),
        'Scarf':shader('Fabric_Scarf',(.32,.34,.32),base=cloth[0],normal=cloth[1],mask=cloth[2]),
        'Panel':shader('Fabric_Reinforcement',(.61,.64,.62),base=cloth[0],normal=cloth[1],mask=cloth[2]),
        'Leather':shader('Leather_Trim',(.6,.61,.62),base=leather[0],normal=leather[1],mask=leather[2]),
        'Sole':shader('Rubber_Sole',(.25,.25,.23),.78),
        'Metal':shader('Metal_Buckle',(.12,.13,.13),.33),
        'Hair':shader('Hair_Dark',(.026,.017,.013),.4),
        'Brows':shader('Brows_Dark',(.032,.020,.016),.6),
        'Lashes':shader('Lashes',(.018,.012,.010),.55),
        'Eyes':shader('Eyes_GreyGreen',(1,1,1),.19,base=iris_map()),
        'Mouth':shader('Oral_Tissue',(.15,.032,.035),.38,sss=.08),
        'Teeth':shader('Teeth',(.71,.66,.54),.3,sss=.04),
    }
