using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Config;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Graphics;
using Silence.Interactions;
using Silence.Player;
using Object=UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class VisualFarmRebuilder
    {
        public const string Output="Assets/_Silence/Generated/VisualRebuild";
        public const string ScenePath=Output+"/Farm_Visual.unity";
        public const string PlayerPath=Output+"/VisualPlayer.prefab";
        public const int Seed=27092026;
        [MenuItem("SILENCE/14 Create Farm Visual Slice")]
        public static void Generate()=>Generate(Seed);
        public static void Generate(int seed)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            if(!File.Exists(CampaignSceneGenerator.ScenePath))throw new InvalidOperationException("Generate the working campaign with menu 09 first.");
            if(AssetDatabase.IsValidFolder(Output))
            {
                if(File.Exists(Output+"/visual-generated.json")&&File.Exists(ScenePath))
                {Debug.Log("SILENCE: existing visual slice preserved: "+ScenePath);return;}
                throw new InvalidOperationException("Partial/existing VisualRebuild folder. Preserve it by renaming it in Unity Project before generating again.");
            }
            if(!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))throw new InvalidOperationException("URP is required; no pipeline migration is performed.");
            Scene previous=SceneManager.GetActiveScene(),source=SceneManager.GetSceneByPath(CampaignSceneGenerator.ScenePath),copy=default;
            bool opened=!source.IsValid()||!source.isLoaded;
            try
            {
                if(opened)source=EditorSceneManager.OpenScene(CampaignSceneGenerator.ScenePath,OpenSceneMode.Additive);
                if(source.isDirty)throw new InvalidOperationException("Save the campaign scene before copying it.");
                VisualAssetLibrary.Folder(Output);
                if(!EditorSceneManager.SaveScene(source,ScenePath,true))throw new IOException("Cannot copy campaign scene.");
                AssetDatabase.Refresh();copy=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(copy);
                T[] All<T>()where T:Component=>copy.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
                var game=All<GameplayDirector>().Single();var farm=All<Transform>().Single(t=>t.name=="Farm_Graybox");
                foreach(string guid in AssetDatabase.FindAssets("",new[]{VisualAssetLibrary.Art}))
                {
                    string path=AssetDatabase.GUIDToAssetPath(guid);
                    if(path.EndsWith(".png",StringComparison.Ordinal)||path.EndsWith(".fbx",StringComparison.Ordinal))
                        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                }
                var art=new VisualAssetLibrary(Output);var hidden=new List<Renderer>();
                void Hide(GameObject go){foreach(var r in go.GetComponentsInChildren<Renderer>(true)){r.enabled=false;hidden.Add(r);}}
                foreach(var t in farm.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if(t.name=="Pine") {Hide(t.gameObject);foreach(var shape in t.GetComponentsInChildren<Collider>())shape.enabled=false;}
                    if(t.name=="Pitched roof"||t.name.StartsWith("Fence",StringComparison.Ordinal)||t.name=="Ground"||t.name=="Path")
                    {
                        if(t.TryGetComponent(out Renderer renderer)){renderer.enabled=false;hidden.Add(renderer);}
                        if(t.name=="Ground"&&t.TryGetComponent(out Collider collider))collider.enabled=false;
                    }
                }
                var visuals=new GameObject("VisualRebuild_AuthoredGeometry");visuals.transform.SetParent(farm,false);
                foreach(string name in new[]{"Home","Barn"})
                {
                    var building=farm.Find(name);if(building==null)throw new InvalidOperationException("Original building missing: "+name);
                    foreach(var renderer in building.GetComponentsInChildren<Renderer>(true)){renderer.enabled=false;hidden.Add(renderer);}
                    art.LodModel(name=="Home"?"Farmhouse":"Barn",visuals.transform,building.localPosition);
                }
                var terrain=TerrainBuilder.Build(visuals.transform,Output,art);VegetationPainter.Paint(visuals.transform,terrain,art,seed);
                foreach(var door in farm.GetComponentsInChildren<ManualDoor>(true))
                {
                    var leaf=door.GetComponentInChildren<BoxCollider>();if(leaf==null)continue;
                    if(leaf.TryGetComponent(out Renderer renderer)){renderer.enabled=false;hidden.Add(renderer);}
                    var model=art.Place("DoorLeaf",door.transform,Vector3.zero,Quaternion.identity);
                    // Keep the existing hinge, collider, obstacle, network entity and breakable component.
                    model.transform.localScale=new Vector3(leaf.bounds.size.x/1.6f,leaf.bounds.size.y/2.22f,1);
                }
                foreach(var prop in farm.GetComponentsInChildren<CarryableProp>(true))
                {
                    if(prop.TryGetComponent(out Renderer renderer)){renderer.enabled=false;hidden.Add(renderer);}
                    Fit(art.Model(prop.name.Contains("Metal")?"MetalCan":"Crate"),prop.GetComponent<Collider>());
                }
                foreach(var item in farm.GetComponentsInChildren<ItemPickup>(true))
                {
                    if(item.TryGetComponent(out Renderer renderer))renderer.sharedMaterial=art.Material(item.Definition.id=="document"?"Plaster":"Rust");
                }
                foreach(var radio in farm.GetComponentsInChildren<MissionRadio>(true))
                {
                    if(radio.TryGetComponent(out Renderer renderer)){renderer.enabled=false;hidden.Add(renderer);}
                    Fit(art.Model("Radio"),radio.GetComponent<Collider>());
                }
                foreach(var t in farm.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    if(t.name!="HomeTable"&&t.name!="BarnTable")continue;
                    if(t.TryGetComponent(out Renderer renderer)){renderer.enabled=false;hidden.Add(renderer);}
                    art.Place("Table",visuals.transform,t.position,Quaternion.identity);
                }
                art.Place("WallCabinet",visuals.transform,new Vector3(-10,0,3.45f),Quaternion.identity);
                art.Place("CeilingLamp",visuals.transform,new Vector3(-10,2.8f,-.3f),Quaternion.identity);
                var practical=new GameObject("Ceiling lamp practical");practical.transform.SetParent(visuals.transform,false);practical.transform.localPosition=new Vector3(-10,2.8f,-.3f);
                var bulb=practical.AddComponent<Light>();bulb.type=LightType.Point;bulb.color=new Color(1,.72f,.43f);bulb.intensity=2.8f;bulb.range=8;
                bulb.shadows=LightShadows.Soft;bulb.shadowBias=.02f;bulb.shadowNormalBias=.1f;bulb.lightmapBakeType=LightmapBakeType.Mixed;
                for(int i=-19;i<=19;i+=2)
                {
                    art.Place("FencePanel",visuals.transform,new Vector3(i,0,-18),Quaternion.identity);
                    if(Mathf.Abs(i)>2)art.Place("FencePanel",visuals.transform,new Vector3(i,0,18),Quaternion.identity);
                }
                for(int z=-17;z<=17;z+=2)foreach(float x in new[]{-20f,20f})art.Place("FencePanel",visuals.transform,new Vector3(x,0,z),Quaternion.Euler(0,90,0));
                // Match existing Home practical light to the authored fixture; no invisible fill light is added.
                var home=farm.Find("Home");foreach(var light in home.GetComponentsInChildren<Light>(true))
                {light.transform.localPosition=new Vector3(-1.2f,2.05f,-3.35f);light.color=new Color(1,.64f,.32f);light.intensity=3.8f;light.range=9;light.shadows=LightShadows.Soft;light.shadowBias=.02f;light.shadowNormalBias=.12f;light.lightmapBakeType=LightmapBakeType.Mixed;}
                visuals.AddComponent<LegacyRendererMask>().Configure(hidden.Distinct().ToArray());
                LightingConfigurator.Build(visuals,game,terrain,art,Output);
                CreatePlayer(art);var manager=All<NetworkManager>().Single();var player=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                var transport=manager.NetworkConfig.NetworkTransport;manager.NetworkConfig=new NetworkConfig{NetworkTransport=transport,PlayerPrefab=player,TickRate=30,
                    ConnectionApproval=true,ProtocolVersion=3,EnableSceneManagement=true,ForceSamePrefabs=true,ClientConnectionBufferTimeout=15};
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab{Prefab=player});
                RebuildNavigation(farm);
                foreach(var renderer in visuals.GetComponentsInChildren<MeshRenderer>(true))
                    if(renderer.GetComponentInParent<Interactable>()==null&&renderer.gameObject.layer!=31)
                        GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic);
                AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(copy,ScenePath))throw new IOException("Cannot save visual scene.");
                File.WriteAllText(Output+"/visual-generated.json","{\"version\":\"visual-2.0\",\"vegetationSeed\":"+seed+",\"source\":\""+CampaignSceneGenerator.ScenePath+"\"}");
                AssetDatabase.Refresh();var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
                Debug.Log("SILENCE: visual copy created: "+ScenePath+". Run 15 validation. Unity visual and multiplayer checks are still required; existing campaign preserved.");
            }
            finally
            {
                if(copy.IsValid()&&copy.isLoaded)EditorSceneManager.CloseScene(copy,true);
                if(opened&&source.IsValid()&&source.isLoaded)EditorSceneManager.CloseScene(source,true);
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }
        private static void Fit(GameObject prefab,Collider target)
        {
            if(target==null)return;var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);var renderer=model.GetComponentsInChildren<Renderer>();
            var bounds=renderer[0].bounds;foreach(var r in renderer)bounds.Encapsulate(r.bounds);
            Vector3 size=target.bounds.size;model.transform.localScale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            var scaled=renderer[0].bounds;foreach(var r in renderer)scaled.Encapsulate(r.bounds);
            model.transform.position+=target.bounds.center-scaled.center;model.transform.SetParent(target.transform,true);
        }
        private static void CreatePlayer(VisualAssetLibrary art)
        {
            if(!AssetDatabase.CopyAsset(CampaignSceneGenerator.PlayerPath,PlayerPath))throw new IOException("Cannot copy player prefab.");
            var prefab=PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var actor=prefab.GetComponent<PlayerContext>();actor.View.fieldOfView=70;actor.View.nearClipPlane=.07f;
                var camera=actor.View.GetComponent<UniversalAdditionalCameraData>()??actor.View.gameObject.AddComponent<UniversalAdditionalCameraData>();
                camera.renderPostProcessing=true;camera.requiresDepthTexture=true;camera.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var lamp=actor.View.GetComponentInChildren<Light>(true);lamp.cookie=art.Texture("FlashlightCookie");lamp.spotAngle=58;lamp.innerSpotAngle=32;
                lamp.shadows=LightShadows.Soft;lamp.shadowBias=.015f;lamp.shadowNormalBias=.08f;lamp.range=18;
                lamp.gameObject.AddComponent<FlashlightPresentation>();
                PrefabUtility.SaveAsPrefabAsset(prefab,PlayerPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        private static void RebuildNavigation(Transform farm)
        {
            var sources=new List<NavMeshBuildSource>();var markups=new List<NavMeshBuildMarkup>();
            foreach(var obj in farm.GetComponentsInChildren<Interactable>(true))markups.Add(new NavMeshBuildMarkup{root=obj.transform,ignoreFromBuild=true});
            NavMeshBuilder.CollectSources(farm,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.4f;settings.agentHeight=2;settings.agentClimb=.3f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(new Vector3(0,4,0),new Vector3(110,24,110)),Vector3.zero,Quaternion.identity);
            if(data==null)throw new InvalidOperationException("Visual terrain navigation bake failed.");
            AssetDatabase.CreateAsset(data,Output+"/FarmNavigation.asset");farm.GetComponent<RuntimeNavigation>().Configure(data);
        }
    }
}
