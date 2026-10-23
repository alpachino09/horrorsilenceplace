using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Graphics;
using Silence.Interactions;
using Silence.Player;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class VisualSceneValidator
    {
        [MenuItem("SILENCE/15 Validate Visual Slice")]
        public static void Menu()=>Validate();
        public static bool Validate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var report=new StringBuilder();var failures=new List<string>();
            void Check(bool pass,string name){report.AppendLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures.Add(name);}
            report.AppendLine("Unity "+Application.unityVersion);
            Check(UnityEditor.PlayerSettings.colorSpace==ColorSpace.Linear,"Linear color space");report.AppendLine("UTC "+DateTime.UtcNow.ToString("O"));
            foreach(var package in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Where(p=>p.name.StartsWith("com.unity.render-pipelines",StringComparison.Ordinal)))report.AppendLine(package.name+" "+package.version);
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(VisualFarmRebuilder.PlayerPath);
            Check(player!=null,"Visual player prefab exists");
            if(player!=null)
            {
                Check(player.GetComponent<NetworkPlayer>()!=null&&player.GetComponent<GameplayActorNetwork>()!=null,"Player network components retained");
                Check(player.GetComponentInChildren<FlashlightPresentation>(true)!=null,"Flashlight presentation assigned");
                Check(player.GetComponentsInChildren<Camera>(true).Length==1&&player.GetComponentsInChildren<AudioListener>(true).Length==1,"One saved player camera/listener");
                var context=player.GetComponent<PlayerContext>();Check(context!=null&&context.View.GetComponent<UniversalAdditionalCameraData>()!=null,"URP camera data assigned");
            }
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(VisualFarmRebuilder.ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
            try
            {
                Check(File.Exists(VisualFarmRebuilder.ScenePath),"Visual scene exists");
                if(File.Exists(VisualFarmRebuilder.ScenePath))
                {
                    if(opened)scene=EditorSceneManager.OpenScene(VisualFarmRebuilder.ScenePath,OpenSceneMode.Additive);
                    var objects=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray();
                    T[] All<T>()where T:Component=>objects.SelectMany(o=>o.GetComponents<T>()).ToArray();
                    Check(!scene.isDirty,"Visual scene saved");Check(All<Terrain>().Length==1&&All<TerrainCollider>().Length==1,"One sculpted terrain/collider");
                    Check(All<GraphicsQualityManager>().Length==1&&All<VisualAtmosphere>().Length==1&&All<FarmFogVolume>().Length==1,"Graphics/atmosphere/fog configured");
                    Check(All<NetworkWorldAuthority>().Length==1&&All<GameplayDirector>().Length==1&&All<NetworkSession>().Length==1,"One authoritative world/director/session retained");
                    Check(All<HearingCreature>().Length==1&&All<RuntimeNavigation>().Length==7,"Creature and seven navigation registrations retained");
                    Check(All<ChapterExit>().Length==7&&All<MissionRadio>().Length==7,"Seven campaign exits/radios retained");
                    Check(All<LODGroup>().Length>=2,"Building and vegetation LOD groups exist");
                    foreach(var manager in All<NetworkManager>())Check(manager.NetworkConfig.PlayerPrefab==player&&manager.NetworkConfig.ProtocolVersion==3,"New player registry, unchanged gameplay protocol");
                    foreach(var terrain in All<Terrain>())Check(terrain.terrainData!=null&&terrain.terrainData.heightmapResolution==129&&terrain.terrainData.terrainLayers.Length==4,"Terrain height/layers assigned");
                    foreach(var target in All<Interactable>())Check(target.GetComponent<NetworkObject>()!=null&&target.GetComponent<NetworkInteractable>()!=null,"Interaction authority retained: "+target.name);
                    foreach(var go in objects)Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go)==0,"No missing scripts: "+go.name);
                    foreach(var renderer in All<MeshRenderer>().Where(r=>r.enabled&&r.transform.IsChildOf(objects.Single(o=>o.name=="VisualRebuild_AuthoredGeometry").transform)))
                    {
                        Check(renderer.sharedMaterials.All(m=>m!=null&&m.shader!=null),"Materials resolved: "+renderer.name);
                        foreach(var mat in renderer.sharedMaterials.Where(m=>m!=null))
                            Check(mat.shader.name.StartsWith("Universal Render Pipeline",StringComparison.Ordinal)||mat.shader.name.StartsWith("SILENCE/",StringComparison.Ordinal),"Supported shader: "+mat.name);
                    }
                    foreach(var lod in All<LODGroup>())
                    {
                        var levels=lod.GetLODs();Check(levels.Length==3&&levels.All(l=>l.renderers.Length>0&&l.renderers.All(r=>r!=null)),"Three assigned LODs: "+lod.name);
                    }
                }
                foreach(string shaderName in new[]{"SILENCE/FoliageWind","SILENCE/HeightFog"})
                {
                    var shader=Shader.Find(shaderName);Check(shader!=null,"Shader imported: "+shaderName);
                    if(shader!=null){Check(shader.isSupported,"Shader supported by current device: "+shaderName);var messages=ShaderUtil.GetShaderMessages(shader);foreach(var message in messages)report.AppendLine("SHADER "+shaderName+" "+message.severity+" "+message.message);Check(!messages.Any(m=>m.severity.ToString()=="Error"),"No reported shader compiler errors: "+shaderName);}
                }
            }
            finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
            string summary="SILENCE Visual validation — Failures: "+failures.Count;report.Insert(0,summary+"\n");
            report.AppendLine("WARN final rigged player/creature art is not included; existing temporary characters retained.");
            report.AppendLine("Not a visual approval, shader-variant/performance test, or multiplayer runtime test.");
            Directory.CreateDirectory("Library/Silence");File.WriteAllText("Library/Silence/VisualValidation.txt",report.ToString());
            if(failures.Count==0)Debug.Log(report.ToString());else Debug.LogError(summary+"\nFAIL "+string.Join("; ",failures));return failures.Count==0;
        }
    }
}
