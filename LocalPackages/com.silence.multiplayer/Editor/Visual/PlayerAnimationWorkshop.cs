using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Player;
using Silence.Graphics.Players;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class PlayerAnimationWorkshop : EditorWindow
    {
        private string message;
        private const string OutputRoot = "Assets/_Silence/Generated/HumanMotion";
        [MenuItem("SILENCE/25 Human Animation Workshop")]
        public static void Open() => GetWindow<PlayerAnimationWorkshop>("SILENCE Animation");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Basic Motions → наши персонажи", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Сначала открой свой игровой мир с уже установленной героиней (не просмотр 23). Установка сохраняет отдельную копию этого мира и player prefab. Исходная сцена, модели, материалы и контроллеры сохраняются.", MessageType.Info);
            EditorGUILayout.LabelField("Открытая сцена", SceneManager.GetActiveScene().path, EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("1. Импортировать присланный Basic Motions пакет")) Run(BasicMotionsCatalog.ImportSuppliedPackage);
            if (GUILayout.Button("Проверить все 23 клипа пакета")) Run(() => { BasicMotionsCatalog.Load(); message = "Найдены 23 реальных Humanoid-клипа. Их визуальное качество проверяется в Play."; });
            if (GUILayout.Button("2. Подключить анимации к КОПИИ текущего мира")) Run(() => { string path = Install(); if (!string.IsNullOrEmpty(path)) message = "Создан мир с анимациями: " + path + ". Он уже открыт. Play → Создать сессию → Начать раунд."; });
            if (GUILayout.Button("Проверить ссылки и настройку текущего мира")) Run(() => { if (ValidateCurrent()) message = "Ссылки проверены. Далее нужны Play и отдельные Host/Client."; });
            if (GUILayout.Button("3. Создать отдельную площадку проверки")) Run(() => AnimationLabBuilder.Create());
            if (GUILayout.Button("Вернуться в последний мир с анимациями")) Run(OpenLastWorld);
            if (GUILayout.Button("Собрать текущий мир: Host и Client")) Run(() => { if (ValidateCurrent()) NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path); });
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        }
        private void Run(Action action)
        { try { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play."); action(); } catch (Exception e) { message = e.Message; Debug.LogException(e); } }
        public static void OpenLastWorld()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!AssetDatabase.IsValidFolder(OutputRoot)) throw new InvalidOperationException("Сначала выполни шаг 2. Исходный мир можно открыть через Project, как раньше.");
            string path = AssetDatabase.FindAssets("t:Scene", new[] { OutputRoot }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith("_Motion.unity", StringComparison.Ordinal)).OrderByDescending(p => p, StringComparer.Ordinal).FirstOrDefault();
            if (path == null) throw new InvalidOperationException("Сначала выполни шаг 2. Исходный мир можно открыть через Project, как раньше.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
        private static NetworkManager Manager(Scene scene)
        {
            var managers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if (managers.Length != 1) throw new InvalidOperationException("Открой свой сохранённый игровой мир с одним NetworkManager. Просмотр героини 23 — отдельная сцена.");
            return managers[0];
        }
        private static IEnumerable<NetworkPrefab> Entries(NetworkManager manager) => manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l => l != null).SelectMany(l => l.PrefabList)
            .Concat(manager.NetworkConfig.Prefabs.Prefabs).Where(p => p != null && p.Prefab != null).GroupBy(p => p.Prefab).Select(g => g.First());
        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play.");
            BasicMotionsCatalog.Load();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return null;
            Scene source = SceneManager.GetActiveScene(); string sourcePath = source.path;
            if (string.IsNullOrEmpty(sourcePath) || source.isDirty) throw new InvalidOperationException("Сохрани открытый игровой мир перед установкой.");
            var sourceManager = Manager(source); var original = sourceManager.NetworkConfig.PlayerPrefab;
            if (original == null || original.GetComponent<PlayerAvatarPresentation>() == null)
                throw new InvalidOperationException("В этом мире ещё нет игровой модели героини. Сначала SILENCE → 21 → Создать копию открытой сцены с персонажем; затем вернись сюда. Старую Campaign не генерируй заново.");
            var entries = Entries(sourceManager).ToArray();
            var players = entries.Select(p => p.Prefab).Append(original).Distinct().Where(p => p.GetComponent<PlayerContext>() != null).ToArray();
            foreach (var player in players)
            {
                var visual = player.GetComponent<PlayerAvatarPresentation>();
                if (visual == null || visual.Model == null || visual.Model.avatar == null || !visual.Model.avatar.isHuman || !visual.Model.avatar.isValid ||
                    player.GetComponent<FirstPersonMotor>() == null || player.GetComponent<NetworkPlayer>() == null || player.GetComponentsInChildren<NetworkObject>(true).Length != 1)
                    throw new InvalidOperationException("Сетевому игроку нужна корректная Humanoid-модель через меню 21: " + player.name);
                if (entries.Any(e => e.Prefab == player && e.Override != NetworkPrefabOverride.None))
                    throw new InvalidOperationException("Player prefab использует override в сетевом реестре; требуется явная проверка этого варианта: " + player.name);
            }
            string output = OutputRoot + "/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string target = output + "/" + Path.GetFileNameWithoutExtension(sourcePath) + "_Motion.unity";
            VisualAssetLibrary.Folder(output); Scene copy = default; bool success = false;
            try
            {
                var replacements = new Dictionary<GameObject, GameObject>();
                for (int i = 0; i < players.Length; i++)
                {
                    string folder = output + "/Player" + i; VisualAssetLibrary.Folder(folder);
                    string playerPath = folder + "/Player.prefab";
                    if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(players[i]), playerPath)) throw new IOException("Не удалось скопировать player prefab.");
                    var contents = PrefabUtility.LoadPrefabContents(playerPath);
                    try
                    {
                        var actor = contents.GetComponent<PlayerContext>(); var visual = contents.GetComponent<PlayerAvatarPresentation>(); var animator = visual.Model;
                        var profile = PlayerAnimationControllerBuilder.Build(animator, folder);
                        animator.runtimeAnimatorController = profile.controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        Transform facing = animator.transform;
                        while (facing.parent != null && facing.parent != contents.transform) facing = facing.parent;
                        if (facing == contents.transform || facing.parent != contents.transform) throw new InvalidOperationException("Графическая модель должна быть отдельним дочерним объектом игрока.");
                        visual.ConfigureAnimation(profile, facing);
                        contents.GetComponent<FirstPersonMotor>().ConfigureAnimationMotion(true);
                        if (contents.GetComponent<PlayerAvatarState>() == null) contents.AddComponent<PlayerAvatarState>();
                        if (contents.GetComponent<PlayerAvatarNetwork>() == null) contents.AddComponent<PlayerAvatarNetwork>();
                        (animator.GetComponent<CharacterIKController>() ?? animator.gameObject.AddComponent<CharacterIKController>()).Configure(actor, profile);
                        (animator.GetComponent<CharacterAnimationActions>() ?? animator.gameObject.AddComponent<CharacterAnimationActions>()).Configure(actor, profile);
                        (animator.GetComponent<FootstepAnimationBridge>() ?? animator.gameObject.AddComponent<FootstepAnimationBridge>()).Configure(actor);
                        if (PrefabUtility.SaveAsPrefabAsset(contents, playerPath) == null) throw new IOException("Не удалось сохранить анимированного игрока.");
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                    replacements.Add(players[i], AssetDatabase.LoadAssetAtPath<GameObject>(playerPath));
                }
                if (!EditorSceneManager.SaveScene(source, target, true)) throw new IOException("Не удалось создать копию мира.");
                AssetDatabase.Refresh(); copy = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive); SceneManager.SetActiveScene(copy);
                var manager = Manager(copy);
                var config = JsonUtility.FromJson<NetworkConfig>(JsonUtility.ToJson(manager.NetworkConfig));
                if (config == null) throw new InvalidOperationException("Не удалось скопировать NetworkConfig.");
                var registry = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(registry, output + "/NetworkPrefabs.asset");
                foreach (var entry in entries)
                    if (!replacements.ContainsKey(entry.Prefab)) registry.Add(entry);
                foreach (var prefab in replacements.Values) registry.Add(new NetworkPrefab { Prefab = prefab });
                config.PlayerPrefab = replacements[original]; config.ForceSamePrefabs = true;
                config.ProtocolVersion = checked((ushort)Math.Max(7, sourceManager.NetworkConfig.ProtocolVersion + 1));
                config.Prefabs = new NetworkPrefabs { NetworkPrefabsLists = new List<NetworkPrefabsList> { registry } };
                manager.NetworkConfig = config; EditorUtility.SetDirty(manager); EditorUtility.SetDirty(registry); AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(copy, target)) throw new IOException("Не удалось сохранить мир с анимациями.");
                File.WriteAllText(output + "/installation.json", JsonUtility.ToJson(new InstallationRecord { sourceScene = sourcePath, createdScene = target,
                    originalPlayers = players.Select(AssetDatabase.GetAssetPath).ToArray(), createdPlayers = replacements.Values.Select(AssetDatabase.GetAssetPath).ToArray(),
                    protocol = config.ProtocolVersion, basicMotionsGameplayClips = PlayerAnimationControllerBuilder.GameplayBasicNames.Length, nativePlayTested = false }, true));
                success = true;
            }
            finally
            {
                if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
                if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
                if (!success) AssetDatabase.DeleteAsset(output);
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.Refresh();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(target, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != target)).ToArray();
            EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
            Debug.Log("SILENCE Human Motion: создана копия текущего мира " + target + ". 14 Basic Motions + 13 собственных клипов на каждого Avatar. Original: " + sourcePath + ". Сначала проверь Play; затем пересобери ОБА Host и Client. Визуальная/сетевая проверка автоматически не заявляется.");
            return target;
        }
        public static bool ValidateCurrent()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Останови Play.");
            var manager = Manager(SceneManager.GetActiveScene()); var prefab = manager.NetworkConfig.PlayerPrefab;
            var errors = new List<string>();
            if (!PlayerAvatarWorkshop.ValidateScene()) errors.Add("Не пройдена проверка базового персонажа (меню 21).");
            if (manager.NetworkConfig.ProtocolVersion < 7) errors.Add("Нужен протокол Human Motion >= 7.");
            foreach (var player in Entries(manager).Select(e => e.Prefab).Append(prefab).Where(p => p != null && p.GetComponent<PlayerContext>() != null).Distinct())
            {
                var visual = player.GetComponent<PlayerAvatarPresentation>(); var animator = visual != null ? visual.Model : null;
                if (animator == null || visual.AnimationProfile == null || animator.runtimeAnimatorController != visual.AnimationProfile.controller || animator.applyRootMotion || !animator.isHuman)
                { errors.Add("Не настроена система анимаций: " + player.name); continue; }
                if (player.GetComponent<FirstPersonMotor>() == null || !player.GetComponent<FirstPersonMotor>().AnimationMotion ||
                    animator.GetComponents<CharacterIKController>().Length != 1 || animator.GetComponents<CharacterAnimationActions>().Length != 1 || animator.GetComponents<FootstepAnimationBridge>().Length != 1)
                    errors.Add("Отсутствуют или дублируются компоненты: " + player.name);
                var controller = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                if (controller == null || !controller.layers.All(l => l.iKPass)) errors.Add("Проверь IK Pass на слоях.");
                if (controller != null && controller.animationClips.Any(c => c == null || c.legacy || !c.isHumanMotion)) errors.Add("Обнаружен неподходящий клип.");
            }
            Directory.CreateDirectory("Library/Silence");
            string report = "SILENCE Human Motion reference validation — Failures: " + errors.Count + "\nScene: " + SceneManager.GetActiveScene().path + "\n" + string.Join("\n", errors) + "\nReference validation only; native Play/retarget appearance/network checks remain required.\n";
            File.WriteAllText("Library/Silence/HumanMotionValidation.txt", report);
            if (errors.Count == 0) Debug.Log(report); else Debug.LogError(report);
            return errors.Count == 0;
        }
        [Serializable] private sealed class InstallationRecord
        {
            public string sourceScene, createdScene;
            public string[] originalPlayers, createdPlayers;
            public ushort protocol;
            public int basicMotionsGameplayClips;
            public bool nativePlayTested;
        }
    }
}
