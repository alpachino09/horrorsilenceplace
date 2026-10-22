using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Silence.Input;
using Silence.Interactions;
using Silence.Networking;
using Silence.Player;

namespace Silence.Editor
{
    public static class SilenceProjectValidator
    {
        [MenuItem("SILENCE/02 Validate Prototype")]
        public static void ValidateMenu() => ValidateProject();

        public static void ValidateBatch()
        {
            if (!ValidateProject()) throw new InvalidOperationException("SILENCE validation failed. See Library/Silence/Validation.txt");
        }

        public static bool ValidateProject()
        {
            var report = new StringBuilder();
            int failures = 0;
            void Check(bool success, string label)
            {
                report.AppendLine((success ? "PASS " : "FAIL ") + label);
                if (!success) failures++;
            }
            report.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            report.AppendLine("Unity: " + Application.unityVersion);
            Check(Application.unityVersion.StartsWith("6000.", StringComparison.Ordinal), "Unity 6");
            Check(GraphicsSettings.currentRenderPipeline != null &&
                GraphicsSettings.currentRenderPipeline.GetType().Name == "UniversalRenderPipelineAsset", "URP active");
            foreach (var package in PackageInfo.GetAllRegisteredPackages().OrderBy(p => p.name))
                report.AppendLine("PACKAGE " + package.name + " " + package.version);

            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(SilenceSceneGenerator.InputPath);
            Check(input != null, "Input Actions imported");
            if (input != null)
                foreach (string name in new[] { "Move", "Look", "LookStick", "Sprint", "Crouch", "Jump",
                    "Interact", "Flashlight", "Inventory", "Pause", "Capture" })
                    Check(input.FindAction("Gameplay/" + name) != null, "Action: " + name);

            Scene previous = SceneManager.GetActiveScene();
            foreach (string path in new[] { SilenceSceneGenerator.BootstrapPath, SilenceSceneGenerator.FarmPath })
            {
                Check(File.Exists(path), "Scene exists: " + path);
                Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path), "Scene enabled in Build Settings: " + path);
                if (!File.Exists(path)) continue;
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.IsValid() || !scene.isLoaded;
                try
                {
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                        .Select(t => t.gameObject).ToArray();
                    foreach (GameObject go in objects)
                    {
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0,
                            "No missing scripts: " + go.name);
                        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                        {
                            if (behaviour == null) continue;
                            var serialized = new SerializedObject(behaviour);
                            SerializedProperty property = serialized.GetIterator();
                            while (property.NextVisible(true))
                                if (property.propertyType == SerializedPropertyType.ObjectReference)
                                    Check(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                                        "Reference: " + go.name + "." + property.propertyPath);
                        }
                    }
                    if (path != SilenceSceneGenerator.FarmPath) continue;
                    var players = objects.Select(o => o.GetComponent<PlayerContext>()).Where(p => p != null).ToArray();
                    Check(players.Length == 1, "Exactly one offline player");
                    Check(objects.Count(o => o.GetComponent<Camera>() != null) == 1, "Exactly one camera including inactive");
                    Check(objects.Count(o => o.GetComponent<AudioListener>() != null) == 1, "Exactly one audio listener");
                    foreach (var player in players)
                    {
                        Check(player.Settings != null && player.Input != null && player.View != null, "Player configured");
                        Check(player.Input != null && player.Input.Template != null, "Player Input Actions assigned");
                        Check(player.GetComponent<CharacterController>() != null, "CharacterController assigned");
                    }
                    foreach (var pickup in objects.Select(o => o.GetComponent<ItemPickup>()).Where(p => p != null))
                        Check(pickup.Definition != null, "Item definition: " + pickup.name);
                    foreach (var bootstrap in objects.Select(o => o.GetComponent<FarmBootstrap>()).Where(b => b != null))
                    {
                        var serialized = new SerializedObject(bootstrap);
                        Check(serialized.FindProperty("authority").objectReferenceValue != null &&
                            serialized.FindProperty("player").objectReferenceValue != null, "Bootstrap wired");
                    }
                }
                finally
                {
                    if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                }
            }
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            report.AppendLine("Failures: " + failures);
            Directory.CreateDirectory("Library/Silence");
            File.WriteAllText("Library/Silence/Validation.txt", report.ToString());
            if (failures == 0) Debug.Log(report.ToString()); else Debug.LogError(report.ToString());
            return failures == 0;
        }
    }
}
