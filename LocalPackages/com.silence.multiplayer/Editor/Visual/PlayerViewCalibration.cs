using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Silence.Player;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class PlayerViewCalibration
    {
        [Serializable] public sealed class Result
        {
            public Vector3 standingOffset = new Vector3(0, -.04f, .16f), crouchingOffset = new Vector3(0, .02f, .22f);
            public bool standingSampled, crouchingSampled, visualFacingCorrected;
            public string note;
        }
        private readonly struct Pose
        {
            public readonly Vector3 Eye, Head;
            public Pose(Vector3 eye, Vector3 head) { Eye = eye; Head = head; }
        }
        public static Result Calibrate(PlayerContext actor, Animator animator, Transform facing)
        {
            var result = new Result();
            var clips = BasicMotionsCatalog.FallbackClips(animator);
            AnimationClip Find(string name) => clips.FirstOrDefault(c => c.name == name || c.name.EndsWith("|" + name, StringComparison.Ordinal));
            var idle = Find("Idle"); var crouch = Find("CrouchIdle");
            if (idle != null && idle.isHumanMotion)
            {
                Pose pose = Sample(actor, animator, idle);
                Vector3 face = Vector3.ProjectOnPlane(pose.Eye - pose.Head, Vector3.up);
                if (face.magnitude > .025f && Vector3.Dot(face.normalized, Vector3.forward) < -.85f)
                {
                    facing.localRotation = Quaternion.AngleAxis(180, Vector3.up) * facing.localRotation;
                    result.visualFacingCorrected = true; pose = Sample(actor, animator, idle);
                }
                if (pose.Eye.y > actor.Settings.standingHeight * .6f && pose.Eye.y < actor.Settings.standingHeight + .15f)
                { result.standingOffset = Offset(pose.Eye, actor.Settings.standingHeight); result.standingSampled = true; }
            }
            if (crouch != null && crouch.isHumanMotion)
            {
                Pose pose = Sample(actor, animator, crouch);
                if (pose.Eye.y > .45f && pose.Eye.y < actor.Settings.crouchingHeight + .3f)
                { result.crouchingOffset = Offset(pose.Eye, actor.Settings.crouchingHeight); result.crouchingSampled = true; }
            }
            result.note = "Camera rendering offsets only. Authoritative eye, capsule and flashlight transforms unchanged. Sample flags indicate Unity pose sampling, not visual/Play acceptance.";
            return result;
        }
        private static Vector3 Offset(Vector3 eye, float height) => new Vector3(Mathf.Clamp(eye.x, -.025f, .025f), Mathf.Clamp(eye.y - (height - .16f), -.18f, .18f), Mathf.Clamp(eye.z + .04f, .10f, .24f));
        private static Pose Sample(PlayerContext actor, Animator source, AnimationClip clip)
        {
            Scene preview = EditorSceneManager.NewPreviewScene(); GameObject clone = null; PlayableGraph graph = default;
            try
            {
                clone = Object.Instantiate(source.gameObject); SceneManager.MoveGameObjectToScene(clone, preview);
                foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                Matrix4x4 local = actor.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                clone.transform.position = local.GetColumn(3); clone.transform.rotation = local.rotation; clone.transform.localScale = local.lossyScale;
                var animator = clone.GetComponent<Animator>(); animator.runtimeAnimatorController = null; animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
                graph = PlayableGraph.Create("SILENCE natural eye calibration"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Neutral pose", animator); output.SetSourcePlayable(playable); graph.Play(); playable.SetTime(0); graph.Evaluate(0);
                var left = animator.GetBoneTransform(HumanBodyBones.LeftEye); var right = animator.GetBoneTransform(HumanBodyBones.RightEye);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (head == null) throw new InvalidOperationException("Нужна кость Head для настройки камеры.");
                Vector3 eye = left != null && right != null ? (left.position + right.position) * .5f : head.position + Vector3.up * .04f + Vector3.forward * .08f;
                return new Pose(eye, head.position);
            }
            finally { if (graph.IsValid()) graph.Destroy(); if (clone != null) Object.DestroyImmediate(clone); EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
