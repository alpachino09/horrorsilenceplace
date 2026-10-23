using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Interactions;
using Silence.Networking;
using Silence.Player;

namespace Silence.Multiplayer.Editor
{
    public static class NetworkProjectValidator
    {
        [MenuItem("SILENCE/06 Validate Network Farm")]
        public static void ValidateMenu() => Validate();
        public static void ValidateBatch()
        {
            if (!Validate()) throw new InvalidOperationException("SILENCE network validation failed.");
        }
        public static bool Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before scene validation.");
            var report = new StringBuilder();
            int failures = 0;
            void Check(bool success, string label)
            { report.AppendLine((success ? "PASS " : "FAIL ") + label); if (!success) failures++; }
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Unity: " + Application.unityVersion);
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (var package in packages.OrderBy(p => p.name)) report.AppendLine("PACKAGE " + package.name + " " + package.version);
            Check(packages.Any(p => p.name == "com.unity.netcode.gameobjects" && p.version == "2.7.0"), "NGO 2.7.0");
            Check(packages.Any(p => p.name == "com.unity.transport" && p.version == "2.6.0"), "Transport 2.6.0");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkFarmGenerator.PlayerPath);
            Check(prefab != null, "Network player prefab exists");
            if (prefab != null)
            {
                Check(prefab.GetComponent<NetworkObject>() != null && prefab.GetComponent<NetworkPlayer>() != null,
                    "NetworkObject and NetworkPlayer on prefab root");
                var context = prefab.GetComponent<PlayerContext>();
                Check(context != null && context.View != null && context.Settings != null &&
                    context.Input != null && context.Input.Template != null, "Player references assigned");
                Check(prefab.GetComponentsInChildren<Camera>(true).Length == 1 &&
                    prefab.GetComponentsInChildren<AudioListener>(true).Length == 1, "One player camera and listener");
                var motor = prefab.GetComponent<FirstPersonMotor>();
                var look = prefab.GetComponent<PlayerLook>();
                Check(motor != null && look != null && !motor.enabled && !look.enabled,
                    "Autonomous offline movement/look disabled");
                Check(prefab.GetComponent<CharacterController>() != null, "CharacterController assigned");
            }
            Check(File.Exists(NetworkFarmGenerator.ScenePath), "Network scene exists");
            Check(EditorBuildSettings.scenes.Any(s => s.path == NetworkFarmGenerator.ScenePath && s.enabled), "Network scene registered for build");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(NetworkFarmGenerator.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (File.Exists(NetworkFarmGenerator.ScenePath))
                {
                    if (opened) scene = EditorSceneManager.OpenScene(NetworkFarmGenerator.ScenePath, OpenSceneMode.Additive);
                    Check(!scene.isDirty, "Network scene saved before build");
                    var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Select(t => t.gameObject).ToArray();
                    T[] Components<T>() where T : Component => objects.SelectMany(o => o.GetComponents<T>()).ToArray();
                    Check(Components<NetworkManager>().Length == 1 && Components<UnityTransport>().Length == 1, "One manager/transport");
                    Check(Components<NetworkSession>().Length == 1 && Components<NetworkWorldAuthority>().Length == 1, "One session/world authority");
                    Check(Components<OfflineWorldAuthority>().Length == 0 && Components<FarmBootstrap>().Length == 0,
                        "Offline authority removed from network copy");
                    Check(Components<PlayerContext>().Length == 0, "Players spawn through connection approval");
                    Check(Components<Camera>().Length == 1 && Components<AudioListener>().Length == 1, "One lobby camera/listener before connection");
                    foreach (var session in Components<NetworkSession>())
                    {
                        var serialized = new SerializedObject(session);
                        foreach (string name in new[] { "manager", "transport", "world", "lobbyCamera" })
                            Check(serialized.FindProperty(name).objectReferenceValue != null, "Session reference: " + name);
                    }
                    foreach (var manager in Components<NetworkManager>())
                    {
                        Check(manager.NetworkConfig.PlayerPrefab == prefab, "Manager player prefab matches");
                        Check(manager.NetworkConfig.ConnectionApproval && manager.NetworkConfig.TickRate == 30 &&
                            manager.NetworkConfig.EnableSceneManagement, "Approval, 30 Hz, scene sync configured");
                        Check(manager.NetworkConfig.NetworkTransport == manager.GetComponent<UnityTransport>(), "Manager transport assigned");
                    }
                    foreach (var target in Components<Interactable>())
                        Check(target.GetComponent<NetworkObject>() != null && target.GetComponent<NetworkInteractable>() != null,
                            "Replicated interactable: " + target.name);
                    foreach (var pickup in Components<ItemPickup>())
                        Check(pickup.Definition != null && !string.IsNullOrWhiteSpace(pickup.Definition.id) &&
                            Encoding.UTF8.GetByteCount(pickup.Definition.id) <= 61, "Network-safe item definition: " + pickup.name);
                    foreach (var go in objects)
                    {
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, "No missing scripts: " + go.name);
                        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                        {
                            if (behaviour == null) continue;
                            var property = new SerializedObject(behaviour).GetIterator();
                            while (property.NextVisible(true))
                                if (property.propertyType == SerializedPropertyType.ObjectReference)
                                    Check(ResolvedOrEmpty(property), "Reference: " + go.name + "." + property.propertyPath);
                        }
                    }
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            report.AppendLine("Failures: " + failures);
            report.AppendLine("Scene validation is NOT a two-process networking test.");
            Directory.CreateDirectory("Library/Silence");
            File.WriteAllText("Library/Silence/NetworkValidation.txt", report.ToString());
            if (failures == 0) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
            return failures == 0;
        }
        private static bool ResolvedOrEmpty(SerializedProperty property)
        {
            if (property.objectReferenceValue != null) return true;
#if UNITY_6000_6_OR_NEWER
            return property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId));
#else
            return property.objectReferenceInstanceIDValue == 0;
#endif
        }
    }
}
