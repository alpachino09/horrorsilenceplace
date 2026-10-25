using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Unity.Netcode;
using Silence.Player;
using Silence.Graphics.Players;
using Silence.Multiplayer.Editor.Visual;

namespace Silence.Tests.Animation
{
    // Delivered for the user's Unity Test Runner, not counted as cloud test passes.
    public sealed class PlayerViewReferenceTests
    {
        private static GameObject[] Players()
        {
            const string root = "Assets/_Silence/Generated/PlayerView";
            if (!AssetDatabase.IsValidFolder(root)) { Assert.Ignore("Run menu 26 on your saved player world first."); return Array.Empty<GameObject>(); }
            var result = AssetDatabase.FindAssets("t:Prefab", new[] { root }).Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null && p.GetComponent<PlayerContext>() != null).ToArray();
            if (result.Length == 0) Assert.Ignore("No camera/body player prefab has been generated."); return result;
        }
        [Test] public void CameraBodyPrefabsKeepOneAuthorityEyeAndOneLocalRig()
        {
            foreach (var prefab in Players())
            {
                var actor = prefab.GetComponent<PlayerContext>();
                Assert.That(prefab.GetComponents<PlayerCameraRig>().Length, Is.EqualTo(1));
                Assert.That(prefab.GetComponentsInChildren<Camera>(true).Length, Is.EqualTo(1), "The presentation camera must be created only for the runtime owner.");
                Assert.That(actor.View, Is.Not.Null); Assert.That(actor.View.transform.IsChildOf(prefab.transform));
                Assert.That(prefab.GetComponentsInChildren<NetworkObject>(true).Length, Is.EqualTo(1));
                Assert.That(prefab.GetComponent<PlayerAvatarPresentation>().Model.GetComponents<CharacterArmPoseSafety>().Length, Is.EqualTo(1));
                Assert.That(prefab.GetComponent<PlayerAvatarVisibility>().BodyRenderers.Count, Is.GreaterThan(0));
            }
        }
        [Test] public void CameraCalibrationDoesNotMoveTheAuthorityEyeOrEditTheSharedController()
        {
            var source = Players().Last(); var sourceAnimator = source.GetComponent<PlayerAvatarPresentation>().Model;
            string controllerPath = AssetDatabase.GetAssetPath(sourceAnimator.runtimeAnimatorController);
            byte[] original = File.ReadAllBytes(controllerPath);
            string folder = "Assets/_Silence/Generated/ViewAudit-" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string prefabPath = folder + "/Player.prefab"; Assert.That(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), prefabPath));
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var actor = contents.GetComponent<PlayerContext>(); Vector3 position = actor.View.transform.localPosition; Quaternion rotation = actor.View.transform.localRotation;
                PlayerViewWorkshop.ConfigurePlayer(contents, folder);
                Assert.That(actor.View.transform.localPosition, Is.EqualTo(position)); Assert.That(actor.View.transform.localRotation, Is.EqualTo(rotation));
                Assert.That(File.ReadAllBytes(controllerPath), Is.EqualTo(original));
                var controller = contents.GetComponent<PlayerAvatarPresentation>().Model.runtimeAnimatorController as AnimatorController;
                Assert.That(controller, Is.Not.Null); Assert.That(controller.layers[0].iKPass);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); AssetDatabase.DeleteAsset(folder); }
        }
    }
}
