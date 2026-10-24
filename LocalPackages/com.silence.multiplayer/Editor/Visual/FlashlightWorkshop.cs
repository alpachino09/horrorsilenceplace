#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Core;
using Silence.Player;
using Silence.Inventory;
using Silence.Interactions;
using Silence.Graphics.Players;
using Object=UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class FlashlightWorkshop : EditorWindow
    {
        [SerializeField] private FlashlightProfile profile;
        private string result;
        [MenuItem("SILENCE/24 Tactical Flashlight Workshop")]
        public static void Open() => GetWindow<FlashlightWorkshop>("SILENCE Flashlight");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Тактический фонарик",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Нужен настоящий импортированный FBX. USDZ конвертируется Tools/Flashlight/convert_usdz.py. Кисти, крышка и батарейки требуют калибровки по модели. Установка копирует открытую доработанную сцену и её player prefab.",MessageType.Info);
            profile=(FlashlightProfile)EditorGUILayout.ObjectField("Art profile",profile,typeof(FlashlightProfile),false);
            if(GUILayout.Button("Создать профиль из выбранного FBX"))profile=CreateProfile();
            if(GUILayout.Button("Проверить профиль")) { var errors=Errors(profile);result=errors.Count==0?"Ссылки проверены. Контакты рук, материалы и свет ещё нужно проверить в Play.":string.Join("\n",errors); }
            EditorGUILayout.LabelField("Открытая сцена",SceneManager.GetActiveScene().path,EditorStyles.wordWrappedLabel);
            if(GUILayout.Button("Установить в копию открытой игровой сцены"))Install(profile);
            if(GUILayout.Button("Разместить настоящий комплект батареек в центре Scene View"))PlacePickup(profile);
            if(!string.IsNullOrEmpty(result))EditorGUILayout.HelpBox(result,MessageType.Info);
        }
        private static string Folder(string category)
        {
            string path="Assets/_Silence/Generated/"+category+"/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");VisualAssetLibrary.Folder(path);return path;
        }
        private static FlashlightProfile CreateProfile()
        {
            var selected=Selection.activeObject as GameObject;
            if(selected==null || !AssetDatabase.GetAssetPath(selected).EndsWith(".fbx",StringComparison.OrdinalIgnoreCase) || selected.GetComponentsInChildren<Renderer>(true).Length==0)
                throw new InvalidOperationException("Select the actual imported flashlight FBX in Project. No replacement model is generated.");
            string output=Folder("FlashlightArt");var original=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var root=new GameObject("TacticalFlashlight_Rig");
            try
            {
                var imported=(GameObject)PrefabUtility.InstantiatePrefab(selected);PrefabUtility.UnpackPrefabInstance(imported,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);imported.transform.SetParent(root.transform,false);
                // Strip control/physics from this separate art copy; the source FBX remains untouched.
                foreach(var component in imported.GetComponentsInChildren<Component>(true).Where(c=>c is Animator || c is Animation || c is Collider || c is Rigidbody || c is Light || c is Camera || c is AudioSource || c is AudioListener).ToArray())Object.DestroyImmediate(component);
                var rig=root.AddComponent<FlashlightArtRig>();
                Bounds bounds=imported.GetComponentsInChildren<Renderer>(true).First().bounds;foreach(var renderer in imported.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(renderer.bounds);
                Transform Marker(string name,Vector3 position) { var marker=new GameObject(name).transform;marker.SetParent(root.transform,false);marker.localPosition=position;return marker; }
                rig.rightWristGrip=Marker("RightWristGrip_CALIBRATE",bounds.center);
                rig.lensEmitter=Marker("LensEmitter_CALIBRATE",bounds.center+Vector3.forward*bounds.extents.z);
                rig.leftPalmTap=Marker("PalmContact_CALIBRATE",bounds.center+Vector3.left*bounds.extents.x);
                rig.leftWristCap=Marker("CapWrist_CALIBRATE",bounds.center-Vector3.forward*bounds.extents.z);
                rig.batterySocket=Marker("BatterySocket_CALIBRATE",bounds.center-Vector3.forward*bounds.extents.z*.8f);
                var transforms=imported.GetComponentsInChildren<Transform>(true);
                rig.cap=transforms.FirstOrDefault(t=>new[]{"tailcap","battery_cap","batterycover"}.Contains(t.name.ToLowerInvariant()));
                rig.oldCells=transforms.FirstOrDefault(t=>new[]{"cells","battery_cells","batteries"}.Contains(t.name.ToLowerInvariant()));
                if(rig.oldCells!=null) { var copy=Object.Instantiate(rig.oldCells.gameObject,rig.oldCells.parent);copy.name="NewCells";rig.newCells=copy.transform;copy.SetActive(false); }
                rig.emissiveLens=imported.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.IndexOf("lens",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,output+"/TacticalFlashlight.prefab");
                var value=ScriptableObject.CreateInstance<FlashlightProfile>();value.modelPrefab=prefab;value.cookie=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Silence/Art/VisualRebuild/Textures/FlashlightCookie.png");
                value.calibrationNotes="Initial marker guesses from actual model bounds. Orient the imported mesh along +Z, lens forward. Adjust wrist, palm and cap markers against HoodedGirl. Split real cap/cells in Blender if absent. Native art/contact review pending.";
                AssetDatabase.CreateAsset(value,output+"/FlashlightProfile.asset");AssetDatabase.SaveAssets();Selection.activeObject=value;return value;
            }
            finally { Object.DestroyImmediate(root);EditorSceneManager.CloseScene(scene,true);if(original.IsValid() && original.isLoaded)SceneManager.SetActiveScene(original); }
        }
        public static List<string> Errors(FlashlightProfile value)
        {
            var errors=new List<string>();if(value==null || value.modelPrefab==null){errors.Add("Назначьте профиль с настоящей моделью фонарика.");return errors;}
            if(value.cookie==null)errors.Add("Не назначена cookie-текстура луча.");
            var art=value.modelPrefab.GetComponent<FlashlightArtRig>();
            if(art==null){errors.Add("В корне prefab нужен FlashlightArtRig.");return errors;}
            if(art.GetComponentsInChildren<Component>(true).Any(c=>c==null || (c is MonoBehaviour && c!=art) || c is Collider || c is Rigidbody || c is Animator || c is Animation || c is Light || c is Camera || c is AudioSource || c is AudioListener))errors.Add("Art-prefab должен содержать графику, маркеры и один FlashlightArtRig, без второй физики/анимации/света/сетевых компонентов.");
            var markers=new[]{art.rightWristGrip,art.lensEmitter,art.leftPalmTap,art.leftWristCap,art.batterySocket};
            if(markers.Any(t=>t==null || !t.IsChildOf(art.transform)))errors.Add("Назначьте пять маркеров внутри реальной модели.");
            var parts=new[]{art.cap,art.oldCells,art.newCells};
            if(parts.Any(t=>t==null || !t.IsChildOf(art.transform) || t.GetComponentsInChildren<Renderer>(true).Length==0) || parts.Distinct().Count()!=3)
                errors.Add("Нужны отдельные настоящие детали: крышка, старые и новые батарейки. Примитивы не создаются.");
            if(parts.Where(t=>t!=null).Any(t=>parts.Any(other=>other!=null && other!=t && t.IsChildOf(other))))errors.Add("Крышка и два комплекта должны быть независимыми деталями, не вложенными друг в друга.");
            if(parts.Where(t=>t!=null).Any(t=>markers.Any(m=>m!=null && m.IsChildOf(t))))errors.Add("Маркеры должны быть на неподвижном корпусе, а не внутри движущейся крышки/батареек.");
            var renderers=art.GetComponentsInChildren<Renderer>(true);
            if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null || !m.shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal))))errors.Add("Воссоздайте материалы настоящего ассета в URP.");
            if(art.emissiveLens==null || art.emissiveLens.Length==0 || art.emissiveLens.Any(r=>r==null || !r.transform.IsChildOf(art.transform)))errors.Add("Назначьте Renderer настоящей линзы.");
            else if(art.emissiveLens.Any(r=>r.sharedMaterials.Length==0 || r.sharedMaterials.Any(m=>m==null || !m.HasProperty("_EmissionColor"))))errors.Add("Материал линзы должен поддерживать _EmissionColor (URP Lit).");
            if(renderers.Length>0){Bounds b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);float size=Mathf.Max(b.size.x,b.size.y,b.size.z);if(size<.08f || size>.4f)errors.Add("Проверьте физический размер фонарика: ожидается 8–40 см.");}
            if((art.transform.localScale-Vector3.one).sqrMagnitude>.00001f)errors.Add("У корня ArtRig должен быть scale=1; масштабируйте дочернюю импортированную модель.");
            return errors;
        }
        public static void Install(FlashlightProfile value)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var errors=Errors(value);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Scene source=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(source.path))throw new InvalidOperationException("Save the improved game scene first.");
            var managers=source.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<NetworkManager>(true)).ToArray();if(managers.Length!=1)throw new InvalidOperationException("Open the improved game scene with one NetworkManager.");
            var old=managers[0].NetworkConfig.PlayerPrefab;
            if(old==null || old.GetComponent<PlayerAvatarPresentation>()==null || old.GetComponent<FlashlightController>()!=null)throw new InvalidOperationException("First install the HoodedGirl player with menu 21. This source player must not already contain the tactical flashlight.");
            string output=Folder("TacticalFlashlight"),playerPath=output+"/Player.prefab",target=output+"/"+Path.GetFileNameWithoutExtension(source.path)+"_Flashlight.unity";
            Scene copy=default;bool saved=false;
            try
            {
                if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(old),playerPath))throw new IOException("Cannot copy player prefab.");
                var contents=PrefabUtility.LoadPrefabContents(playerPath);
                try
                {
                    var animator=contents.GetComponent<PlayerAvatarPresentation>().Model;
                    if(animator==null || animator.avatar==null || !animator.avatar.isHuman || !animator.avatar.isValid)throw new InvalidOperationException("Player Humanoid Animator is missing.");
                    var input=contents.GetComponent<PlayerContext>().Input;
                    if(input==null || input.Template==null || input.Template.FindAction("Gameplay/ReloadFlashlight",false)==null || input.Template.FindAction("Gameplay/HolsterFlashlight",false)==null)throw new InvalidOperationException("Update the player's Input Actions with ReloadFlashlight and HolsterFlashlight first.");
                    string controllerPath=AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),newController=output+"/FlashlightBody.controller";
                    if(!controllerPath.EndsWith(".controller",StringComparison.OrdinalIgnoreCase) || !AssetDatabase.CopyAsset(controllerPath,newController))throw new InvalidOperationException("Expected the prepared body AnimatorController.");
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(newController);var layers=controller.layers;layers[0].iKPass=true;controller.layers=layers;EditorUtility.SetDirty(controller);animator.runtimeAnimatorController=controller;
                    contents.AddComponent<FlashlightController>();contents.AddComponent<FlashlightNetworkSync>();contents.AddComponent<FlashlightVisuals>().Configure(value,animator);
                    foreach(var lamp in contents.GetComponent<PlayerContext>().View.GetComponentsInChildren<Light>(true))if(lamp.type==LightType.Spot)lamp.enabled=false;
                    if(PrefabUtility.SaveAsPrefabAsset(contents,playerPath)==null)throw new IOException("Cannot save tactical player.");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                if(!EditorSceneManager.SaveScene(source,target,true))throw new IOException("Cannot copy source scene.");
                AssetDatabase.Refresh();copy=EditorSceneManager.OpenScene(target,OpenSceneMode.Additive);SceneManager.SetActiveScene(copy);
                var manager=copy.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<NetworkManager>(true)).Single();
                var config=JsonUtility.FromJson<NetworkConfig>(JsonUtility.ToJson(manager.NetworkConfig));if(config==null)throw new InvalidOperationException("Cannot copy network config.");
                var registry=ScriptableObject.CreateInstance<NetworkPrefabsList>();AssetDatabase.CreateAsset(registry,output+"/NetworkPrefabs.asset");
                foreach(var entry in manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l=>l!=null).SelectMany(l=>l.PrefabList).Concat(manager.NetworkConfig.Prefabs.Prefabs).Distinct().Where(p=>p!=null && p.Prefab!=old))registry.Add(entry);
                var player=AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);registry.Add(new NetworkPrefab { Prefab=player });EditorUtility.SetDirty(registry);
                config.PlayerPrefab=player;config.ProtocolVersion=checked((ushort)Math.Max(6,manager.NetworkConfig.ProtocolVersion+1));config.ForceSamePrefabs=true;
                config.Prefabs=new NetworkPrefabs { NetworkPrefabsLists=new List<NetworkPrefabsList>{registry} };manager.NetworkConfig=config;EditorUtility.SetDirty(manager);AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(copy,target))throw new IOException("Cannot save copied scene.");saved=true;
                File.WriteAllText(output+"/installation.txt","Source: "+source.path+"\nCopy: "+target+"\nProtocol: "+config.ProtocolVersion+"\nNative Play, contacts, battery animation, URP/Metal, Host/Client and FPS not tested.\n");
            }
            finally
            {
                if(copy.IsValid() && copy.isLoaded)EditorSceneManager.CloseScene(copy,true);if(source.IsValid() && source.isLoaded)SceneManager.SetActiveScene(source);
                if(!saved)AssetDatabase.DeleteAsset(output);
            }
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(target,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=target)).ToArray();EditorSceneManager.OpenScene(target,OpenSceneMode.Single);
            Debug.Log("SILENCE: tactical flashlight installed in scene COPY "+target+". Test actual asset, contact and light; build BOTH Host and Client via menu 21. No native tests have been run by this installer.");
        }
        private static void PlacePickup(FlashlightProfile value)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            var errors=Errors(value);if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            var scene=SceneManager.GetActiveScene();var manager=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NetworkManager>(true)).SingleOrDefault();
            if(manager==null || manager.NetworkConfig.PlayerPrefab.GetComponent<FlashlightController>()==null)throw new InvalidOperationException("Open the copied tactical flashlight game scene.");
            string path="Assets/_Silence/Generated/FlashlightBatteryPack.asset";var definition=AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if(definition==null){definition=ScriptableObject.CreateInstance<ItemDefinition>();definition.id=InventoryModel.FlashlightCellId;definition.labelRu="Комплект батареек";definition.labelEn="Battery pack";AssetDatabase.CreateAsset(definition,path);}
            var root=new GameObject("Flashlight Battery Pack");Undo.RegisterCreatedObjectUndo(root,"Place actual battery pack");
            var rig=value.modelPrefab.GetComponent<FlashlightArtRig>();var visual=Object.Instantiate(rig.newCells.gameObject);visual.name="Actual Cells";visual.SetActive(true);visual.transform.SetParent(root.transform,false);
            Matrix4x4 m=rig.transform.worldToLocalMatrix*rig.newCells.localToWorldMatrix;visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=m.rotation;visual.transform.localScale=m.lossyScale;
            Bounds bounds=visual.GetComponentsInChildren<Renderer>(true).First().bounds;foreach(var r in visual.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
            var collider=root.AddComponent<BoxCollider>();collider.center=root.transform.InverseTransformPoint(bounds.center);collider.size=bounds.size;
            root.AddComponent<ItemPickup>().Configure(definition);root.AddComponent<NetworkObject>();root.AddComponent<NetworkInteractable>();
            root.transform.position=SceneView.lastActiveSceneView!=null?SceneView.lastActiveSceneView.pivot:Vector3.up;
            Selection.activeGameObject=root;EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        }
    }
}
#endif
