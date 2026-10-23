using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    internal sealed class VisualAssetLibrary
    {
        public const string Art = "Assets/_Silence/Art/VisualRebuild";
        private readonly string output;
        private readonly Dictionary<string,Material> materials = new Dictionary<string,Material>();
        private readonly Dictionary<string,GameObject> prefabs = new Dictionary<string,GameObject>();
        public VisualAssetLibrary(string destination)
        {
            output=destination;
            Folder(output+"/Materials"); Folder(output+"/Prefabs");
            foreach (string name in new[]{"AgedWood","Plaster","Brick","Roof","Rust","Stone","Bark","Forest","Grass","Mud","Gravel"}) MakePbr(name);
            materials["Wood"]=materials["AgedWood"]; materials["Metal"]=materials["Rust"]; materials["Fabric"]=materials["Plaster"];
            var glass=new Material(RequireShader("Universal Render Pipeline/Lit")){name="Weathered window glass"};
            glass.SetColor("_BaseColor",new Color(.13f,.18f,.17f,.22f)); glass.SetFloat("_Surface",1);
            glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            glass.SetOverrideTag("RenderType","Transparent");glass.SetFloat("_ZWrite",0);glass.SetFloat("_Smoothness",.72f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;glass.SetShaderPassEnabled("ShadowCaster",false);glass.SetShaderPassEnabled("DepthOnly",false);
            AssetDatabase.CreateAsset(glass,output+"/Materials/Glass.mat");materials["Glass"]=glass;
            var bulb=new Material(RequireShader("Universal Render Pipeline/Lit")){name="Practical lamp lens"};
            bulb.SetColor("_BaseColor",new Color(.7f,.5f,.2f));bulb.SetColor("_EmissionColor",new Color(1,.45f,.15f)*2);bulb.EnableKeyword("_EMISSION");bulb.SetShaderPassEnabled("ShadowCaster",false);bulb.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            AssetDatabase.CreateAsset(bulb,output+"/Materials/Bulb.mat");materials["Bulb"]=bulb;
            foreach (var pair in new[]{("Leaf","PineNeedles"),("GrassCard","GrassAtlas"),("Fern","FernAtlas")})
            {
                var mat=new Material(RequireShader("SILENCE/FoliageWind")){name=pair.Item1,enableInstancing=true};
                mat.SetTexture("_BaseMap",Texture(pair.Item2));mat.SetFloat("_Cutoff",.38f);mat.SetFloat("_WindStrength",pair.Item1=="Leaf"?.025f:.045f);
                AssetDatabase.CreateAsset(mat,output+"/Materials/"+pair.Item1+".mat");materials[pair.Item1]=mat;
            }
        }
        public Material Material(string name)=>materials[name];
        public Texture2D Texture(string name)
        {
            var value=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+name+".png");
            if(value==null)throw new InvalidOperationException("Missing visual texture: "+name+". Copy the updated source updater first.");return value;
        }
        private void MakePbr(string name)
        {
            var mat=new Material(RequireShader("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            mat.SetTexture("_BaseMap",Texture(name+"_BaseColor"));mat.SetTexture("_BumpMap",Texture(name+"_Normal"));
            mat.SetTexture("_MetallicGlossMap",Texture(name+"_Mask"));mat.SetTexture("_OcclusionMap",Texture(name+"_Mask"));
            mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Smoothness",1);mat.SetFloat("_OcclusionStrength",.8f);
            mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.EnableKeyword("_OCCLUSIONMAP");
            AssetDatabase.CreateAsset(mat,output+"/Materials/"+name+".mat");materials[name]=mat;
        }
        public GameObject Model(string name)
        {
            if(prefabs.TryGetValue(name,out var ready))return ready;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Models/"+name+".fbx");
            if(source==null)throw new InvalidOperationException("Missing FBX: "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                AssignMaterials(instance);
                ready=PrefabUtility.SaveAsPrefabAsset(instance,output+"/Prefabs/"+name+".prefab");
                if(ready==null)throw new InvalidOperationException("Cannot save visual prefab "+name);
                prefabs[name]=ready;return ready;
            }
            finally{Object.DestroyImmediate(instance);}
        }
        public void AssignMaterials(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)
                {
                    string name=slots[i]!=null?slots[i].name.Split('.')[0]:"";
                    if(!materials.TryGetValue(name,out var mat))throw new InvalidOperationException("Unknown FBX material slot: "+name+" on "+root.name);
                    slots[i]=mat;
                }
                renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
        }
        public GameObject LodModel(string name,Transform parent,Vector3 position,float scale=1)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);root.transform.localPosition=position;root.transform.localScale=Vector3.one*scale;
            var lods=new LOD[3];
            for(int i=0;i<3;i++)
            {
                var child=(GameObject)PrefabUtility.InstantiatePrefab(Model(name+"_LOD"+i));child.transform.SetParent(root.transform,false);
                lods[i]=new LOD(i==0?.13f:i==1?.045f:.012f,child.GetComponentsInChildren<Renderer>(true));
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();return root;
        }
        public GameObject Place(string name,Transform parent,Vector3 position,Quaternion rotation)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(Model(name));obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localRotation=rotation;return obj;
        }
        public static Shader RequireShader(string name)
        {var shader=Shader.Find(name);if(shader==null)throw new InvalidOperationException("Shader unavailable: "+name);return shader;}
        public static void Folder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
