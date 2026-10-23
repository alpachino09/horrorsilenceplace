using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Audio;
using Silence.Config;
using Silence.Core;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Interactions;
using Silence.Player;
using Silence.SoundSimulation;
using Silence.UI;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor
{
    public static class CampaignSceneGenerator
    {
        public const string Output = "Assets/_Silence/Generated/Gameplay";
        public const string ScenePath = Output + "/Campaign.unity";
        public const string PlayerPath = Output + "/GamePlayer.prefab";
        public const string SettingsPath = Output + "/GameSettings.asset";
        private const string Marker = Output + "/silence-gameplay-generated.txt";

        [MenuItem("SILENCE/09 Generate Campaign Gameplay")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (AssetDatabase.IsValidFolder(Output))
            {
                if (File.Exists(Marker) && File.Exists(ScenePath) && File.Exists(PlayerPath) && File.Exists(SettingsPath))
                { RegisterScene(); Debug.Log("SILENCE: existing campaign preserved: " + ScenePath); return; }
                throw new InvalidOperationException("Partial or existing Gameplay folder. Preserve it by renaming it in Project before generating again.");
            }
            if (!File.Exists(NetworkFarmGenerator.ScenePath) || !File.Exists(NetworkFarmGenerator.PlayerPath))
                throw new InvalidOperationException("Run menu 04 first. Campaign generation copies that scene without changing it.");
            if (Shader.Find("Universal Render Pipeline/Lit") == null) throw new InvalidOperationException("URP Lit shader is required.");
            if (NavMesh.GetSettingsCount() == 0) throw new InvalidOperationException("Create a Humanoid navigation agent in Window / AI / Navigation first.");
            Scene previous = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(NetworkFarmGenerator.ScenePath), scene = default;
            bool opened = !source.IsValid() || !source.isLoaded;
            try
            {
                if (opened) source = EditorSceneManager.OpenScene(NetworkFarmGenerator.ScenePath, OpenSceneMode.Additive);
                if (source.isDirty) throw new InvalidOperationException("Save the network farm before copying it.");
                AssetDatabase.CreateFolder("Assets/_Silence/Generated", "Gameplay");
                AssetDatabase.CreateFolder(Output, "Materials"); AssetDatabase.CreateFolder(Output, "Chapters");
                CreatePlayer();
                if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("Cannot copy source scene.");
                AssetDatabase.Refresh(); scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var probe in root.GetComponentsInChildren<NetworkSmokeProbe>(true)) Object.DestroyImmediate(probe);
                    if (root.name.StartsWith("NetworkSmoke", StringComparison.Ordinal)) Object.DestroyImmediate(root);
                }
                T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
                var world = All<NetworkWorldAuthority>().Single();
                var game = world.gameObject.AddComponent<GameplayDirector>();
                var settings = ScriptableObject.CreateInstance<GameplaySettings>();
                settings.chapters = new ChapterDefinition[7]; AssetDatabase.CreateAsset(settings, SettingsPath);
                var builder = new CampaignWorldBuilder(game);
                var farm = All<Transform>().First(t => t.name == "Farm_Graybox");
                foreach (var old in All<PrototypeDoor>())
                {
                    var go = old.gameObject; Object.DestroyImmediate(old);
                    go.AddComponent<ManualDoor>().Configure(game); AddDoorObstacle(go);
                }
                foreach (var old in All<PrototypeRadio>())
                { var go = old.gameObject; Object.DestroyImmediate(old); go.AddComponent<MissionRadio>(); }
                foreach (var old in All<Silence.Missions.PrototypeExit>())
                { var go = old.gameObject; Object.DestroyImmediate(old); go.AddComponent<ChapterExit>(); }
                builder.DressFarm(farm);
                Transform[] maps = new Transform[7]; maps[0] = farm;
                for (int i = 0; i < 7; i++)
                {
                    var chapter = builder.CreateDefinition(i); settings.chapters[i] = chapter;
                    if (i > 0) maps[i] = builder.CreateMap(chapter);
                    builder.AddSupplies(maps[i], i == 0);
                }
                var enemy = builder.CreateCreature();
                game.Configure(settings, enemy);
                world.gameObject.AddComponent<GameplayWorldNetwork>();
                world.gameObject.AddComponent<WorldAudio>();
                All<NetworkSession>().Single().gameObject.AddComponent<GameplaySmokeProbe>();
                var atmosphere = world.gameObject.AddComponent<WorldAtmosphere>(); atmosphere.game = game;
                atmosphere.sun = All<Light>().FirstOrDefault(l => l.type == LightType.Directional);
                foreach (var target in All<Interactable>()) EnsureNetwork(target.gameObject);
                var manager = All<NetworkManager>().Single();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                // Replace the copy's prefab registry; old generated scenes keep their original GUIDs and config.
                var transport = manager.NetworkConfig.NetworkTransport;
                manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, PlayerPrefab = prefab,
                    TickRate = 30, ConnectionApproval = true, ProtocolVersion = 3, EnableSceneManagement = true,
                    ForceSamePrefabs = true, ClientConnectionBufferTimeout = 15 };
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                foreach (var camera in All<Camera>()) camera.farClipPlane = 115;
                for (int i = 0; i < maps.Length; i++) BakeNavigation(maps[i], settings.chapters[i]);
                EditorUtility.SetDirty(settings); AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Cannot save campaign.");
                File.WriteAllText(Marker, "SILENCE gameplay 0.3.0. Generated assets are preserved on subsequent runs.\n");
                AssetDatabase.Refresh(); RegisterScene();
                Debug.Log("SILENCE: campaign created at " + ScenePath + ". Run menu 11, then open Campaign and press Play. Native gameplay testing is still required.");
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (opened && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        private static void CreatePlayer()
        {
            if (!AssetDatabase.CopyAsset(NetworkFarmGenerator.PlayerPath, PlayerPath)) throw new IOException("Cannot copy player prefab.");
            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                player.name = "GamePlayer";
                player.AddComponent<ActorGameplayInput>(); player.AddComponent<GameplayHud>();
                player.AddComponent<MicrophoneNoiseMeter>(); player.AddComponent<SpectatorView>();
                player.AddComponent<TeammateAid>(); player.AddComponent<NetworkInteractable>();
                var target = new GameObject("TeammateTarget"); target.transform.SetParent(player.transform, false);
                var capsule = target.AddComponent<CapsuleCollider>(); capsule.height = 1.8f;
                capsule.radius = .3f; capsule.center = Vector3.up * .9f; capsule.enabled = false;
                player.AddComponent<GameplayActorNetwork>();
                player.GetComponent<PlayerContext>().View.farClipPlane = 115;
                if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPath) == null) throw new IOException("Cannot save game player.");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }
        internal static void EnsureNetwork(GameObject go)
        {
            if (go.GetComponent<NetworkObject>() == null) go.AddComponent<NetworkObject>();
            if (go.GetComponent<NetworkInteractable>() == null) go.AddComponent<NetworkInteractable>();
        }
        internal static void AddDoorObstacle(GameObject door)
        {
            var leaf = door.GetComponentInChildren<BoxCollider>();
            if (leaf == null) return;
            var obstacle = leaf.gameObject.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = leaf.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = leaf.center;
            obstacle.size = leaf.size; obstacle.carving = true; obstacle.carveOnlyStationary = false;
        }
        private static void BakeNavigation(Transform root, ChapterDefinition chapter)
        {
            var sources = new System.Collections.Generic.List<NavMeshBuildSource>();
            var markups = new System.Collections.Generic.List<NavMeshBuildMarkup>();
            foreach (var target in root.GetComponentsInChildren<Interactable>(true))
                markups.Add(new NavMeshBuildMarkup { root = target.transform, ignoreFromBuild = true });
            NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            var config = NavMesh.GetSettingsByIndex(0); config.agentRadius = .4f;
            config.agentHeight = 2; config.agentClimb = .3f; config.agentSlope = 45;
            var data = NavMeshBuilder.BuildNavMeshData(config, sources,
                new Bounds(chapter.center + Vector3.up * 4, new Vector3(110, 24, 110)), Vector3.zero, Quaternion.identity);
            if (data == null) throw new InvalidOperationException("Navigation bake failed: " + chapter.id);
            AssetDatabase.CreateAsset(data, Output + "/Chapters/" + chapter.id + "Navigation.asset");
            root.gameObject.AddComponent<RuntimeNavigation>().Configure(data);
        }
        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
        [MenuItem("SILENCE/12 Run Solo Gameplay Checks")]
        public static void RunChecks()
        {
            if (!CampaignProjectValidator.Validate()) throw new InvalidOperationException("Run menu 11 and fix campaign asset validation first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Environment.SetEnvironmentVariable("SILENCE_GAMEPLAY_CHECKS", Path.GetFullPath("Library/Silence/GameplayChecks.json"));
            EditorApplication.isPlaying = true;
        }
        [MenuItem("SILENCE/10 Build Campaign Host and Client")]
        public static void Build()
        {
            if (!CampaignProjectValidator.Validate()) throw new InvalidOperationException("Campaign validation failed. See Library/Silence/CampaignValidation.txt");
            NetworkFarmGenerator.BuildScene(ScenePath);
        }
    }
}
