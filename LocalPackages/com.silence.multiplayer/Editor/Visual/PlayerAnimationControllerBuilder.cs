using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Silence.Graphics.Players;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class PlayerAnimationControllerBuilder
    {
        [Serializable] public sealed class ClipReport { public AnimationClipPreparation.Measurements[] clips; public string[] previewOnlyBasicMotions; }
        public static readonly string[] GameplayBasicNames = { "Idle", "Walk", "Run", "Run Fast", "Run Backward", "Run Left", "Run Right", "Turn Left", "Turn Right", "Walking Turn 180", "Run Turn 180", "Jumping Up", "Tripping", "Dying" };
        private static readonly string[] OwnNames = { "Idle", "WalkBack", "StrafeLeft", "StrafeRight", "CrouchIdle", "CrouchWalk", "CarryIdle", "CarryWalk", "WoundedIdle", "WoundedWalk", "Fall", "Land", "UseItem" };
        public static PlayerAnimationProfile Build(Animator target, string output)
        {
            var basic = BasicMotionsCatalog.Load(); var ownClips = BasicMotionsCatalog.FallbackClips(target);
            var own = new Dictionary<string, AnimationClip>();
            foreach (string name in OwnNames)
            {
                var candidates = ownClips.Where(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal)).ToArray();
                if (candidates.Length == 0) throw new InvalidOperationException("Для этого персонажа не найден собственный Humanoid-клип " + name + ". Нужны исходные анимации HoodedGirl; демонстрационная модель PolyOne не используется.");
                own[name] = candidates[0];
            }
            VisualAssetLibrary.Folder(output + "/Clips");
            var clips = new Dictionary<string, AnimationClip>();
            var measured = new Dictionary<string, AnimationClipPreparation.Measurements>();
            bool Moving(string name) => name == "Walk" || name.StartsWith("Run", StringComparison.Ordinal) && !name.Contains("Turn") || name.Contains("Walk") || name.StartsWith("Strafe", StringComparison.Ordinal);
            bool Loop(string name) => name == "Idle" || name.EndsWith("Idle", StringComparison.Ordinal) || Moving(name) && !name.Contains("Turn");
            void Add(string key, AnimationClip original)
            {
                if (EditorUtility.DisplayCancelableProgressBar("SILENCE — реальный Humanoid / контакты стоп", key, clips.Count / 27f)) throw new OperationCanceledException("Установка отменена. Исходная сцена сохранена.");
                var clone = AnimationClipPreparation.Clone(original, target, output + "/Clips/" + key.Replace(':', '_') + ".anim", Loop(original.name), Moving(original.name), out var result);
                // Layered actions must not overwrite the base gait's contact parameters.
                if (key == "Own:UseItem" || key == "Basic:Tripping")
                    foreach (string property in new[] { "LeftFootPlant", "RightFootPlant" }) AnimationUtility.SetEditorCurve(clone, EditorCurveBinding.FloatCurve("", typeof(Animator), property), null);
                clips.Add(key, clone); measured.Add(key, result);
            }
            foreach (string name in GameplayBasicNames) Add("Basic:" + name, basic[name]);
            foreach (string name in OwnNames) Add("Own:" + name, own[name]);
            var profile = ScriptableObject.CreateInstance<PlayerAnimationProfile>(); profile.name = "SILENCE Human Motion";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/HumanMotion.controller");
            profile.controller = controller;
            profile.provenance = "PolyOne Basic Motions: 14 real Humanoid clips from user-provided package SHA256 " + BasicMotionsCatalog.PackageSha256 + ". 13 HoodedGirl fallback clips. No Quantum Stage clips. No Motion Matching database. Contact/stride values sampled on this Avatar in Unity; visual Play/network acceptance still required.";
            AssetDatabase.CreateAsset(profile, output + "/HumanMotion.asset");
            foreach (string name in PlayerAvatarDefinition.FloatParameters) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            foreach (string name in PlayerAvatarDefinition.BoolParameters) controller.AddParameter(name, AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "GaitRate", "TurnAngle", "IdleVariant", "LeftFootPlant", "RightFootPlant" }) controller.AddParameter(name, AnimatorControllerParameterType.Float);
            controller.AddParameter("HoldingFlashlight", AnimatorControllerParameterType.Bool); controller.AddParameter("FlashlightState", AnimatorControllerParameterType.Int);
            controller.AddParameter("TurnMirrored", AnimatorControllerParameterType.Bool);
            foreach (string name in new[] { "Land", "Turn", "Pivot" }) controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            var parameters = controller.parameters;
            foreach (var p in parameters) if (p.name == "GaitRate") p.defaultFloat = 1;
            controller.parameters = parameters;
            AnimationClip Clip(string name) => clips[name];
            BlendTree Tree(string name, BlendTreeType type, string x, string y = null)
            { var t = new BlendTree { name = name, blendType = type, blendParameter = x, blendParameterY = y ?? "", useAutomaticThresholds = false }; AssetDatabase.AddObjectToAsset(t, controller); return t; }
            float Rate(string key, float metres)
            {
                float native = measured[key].measuredStanceSpeed;
                if (native < .1f) throw new InvalidOperationException("Не измерена скорость клипа " + key);
                float value = metres / native;
                // Reject an obviously incompatible retarget rather than forcing a 10x gait.
                if (value < .25f || value > 3) throw new InvalidOperationException("Неестественный масштаб скорости " + key + ": " + value + ". Проверь Avatar/масштаб модели.");
                return value;
            }
            void Direction(BlendTree tree, string key, Vector2 at)
            {
                var list = tree.children.ToList(); list.Add(new ChildMotion { motion = Clip(key), position = at, timeScale = at.sqrMagnitude > .01f ? Rate(key, at.magnitude) : 1 }); tree.children = list.ToArray();
            }
            var idleTree = Tree("Two actual idle recordings", BlendTreeType.Simple1D, "IdleVariant"); idleTree.AddChild(Clip("Basic:Idle"), 0); idleTree.AddChild(Clip("Own:Idle"), 1);
            var move = Tree("Measured directional locomotion (m/s)", BlendTreeType.FreeformCartesian2D, "MoveX", "MoveZ");
            Direction(move, "Basic:Walk", new Vector2(0, profile.walkMetresPerSecond));
            Direction(move, "Basic:Run", new Vector2(0, profile.runMetresPerSecond));
            Direction(move, "Basic:Run Fast", new Vector2(0, profile.sprintMetresPerSecond));
            Direction(move, "Own:WalkBack", new Vector2(0, -profile.walkMetresPerSecond));
            Direction(move, "Own:StrafeLeft", new Vector2(-profile.walkMetresPerSecond, 0)); Direction(move, "Own:StrafeRight", new Vector2(profile.walkMetresPerSecond, 0));
            Direction(move, "Basic:Run Backward", new Vector2(0, -profile.runMetresPerSecond));
            Direction(move, "Basic:Run Left", new Vector2(-profile.runMetresPerSecond, 0)); Direction(move, "Basic:Run Right", new Vector2(profile.runMetresPerSecond, 0));
            Direction(move, "Basic:Run Backward", new Vector2(0, -profile.sprintMetresPerSecond));
            Direction(move, "Basic:Run Left", new Vector2(-profile.sprintMetresPerSecond, 0)); Direction(move, "Basic:Run Right", new Vector2(profile.sprintMetresPerSecond, 0));
            var machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            AnimatorState State(string name, Motion motion, bool gait = false)
            { var s = machine.AddState(name); s.motion = motion; s.writeDefaultValues = false; s.speedParameterActive = gait; s.speedParameter = gait ? "GaitRate" : ""; states.Add(name, s); return s; }
            // Long Idle recordings must not lengthen the gait cycle through BlendTree
            // time synchronization. Keep Idle in separate states; normalize slow motion
            // to the walk ring and adjust only its playback cadence at runtime.
            State("Idle", idleTree).tag = "PlantedIdle"; State("Locomotion", move, true);
            State("CrouchIdle", Clip("Own:CrouchIdle")).tag = "PlantedIdle";
            State("Crouch", Clip("Own:CrouchWalk"), true).speed = Rate("Own:CrouchWalk", profile.crouchMetresPerSecond);
            State("CarryIdle", Clip("Own:CarryIdle")).tag = "PlantedIdle";
            State("Carry", Clip("Own:CarryWalk"), true).speed = Rate("Own:CarryWalk", profile.walkMetresPerSecond);
            State("WoundedIdle", Clip("Own:WoundedIdle")).tag = "PlantedIdle";
            State("Wounded", Clip("Own:WoundedWalk"), true).speed = Rate("Own:WoundedWalk", 1.1f);
            State("Jump", Clip("Basic:Jumping Up")); State("Fall", Clip("Own:Fall")); State("Land", Clip("Own:Land")); State("Dead", Clip("Basic:Dying"));
            State("TurnLeft", Clip("Basic:Turn Left")); State("TurnRight", Clip("Basic:Turn Right"));
            State("PivotWalk", Clip("Basic:Walking Turn 180")); State("PivotRun", Clip("Basic:Run Turn 180"));
            foreach (string name in new[] { "PivotWalk", "PivotRun" }) { states[name].mirrorParameter = "TurnMirrored"; states[name].mirrorParameterActive = true; }
            machine.defaultState = states["Idle"];
            void Flag(AnimatorStateTransition t, string n, bool value) => t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, n);
            void Ground(AnimatorStateTransition t, string to)
            {
                bool idle = to == "Idle" || to.EndsWith("Idle", StringComparison.Ordinal);
                t.AddCondition(idle ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, .09f, "Speed");
                bool crouch = to.StartsWith("Crouch", StringComparison.Ordinal), carry = to.StartsWith("Carry", StringComparison.Ordinal);
                Flag(t, "Dead", false); Flag(t, "Grounded", true); Flag(t, "Crouched", crouch);
                if (crouch) return;
                Flag(t, "Carrying", carry); if (!carry) Flag(t, "Wounded", to.StartsWith("Wounded", StringComparison.Ordinal));
            }
            void Blend(AnimatorStateTransition t, float duration) { t.duration = duration; t.hasFixedDuration = true; t.canTransitionToSelf = false; }
            foreach (string from in new[] { "Idle", "Locomotion", "CrouchIdle", "Crouch", "CarryIdle", "Carry", "WoundedIdle", "Wounded", "Land", "Dead", "TurnLeft", "TurnRight", "PivotWalk", "PivotRun" })
                foreach (string to in new[] { "Idle", "Locomotion", "CrouchIdle", "Crouch", "CarryIdle", "Carry", "WoundedIdle", "Wounded" })
                {
                    if (from == to) continue;
                    var t = states[from].AddTransition(states[to]); Blend(t, to.StartsWith("Crouch", StringComparison.Ordinal) || from.StartsWith("Crouch", StringComparison.Ordinal) ? .18f : .13f); Ground(t, to);
                    if (from == "Land" || from.StartsWith("Turn", StringComparison.Ordinal) || from.StartsWith("Pivot", StringComparison.Ordinal)) { t.hasExitTime = true; t.exitTime = from == "Land" ? .65f : .8f; }
                }
            var death = machine.AddAnyStateTransition(states["Dead"]); Blend(death, .1f); Flag(death, "Dead", true);
            foreach (string to in new[] { "Jump", "Fall" })
            {
                var t = machine.AddAnyStateTransition(states[to]); Blend(t, .08f); Flag(t, "Dead", false); Flag(t, "Grounded", false);
                t.AddCondition(to == "Jump" ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, .1f, "VerticalSpeed");
                var land = states[to].AddTransition(states["Land"]); Blend(land, .08f); Flag(land, "Grounded", true); Flag(land, "Dead", false); Flag(land, "Crouched", false);
                foreach (string destinationState in new[] { "Crouch", "CrouchIdle" }) { var crouch = states[to].AddTransition(states[destinationState]); Blend(crouch, .08f); Ground(crouch, destinationState); }
            }
            foreach (bool left in new[] { true, false })
            {
                var t = states["Idle"].AddTransition(states[left ? "TurnLeft" : "TurnRight"]); Blend(t, .12f); Ground(t, "Idle");
                t.AddCondition(AnimatorConditionMode.If, 0, "Turn"); t.AddCondition(AnimatorConditionMode.Less, .25f, "Speed");
                t.AddCondition(left ? AnimatorConditionMode.Less : AnimatorConditionMode.Greater, left ? -30 : 30, "TurnAngle");
            }
            foreach (bool running in new[] { false, true })
            {
                var t = states["Locomotion"].AddTransition(states[running ? "PivotRun" : "PivotWalk"]); Blend(t, .1f); Ground(t, "Locomotion");
                t.AddCondition(AnimatorConditionMode.If, 0, "Pivot"); t.AddCondition(running ? AnimatorConditionMode.Greater : AnimatorConditionMode.Less, profile.runMetresPerSecond - .1f, "Speed");
            }
            void Layer(string name, string stateName, AnimationClip clip, bool upper)
            {
                var stateMachine = new AnimatorStateMachine { name = name }; AssetDatabase.AddObjectToAsset(stateMachine, controller);
                var s = stateMachine.AddState(stateName); s.motion = clip; s.writeDefaultValues = false;
                if (upper) s.speed = clip.length / Mathf.Max(.2f, profile.interactionSeconds);
                stateMachine.defaultState = s;
                var mask = new AvatarMask { name = name };
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                    mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, upper ? i == (int)AvatarMaskBodyPart.LeftArm || i == (int)AvatarMaskBodyPart.LeftFingers : i != (int)AvatarMaskBodyPart.Root);
                AssetDatabase.CreateAsset(mask, output + "/" + name + ".mask");
                controller.AddLayer(new AnimatorControllerLayer { name = name, stateMachine = stateMachine, defaultWeight = 0, avatarMask = mask, iKPass = true, blendingMode = AnimatorLayerBlendingMode.Override });
            }
            Layer("UpperBody", "Reach", Clip("Own:UseItem"), true); Layer("Reactions", "Trip", Clip("Basic:Tripping"), false);
            var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers;
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            File.WriteAllText(output + "/clip-measurements.json", JsonUtility.ToJson(new ClipReport { clips = measured.Values.ToArray(), previewOnlyBasicMotions = BasicMotionsCatalog.Names.Except(GameplayBasicNames).ToArray() }, true));
            return profile;
        }
    }
}
