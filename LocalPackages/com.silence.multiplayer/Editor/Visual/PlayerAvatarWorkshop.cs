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
    public sealed class PlayerAvatarWorkshop : EditorWindow
    {
        [SerializeField] private PlayerAvatarDefinition definition;
        private string result;
        [MenuItem("SILENCE/21 Player Avatar Workshop")]
        public static void Open() => GetWindow<PlayerAvatarWorkshop>("SILENCE Player");
        public static void Show(PlayerAvatarDefinition ready) { var window = GetWindow<PlayerAvatarWorkshop>("SILENCE Player"); window.definition = ready; }
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Главный игровой персонаж", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Установка копирует открытую игровую сцену и её player prefab. Сначала открой сцену с нужным окружением. Для установки нужна готовая модель и AnimatorController; внешний вид этот инструмент не генерирует.", MessageType.Info);
            EditorGUILayout.LabelField("Источник сцены", SceneManager.GetActiveScene().path, EditorStyles.wordWrappedLabel);
            definition = (PlayerAvatarDefinition)EditorGUILayout.ObjectField("Модель и настройки", definition, typeof(PlayerAvatarDefinition), false);
            if (GUILayout.Button("Проверить модель и настройки"))
            {
                var errors = DefinitionErrors(definition);
                result = errors.Count == 0 ? "Проверка ссылок пройдена. Внешность и анимации требуют проверки в Play." : string.Join("\n", errors);
            }
            if (GUILayout.Button("Создать копию открытой сцены с персонажем")) Install(definition);
            if (GUILayout.Button("Проверить текущую сцену персонажа")) ValidateScene();
            if (GUILayout.Button("Собрать текущую сцену: Host и Client"))
            {
                if (ValidateScene()) NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path);
            }
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
        }
        public static List<string> DefinitionErrors(PlayerAvatarDefinition value)
        {
            var errors = new List<string>();
            if (value == null) { errors.Add("Создай Player Avatar Definition через Assets / Create / SILENCE."); return errors; }
            if (value.modelPrefab == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(value.modelPrefab)))
            { errors.Add("Укажи сохранённый art-only prefab или FBX главного персонажа."); return errors; }
            bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
            foreach (var vector in new[] { value.localPosition, value.localEulerAngles, value.localScale })
                if (!Finite(vector.x) || !Finite(vector.y) || !Finite(vector.z)) errors.Add("Некорректное положение, вращение или масштаб модели.");
            if (value.localScale.x <= 0 || value.localScale.y <= 0 || value.localScale.z <= 0) errors.Add("Масштаб модели должен быть положительным.");
            var animators = value.modelPrefab.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1) errors.Add("В модели должен быть один Animator.");
            else
            {
                var avatar = animators[0].avatar;
                if (avatar == null || !avatar.isValid || (value.requireHumanoid && !avatar.isHuman)) errors.Add("Импортируй подходящий Avatar; для Crossbow Girl требуется Humanoid.");
                bool inactive = !animators[0].enabled;
                for (var node = animators[0].transform; node != null; node = node.parent) inactive |= !node.gameObject.activeSelf;
                if (inactive) errors.Add("Animator модели должен быть активным.");
            }
            if (value.controller == null) errors.Add("Укажи AnimatorController с реальными анимациями.");
            else
            {
                RuntimeAnimatorController original = value.controller;
                for (int depth = 0; depth < 16 && original is AnimatorOverrideController; depth++) original = ((AnimatorOverrideController)original).runtimeAnimatorController;
                var controller = original as AnimatorController;
                if (controller == null) errors.Add("Не удалось прочитать параметры AnimatorController.");
                else
                {
                    foreach (string name in PlayerAvatarDefinition.FloatParameters)
                        if (!controller.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Float)) errors.Add("Animator: нужен float " + name);
                    foreach (string name in PlayerAvatarDefinition.BoolParameters)
                        if (!controller.parameters.Any(p => p.name == name && p.type == AnimatorControllerParameterType.Bool)) errors.Add("Animator: нужен bool " + name);
                }
                if (!value.controller.animationClips.Any(c => c != null && c.length > .05f)) errors.Add("В контроллере нет реальных анимационных клипов.");
                foreach (var clip in value.controller.animationClips.Where(c => c != null).Distinct())
                    if (AnimationUtility.GetAnimationEvents(clip).Length > 0) errors.Add("Удали AnimationEvents из отдельной графической копии клипа: " + clip.name);
            }
            var renderers = value.modelPrefab.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) errors.Add("В модели нет Renderer.");
            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(m => m == null || m.shader == null)) errors.Add("Отсутствует материал: " + renderer.name);
                if (renderer is SkinnedMeshRenderer skin && (skin.sharedMesh == null || skin.bones.Length == 0 || skin.bones.Any(b => b == null))) errors.Add("Некорректная skinned-сетка: " + renderer.name);
            }
            foreach (var component in value.modelPrefab.GetComponentsInChildren<Component>(true))
                if (component == null || component is MonoBehaviour || component is Collider || component is Rigidbody ||
                    component is Camera || component is AudioSource || component is AudioListener || component is UnityEngine.AI.NavMeshAgent || component is Light || component is Animation)
                    errors.Add("Модель должна содержать только графику, скелет и Animator: " + (component != null ? component.GetType().Name : "Missing script"));
            var hidden = new HashSet<Renderer>();
            foreach (string path in value.firstPersonHiddenRenderers ?? Array.Empty<string>())
            {
                var renderer = FindRenderer(value.modelPrefab, path);
                if (renderer == null) errors.Add("Не найден Renderer головы/волос: " + path);
                else if (!hidden.Add(renderer)) errors.Add("Renderer головы указан повторно: " + path);
            }
            if (hidden.Count == 0) errors.Add("Укажи отдельные Renderer головы, глаз и волос для вида от первого лица.");
            if (renderers.Length > 0 && !renderers.Except(hidden).Any(r => r.enabled)) errors.Add("Для рук и тела нужен отдельный видимый Renderer.");
            return errors;
        }
        private static Renderer FindRenderer(GameObject root, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var node = path == "." ? root.transform : root.transform.Find(path);
            return node != null ? node.GetComponent<Renderer>() : null;
        }
        public static void Install(PlayerAvatarDefinition value)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var errors = DefinitionErrors(value);
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var source = SceneManager.GetActiveScene(); string sourcePath = source.path;
            var managers = source.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if (string.IsNullOrEmpty(sourcePath) || managers.Length != 1) throw new InvalidOperationException("Open the latest saved game scene with one NetworkManager.");
            var oldPrefab = managers[0].NetworkConfig.PlayerPrefab;
            if (oldPrefab == null || oldPrefab.GetComponent<NetworkPlayer>() == null || oldPrefab.GetComponent<PlayerContext>() == null ||
                oldPrefab.GetComponentsInChildren<NetworkObject>(true).Length != 1) throw new InvalidOperationException("Existing network player prefab is not configured.");
            if (oldPrefab.GetComponent<PlayerAvatarPresentation>() != null) throw new InvalidOperationException("This player already has an avatar. Use the created scene to play, or open your improved source scene to create another copy.");
            string output = "Assets/_Silence/Generated/PlayerAvatar/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string target = output + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_Player.unity";
            VisualAssetLibrary.Folder(output);
            Scene copy = default; bool saved = false;
            try
            {
                string playerPath = output + "/Player.prefab";
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(oldPrefab), playerPath)) throw new IOException("Cannot copy existing player prefab.");
                var contents = PrefabUtility.LoadPrefabContents(playerPath);
                try
                {
                    var wrapper = new GameObject("Player Visual"); wrapper.transform.SetParent(contents.transform, false);
                    wrapper.transform.localPosition = value.localPosition; wrapper.transform.localRotation = Quaternion.Euler(value.localEulerAngles); wrapper.transform.localScale = value.localScale;
                    var art = (GameObject)PrefabUtility.InstantiatePrefab(value.modelPrefab);
                    PrefabUtility.UnpackPrefabInstance(art, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    art.transform.SetParent(wrapper.transform, false);
                    var animator = art.GetComponentsInChildren<Animator>(true).Single(); animator.runtimeAnimatorController = value.controller;
                    contents.AddComponent<PlayerAvatarState>();
                    contents.AddComponent<PlayerAvatarPresentation>().Configure(animator);
                    contents.AddComponent<PlayerAvatarFace>().Configure(art.GetComponentsInChildren<SkinnedMeshRenderer>(true));
                    var hidden = value.firstPersonHiddenRenderers.Select(p => FindRenderer(art, p)).ToArray();
                    contents.AddComponent<PlayerAvatarVisibility>().Configure(hidden);
                    contents.AddComponent<PlayerAvatarNetwork>();
                    var legacy = contents.transform.Find("Body_Graybox");
                    if (legacy != null) foreach (var renderer in legacy.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                    if (PrefabUtility.SaveAsPrefabAsset(contents, playerPath) == null) throw new IOException("Cannot save copied player prefab.");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                if (!EditorSceneManager.SaveScene(source, target, true)) throw new IOException("Cannot copy active scene.");
                AssetDatabase.Refresh(); copy = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive); SceneManager.SetActiveScene(copy);
                var manager = copy.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).Single();
                var newPlayer = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
                var config = JsonUtility.FromJson<NetworkConfig>(JsonUtility.ToJson(manager.NetworkConfig));
                if (config == null) throw new InvalidOperationException("Cannot copy network configuration.");
                var registry = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(registry, output + "/NetworkPrefabs.asset");
                var entries = manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l => l != null).SelectMany(l => l.PrefabList)
                    .Concat(manager.NetworkConfig.Prefabs.Prefabs).Distinct().Where(p => p != null && p.Prefab != oldPrefab);
                foreach (var entry in entries) registry.Add(entry);
                registry.Add(new NetworkPrefab { Prefab = newPlayer }); EditorUtility.SetDirty(registry);
                config.PlayerPrefab = newPlayer;
                config.ProtocolVersion = checked((ushort)Math.Max(5, manager.NetworkConfig.ProtocolVersion + 1));
                config.Prefabs = new NetworkPrefabs { NetworkPrefabsLists = new List<NetworkPrefabsList> { registry } };
                manager.NetworkConfig = config; EditorUtility.SetDirty(manager); AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(copy, target)) throw new IOException("Cannot save player scene.");
                File.WriteAllText(output + "/installation.json", JsonUtility.ToJson(new InstallationRecord {
                    sourceScene = sourcePath, createdScene = target, sourcePlayer = AssetDatabase.GetAssetPath(oldPrefab),
                    createdPlayer = playerPath, reference = value.referenceUrl, protocol = config.ProtocolVersion,
                    nativePlayTested = false }, true));
                saved = true;
            }
            finally
            {
                if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
                if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
                if (!saved) AssetDatabase.DeleteAsset(output);
            }
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(target, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != target)).ToArray();
            EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
            Debug.Log("SILENCE: copied player scene from " + sourcePath + " to " + target + ". Test actual first person, spectator, shadows, animation and rebuild BOTH Host and Client with the same new prefab.");
        }
        public static bool ValidateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before reference validation.");
            var scene = SceneManager.GetActiveScene(); var errors = new List<string>();
            if (string.IsNullOrEmpty(scene.path) || scene.isDirty) errors.Add("Сохрани текущую сцену.");
            var managers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if (managers.Length != 1) errors.Add("В сцене должен быть один NetworkManager.");
            else
            {
                var manager = managers[0]; var player = manager.NetworkConfig.PlayerPrefab;
                if (manager.NetworkConfig.ProtocolVersion < 5 || !manager.NetworkConfig.ForceSamePrefabs) errors.Add("Нужны новый протокол и ForceSamePrefabs.");
                if (player == null) errors.Add("Не задан player prefab.");
                else
                {
                    if (player.GetComponent<PlayerContext>() == null || player.GetComponent<NetworkPlayer>() == null || player.GetComponent<FirstPersonMotor>() == null ||
                        player.GetComponentsInChildren<NetworkObject>(true).Length != 1) errors.Add("Нарушена исходная структура сетевого игрока.");
                    var context = player.GetComponent<PlayerContext>();
                    if (context != null && (context.Settings == null || context.Input == null || context.View == null || !context.View.transform.IsChildOf(player.transform))) errors.Add("Не сохранены исходные настройки, ввод или камера игрока.");
                    var visual = player.GetComponent<PlayerAvatarPresentation>();
                    if (visual == null || visual.Model == null || visual.Model.applyRootMotion || visual.Model.runtimeAnimatorController == null) errors.Add("Не настроен Animator без root motion.");
                    if (player.GetComponent<PlayerAvatarState>() == null || player.GetComponent<PlayerAvatarNetwork>() == null) errors.Add("Не подключены подтверждённые сетевые состояния персонажа.");
                    var visibility = player.GetComponent<PlayerAvatarVisibility>();
                    if (visibility == null || visibility.HiddenRenderers.Count == 0 || visibility.HiddenRenderers.Any(r => r == null)) errors.Add("Не настроено скрытие головы для нужной камеры.");
                    if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l => l != null).SelectMany(l => l.PrefabList).Any(p => p.Prefab == player)) errors.Add("Новый player prefab не сохранён в NetworkPrefabsList.");
                    foreach (var t in player.GetComponentsInChildren<Transform>(true))
                        if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0) errors.Add("Missing script: " + t.name);
                }
            }
            string report = "SILENCE player reference validation — Failures: " + errors.Count + "\nScene: " + scene.path + "\n" + string.Join("\n", errors) +
                "\nReference checks only. Native Play, animation quality, camera rendering and multiplayer tests still required.\n";
            Directory.CreateDirectory("Library/Silence"); File.WriteAllText("Library/Silence/PlayerAvatarValidation.txt", report);
            if (errors.Count == 0) Debug.Log(report); else Debug.LogError(report);
            return errors.Count == 0;
        }
        [Serializable] private sealed class InstallationRecord
        {
            public string sourceScene, createdScene, sourcePlayer, createdPlayer, reference;
            public ushort protocol;
            public bool nativePlayTested;
        }
    }
}
