using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Correct only crossed/rear-clasped wrists after retargeting. Normal mocap arm
    // swing remains intact; interaction and flashlight IK retain their ownership.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(710)]
    public sealed class CharacterArmPoseSafety : MonoBehaviour
    {
        [SerializeField] private PlayerContext actor;
        [SerializeField] private bool resetHandGoals;
        private Animator animator;
        private FirstPersonMotor motor;
        private PlayerAvatarState state;
        private CharacterAnimationActions actions;
        private PlayerAvatarPresentation presentation;
        private FlashlightVisuals flashlight;
        private Transform hips, leftHand, rightHand, leftShoulder, rightShoulder, leftElbow, rightElbow;
        private float leftWeight, rightWeight;
        public void Configure(PlayerContext value, bool resetGoals) { actor = value; resetHandGoals = resetGoals; }
        private void Start()
        {
            animator = GetComponent<Animator>();
            if (actor == null || !animator.isHuman) { enabled = false; return; }
            motor = actor.GetComponent<FirstPersonMotor>(); state = actor.GetComponent<PlayerAvatarState>();
            flashlight = actor.GetComponent<FlashlightVisuals>(); actions = GetComponent<CharacterAnimationActions>();
            presentation = actor.GetComponent<PlayerAvatarPresentation>();
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }
        private void OnAnimatorIK(int layer)
        {
            if (animator == null || !PlayerAnimatorIK.IsFinalPass(animator, layer) || actor == null || hips == null) return;
            // Hints from a previous correction must not bend an elbow after this
            // hand is released. The flashlight owner writes its hints later (750).
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0);
            if (resetHandGoals)
                foreach (var goal in HandGoals) { animator.SetIKPositionWeight(goal, 0); animator.SetIKRotationWeight(goal, 0); }
            if (!actor.Vitals.Alive || actor.CarriedBy != null || state != null && state.Ready && !state.Grounded || actions != null && !actions.AllowFeet)
            { leftWeight = rightWeight = 0; return; }
            Quaternion basis = Quaternion.Euler(0, presentation != null ? presentation.BodyYaw : actor.transform.eulerAngles.y, 0);
            Vector3 forward = basis * Vector3.forward, right = basis * Vector3.right;
            bool bothBehind = leftHand != null && rightHand != null && Vector3.Dot(leftHand.position - hips.position, forward) < -.08f && Vector3.Dot(rightHand.position - hips.position, forward) < -.08f;
            bool holdsLight = flashlight != null && flashlight.isActiveAndEnabled && flashlight.Art != null && actor.Flashlight != null && actor.Flashlight.HasState && actor.Flashlight.Frame.state != FlashlightState.Holstered;
            bool busyLight = holdsLight && actor.Flashlight.Frame.state != FlashlightState.Ready;
            bool carrying = actor.HeldProp != null || actor.HeldTeammate != null;
            Correct(true, leftHand, leftShoulder, leftElbow, ref leftWeight, !carrying && !busyLight && (actions == null || !actions.OwnsLeftHand), bothBehind, forward, right);
            Correct(false, rightHand, rightShoulder, rightElbow, ref rightWeight, !carrying && !holdsLight, bothBehind, forward, right);
        }
        private static readonly AvatarIKGoal[] HandGoals = { AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand };
        private void Correct(bool left, Transform hand, Transform shoulder, Transform elbow, ref float weight, bool available, bool bothBehind, Vector3 forward, Vector3 right)
        {
            if (hand == null || shoulder == null || elbow == null) return;
            float side = Mathf.Sign(Vector3.Dot(shoulder.position - hips.position, right));
            if (Mathf.Abs(side) < .1f) return;
            Vector3 local = hand.position - hips.position;
            float speed = state != null && state.HasVelocity ? state.Velocity.magnitude : motor != null ? motor.PlanarVelocity.magnitude : 0;
            bool bad = PlayerViewRules.BadArmPose(Vector3.Dot(local, right) * side, Vector3.Dot(local, forward), speed, bothBehind);
            weight = available ? Mathf.MoveTowards(weight, bad ? 1 : 0, Time.deltaTime * 8) : 0;
            if (weight <= .001f) return;
            float reach = Vector3.Distance(shoulder.position, elbow.position) + Vector3.Distance(elbow.position, hand.position);
            Vector3 desired = shoulder.position - Vector3.up * reach * .88f + right * side * .06f + forward * .09f;
            AvatarIKGoal goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            animator.SetIKPositionWeight(goal, weight); animator.SetIKPosition(goal, desired);
            AvatarIKHint hint = left ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
            animator.SetIKHintPositionWeight(hint, weight * .75f);
            animator.SetIKHintPosition(hint, shoulder.position - Vector3.up * reach * .44f + right * side * .12f + forward * .07f);
        }
        private void OnDisable() { leftWeight = rightWeight = 0; }
    }
}
