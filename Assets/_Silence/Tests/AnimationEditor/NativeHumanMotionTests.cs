using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Graphics.Players;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // These tests are delivered for Unity Test Runner. They are explicitly NOT
    // counted among the cloud .NET tests: they require actual Unity/Avatar import.
    public sealed class NativeHumanMotionTests
    {
        private static void RequireSource()
        { if (!File.Exists(BasicMotionsCatalog.Root + "Idle.anim")) Assert.Ignore("Import the user-provided Basic Motions package via menu 25 first."); }
        [Test] public void SuppliedPackageHas23RealHumanoidClips()
        {
            RequireSource(); var clips = BasicMotionsCatalog.Load(); Assert.That(clips.Count, Is.EqualTo(23));
            foreach (var clip in clips.Values) { Assert.That(clip.isHumanMotion); Assert.That(clip.legacy, Is.False); Assert.That(AnimationUtility.GetCurveBindings(clip).Length, Is.GreaterThanOrEqualTo(130)); }
        }
        [Test] public void RightStrafeKeepsTheVendorsMirroredMotion()
        {
            RequireSource(); var clips = BasicMotionsCatalog.Load();
            Assert.That(AnimationUtility.GetAnimationClipSettings(clips["Run Right"]).mirror);
            Assert.That(AnimationUtility.GetAnimationClipSettings(clips["Run Left"]).mirror, Is.False);
        }
        private static PlayerAnimationProfile[] Profiles()
        {
            const string root = "Assets/_Silence/Generated/HumanMotion";
            if (!AssetDatabase.IsValidFolder(root)) { Assert.Ignore("Install Human Motion in a copied world via menu 25 step 2."); return Array.Empty<PlayerAnimationProfile>(); }
            var profiles = AssetDatabase.FindAssets("t:PlayerAnimationProfile", new[] { root }).Select(g => AssetDatabase.LoadAssetAtPath<PlayerAnimationProfile>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            if (profiles.Length == 0) Assert.Ignore("No generated Human Motion profile."); return profiles;
        }
        [Test] public void GeneratedControllersHaveRealMotionsMasksContactsAndIK()
        {
            foreach (var profile in Profiles())
            {
                var controller = profile.controller as AnimatorController; Assert.That(controller, Is.Not.Null);
                Assert.That(controller.layers.Select(l => l.name), Is.EqualTo(new[] { "Base Layer", "UpperBody", "Reactions" }));
                Assert.That(controller.layers.All(l => l.iKPass));
                foreach (var layer in controller.layers)
                    foreach (var state in layer.stateMachine.states) Assert.That(state.state.motion, Is.Not.Null, state.state.name);
                foreach (var clip in controller.animationClips.Distinct())
                {
                    Assert.That(clip.isHumanMotion); Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    Assert.That(settings.loopBlendPositionXZ, Is.False, "Planar travel must be extracted and discarded, not baked into the hips.");
                }
                Assert.That(controller.parameters.Single(p => p.name == "LeftFootPlant").type, Is.EqualTo(AnimatorControllerParameterType.Float));
                Assert.That(controller.parameters.Single(p => p.name == "RightFootPlant").type, Is.EqualTo(AnimatorControllerParameterType.Float));
                Assert.That(controller.layers[1].avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.False, "The interaction layer must not override the flashlight hand.");
            }
        }
        [Test] public void RealWalkSamplesOnTheInstalledAvatarWithoutMutatingSourceOrOpenScene()
        {
            RequireSource(); var profile = Profiles().Last();
            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(profile)).Replace('\\', '/');
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Player.prefab"); Assert.That(prefab, Is.Not.Null);
            var animator = prefab.GetComponent<PlayerAvatarPresentation>().Model; Assert.That(animator.avatar.isValid && animator.avatar.isHuman);
            var source = BasicMotionsCatalog.Load()["Walk"]; string path = AssetDatabase.GetAssetPath(source); byte[] original = File.ReadAllBytes(path);
            int sceneCount = SceneManager.sceneCount; var scene = SceneManager.GetActiveScene(); bool dirty = scene.isDirty;
            string testFolder = "Assets/_Silence/Generated/NativeAnimationAudit-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(testFolder); AssetDatabase.Refresh();
            try
            {
                var result = AnimationClipPreparation.Clone(source, animator, testFolder + "/Walk.anim", true, true, out var measured);
                Assert.That(measured.nativePoseSampled); Assert.That(measured.measuredStanceSpeed, Is.GreaterThan(.15f));
                foreach (string property in new[] { "LeftFootPlant", "RightFootPlant" })
                {
                    var curve = AnimationUtility.GetEditorCurve(result, EditorCurveBinding.FloatCurve("", typeof(Animator), property));
                    Assert.That(curve, Is.Not.Null); Assert.That(curve.keys.Min(k => k.value), Is.LessThan(.35f)); Assert.That(curve.keys.Max(k => k.value), Is.GreaterThan(.65f));
                }
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(original)); Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(scene)); Assert.That(scene.isDirty, Is.EqualTo(dirty));
            }
            finally { AssetDatabase.DeleteAsset(testFolder); }
        }
    }
}
