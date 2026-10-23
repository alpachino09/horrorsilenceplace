using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Creatures;
using Silence.Graphics;
using Object = UnityEngine.Object;
using Unity.Netcode;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class VesperGameInstaller
    {
        private const string Art = "Assets/Creatures/SilencePredator";
        [MenuItem("SILENCE/18 Install Vesper in Game Scene")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            foreach (string name in new[] { "Skin", "Armor", "Tissue", "Bone" })
                foreach (string suffix in new[] { "BaseColor", "Normal", "Mask" })
                    if (!File.Exists(Art + "/Textures/" + name + "_" + suffix + ".png")) throw new FileNotFoundException("Copy the prepared Vesper assets first: " + name + "_" + suffix);
            if (!File.Exists(Art + "/Models/Vesper_LOD0.fbx")) throw new FileNotFoundException("Prepared Vesper FBX missing.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var original = SceneManager.GetActiveScene();
            var originalCreature = Creatures(original);
            if (originalCreature.Length != 1 || string.IsNullOrEmpty(original.path))
                throw new InvalidOperationException("Open the saved Campaign game scene containing one HearingCreature, then run 18.");
            if (originalCreature[0].GetComponent<VesperGamePresentation>() != null)
                throw new InvalidOperationException("This scene already has Vesper. Open the original Campaign scene for another installation.");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader missing.");
            foreach (string name in new[] { "Skin", "Armor", "Tissue", "Bone" })
                foreach (string suffix in new[] { "BaseColor", "Normal", "Mask" })
                {
                    var texture = AssetImporter.GetAtPath(Art + "/Textures/" + name + "_" + suffix + ".png") as TextureImporter;
                    if (texture == null) throw new InvalidOperationException("Wait for texture import before installation.");
                    texture.textureType = suffix == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.sRGBTexture = suffix == "BaseColor"; texture.maxTextureSize = 4096; texture.mipmapEnabled = true;
                    texture.anisoLevel = 8; texture.textureCompression = TextureImporterCompression.CompressedHQ; texture.SaveAndReimport();
                }
            for (int level = 0; level < 3; level++)
            {
                string path = Art + "/Models/Vesper_LOD" + level + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("Import FBX before running installation: " + path);
                importer.animationType = ModelImporterAnimationType.Generic; importer.optimizeGameObjects = false;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = level == 0; importer.addCollider = false;
                importer.animationCompression = ModelImporterAnimationCompression.Off;
                importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk;
                if (level == 0)
                {
                    string[] loops = { "Idle", "IdleBreathing", "Walk", "Run", "GameWalk", "GameRun", "BreathGame", "ListenLoop" };
                    var clips = importer.defaultClipAnimations;
                    foreach (var clip in clips)
                    {
                        clip.name = clip.name.Split('|').Last(); clip.loopTime = loops.Contains(clip.name);
                        clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                        clip.hasAdditiveReferencePose = clip.name == "BreathGame"; clip.additiveReferencePoseFrame = clip.firstFrame;
                        clip.events = Array.Empty<AnimationEvent>();
                    }
                    if (!new[] { "GameWalk", "GameRun", "BreathGame", "ListenStart", "ListenLoop", "ListenEnd", "AttackBite" }.All(n => clips.Any(c => c.name == n)))
                        throw new InvalidOperationException("This is the art-only FBX. Copy the game FBX containing GameWalk, GameRun and BreathGame.");
                    importer.clipAnimations = clips;
                }
                importer.SaveAndReimport();
            }
            string output = "Assets/_Silence/Generated/VesperGame/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            VisualAssetLibrary.Folder(output);
            string target = output + "/Campaign_Vesper.unity";
            if (!EditorSceneManager.SaveScene(original, target, true)) throw new IOException("Cannot copy game scene.");
            Scene scene = default; bool saved = false;
            try
            {
                AssetDatabase.Refresh(); scene = EditorSceneManager.OpenScene(target, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
                var creature = Creatures(scene).Single(); var previous = creature.GetComponentsInChildren<Renderer>(true);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var manager in root.GetComponentsInChildren<NetworkManager>(true)) manager.NetworkConfig.ProtocolVersion = 4;
                foreach (var placeholder in creature.GetComponents<CreatureVisual>()) placeholder.enabled = false;
                foreach (var legacy in creature.GetComponents<CreatureModelPresentation>()) legacy.enabled = false;
                var wrapper = new GameObject("Vesper Visual"); wrapper.transform.SetParent(creature.transform, false);
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/Vesper_LOD0.fbx"));
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                imported.transform.SetParent(wrapper.transform, false);
                foreach (var component in imported.GetComponentsInChildren<MonoBehaviour>(true)) if (component != null) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(component);
                var animator = imported.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("Generic Animator missing after FBX import.");
                var renderers = imported.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (renderers.Length != 4) throw new InvalidOperationException("Expected four skinned material meshes.");
                var bounds = renderers[0].bounds; foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < .01f) throw new InvalidOperationException("Invalid model bounds.");
                float scale = 2 / bounds.size.y;
                Vector3 offset = creature.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                wrapper.transform.localScale = Vector3.one * scale; wrapper.transform.localPosition = -offset * scale;
                var materials = new Dictionary<string, Material>();
                foreach (string name in new[] { "Skin", "Armor", "Tissue", "Bone" })
                {
                    var material = new Material(shader) { name = "Vesper " + name };
                    Texture2D Map(string suffix) => AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/" + name + "_" + suffix + ".png");
                    material.SetTexture("_BaseMap", Map("BaseColor")); material.SetTexture("_BumpMap", Map("Normal"));
                    material.SetTexture("_MetallicGlossMap", Map("Mask")); material.SetTexture("_OcclusionMap", Map("Mask"));
                    material.SetFloat("_Smoothness", 1); material.SetFloat("_BumpScale", .55f);
                    material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP");
                    AssetDatabase.CreateAsset(material, output + "/" + name + ".mat"); materials[name] = material;
                }
                Assign(renderers, materials);
                var bones = imported.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var lods = new LOD[3]; lods[0] = new LOD(.45f, renderers);
                for (int level = 1; level <= 2; level++)
                {
                    var bucket = new GameObject("LOD" + level); bucket.transform.SetParent(wrapper.transform, false);
                    var temp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/Models/Vesper_LOD" + level + ".fbx"));
                    var list = new List<SkinnedMeshRenderer>();
                    foreach (var skin in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var child = new GameObject(skin.name); child.transform.SetParent(bucket.transform, false);
                        Matrix4x4 matrix = temp.transform.worldToLocalMatrix * skin.transform.localToWorldMatrix;
                        child.transform.localPosition = matrix.GetColumn(3); child.transform.localRotation = matrix.rotation; child.transform.localScale = matrix.lossyScale;
                        var renderer = child.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = skin.sharedMesh;
                        renderer.bones = skin.bones.Select(b => bones[b.name]).ToArray();
                        renderer.rootBone = skin.rootBone != null ? bones[skin.rootBone.name] : animator.transform;
                        renderer.localBounds = skin.localBounds; list.Add(renderer);
                    }
                    Object.DestroyImmediate(temp); Assign(list.ToArray(), materials); lods[level] = new LOD(level == 1 ? .16f : .025f, list.ToArray());
                }
                var group = wrapper.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                animator.runtimeAnimatorController = Controller(output, animator); animator.applyRootMotion = false;
                var presentation = creature.gameObject.AddComponent<VesperGamePresentation>();
                presentation.Configure(animator, 1.35f / ((23f / 30) * .64f), 1.60f / ((13f / 30) * .40f));
                foreach (var renderer in previous) renderer.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(wrapper, output + "/Vesper_Visual.prefab");
                AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(scene, target)) throw new IOException("Cannot save Vesper scene.");
                saved = true;
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            }
            if (saved)
            {
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(target, true) }.Concat(EditorBuildSettings.scenes.Where(s => s.path != target)).ToArray();
                EditorSceneManager.OpenScene(target, OpenSceneMode.Single);
                Debug.Log("SILENCE: Vesper installed in " + target + ". Original scene preserved. Test Play, grounding, hearing, host/client and rebuild BOTH network binaries from this same scene.");
            }
        }
        [MenuItem("SILENCE/19 Validate Vesper Game Scene")]
        public static void ValidateMenu() => Validate();
        public static bool Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play before validation.");
            var lines = new List<string>(); int failures = 0;
            void Check(bool value, string label) { lines.Add((value ? "PASS " : "FAIL ") + label); if (!value) failures++; }
            var scene = SceneManager.GetActiveScene(); var creatures = Creatures(scene);
            var managers = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            Check(managers.Length == 1, "One game network manager");
            Check(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Silence.Gameplay.GameplayDirector>(true)).Count() == 1, "One existing gameplay director");
            Check(!string.IsNullOrEmpty(scene.path) && !scene.isDirty, "Active game scene saved");
            Check(creatures.Length == 1, "One authoritative hearing creature");
            foreach (var creature in creatures)
            {
                var presentation = creature.GetComponent<VesperGamePresentation>();
                Check(presentation != null && presentation.enabled, "Vesper presentation enabled");
                var animator = presentation != null ? presentation.Model : null;
                Check(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion, "Animator configured without root motion");
                if (animator != null)
                {
                    Check(animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman, "Valid Generic avatar");
                    var controller = animator.runtimeAnimatorController as AnimatorController;
                    Check(controller != null && controller.layers.Length == 3 && controller.layers.Any(l => l.name == "Hearing") &&
                        controller.layers.Any(l => l.name == "Breathing" && l.blendingMode == AnimatorLayerBlendingMode.Additive), "Locomotion, additive breathing and masked hearing layers");
                    Check(animator.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 4, "Four skinned LOD0 surfaces");
                }
                Check(creature.GetComponentsInChildren<LODGroup>(true).Any(g => g.lodCount == 3), "Three visual LODs");
                Check(creature.GetComponentsInChildren<NetworkObject>(true).Length == 1 && creature.GetComponent<GameplayCreatureNetwork>() != null,
                    "One original network root with creature replication");
                Check(creature.GetComponents<CreatureVisual>().All(c => !c.enabled) && creature.GetComponents<CreatureModelPresentation>().All(c => !c.enabled), "Old animation drivers disabled");
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var manager in root.GetComponentsInChildren<NetworkManager>(true)) Check(manager.NetworkConfig.ProtocolVersion == 4, "Vesper network protocol 4");
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "No missing script " + transform.name);
            }
            string report = "SILENCE Vesper validation — Failures: " + failures + "\n" + string.Join("\n", lines) +
                "\nAsset/reference validation only. Play, foot grounding, visual quality, audio and host/client still need native checks.\n";
            Directory.CreateDirectory("Library/Silence"); File.WriteAllText("Library/Silence/VesperValidation.txt", report);
            if (failures == 0) Debug.Log(report); else Debug.LogError(report);
            return failures == 0;
        }
        [MenuItem("SILENCE/20 Build Vesper Host and Client")]
        public static void Build()
        {
            if (!Validate()) throw new InvalidOperationException("Run 19 and fix Vesper scene references first.");
            NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path);
        }
        private static HearingCreature[] Creatures(Scene scene) => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<HearingCreature>(true)).ToArray();
        private static void Assign(SkinnedMeshRenderer[] renderers, Dictionary<string, Material> materials)
        { foreach (var renderer in renderers) renderer.sharedMaterials = new[] { materials.First(p => renderer.name.StartsWith(p.Key, StringComparison.Ordinal)).Value }; }
        private static AnimatorController Controller(string output, Animator animator)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(Art + "/Models/Vesper_LOD0.fbx").OfType<AnimationClip>().ToArray();
            AnimationClip Clip(string name) => clips.First(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal));
            var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/Vesper.controller");
            controller.AddParameter("Playback", AnimatorControllerParameterType.Float); controller.AddParameter("BreathRate", AnimatorControllerParameterType.Float);
            var parameters = controller.parameters; foreach (var p in parameters) p.defaultFloat = 1; controller.parameters = parameters;
            var machine = controller.layers[0].stateMachine;
            foreach (var pair in new[] { ("Idle", "Idle"), ("Walk", "GameWalk"), ("Run", "GameRun"), ("Attack", "AttackBite"), ("Recover", "Recover") })
            {
                var state = machine.AddState(pair.Item1); state.motion = Clip(pair.Item2);
                if (pair.Item1 == "Walk" || pair.Item1 == "Run") { state.speedParameterActive = true; state.speedParameter = "Playback"; }
                if (pair.Item1 == "Idle") machine.defaultState = state;
            }
            AvatarMask Mask(string name, Func<string, bool> include)
            {
                var transforms = animator.GetComponentsInChildren<Transform>(true); var mask = new AvatarMask { name = name, transformCount = transforms.Length };
                for (int i = 0; i < transforms.Length; i++) { mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(transforms[i], animator.transform)); mask.SetTransformActive(i, include(transforms[i].name)); }
                AssetDatabase.CreateAsset(mask, output + "/" + name + ".mask"); return mask;
            }
            var breathing = new AnimatorStateMachine { name = "Breathing" }; AssetDatabase.AddObjectToAsset(breathing, controller);
            var breath = breathing.AddState("Breath"); breath.motion = Clip("BreathGame"); breath.speedParameterActive = true; breath.speedParameter = "BreathRate"; breathing.defaultState = breath;
            controller.AddLayer(new AnimatorControllerLayer { name = "Breathing", stateMachine = breathing, defaultWeight = 1,
                blendingMode = AnimatorLayerBlendingMode.Additive, avatarMask = Mask("Breathing", n => n == "Chest" || n == "Spine") });
            var hearing = new AnimatorStateMachine { name = "Hearing" }; AssetDatabase.AddObjectToAsset(hearing, controller);
            var ears = new Dictionary<string, AnimatorState>();
            foreach (string name in new[] { "ListenStart", "ListenLoop", "ListenEnd" }) { var state = hearing.AddState(name); state.motion = Clip(name); ears[name] = state; }
            hearing.defaultState = ears["ListenEnd"];
            var transition = ears["ListenStart"].AddTransition(ears["ListenLoop"]); transition.hasExitTime = true; transition.exitTime = 1; transition.duration = .06f;
            controller.AddLayer(new AnimatorControllerLayer { name = "Hearing", stateMachine = hearing, defaultWeight = 0,
                blendingMode = AnimatorLayerBlendingMode.Override, avatarMask = Mask("Hearing", n => n == "Neck" || n == "Head" || n.StartsWith("Hearing_", StringComparison.Ordinal) || n.StartsWith("Membrane_", StringComparison.Ordinal)) });
            return controller;
        }
    }
}
