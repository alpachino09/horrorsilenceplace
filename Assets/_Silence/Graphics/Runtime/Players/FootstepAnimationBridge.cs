using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Sends contact to the motor's existing noise source, never emits a second noise stream.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(690)]
    public sealed class FootstepAnimationBridge : MonoBehaviour
    {
        [SerializeField] private PlayerContext actor;
        private Animator animator;
        private FirstPersonMotor motor;
        private PlayerAvatarState state;
        private Transform left, right;
        private CharacterAnimationActions actions;
        private PlayerAvatarPresentation presentation;
        private readonly FootContactModel contacts = new FootContactModel();
        private int lastFrame = -1;
        public void Configure(PlayerContext value) => actor = value;
        private void Start()
        {
            animator = GetComponent<Animator>();
            if (actor == null || !animator.isHuman) { enabled = false; return; }
            motor = actor.GetComponent<FirstPersonMotor>(); state = actor.GetComponent<PlayerAvatarState>(); actions = GetComponent<CharacterAnimationActions>();
            presentation = actor.GetComponent<PlayerAvatarPresentation>();
            left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        private void OnAnimatorIK(int layer)
        {
            if (animator == null || !PlayerAnimatorIK.IsFinalPass(animator, layer) || lastFrame == Time.frameCount || actor == null || motor == null || state == null || !state.Ready ||
                actor.Authority == null || !actor.Authority.CanSimulate(actor) || Time.deltaTime <= 0) return;
            lastFrame = Time.frameCount;
            if (!actor.Vitals.Alive || presentation == null || !presentation.enabled || actions != null && !actions.AllowFeet) { contacts.Reset(); return; }
            if (presentation.AnimationProfile == null || !animator.IsParameterControlledByCurve("LeftFootPlant") || !animator.IsParameterControlledByCurve("RightFootPlant"))
            { contacts.Reset(); return; }
            motor.AnimationContactHeartbeat();
            float speed = motor.PlanarVelocity.magnitude;
            if (contacts.Sample(PlayerFootContacts.Read(animator, true), PlayerFootContacts.Read(animator, false),
                speed * Time.deltaTime, state.Grounded, speed, Time.timeAsDouble, out bool isLeft))
            {
                Transform foot = isLeft ? left : right;
                motor.EmitAnimationStep(foot != null ? foot.position : actor.transform.position);
            }
        }
        private void OnDisable() { contacts.Reset(); lastFrame = -1; }
    }
}
