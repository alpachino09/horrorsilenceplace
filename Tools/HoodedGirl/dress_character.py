import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from common import *
import wardrobe,oral_geometry

bpy.ops.wm.open_mainfile(filepath=str(STAGES/'04_face_refined.blend'),use_scripts=False)
human=Human()
names={'Fabric':'Fabric_Olive','Hood':'Fabric_Hood','Scarf':'Fabric_Scarf','Panel':'Fabric_Reinforcement','Leather':'Leather_Trim','Sole':'Rubber_Sole','Metal':'Metal_Buckle','Hair':'Hair_Dark','Hands':'Skin_Hands','Mouth':'Oral_Tissue','Teeth':'Teeth'}
materials={key:bpy.data.materials[name] for key,name in names.items()}
oral_geometry.build(materials)
wardrobe.build(human,materials)
cam=camera('Full_Review',(2,-4.8,1.55),(0,0,.88),70)
render(SOURCE/'Previews'/'body_initial.png',cam,800,1100,20)
cam=camera('Hood_Face_Review',(0,-1.05,1.62),(0,-.08,1.60),85)
render(SOURCE/'Previews'/'hood_face_initial.png',cam,768,960,24)
