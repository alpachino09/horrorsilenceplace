using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Editor;
using Silence.Interactions;
using Silence.Inventory;
using Silence.Networking;
using Silence.Player;
using Silence.SoundSimulation;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor
{
    public static class NetworkFarmGenerator
    {
        public const string Output = "Assets/_Silence/Generated/Sprint02";
        public const string ScenePath = Output + "/Farm_Network.unity";
        public const string PlayerPath = Output + "/NetworkPlayer.prefab";
        private const string Marker = Output + "/silence-network-generated.txt";

        [MenuItem("SILENCE/04 Generate Network Farm")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(NetworkManager).Assembly);
            if (package == null || package.version != "2.7.0")
                throw new InvalidOperationException("This adapter targets official Netcode for GameObjects 2.7.0. Installed: " + package?.version);
            if (AssetDatabase.IsValidFolder(Output))
            {
                if (File.Exists(Marker) && File.Exists(ScenePath) && File.Exists(PlayerPath))
                { RegisterScene(); Debug.Log("SILENCE: existing network scene preserved: " + ScenePath); return; }
                throw new InvalidOperationException("Network output already exists or is partial. Preserve/rename it before generating: " + Output);
            }
            if (!File.Exists(SilenceSceneGenerator.FarmPath) ||
                !File.Exists(SilenceSceneGenerator.Output + "/Player.prefab"))
                throw new InvalidOperationException("Run SILENCE / 01 Generate Farm Prototype first.");
            Scene previous = SceneManager.GetActiveScene();
            Scene source = SceneManager.GetSceneByPath(SilenceSceneGenerator.FarmPath);
            bool loadedSource = !source.IsValid() || !source.isLoaded;
            Scene network = default;
            try
            {
                if (loadedSource) source = EditorSceneManager.OpenScene(SilenceSceneGenerator.FarmPath, OpenSceneMode.Additive);
                if (source.isDirty) throw new InvalidOperationException("Save the offline farm before making a network copy.");
                AssetDatabase.CreateFolder("Assets/_Silence/Generated", "Sprint02");
                CreatePlayerPrefab();
                if (!EditorSceneManager.SaveScene(source, ScenePath, true)) throw new IOException("Cannot copy farm scene.");
                AssetDatabase.Refresh();
                network = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                SceneManager.SetActiveScene(network);
                foreach (var root in network.GetRootGameObjects())
                {
                    foreach (var bootstrap in root.GetComponentsInChildren<FarmBootstrap>(true)) Object.DestroyImmediate(bootstrap);
                    foreach (var authority in root.GetComponentsInChildren<OfflineWorldAuthority>(true)) Object.DestroyImmediate(authority);
                    foreach (var actor in root.GetComponentsInChildren<PlayerContext>(true)) Object.DestroyImmediate(actor.gameObject);
                }
                foreach (var root in network.GetRootGameObjects())
                    foreach (var target in root.GetComponentsInChildren<Interactable>(true))
                    {
                        if (target.GetComponent<NetworkObject>() == null) target.gameObject.AddComponent<NetworkObject>();
                        target.gameObject.AddComponent<NetworkInteractable>();
                    }
                var worldObject = new GameObject("AuthoritativeWorld");
                worldObject.AddComponent<NetworkObject>();
                worldObject.AddComponent<NoiseSystem>();
                var world = worldObject.AddComponent<NetworkWorldAuthority>();
                var networkObject = new GameObject("NetworkManager");
                var transport = networkObject.AddComponent<UnityTransport>();
                var manager = networkObject.AddComponent<NetworkManager>();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
                manager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport, PlayerPrefab = prefab,
                    TickRate = 30, ConnectionApproval = true, ProtocolVersion = 2,
                    EnableSceneManagement = true, ForceSamePrefabs = true,
                    ClientConnectionBufferTimeout = 15
                };
                manager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
                var cameraObject = new GameObject("LobbyCamera");
                cameraObject.transform.SetPositionAndRotation(new Vector3(0, 4, -16), Quaternion.Euler(12, 0, 0));
                var camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                var session = new GameObject("NetworkSession").AddComponent<NetworkSession>();
                session.Configure(manager, transport, world, camera);
                session.gameObject.AddComponent<NetworkSmokeProbe>().Configure(CreateSmokeFixture());
                if (!EditorSceneManager.SaveScene(network, ScenePath)) throw new IOException("Cannot save network scene.");
                File.WriteAllText(Marker, "SILENCE Sprint02 network scene. Never automatically overwritten.\n");
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                RegisterScene();
                Debug.Log("SILENCE: network scene created. Open " + ScenePath + " and press Play. Offline farm preserved.");
            }
            finally
            {
                if (network.IsValid() && network.isLoaded) EditorSceneManager.CloseScene(network, true);
                if (loadedSource && source.IsValid() && source.isLoaded) EditorSceneManager.CloseScene(source, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static void CreatePlayerPrefab()
        {
            if (!AssetDatabase.CopyAsset(SilenceSceneGenerator.Output + "/Player.prefab", PlayerPath))
                throw new IOException("Cannot copy offline player prefab to network output.");
            // Edit the new asset in its own context so NGO sees its new prefab GUID.
            var player = PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                player.name = "NetworkPlayer";
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                player.AddComponent<NetworkObject>();
                player.AddComponent<NetworkPlayer>();
                player.GetComponent<FirstPersonMotor>().enabled = false;
                player.GetComponent<PlayerLook>().enabled = false;
                player.GetComponent<PlayerFlashlight>().enabled = false;
                var context = player.GetComponent<PlayerContext>();
                context.Input.enabled = false;
                context.View.gameObject.SetActive(true);
                context.View.enabled = false;
                context.View.GetComponent<AudioListener>().enabled = false;
                if (PrefabUtility.SaveAsPrefabAsset(player, PlayerPath) == null) throw new IOException("Cannot save network prefab.");
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
        }

        private static void RegisterScene()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == ScenePath);
            if (index < 0) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static ItemPickup CreateSmokeFixture()
        {
            // Outside the playable farm; used only by the development-build smoke probe.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "NetworkSmokeFloor";
            floor.transform.position = new Vector3(80, -.25f, 0);
            floor.transform.localScale = new Vector3(10, .5f, 10);
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "NetworkSmokeContestedPickup";
            item.transform.position = new Vector3(80, 1.1f, 0);
            item.transform.localScale = Vector3.one * .25f;
            var pickup = item.AddComponent<ItemPickup>();
            pickup.Configure(AssetDatabase.LoadAssetAtPath<ItemDefinition>(SilenceSceneGenerator.Output + "/battery.asset"));
            item.AddComponent<NetworkObject>();
            item.AddComponent<NetworkInteractable>();
            return pickup;
        }

        [MenuItem("SILENCE/05 Build Host and Client")]
        public static void BuildPlayers()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("Generate network farm first.");
            if (!NetworkProjectValidator.Validate()) throw new InvalidOperationException("Network validation failed. See Library/Silence/NetworkValidation.txt");
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            if (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneLinux64)
                throw new InvalidOperationException("Select Windows, Mac or Linux standalone in Build Profiles first.");
            string folder = EditorUtility.SaveFolderPanel("SILENCE: host and client builds", Path.GetFullPath("."), "Builds");
            if (string.IsNullOrEmpty(folder)) return;
            string extension = target == BuildTarget.StandaloneOSX ? ".app" : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
            foreach (string role in new[] { "Host", "Client" })
            {
                string location = Path.Combine(folder, role, "SILENCE" + extension);
                Directory.CreateDirectory(Path.GetDirectoryName(location));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = location,
                    target = target, options = BuildOptions.Development
                });
                if (report.summary.result != BuildResult.Succeeded)
                    throw new InvalidOperationException(role + " build failed: " + report.summary.result);
            }
            Debug.Log("SILENCE: Host and Client builds completed in " + folder + ". Run both and test connections; build success alone is not a network test.");
        }

        [MenuItem("SILENCE/07 Launch Host and Client")]
        public static void LaunchPlayers()
        {
            string folder = EditorUtility.OpenFolderPanel("Select folder containing Host and Client builds", Path.GetFullPath("."), "");
            if (string.IsNullOrEmpty(folder)) return;
            bool mac = Application.platform == RuntimePlatform.OSXEditor;
            string extension = mac ? ".app" : Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : "";
            string[] paths = new[] { "Host", "Client" }.Select(role => Path.Combine(folder, role, "SILENCE" + extension)).ToArray();
            if (paths.Any(path => mac ? !Directory.Exists(path) : !File.Exists(path)))
                throw new FileNotFoundException("Select the build folder containing both Host/SILENCE and Client/SILENCE. Run menu 05 first.");
            foreach (string path in paths)
            {
                var start = new System.Diagnostics.ProcessStartInfo { UseShellExecute = false };
                if (mac)
                {
                    start.FileName = "/usr/bin/open";
                    // LaunchServices otherwise may focus an existing app with the same bundle identifier.
                    start.Arguments = "-n \"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                }
                else start.FileName = path;
                using (System.Diagnostics.Process.Start(start)) { }
            }
        }
    }
}
