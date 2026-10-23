using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AI;
using Unity.Netcode;
using Silence.Audio;
using Silence.Config;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Interactions;
using Silence.Player;
using Silence.UI;

namespace Silence.Multiplayer.Editor
{
    public static class CampaignProjectValidator
    {
        [MenuItem("SILENCE/11 Validate Campaign")]
        public static void Menu() => Validate();
        public static bool Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before validation.");
            var text = new StringBuilder(); var failed = new List<string>();
            void Check(bool result, string label) { text.AppendLine((result ? "PASS " : "FAIL ") + label); if (!result) failed.Add(label); }
            text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O")); text.AppendLine("Unity: " + Application.unityVersion);
            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            Check(packages.Any(p => p.name == "com.unity.netcode.gameobjects" && p.version == "2.13.3"), "NGO 2.13.3 installed");
            Check(packages.Any(p => p.name == "com.unity.transport" && (p.version == "2.6.0" || p.version == "6.6.0")), "Supported Transport installed");
            var config = AssetDatabase.LoadAssetAtPath<GameplaySettings>(CampaignSceneGenerator.SettingsPath);
            Check(config != null && config.chapters != null && config.chapters.Length == 7, "Seven chapter definitions");
            if (config != null && config.chapters != null)
                for (int i = 0; i < config.chapters.Length; i++)
                {
                    var chapter = config.chapters[i];
                    Check(chapter != null && (int)chapter.id == i && chapter.spawnPoints != null && chapter.spawnPoints.Length == 4 &&
                        chapter.patrolPoints != null && chapter.patrolPoints.Length >= 3, "Chapter " + i + " identity, spawns, patrol");
                    if (chapter == null) continue;
                    Check(!string.IsNullOrWhiteSpace(chapter.noteRu) && !string.IsNullOrWhiteSpace(chapter.noteEn) &&
                        !string.IsNullOrWhiteSpace(chapter.radioRu) && !string.IsNullOrWhiteSpace(chapter.radioEn), "Chapter " + i + " RU/EN content");
                    Check(AssetDatabase.LoadAssetAtPath<NavMeshData>(CampaignSceneGenerator.Output + "/Chapters/" + chapter.id + "Navigation.asset") != null,
                        "Chapter " + i + " baked navigation data exists");
                }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CampaignSceneGenerator.PlayerPath);
            Check(prefab != null, "Game player prefab exists");
            if (prefab != null)
            {
                foreach (var type in new[] { typeof(NetworkObject), typeof(NetworkPlayer), typeof(GameplayActorNetwork), typeof(ActorGameplayInput),
                    typeof(GameplayHud), typeof(MicrophoneNoiseMeter), typeof(SpectatorView), typeof(TeammateAid), typeof(NetworkInteractable) })
                    Check(prefab.GetComponent(type) != null, "Player component " + type.Name);
                var actor = prefab.GetComponent<PlayerContext>();
                Check(actor != null && actor.View != null && actor.Input != null && actor.Settings != null && actor.Input.Template != null, "Player references");
                if (actor != null && actor.Input != null && actor.Input.Template != null)
                    foreach (string action in new[] { "Use1", "Use2", "Use3", "Use4", "Use5", "Drop", "Throw", "DoorControl" })
                        Check(actor.Input.Template.FindAction("Gameplay/" + action) != null, "Input action " + action);
                Check(prefab.GetComponentsInChildren<Camera>(true).Length == 1 && prefab.GetComponentsInChildren<AudioListener>(true).Length == 1,
                    "One saved player camera/listener");
            }
            Scene previous = SceneManager.GetActiveScene(), scene = SceneManager.GetSceneByPath(CampaignSceneGenerator.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                Check(File.Exists(CampaignSceneGenerator.ScenePath), "Campaign scene exists");
                if (File.Exists(CampaignSceneGenerator.ScenePath))
                {
                    if (opened) scene = EditorSceneManager.OpenScene(CampaignSceneGenerator.ScenePath, OpenSceneMode.Additive);
                    var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToArray();
                    T[] All<T>() where T : Component => objects.SelectMany(o => o.GetComponents<T>()).ToArray();
                    Check(!scene.isDirty, "Campaign scene saved");
                    Check(All<NetworkManager>().Length == 1 && All<NetworkSession>().Length == 1 && All<NetworkWorldAuthority>().Length == 1,
                        "One network manager/session/world");
                    Check(All<GameplayDirector>().Length == 1 && All<GameplayWorldNetwork>().Length == 1, "Campaign director and replication");
                    Check(All<RuntimeNavigation>().Length == 7, "Seven runtime navigation registrations");
                    Check(All<HearingCreature>().Length == 1 && All<GameplayCreatureNetwork>().Length == 1, "Hearing creature and replication");
                    Check(All<NetworkSmokeProbe>().Length == 0 && !objects.Any(o => o.name.StartsWith("NetworkSmoke", StringComparison.Ordinal)), "Legacy smoke fixture removed from campaign copy");
                    Check(All<PrototypeDoor>().Length == 0 && All<PrototypeRadio>().Length == 0 && All<Silence.Missions.PrototypeExit>().Length == 0,
                        "Prototype interactions replaced in campaign copy");
                    Check(All<ChapterExit>().Length == 7 && All<MissionRadio>().Length == 7, "Seven exits and radios");
                    Check(All<PlayerContext>().Length == 0, "Players spawn through network approval");
                    foreach (var manager in All<NetworkManager>()) Check(manager.NetworkConfig.PlayerPrefab == prefab &&
                        manager.NetworkConfig.ProtocolVersion == 3 && manager.NetworkConfig.ConnectionApproval && manager.NetworkConfig.EnableSceneManagement,
                        "Campaign network config and player prefab");
                    foreach (var director in All<GameplayDirector>()) Check(director.Settings == config && director.Creature != null, "Director settings and creature references");
                    foreach (var target in All<Interactable>()) Check(target.GetComponent<NetworkObject>() != null && target.GetComponent<NetworkInteractable>() != null,
                        "Replicated interaction " + target.name);
                    foreach (var pickup in All<ItemPickup>()) Check(pickup.Definition != null && !string.IsNullOrWhiteSpace(pickup.Definition.id) &&
                        Encoding.UTF8.GetByteCount(pickup.Definition.id) <= 61, "Network item ID " + pickup.name);
                    foreach (var go in objects)
                    {
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) == 0, "No missing scripts " + go.name);
                        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
                        {
                            if (behaviour == null) continue;
                            var iterator = new SerializedObject(behaviour).GetIterator();
                            while (iterator.NextVisible(true))
                            {
                                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.objectReferenceValue != null) continue;
#if UNITY_6000_6_OR_NEWER
                                Check(iterator.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)), "Resolved reference " + go.name + "." + iterator.propertyPath);
#else
                                Check(iterator.objectReferenceInstanceIDValue == 0, "Resolved reference " + go.name + "." + iterator.propertyPath);
#endif
                            }
                        }
                    }
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            string summary = "SILENCE Campaign validation — Failures: " + failed.Count;
            text.Insert(0, summary + "\n"); text.AppendLine("This validates assets and references, not gameplay or multiplayer behaviour.");
            Directory.CreateDirectory("Library/Silence"); File.WriteAllText("Library/Silence/CampaignValidation.txt", text.ToString());
            if (failed.Count == 0) Debug.Log(summary + "\n" + text); else Debug.LogError(summary + "\nFAIL " + string.Join("; ", failed));
            return failed.Count == 0;
        }
    }
}
