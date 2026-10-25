using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Player;
using Silence.Graphics.Players;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class PlayerViewWorkshop : EditorWindow
    {
        private string message;
        [MenuItem("SILENCE/26 Player Camera and Body")]
        public static void Open() => GetWindow<PlayerViewWorkshop>("SILENCE Camera");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Открой свой нынешний игровой мир с героиней. Кнопка создаст копию этого мира и его игроков, настроит естественный вид от первого лица, камеру от третьего лица (V) и защиту позы рук. Окружение и анимационные клипы сохраняются.", MessageType.Info);
            EditorGUILayout.LabelField("Текущий мир", SceneManager.GetActiveScene().path, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Создать копию текущего мира: камера и тело"))
                try { string result = Install(); if (result != null) message = "Открыт " + result + ". Play → Создать сессию → Начать раунд. V переключает камеру."; }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            if (GUILayout.Button("Проверить камеру и тело в текущем мире"))
                try { message = ValidateCurrent() ? "Ссылки проверены. В Play проверь V, стены, взгляд вниз и положение рук." : "Смотри Console."; }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            if (GUILayout.Button("Собрать текущий мир: Host и Client"))
                try { if (ValidateCurrent()) NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path); }
                catch (Exception error) { message = error.Message; Debug.LogException(error); }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }
        private static NetworkManager Manager(Scene scene)
        {
            var managers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if (managers.Length != 1) throw new InvalidOperationException("Открой свой сохранённый игровой мир с одним NetworkManager, например ..._Motion. Просмотры 23 и лаборатория не являются игровым миром.");
            return managers[0];
        }
        private static NetworkPrefab[] Entries(NetworkManager manager) => manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l => l != null).SelectMany(l => l.PrefabList)
            .Concat(manager.NetworkConfig.Prefabs.Prefabs).Where(p => p != null && p.Prefab != null)
            .GroupBy(p => (p.Override, p.Prefab, p.SourcePrefabToOverride, p.SourceHashToOverride, p.OverridingTargetPrefab)).Select(g => g.First()).ToArray();
        public static PlayerViewCalibration.Result ConfigurePlayer(GameObject player, string folder)
        {
            var actor = player.GetComponent<PlayerContext>(); var visual = player.GetComponent<PlayerAvatarPresentation>();
            if (actor == null || actor.Settings == null || actor.View == null || visual == null || visual.Model == null ||
                visual.Model.avatar == null || !visual.Model.avatar.isValid || !visual.Model.avatar.isHuman)
                throw new InvalidOperationException("Нужен существующий игрок с подготовленной Humanoid-героиней (меню 21/25).");
            var animator = visual.Model;
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) throw new InvalidOperationException("Для этого обновления нужен обычный сохранённый AnimatorController героини.");
            var layers = controller.layers;
            bool changeIK = !layers[0].iKPass || layers.Any(l => (l.name == "UpperBody" || l.name == "Reactions") && !l.iKPass);
            if (changeIK)
            {
                string path = folder + "/CameraBody.controller";
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(controller), path)) throw new IOException("Не удалось скопировать controller для IK рук.");
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); layers = controller.layers; layers[0].iKPass = true;
                for (int i = 1; i < layers.Length; i++) if (layers[i].name == "UpperBody" || layers[i].name == "Reactions") layers[i].iKPass = true;
                controller.layers = layers; EditorUtility.SetDirty(controller); animator.runtimeAnimatorController = controller;
                if (visual.AnimationProfile != null)
                {
                    var profile = Object.Instantiate(visual.AnimationProfile); profile.controller = controller;
                    AssetDatabase.CreateAsset(profile, folder + "/CameraAnimation.asset"); visual.ConfigureAnimation(profile, visual.FacingRoot);
                }
            }
            Transform facing = animator.transform;
            while (facing.parent != null && facing.parent != player.transform) facing = facing.parent;
            if (facing.parent != player.transform) throw new InvalidOperationException("Графика должна находиться в отдельнем дочернем объекте игрока.");
            var calibration = PlayerViewCalibration.Calibrate(actor, animator, facing);
            (player.GetComponent<PlayerCameraRig>() ?? player.AddComponent<PlayerCameraRig>()).Configure(calibration.standingOffset, calibration.crouchingOffset);
            var visibility = player.GetComponent<PlayerAvatarVisibility>() ?? player.AddComponent<PlayerAvatarVisibility>();
            var surfaces = facing.GetComponentsInChildren<Renderer>(true);
            var hidden = visibility.HiddenRenderers.Where(r => r != null).Concat(surfaces.Where(r => HeadSurface(r.name))).Distinct().ToArray();
            if (hidden.Length == 0) throw new InvalidOperationException("Не найдены отдельные поверхности головы/капюшона; требуется настройка модели через 21.");
            visibility.Configure(hidden); visibility.ConfigureBody(surfaces);
            (animator.GetComponent<CharacterArmPoseSafety>() ?? animator.gameObject.AddComponent<CharacterArmPoseSafety>()).Configure(actor, animator.GetComponent<CharacterIKController>() == null);
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            File.WriteAllText(folder + "/view-calibration.json", JsonUtility.ToJson(calibration, true));
            return calibration;
        }
        private static bool HeadSurface(string name) => new[] { "Face", "Eye.", "Brows", "Lashes", "Hair", "Hood", "Neck_Wrap", "Mouth_", "Teeth_", "Tongue" }.Any(p => name.StartsWith(p, StringComparison.Ordinal));
        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            Scene source = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(source.path) || source.isDirty) throw new InvalidOperationException("Сохрани игровой мир перед установкой.");
            var manager = Manager(source); var original = manager.NetworkConfig.PlayerPrefab; var entries = Entries(manager);
            var players = entries.Select(p => p.Prefab).Append(original).Where(p => p != null && p.GetComponent<PlayerContext>() != null).Distinct().ToArray();
            if (original == null || !players.Contains(original)) throw new InvalidOperationException("В мире не настроен сетевой игрок.");
            foreach (var prefab in players)
                if (prefab.GetComponent<PlayerAvatarPresentation>() == null || prefab.GetComponent<NetworkPlayer>() == null || prefab.GetComponentsInChildren<NetworkObject>(true).Length != 1 ||
                    entries.Any(e => e.Prefab == prefab && e.Override != NetworkPrefabOverride.None)) throw new InvalidOperationException("Не поддержана структура игрока: " + prefab.name + ". Сначала установи героиню через 21.");
            string folder = "Assets/_Silence/Generated/PlayerView/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"); VisualAssetLibrary.Folder(folder);
            string scenePath = folder + "/" + Path.GetFileNameWithoutExtension(source.path) + "_View.unity";
            Scene copy = default; bool saved = false;
            try
            {
                var replacements = new Dictionary<GameObject, GameObject>();
                for (int i = 0; i < players.Length; i++)
                {
                    string output = folder + "/Player" + i; VisualAssetLibrary.Folder(output); string path = output + "/Player.prefab";
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(players[i]), path)) throw new IOException("Не удалось скопировать игрока.");
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    try { ConfigurePlayer(contents, output); if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new IOException("Не удалось сохранить игрока."); }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    replacements.Add(players[i], AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
                if (!EditorSceneManager.SaveScene(source, scenePath, true)) throw new IOException("Не удалось создать копию игрового мира.");
                AssetDatabase.Refresh(); copy = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(copy);
                var copiedManager = Manager(copy); var config = JsonUtility.FromJson<NetworkConfig>(JsonUtility.ToJson(copiedManager.NetworkConfig));
                if (config == null) throw new InvalidOperationException("Не удалось скопировать сетевые настройки.");
                var registry = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(registry, folder + "/NetworkPrefabs.asset");
                foreach (var entry in entries) if (!replacements.ContainsKey(entry.Prefab)) registry.Add(entry);
                foreach (var prefab in replacements.Values) registry.Add(new NetworkPrefab { Prefab = prefab });
                config.PlayerPrefab = replacements[original]; config.ForceSamePrefabs = true; config.ProtocolVersion = checked((ushort)Math.Max(8, manager.NetworkConfig.ProtocolVersion + 1));
                config.Prefabs = new NetworkPrefabs { NetworkPrefabsLists = new List<NetworkPrefabsList> { registry } };
                copiedManager.NetworkConfig = config; EditorUtility.SetDirty(copiedManager); EditorUtility.SetDirty(registry); AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(copy, scenePath)) throw new IOException("Не удалось сохранить мир с новой камерой.");
                File.WriteAllText(folder + "/installation.json", JsonUtility.ToJson(new Installation { sourceScene = source.path, createdScene = scenePath, protocol = config.ProtocolVersion,
                    originalPlayers = players.Select(AssetDatabase.GetAssetPath).ToArray(), createdPlayers = replacements.Values.Select(AssetDatabase.GetAssetPath).ToArray(), nativePlayTested = false }, true));
                saved = true;
            }
            finally
            {
                if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
                if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
                if (!saved) AssetDatabase.DeleteAsset(folder);
            }
            AssetDatabase.Refresh(); EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != scenePath)).ToArray();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log("SILENCE: создана копия мира с локальной камерой и защитой позы рук: " + scenePath + ". V — вид от первого/третьего лица. Авторитетный взгляд/двери/фонарик не перемещены. Требуются Play и сборки Host/Client.");
            return scenePath;
        }
        public static bool ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play.");
            var manager = Manager(SceneManager.GetActiveScene()); var errors = new List<string>();
            if (manager.NetworkConfig.ProtocolVersion < 8) errors.Add("Нужна созданная копия мира с protocol >= 8.");
            if (!PlayerAvatarWorkshop.ValidateScene()) errors.Add("Не пройдена базовая проверка персонажа.");
            foreach (var prefab in Entries(manager).Select(e => e.Prefab).Append(manager.NetworkConfig.PlayerPrefab).Where(p => p != null && p.GetComponent<PlayerContext>() != null).Distinct())
            {
                var visual = prefab.GetComponent<PlayerAvatarPresentation>(); var model = visual != null ? visual.Model : null;
                if (prefab.GetComponents<PlayerCameraRig>().Length != 1 || model == null || model.GetComponents<CharacterArmPoseSafety>().Length != 1)
                    errors.Add("Нужны одна камера-риг и одна защита рук: " + prefab.name);
                var visibility = prefab.GetComponent<PlayerAvatarVisibility>(); if (visibility == null || visibility.BodyRenderers.Count == 0) errors.Add("Не настроены поверхности тела.");
            }
            Directory.CreateDirectory("Library/Silence"); string report = "SILENCE Camera/Body reference validation — Failures: " + errors.Count + "\n" + string.Join("\n", errors) + "\nReference checks only. Native Play, wall clearance, hands, camera rendering and multiplayer still need testing.\n";
            File.WriteAllText("Library/Silence/PlayerViewValidation.txt", report); if (errors.Count == 0) Debug.Log(report); else Debug.LogError(report); return errors.Count == 0;
        }
        [Serializable] private sealed class Installation { public string sourceScene, createdScene; public string[] originalPlayers, createdPlayers; public ushort protocol; public bool nativePlayTested; }
    }
}
