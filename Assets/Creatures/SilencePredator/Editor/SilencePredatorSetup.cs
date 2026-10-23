#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Silence.Predator;
using Object = UnityEngine.Object;
namespace Silence.Predator.Editor
{
    internal sealed class PredatorImporter : AssetPostprocessor
    {
        private const string Root="Assets/Creatures/SilencePredator/";
        private void OnPreprocessModel()
        {
            if(!assetPath.StartsWith(Root+"Models/",StringComparison.Ordinal))return;
            var m=(ModelImporter)assetImporter;m.animationType=ModelImporterAnimationType.Generic;
            m.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;m.optimizeGameObjects=false;
            m.importAnimation=assetPath.EndsWith("LOD0.fbx",StringComparison.Ordinal);m.addCollider=false;
            m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.CalculateMikk;
        }
        private void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith(Root+"Textures/",StringComparison.Ordinal))return;
            var t=(TextureImporter)assetImporter;t.maxTextureSize=4096;t.mipmapEnabled=true;t.anisoLevel=8;
            t.sRGBTexture=assetPath.Contains("_BaseColor");
            if(assetPath.Contains("_Normal"))t.textureType=TextureImporterType.NormalMap;
            t.textureCompression=TextureImporterCompression.CompressedHQ;
        }
    }
    public static class SilencePredatorSetup
    {
        private const string Root="Assets/Creatures/SilencePredator";
        [MenuItem("SILENCE/17 Create Original Predator Demo")]
        public static void Generate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            string scenePath=Root+"/Demo/Monster_Demo.unity";
            if(File.Exists(scenePath)){if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(scenePath);return;}
            if(!File.Exists(Root+"/Models/Vesper_LOD0.fbx"))throw new InvalidOperationException("Run the Blender generator, then copy its Assets/Creatures/SilencePredator folder here.");
            if(Shader.Find("Universal Render Pipeline/Lit")==null)throw new InvalidOperationException("This demo requires URP Lit.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            foreach(string folder in new[]{"Materials","Animations","Prefabs","Demo"})Folder(Root+"/"+folder);
            for(int i=0;i<3;i++)AssetDatabase.ImportAsset(Root+"/Models/Vesper_LOD"+i+".fbx",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+"/Models/Vesper_LOD0.fbx");
            var takes=importer.defaultClipAnimations;
            string[] loops={"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenLoop","Search","Investigate"};
            foreach(var take in takes){take.name=take.name.Split('|').Last();take.loopTime=loops.Contains(take.name);take.lockRootRotation=true;take.lockRootHeightY=true;take.lockRootPositionXZ=true;}
            if(takes.Length!=19)throw new InvalidOperationException("Expected 19 imported takes; inspect the FBX Animation tab.");
            importer.clipAnimations=takes;importer.SaveAndReimport();
            var mats=new Dictionary<string,Material>();foreach(string name in new[]{"Skin","Armor","Tissue","Bone"})mats[name]=Material(name);
            var controller=Controller();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Monster_Creature");
            var main=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Vesper_LOD0.fbx"));main.transform.SetParent(root.transform,false);
            var animator=main.GetComponentInChildren<Animator>(true);if(animator==null)throw new InvalidOperationException("Generic Animator missing; inspect FBX rig import.");
            animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
            var driver=root.AddComponent<MonsterAnimationController>();driver.animator=animator;
            var bones=main.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
            var lods=new LOD[3];var first=main.GetComponentsInChildren<SkinnedMeshRenderer>(true);Assign(first,mats);lods[0]=new LOD(.45f,first);
            for(int level=1;level<=2;level++)
            {
                var bucket=new GameObject("LOD"+level);bucket.transform.SetParent(root.transform,false);
                var temp=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Vesper_LOD"+level+".fbx"));
                var renderers=new List<SkinnedMeshRenderer>();
                foreach(var source in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var go=new GameObject(source.name);go.transform.SetParent(bucket.transform,false);
                    var matrix=temp.transform.worldToLocalMatrix*source.transform.localToWorldMatrix;
                    go.transform.localPosition=matrix.GetColumn(3);go.transform.localRotation=matrix.rotation;go.transform.localScale=matrix.lossyScale;
                    var skin=go.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=source.sharedMesh;
                    skin.bones=source.bones.Select(b=>bones.TryGetValue(b.name,out var mapped)?mapped:throw new InvalidOperationException("Bone missing: "+b.name)).ToArray();
                    skin.rootBone=source.rootBone!=null&&bones.TryGetValue(source.rootBone.name,out var rb)?rb:animator.transform;
                    skin.localBounds=source.localBounds;renderers.Add(skin);
                }
                Object.DestroyImmediate(temp);Assign(renderers.ToArray(),mats);lods[level]=new LOD(level==1?.16f:.03f,renderers.ToArray());
            }
            var group=root.AddComponent<LODGroup>();group.SetLODs(lods);group.RecalculateBounds();
            var body=root.AddComponent<CapsuleCollider>();body.height=2.9f;body.center=Vector3.up*1.45f;body.radius=.5f;
            foreach(string name in new[]{"Head","Chest","Pelvis"})if(bones.TryGetValue(name,out var bone))
            {var trigger=bone.gameObject.AddComponent<SphereCollider>();trigger.radius=.3f;trigger.isTrigger=true;}
            foreach(var pair in new[]{("Audio_Mouth","Jaw"),("Audio_Hearing","Head"),("VFX_Breath","Head")})if(bones.TryGetValue(pair.Item2,out var parent))new GameObject(pair.Item1).transform.SetParent(parent,false);
            PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Monster_Creature.prefab");driver.demoControls=true;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Demo floor";floor.transform.position=Vector3.down*.05f;floor.transform.localScale=new Vector3(16,.1f,16);
            var scale=GameObject.CreatePrimitive(PrimitiveType.Capsule);scale.name="Human height reference 1.75m";scale.transform.position=new Vector3(2.4f,.875f,0);scale.transform.localScale=new Vector3(.5f,.875f,.5f);
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(4.4f,3.7f,7);camera.transform.LookAt(new Vector3(0,1.5f,0));camera.fieldOfView=45;camera.gameObject.AddComponent<AudioListener>();
            Light("Key",new Vector3(4,6,5),new Color(1,.85f,.7f),120,root.transform);Light("Fill",new Vector3(-4,4,2),new Color(.65f,.75f,1),70,root.transform);Light("Rim",new Vector3(0,5,-5),Color.white,130,root.transform);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.17f,.19f,.23f);
            AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene,scenePath))throw new IOException("Cannot save monster demo.");
            Debug.Log("SILENCE: prefab and demo created. Check deformation, clips, hearing plates, LOD and materials in Play. This is not native visual/performance approval or a replacement of the authoritative game creature.");
        }
        private static void Assign(SkinnedMeshRenderer[] renderers,Dictionary<string,Material> materials)
        {
            foreach(var r in renderers){string key=materials.Keys.First(k=>r.name.StartsWith(k,StringComparison.Ordinal));r.sharedMaterials=new[]{materials[key]};r.localBounds=new Bounds(new Vector3(0,1.45f,0),new Vector3(6,5,7));}
        }
        private static Material Material(string name)
        {
            string path=Root+"/Materials/"+name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old!=null)return old;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            Texture2D Map(string suffix)=>AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Textures/"+name+suffix+".png")??throw new InvalidOperationException("Missing map "+name+suffix);
            mat.SetTexture("_BaseMap",Map("_BaseColor"));mat.SetTexture("_BumpMap",Map("_Normal"));mat.SetTexture("_MetallicGlossMap",Map("_Mask"));mat.SetTexture("_OcclusionMap",Map("_Mask"));
            mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.EnableKeyword("_OCCLUSIONMAP");
            AssetDatabase.CreateAsset(mat,path);return mat;
        }
        private static AnimatorController Controller()
        {
            string path=Root+"/Animations/Monster.controller";var old=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);if(old!=null)return old;
            var clips=AssetDatabase.LoadAllAssetsAtPath(Root+"/Models/Vesper_LOD0.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__",StringComparison.Ordinal)).ToArray();
            string[] names={"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenStart","ListenLoop","ListenEnd","Alert","TurnLeft","TurnRight","AttackBite","AttackClaw","Lunge","Search","Investigate","Stunned","Recover"};
            var controller=AnimatorController.CreateAnimatorControllerAtPath(path);var machine=controller.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();
            foreach(string name in names){var clip=clips.FirstOrDefault(c=>c.name==name||c.name.EndsWith("|"+name,StringComparison.Ordinal));if(clip==null)throw new InvalidOperationException("Imported clip missing: "+name);var state=machine.AddState(name);state.motion=clip;states[name]=state;}
            machine.defaultState=states["Idle"];
            foreach(string name in new[]{"ListenStart","ListenEnd","Alert","AttackBite","AttackClaw","Lunge","Stunned","Recover"}){var t=states[name].AddTransition(states[name=="ListenStart"?"ListenLoop":"Idle"]);t.hasExitTime=true;t.exitTime=1;t.duration=.08f;}
            return controller;
        }
        private static void Light(string name,Vector3 position,Color color,float intensity,Transform subject){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Spot;l.transform.position=position;l.transform.LookAt(subject.position+Vector3.up*1.5f);l.range=20;l.spotAngle=80;l.color=color;l.intensity=intensity;l.shadows=LightShadows.Soft;}
        private static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');Folder(path.Substring(0,slash));AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));}
    }
}
#endif
