using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Core;
using Silence.Player;
using Silence.Graphics.Players;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class CharacterAuditWorkshop : EditorWindow
    {
        public const string Version="0.6.0";
        private string message;
        private const string ReportPath="Library/Silence/CharacterProjectAudit.json";
        [MenuItem("SILENCE/29 Character Audit and Review")]
        public static void Open()=>GetWindow<CharacterAuditWorkshop>("SILENCE Character V2");
        private void Run(Action action){try{if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Останови Play перед редакторской операцией.");action();}catch(Exception e){message=e.Message;Debug.LogException(e);}finally{EditorUtility.ClearProgressBar();}}
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Персонажи / руки / материалы — "+Version,EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Открытый мир",SceneManager.GetActiveScene().path,EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("Сначала проверь нынешний игровой мир. Исправления материалов и аудит поз применяются к одной копии этого мира. Площадка проверки отдельная: в ней 15 сценариев и отчёт, который не выдаёт запись за визуальный успех.",MessageType.Info);
            if(GUILayout.Button("1. Проверить текущих персонажей без изменений"))Run(()=>{var report=Audit();Directory.CreateDirectory("Library/Silence");File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));message="Аудит: "+report.characters.Count+" персонажей. Отчёт можно скопировать ниже.";Debug.Log(message+" "+ReportPath);});
            if(GUILayout.Button("2. Применить исправления к копии нынешнего мира"))Run(()=>{var path=PlayerViewWorkshop.Install(true);if(path!=null)message="Открыта исправленная копия текущего мира. Теперь можно создать площадку или проверить игру в Play.";});
            if(GUILayout.Button("3. Создать отдельную площадку 15 проверок"))Run(()=>{string path=AnimationLabBuilder.Create();if(path!=null)message="Открыта площадка. Play → записывай сценарии и осматривай персонажа. Затем останови Play и вернись кнопкой ниже.";});
            if(GUILayout.Button("Подобрать реальные замены движений в окне 28"))ImportedAssetsWorkshop.Open();
            if(GUILayout.Button("Вернуться из площадки в игровой мир"))Run(AnimationLabBuilder.ReturnToSource);
            if(GUILayout.Button("Скопировать редакторский аудит"))Run(()=>{if(!File.Exists(ReportPath))throw new InvalidOperationException("Сначала нажми проверку текущих персонажей.");GUIUtility.systemCopyBuffer=File.ReadAllText(ReportPath);message="Отчёт скопирован.";});
            if(GUILayout.Button("Проверить исправленную копию"))Run(()=>message=CharacterReworkWorkshop.ValidateCurrent()?"Ссылки проверены. Визуальная приёмка выполняется в Play.":"Смотри Console.");
            if(GUILayout.Button("Собрать игровой мир: Host и Client"))Run(()=>{if(CharacterReworkWorkshop.ValidateCurrent())NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path);});
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
        }
        [Serializable] public sealed class MaterialRecord
        {
            public string renderer,path,shader,role;
            public int queue;
            public float alpha=1,surface=-1,zWrite=-1;
            public bool rendererEnabled,depthPass;
        }
        [Serializable] public sealed class CharacterRecord
        {
            public string prefab,avatar,controller;
            public bool humanoid,validAvatar,rootMotion,flashlightModelPresent;
            public int skinMeshes,armCoordinators,sourceClips;
            public List<MaterialRecord> materials=new List<MaterialRecord>();
            public List<string> notes=new List<string>();
        }
        [Serializable] public sealed class Report
        {
            public string version=Version,utc,scene;
            public bool readOnly=true,nativePlayTested,meshIntersectionsVerified;
            public List<CharacterRecord> characters=new List<CharacterRecord>();
        }
        public static Report Audit()
        {
            var scene=SceneManager.GetActiveScene();var managers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if(managers.Length!=1)throw new InvalidOperationException("Открой свой игровой мир с одним NetworkManager, а не площадку/просмотр.");
            var manager=managers[0];
            var prefabs=manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l=>l!=null).SelectMany(l=>l.PrefabList).Concat(manager.NetworkConfig.Prefabs.Prefabs)
                .Where(e=>e!=null && e.Prefab!=null).Select(e=>e.Prefab).Append(manager.NetworkConfig.PlayerPrefab).Where(p=>p!=null).Distinct();
            var result=new Report{utc=DateTime.UtcNow.ToString("O"),scene=scene.path};
            foreach(var prefab in prefabs)
            {
                var skins=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);var animator=prefab.GetComponentInChildren<Animator>(true);
                bool creature=prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c!=null && (c.GetType().Name.Contains("Monster") || c.GetType().Name.Contains("Creature")));
                if(skins.Length==0 && animator==null && prefab.GetComponent<PlayerContext>()==null && !creature)continue;
                var record=new CharacterRecord{prefab=AssetDatabase.GetAssetPath(prefab),skinMeshes=skins.Length};
                if(animator!=null)
                {
                    record.humanoid=animator.isHuman;record.validAvatar=animator.avatar!=null && animator.avatar.isValid;
                    record.avatar=AssetDatabase.GetAssetPath(animator.avatar);record.controller=AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                    record.rootMotion=animator.applyRootMotion;record.sourceClips=animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.animationClips.Distinct().Count():0;
                    if(animator.runtimeAnimatorController!=null)foreach(var clip in animator.runtimeAnimatorController.animationClips.Distinct())
                    {
                        string path=AssetDatabase.GetAssetPath(clip),name=clip.name.Split('|').Last();
                        bool own=Path.GetFileNameWithoutExtension(path).StartsWith("Own_",StringComparison.Ordinal) || path.StartsWith("Assets/Characters/HoodedGirl/Models/",StringComparison.Ordinal);
                        if(own && new[]{"WalkBack","CrouchIdle","WoundedWalk","StrafeLeft","StrafeRight","Death"}.Contains(name))
                            record.notes.Add("Исходный fallback "+name+": в поставленном Blender-исходнике найдены пересечения выбранных hand/body поверхностей. Проверь свой клип; для замены назначь соответствующую реальную запись в 28. Native-версия клипа могла быть изменена и отдельно не измерена.");
                    }
                    record.armCoordinators=animator.GetComponents<CharacterArmPoseSafety>().Length;
                    if(record.humanoid && record.armCoordinators!=1)record.notes.Add("Нужен один координатор IK рук.");
                    if(record.humanoid && record.rootMotion)record.notes.Add("Root motion включён; существующий motor ожидает in-place presentation.");
                }
                var flashlight=prefab.GetComponent<FlashlightVisuals>();record.flashlightModelPresent=flashlight!=null && flashlight.Profile!=null && flashlight.Profile.modelPrefab!=null;
                if(flashlight!=null && !record.flashlightModelPresent)record.notes.Add("Нет реальной модели фонарика для проверки хвата и крышки.");
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                {
                    if(material==null){record.notes.Add("Отсутствует материал: "+renderer.name);continue;}
                    var role=CharacterSurfaceRules.Classify(renderer.name,material.name);
                    var entry=new MaterialRecord{renderer=renderer.name,path=AssetDatabase.GetAssetPath(material),shader=material.shader!=null?material.shader.name:"missing",role=role.ToString(),queue=material.renderQueue,rendererEnabled=renderer.enabled,
                        alpha=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor").a:1,surface=material.HasProperty("_Surface")?material.GetFloat("_Surface"):-1,zWrite=material.HasProperty("_ZWrite")?material.GetFloat("_ZWrite"):-1,depthPass=material.GetShaderPassEnabled("DepthOnly")};
                    record.materials.Add(entry);
                    if(role==CharacterSurfaceRole.Solid && (entry.surface!=0 || entry.alpha<.999f || entry.queue>=3000 || entry.zWrite!=1 || !entry.depthPass))record.notes.Add("Непрозрачность/depth требует исправления: "+renderer.name+" / "+material.name);
                    if(role==CharacterSurfaceRole.Unknown)record.notes.Add("Неизвестное назначение материала: "+renderer.name+" / "+material.name+". Не заменён автоматически.");
                }
                foreach(var skin in skins)if(skin.sharedMesh==null || skin.bones.Any(b=>b==null))record.notes.Add("Повреждён mesh/bone reference: "+skin.name);
                result.characters.Add(record);
            }
            return result;
        }
    }
}
