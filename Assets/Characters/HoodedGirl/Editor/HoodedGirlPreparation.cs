#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Graphics.Players;
using Silence.Multiplayer.Editor.Visual;
using Object = UnityEngine.Object;

namespace Silence.HoodedGirl.Editor
{
    public static class HoodedGirlPreparation
    {
        private const string Art = "Assets/Characters/HoodedGirl";
        [Serializable] private sealed class MaterialFile { public MaterialEntry[] materials; }
        [Serializable] private sealed class MaterialEntry
        {
            public string name, @base, normal, mask;
            public float[] color;
            public float roughness, metallic;
            public bool alphaClip, doubleSided;
        }
        private static readonly HashSet<string> Loops = new HashSet<string> {
            "Idle","Walk","WalkBack","StrafeLeft","StrafeRight","Run","CrouchIdle","CrouchWalk","LookAround","Fall","CarryIdle","CarryWalk","WoundedIdle","WoundedWalk","BreathGame" };
        [MenuItem("SILENCE/22 Prepare Hooded Girl Assets")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            if (Shader.Find("Universal Render Pipeline/Lit") == null) throw new InvalidOperationException("URP Lit is required.");
            if (!File.Exists(Art + "/Documentation/materials.json")) throw new FileNotFoundException("Copy the complete HoodedGirl assets first.");
            foreach (string path in Directory.GetFiles(Art + "/Textures", "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Wait for texture import: " + path);
                bool normal = path.Contains("_Normal"), mask = path.Contains("_Mask");
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !normal && !mask; importer.maxTextureSize = path.Contains("Skin_") ? 4096 : 2048;
                importer.mipmapEnabled = true; importer.anisoLevel = 8; importer.alphaIsTransparency = path.Contains("Alpha");
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            }
            for (int level = 0; level < 3; level++) PrepareModel(level);
            string output = Art + "/Generated/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            Folder(output); Folder(output + "/Materials"); Folder(output + "/Animations");
            var materialData = JsonUtility.FromJson<MaterialFile>(File.ReadAllText(Art + "/Documentation/materials.json"));
            var materials = Materials(materialData, output);
            var clips = Clips(output); var controller = GameplayController(clips, output);
            var originalScene = SceneManager.GetActiveScene();
            var workScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(workScene);
            var root = new GameObject("HoodedGirl_Visual");
            try
            {
                var main = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(0)));
                PrefabUtility.UnpackPrefabInstance(main, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); main.transform.SetParent(root.transform, false);
                var animator = main.GetComponentInChildren<Animator>(true);
                if (animator == null || animator.avatar == null || !animator.avatar.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Humanoid Avatar invalid. Inspect the FBX Rig / Configure mapping.");
                animator.applyRootMotion = false; animator.runtimeAnimatorController = controller;
                var bones = main.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                var levels = new LOD[3]; var first = main.GetComponentsInChildren<SkinnedMeshRenderer>(true); Assign(first, materials); levels[0] = new LOD(.42f, first);
                for (int level = 1; level < 3; level++)
                {
                    var bucket = new GameObject("LOD" + level); bucket.transform.SetParent(root.transform, false);
                    var temp = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(level)));
                    var list = new List<SkinnedMeshRenderer>();
                    foreach (var source in temp.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        var child = new GameObject(source.name); child.transform.SetParent(bucket.transform, false);
                        Matrix4x4 matrix = temp.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                        child.transform.localPosition = matrix.GetColumn(3); child.transform.localRotation = matrix.rotation; child.transform.localScale = matrix.lossyScale;
                        var renderer = child.AddComponent<SkinnedMeshRenderer>(); renderer.sharedMesh = source.sharedMesh;
                        renderer.bones = source.bones.Select(b => bones.TryGetValue(b.name, out var found) ? found : throw new InvalidOperationException("LOD bone missing: " + b.name)).ToArray();
                        renderer.rootBone = source.rootBone != null ? bones[source.rootBone.name] : animator.transform; renderer.localBounds = source.localBounds;
                        renderer.sharedMaterials = source.sharedMaterials; list.Add(renderer);
                    }
                    Object.DestroyImmediate(temp); Assign(list.ToArray(), materials); levels[level] = new LOD(level == 1 ? .16f : .025f, list.ToArray());
                }
                var lod = root.AddComponent<LODGroup>(); lod.SetLODs(levels); lod.RecalculateBounds();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, output + "/HoodedGirl.prefab");
                if (prefab == null) throw new IOException("Cannot save visual prefab.");
                var definition = ScriptableObject.CreateInstance<PlayerAvatarDefinition>(); definition.modelPrefab = prefab; definition.controller = controller;
                definition.firstPersonHiddenRenderers = prefab.GetComponentsInChildren<Renderer>(true).Where(r => IsHeadSurface(r.name))
                    .Select(r => AnimationUtility.CalculateTransformPath(r.transform, prefab.transform)).ToArray();
                AssetDatabase.CreateAsset(definition, output + "/HoodedGirl.asset");
                var errors = PlayerAvatarWorkshop.DefinitionErrors(definition);
                if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
                CreateDemo(prefab, clips, output);
                AssetDatabase.SaveAssets(); PlayerAvatarWorkshop.Show(definition);
                Debug.Log("SILENCE: HoodedGirl assets prepared at " + output + ". Current scene: " + originalScene.path +
                    ". Menu 21 copies the OPEN improved scene. Native first-person rendering, animation quality and Host/Client still need Play checks. Blender SSS is not reproduced by URP Lit.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (workScene.IsValid() && workScene.isLoaded) EditorSceneManager.CloseScene(workScene, true);
                if (originalScene.IsValid() && originalScene.isLoaded) SceneManager.SetActiveScene(originalScene);
            }
        }
        [MenuItem("SILENCE/23 Open Hooded Girl Preview")]
        public static void OpenDemo()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var scenes = AssetDatabase.FindAssets("HoodedGirl_Demo t:Scene", new[] { Art }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Run menu 22 first.");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(scenes[scenes.Length - 1]);
        }
        private static void CreateDemo(GameObject prefab, Dictionary<string, AnimationClip> clips, string output)
        {
            var demoController = AnimatorController.CreateAnimatorControllerAtPath(output + "/HoodedGirl_Demo.controller");
            foreach (var pair in clips)
            {
                var state = demoController.layers[0].stateMachine.AddState(pair.Key); state.motion = pair.Value;
                if (pair.Key == "Idle") demoController.layers[0].stateMachine.defaultState = state;
            }
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var actor = (GameObject)PrefabUtility.InstantiatePrefab(prefab); var animator = actor.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = demoController; animator.applyRootMotion = false;
                var demo = actor.AddComponent<Silence.HoodedGirl.HoodedGirlDemo>(); demo.model = animator;
                var view = new GameObject("Preview Camera").AddComponent<Camera>(); view.tag = "MainCamera"; view.nearClipPlane = .02f; view.farClipPlane = 30; view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f, .028f, .033f); view.gameObject.AddComponent<AudioListener>();
                view.transform.position = new Vector3(1.8f, 1.18f, 3.7f); view.transform.LookAt(Vector3.up * .88f); view.fieldOfView = 30; demo.viewCamera = view;
                foreach (var value in new[] { (new Vector3(35,-35,0),1.6f,new Color(1,.91f,.83f)),(new Vector3(20,145,0),.6f,new Color(.8f,.88f,1)) })
                { var light = new GameObject("Studio light").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(value.Item1); light.intensity = value.Item2; light.color = value.Item3; light.shadows = LightShadows.Soft; }
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Preview floor"; floor.transform.position = new Vector3(0,-.002f,0); Object.DestroyImmediate(floor.GetComponent<Collider>());
                var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Preview floor" }; floorMaterial.SetColor("_BaseColor", new Color(.035f,.04f,.045f));
                AssetDatabase.CreateAsset(floorMaterial, output + "/Materials/PreviewFloor.mat"); floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
                if (!EditorSceneManager.SaveScene(scene, output + "/HoodedGirl_Demo.unity")) throw new IOException("Cannot save art preview scene.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static bool IsHeadSurface(string name) => new[] { "Face", "Eye.", "Brows", "Lashes", "Hair", "Hood", "Neck_Wrap", "Mouth_", "Teeth_", "Tongue" }.Any(p => name.StartsWith(p, StringComparison.Ordinal));
        private static string ModelPath(int level) => Art + "/Models/HoodedGirl_LOD" + level + ".fbx";
        private static void PrepareModel(int level)
        {
            string path = ModelPath(level); var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("FBX missing or import incomplete: " + path);
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var transforms = imported.GetComponentsInChildren<Transform>(true);
            var names = new HashSet<string>(transforms.Select(t => t.name));
            var human = HumanTrait.BoneName.Where(names.Contains).Select(n => new HumanBone { boneName = n, humanName = n, limit = new HumanLimit { useDefaultValues = true } }).ToArray();
            if (human.Length < 50) throw new InvalidOperationException("Expected the prepared Humanoid hierarchy: " + path);
            var description = importer.humanDescription; description.human = human;
            description.skeleton = transforms.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            description.armStretch = .05f; description.legStretch = .05f; description.upperArmTwist = .5f; description.lowerArmTwist = .5f;
            description.upperLegTwist = .5f; description.lowerLegTwist = .5f; description.feetSpacing = 0;
            importer.animationType = ModelImporterAnimationType.Human; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.humanDescription = description;
            importer.optimizeGameObjects = false; importer.importBlendShapes = true; importer.importAnimation = level == 0; importer.addCollider = false;
            importer.importNormals = ModelImporterNormals.Import; importer.importTangents = ModelImporterTangents.CalculateMikk; importer.animationCompression = ModelImporterAnimationCompression.Off;
            if (level == 0)
            {
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = clip.name.Split('|').Last(); clip.loopTime = Loops.Contains(clip.name); clip.events = Array.Empty<AnimationEvent>();
                    clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                    clip.hasAdditiveReferencePose = clip.name == "BreathGame"; clip.additiveReferencePoseFrame = clip.firstFrame;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }
        private static Dictionary<string, Material> Materials(MaterialFile file, string output)
        {
            if (file == null || file.materials == null) throw new InvalidOperationException("Invalid material manifest.");
            var result = new Dictionary<string, Material>();
            foreach (var entry in file.materials)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = entry.name };
                Texture2D Map(string name) => string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/Textures/" + name) ?? throw new FileNotFoundException(name);
                material.SetColor("_BaseColor", new Color(entry.color[0], entry.color[1], entry.color[2], entry.color[3]).gamma);
                material.SetTexture("_BaseMap", Map(entry.@base)); material.SetFloat("_Metallic", entry.metallic); material.SetFloat("_Smoothness", 1 - entry.roughness);
                if (!string.IsNullOrEmpty(entry.normal)) { material.SetTexture("_BumpMap", Map(entry.normal)); material.SetFloat("_BumpScale", .35f); material.EnableKeyword("_NORMALMAP"); }
                if (!string.IsNullOrEmpty(entry.mask))
                { material.SetTexture("_MetallicGlossMap", Map(entry.mask)); material.SetTexture("_OcclusionMap", Map(entry.mask)); material.SetFloat("_Smoothness", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_OCCLUSIONMAP"); }
                if (entry.alphaClip)
                { material.SetFloat("_AlphaClip", 1); material.SetFloat("_Cutoff", .35f); material.EnableKeyword("_ALPHATEST_ON"); material.SetOverrideTag("RenderType", "TransparentCutout"); material.renderQueue = 2450; }
                if (entry.doubleSided) { material.SetFloat("_Cull", 0); material.doubleSidedGI = true; }
                AssetDatabase.CreateAsset(material, output + "/Materials/" + entry.name + ".mat"); result.Add(entry.name, material);
            }
            return result;
        }
        private static void Assign(SkinnedMeshRenderer[] renderers, Dictionary<string, Material> materials)
        {
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && materials.TryGetValue(m.name, out var found) ? found : throw new InvalidOperationException("Material mapping missing on " + renderer.name)).ToArray();
        }
        private static Dictionary<string, AnimationClip> Clips(string output)
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (var source in AssetDatabase.LoadAllAssetsAtPath(ModelPath(0)).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__", StringComparison.Ordinal)))
            {
                string name = source.name.Split('|').Last(); if (result.ContainsKey(name)) continue;
                var clip = Object.Instantiate(source); clip.name = name;
                // Body clips must allow independent facial animation; remove exported constant zeros.
                foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("blendShape.", StringComparison.Ordinal))) AnimationUtility.SetEditorCurve(clip, binding, null);
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>()); AssetDatabase.CreateAsset(clip, output + "/Animations/" + name + ".anim"); result.Add(name, clip);
            }
            return result;
        }
        private static AnimatorController GameplayController(Dictionary<string, AnimationClip> clips, string output)
        {
            AnimationClip Clip(string name) => clips.TryGetValue(name, out var clip) ? clip : throw new InvalidOperationException("FBX clip missing: " + name);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/HoodedGirl.controller");
            foreach (string name in PlayerAvatarDefinition.FloatParameters) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in PlayerAvatarDefinition.BoolParameters) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            AnimatorState State(string name, Motion motion) { var state = machine.AddState(name); state.motion = motion; states[name] = state; return state; }
            BlendTree SpeedTree(string idle, string walk, float speed)
            {
                var tree = new BlendTree { name = idle + " speed", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(tree, controller); tree.AddChild(Clip(idle), 0); tree.AddChild(Clip(walk), speed); return tree;
            }
            var move = new BlendTree { name = "Directional movement", blendType = BlendTreeType.FreeformCartesian2D, blendParameter = "MoveX", blendParameterY = "MoveZ" };
            AssetDatabase.AddObjectToAsset(move, controller);
            foreach (var pair in new[] { ("Idle", Vector2.zero), ("Walk", new Vector2(0, 1.8f)), ("WalkBack", new Vector2(0, -1.8f)), ("StrafeLeft", new Vector2(-1.8f, 0)), ("StrafeRight", new Vector2(1.8f, 0)), ("Run", new Vector2(0, 5.5f)) }) move.AddChild(Clip(pair.Item1), pair.Item2);
            State("Stand", move); State("Crouch", SpeedTree("CrouchIdle", "CrouchWalk", 1.4f)); State("Carry", SpeedTree("CarryIdle", "CarryWalk", 1.8f)); State("Wounded", SpeedTree("WoundedIdle", "WoundedWalk", 1.1f));
            State("Jump", Clip("Jump")); State("Fall", Clip("Fall")); State("Land", Clip("Land")); State("Dead", Clip("Death")); machine.defaultState = states["Stand"];
            void Flag(AnimatorStateTransition t, string name, bool value) => t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, name);
            void GroundCondition(AnimatorStateTransition t, string target)
            {
                Flag(t, "Dead", false); Flag(t, "Grounded", true); Flag(t, "Crouched", target == "Crouch");
                if (target == "Crouch") return;
                Flag(t, "Carrying", target == "Carry"); if (target != "Carry") Flag(t, "Wounded", target == "Wounded");
            }
            foreach (string from in new[] { "Stand", "Crouch", "Carry", "Wounded", "Land", "Dead" })
                foreach (string to in new[] { "Stand", "Crouch", "Carry", "Wounded" })
                {
                    if (from == to) continue; var t = states[from].AddTransition(states[to]); t.duration = .12f; GroundCondition(t, to);
                    if (from == "Land" && to != "Crouch") { t.hasExitTime = true; t.exitTime = .7f; }
                }
            var death = machine.AddAnyStateTransition(states["Dead"]); death.canTransitionToSelf = false; death.duration = .1f; Flag(death, "Dead", true);
            foreach (string target in new[] { "Jump", "Fall" })
            {
                var t = machine.AddAnyStateTransition(states[target]); t.canTransitionToSelf = false; t.duration = .08f; Flag(t, "Dead", false); Flag(t, "Grounded", false);
                t.AddCondition(target == "Jump" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .1f, "VerticalSpeed");
                var crouch = states[target].AddTransition(states["Crouch"]); crouch.duration = .08f; GroundCondition(crouch, "Crouch");
                var land = states[target].AddTransition(states["Land"]); land.duration = .06f; Flag(land, "Grounded", true); Flag(land, "Crouched", false); Flag(land, "Dead", false);
            }
            var breathMachine = new AnimatorStateMachine { name = "Breathing" }; AssetDatabase.AddObjectToAsset(breathMachine, controller);
            var breath = breathMachine.AddState("Breath"); breath.motion = Clip("BreathGame"); breath.speedParameterActive = true; breath.speedParameter = "BreathRate"; breathMachine.defaultState = breath;
            controller.AddParameter("BreathRate", AnimatorControllerParameterType.Float);
            var parameters = controller.parameters; foreach (var p in parameters) if (p.name == "BreathRate") p.defaultFloat = 1; controller.parameters = parameters;
            var mask = new AvatarMask { name = "Breathing" };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, i == (int)AvatarMaskBodyPart.Body);
            AssetDatabase.CreateAsset(mask, output + "/Breathing.mask");
            controller.AddLayer(new AnimatorControllerLayer { name = "Breathing", stateMachine = breathMachine, defaultWeight = 1, avatarMask = mask, blendingMode = AnimatorLayerBlendingMode.Additive });
            return controller;
        }
        private static void Folder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; int i = path.LastIndexOf('/'); Folder(path.Substring(0, i)); AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1)); }
    }
}
#endif
