using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Core;
using Silence.Player;
using Silence.Networking;
using Silence.Interactions;
using Silence.Inventory;
using Silence.SoundSimulation;
using Silence.Graphics.Players;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class AnimationLabBuilder
    {
        private static string ReturnKey=>"SILENCE.CharacterReview.Source:"+Application.dataPath;
        public static void ReturnToSource()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Сначала останови Play.");
            string path=EditorPrefs.GetString(ReturnKey,"");
            if(!path.StartsWith("Assets/",StringComparison.Ordinal) || !File.Exists(path))throw new InvalidOperationException("Нет записанного исходного мира этой площадки. Открой свой игровой мир в Project.");
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        }
        public static string Create()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play.");
            if (!PlayerAnimationWorkshop.ValidateCurrent()) throw new InvalidOperationException("Сначала подключи анимации к копии игрового мира (шаг 2).");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            Scene source = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(source.path) || source.isDirty) throw new InvalidOperationException("Сохрани игровой мир.");
            var manager = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).Single();
            var player = manager.NetworkConfig.PlayerPrefab;
            string folder = Path.GetDirectoryName(source.path).Replace('\\', '/') + "/Review-" + DateTime.UtcNow.ToString("HHmmss-fff");
            VisualAssetLibrary.Folder(folder); Scene scene = default; bool saved = false;
            try
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("Для площадки нужен существующий URP/Lit шейдер.");
                GameObject Box(string name, Vector3 position, Vector3 size, Color color, SurfaceKind surface)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.position = position; box.transform.localScale = size;
                    var material = new Material(shader); material.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(material, folder + "/" + name.Replace(' ', '_') + ".mat");
                    box.GetComponent<Renderer>().sharedMaterial = material; box.AddComponent<SurfaceMaterial>().kind = surface; return box;
                }
                var kinds = new[] { SurfaceKind.Concrete, SurfaceKind.Wood, SurfaceKind.Metal, SurfaceKind.Dirt, SurfaceKind.Grass, SurfaceKind.Gravel, SurfaceKind.Water };
                for (int i = 0; i < kinds.Length; i++) Box(kinds[i].ToString(), new Vector3((i - 3) * 3.5f, -.15f, 1), new Vector3(3.5f, .3f, 18), Color.HSVToRGB(i / 10f, .2f, .3f), kinds[i]);
                for (int i = 0; i < 5; i++) Box("Small step " + i, new Vector3(-6, .03f * (i + 1), i * .5f + 2), new Vector3(2, .06f * (i + 1), .5f), new Color(.35f, .36f, .36f), SurfaceKind.Concrete);
                var ramp = Box("Small slope", new Vector3(5, .17f, 3), new Vector3(2, .2f, 3), new Color(.3f, .25f, .2f), SurfaceKind.Wood); ramp.transform.rotation = Quaternion.Euler(-8, 0, 0);
                Box("Low ceiling", new Vector3(0, 1.35f, 5), new Vector3(2.5f, .2f, 2), new Color(.2f, .2f, .2f), SurfaceKind.Concrete);
                var hinge = new GameObject("Test Door"); hinge.transform.position = new Vector3(2, 0, -1); hinge.AddComponent<PrototypeDoor>();
                var leaf = Box("Door leaf", Vector3.zero, new Vector3(1, 2, .1f), new Color(.2f, .14f, .09f), SurfaceKind.Wood); leaf.transform.SetParent(hinge.transform, false); leaf.transform.localPosition = new Vector3(.5f, 1, 0);
                var battery = ScriptableObject.CreateInstance<ItemDefinition>(); battery.id = InventoryModel.FlashlightCellId; battery.labelRu = "Запас батареек"; battery.labelEn = "Spare battery pack";
                AssetDatabase.CreateAsset(battery, folder + "/LabBattery.asset");
                var cells = Box("Battery pickup", new Vector3(-1, .7f, -2), new Vector3(.15f, .15f, .1f), new Color(.5f, .4f, .1f), SurfaceKind.Metal); cells.AddComponent<ItemPickup>().Configure(battery);
                var actorObject = Object.Instantiate(player); actorObject.name = "Human Motion Lab Player"; actorObject.transform.position = new Vector3(0, .03f, -5);
                // The lab is intentionally offline: do not leave unspawned NetworkBehaviours
                // waiting for a manager, and never modify the source prefab.
                foreach (var behaviour in actorObject.GetComponentsInChildren<NetworkBehaviour>(true)) Object.DestroyImmediate(behaviour);
                foreach (var network in actorObject.GetComponentsInChildren<NetworkObject>(true)) Object.DestroyImmediate(network);
                var oldHud = actorObject.GetComponent<Silence.UI.PrototypeHud>(); if (oldHud != null) Object.DestroyImmediate(oldHud);
                var actor = actorObject.GetComponent<PlayerContext>(); actor.View.tag = "MainCamera";
                var flashlight = actorObject.GetComponent<FlashlightController>();
                if (flashlight != null) { var serialized = new SerializedObject(flashlight); serialized.FindProperty("batterySeconds").floatValue = 12; serialized.ApplyModifiedPropertiesWithoutUndo(); }
                var service = new GameObject("Offline lab services"); var noise = service.AddComponent<NoiseSystem>(); var authority = service.AddComponent<OfflineWorldAuthority>(); authority.Configure(noise);
                service.AddComponent<FarmBootstrap>().Configure(authority, actor);
                var observer = new GameObject("Body review camera").AddComponent<Camera>(); observer.enabled = false;
                var lab = service.AddComponent<HumanMotionLab>(); lab.actor = actor; lab.observer = observer;
                lab.originalBasicClips=actorObject.GetComponent<PlayerAvatarPresentation>().Model.runtimeAnimatorController.animationClips.Where(c=>c!=null).Distinct().OrderBy(c=>c.name).ToArray();
                service.AddComponent<CharacterReviewProbe>().actor=actor;
                var light = new GameObject("Review light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.6f; light.transform.rotation = Quaternion.Euler(40, -25, 0);
                RenderSettings.ambientLight = new Color(.35f, .35f, .35f);
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, folder + "/HumanMotion_Lab.unity")) throw new IOException("Не удалось сохранить площадку.");
                File.WriteAllText(folder+"/review-source.json",JsonUtility.ToJson(new ReviewSource{sourceScene=source.path},true));saved = true;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
                if (!saved) AssetDatabase.DeleteAsset(folder);
            }
            AssetDatabase.Refresh(); string path = folder + "/HumanMotion_Lab.unity"; EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorPrefs.SetString(ReturnKey,source.path);
            Debug.Log("SILENCE: отдельная площадка " + path + ". Основной мир не изменён; build settings не заменены. Play, затем кнопка управления мышью. Это локальная проверка, не сетевой тест.");
            return path;
        }
        [Serializable] private sealed class ReviewSource {public string sourceScene;}
    }
}
