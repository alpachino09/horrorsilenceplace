using UnityEngine;
using Silence.Core;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Graphics.Players
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(700)]
    public sealed class CharacterAnimationActions : MonoBehaviour, ILeftHandIKClaim
    {
        [SerializeField] private PlayerContext actor;
        [SerializeField] private PlayerAnimationProfile profile;
        private Animator animator;
        private readonly AnimationActionGate gate = new AnimationActionGate();
        private NoiseSystem subscribedNoise;
        private Vector3 point;
        private float lookUntil, nextNoiseReaction, actionUntil, upperWeight, reactionWeight;
        private AnimationCueKind current;
        private int upper = -1, reactions = -1;
        public bool OwnsLeftHand => actor != null && actor.Vitals.Alive && current == AnimationCueKind.Interact && Time.time < actionUntil && !FlashlightBusy && !Carrying;
        public bool AllowFeet => current != AnimationCueKind.Trip || Time.time >= actionUntil;
        public bool HasLookTarget => Time.time < lookUntil;
        public Vector3 LookTarget => point;
        private bool Carrying => actor.HeldProp != null || actor.HeldTeammate != null || actor.CarriedBy != null;
        private bool FlashlightBusy => actor.Flashlight != null && actor.Flashlight.HasState &&
            actor.Flashlight.Frame.state != FlashlightState.Ready && actor.Flashlight.Frame.state != FlashlightState.Holstered;
        public void Configure(PlayerContext value, PlayerAnimationProfile settings) { actor = value; profile = settings; }
        private void Start()
        {
            animator = GetComponent<Animator>();
            upper = animator.GetLayerIndex("UpperBody"); reactions = animator.GetLayerIndex("Reactions");
        }
        private void OnEnable() { if (actor != null) actor.AnimationCue += Receive; }
        private void OnDisable()
        {
            if (actor != null) actor.AnimationCue -= Receive;
            if (subscribedNoise != null) subscribedNoise.Emitted -= Hear;
            subscribedNoise = null; gate.Reset(); actionUntil = lookUntil = 0;
            if (animator != null) { if (upper >= 0) animator.SetLayerWeight(upper, 0); if (reactions >= 0) animator.SetLayerWeight(reactions, 0); }
        }
        private void Update()
        {
            if (actor == null || profile == null || animator == null) return;
            // Game binding happens at network spawn, after the prefab's Awake/OnEnable.
            var noise = actor.Game != null ? actor.Game.Noise : null;
            if (noise != subscribedNoise)
            {
                if (subscribedNoise != null) subscribedNoise.Emitted -= Hear;
                subscribedNoise = noise; if (subscribedNoise != null) subscribedNoise.Emitted += Hear;
            }
            bool active = actor.Vitals.Alive && Time.time < actionUntil && !Carrying;
            upperWeight = Mathf.MoveTowards(upperWeight, active && current == AnimationCueKind.Interact && !FlashlightBusy ? 1 : 0, Time.deltaTime * 8);
            reactionWeight = Mathf.MoveTowards(reactionWeight, active && current == AnimationCueKind.Trip ? 1 : 0, Time.deltaTime * 8);
            if (!actor.Vitals.Alive) upperWeight = reactionWeight = 0;
            if (upper >= 0) animator.SetLayerWeight(upper, upperWeight);
            if (reactions >= 0) animator.SetLayerWeight(reactions, reactionWeight);
        }
        private void Hear(NoiseEvent noise)
        {
            if (actor == null || actor.Authority == null || !actor.Authority.CanSimulate(actor) || !actor.Vitals.Alive ||
                noise.SourceId == actor.SourceId || noise.Kind != NoiseKind.Impact || noise.Intensity < .55f || Time.time < nextNoiseReaction ||
                Vector3.Distance(actor.transform.position, noise.Position) > noise.Range * .55f) return;
            nextNoiseReaction = Time.time + 3;
            actor.PublishAnimationCue(AnimationCueKind.Startle, noise.Position, noise.Intensity);
        }
        private void Receive(PlayerAnimationCue cue)
        {
            if (actor == null || profile == null || animator == null) return;
            float duration = cue.Kind == AnimationCueKind.Trip ? profile.tripSeconds : cue.Kind == AnimationCueKind.Hit ? profile.hitSeconds :
                cue.Kind == AnimationCueKind.Startle ? profile.startleSeconds : profile.interactionSeconds;
            if (!gate.TryBegin(cue.Kind, Time.timeAsDouble, duration, actor.Vitals.Alive)) return;
            point = cue.Point; lookUntil = Time.time + Mathf.Min(duration, 1.4f); current = cue.Kind; actionUntil = Time.time + duration;
            // No invented mocap: Startle/Hit use gaze and the existing wounded state until
            // purpose-authored flinch clips are supplied. No input/physics lock is added.
            if (cue.Kind == AnimationCueKind.Interact && upper >= 0 && !FlashlightBusy && !Carrying && animator.HasState(upper, Animator.StringToHash("UpperBody.Reach")))
                animator.CrossFadeInFixedTime("UpperBody.Reach", .1f, upper, 0);
            if (cue.Kind == AnimationCueKind.Trip && reactions >= 0 && animator.HasState(reactions, Animator.StringToHash("Reactions.Trip")))
                animator.CrossFadeInFixedTime("Reactions.Trip", .08f, reactions, 0);
        }
        private void OnAnimatorIK(int layer)
        {
            if (animator == null || !PlayerAnimatorIK.IsFinalPass(animator, layer) || !OwnsLeftHand || !animator.isHuman) return;
            var shoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (shoulder == null || hand == null) return;
            // Far-away prompts do not stretch the arm beyond its actual reach.
            float distance = Vector3.Distance(shoulder.position, point);
            float reach = Vector3.Distance(shoulder.position, animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position) +
                Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftLowerArm).position, hand.position);
            if (distance > reach * 1.12f || distance < .08f) return;
            float phase = 1 - Mathf.Clamp01((actionUntil - Time.time) / Mathf.Max(.1f, profile.interactionSeconds));
            float weight = Mathf.Sin(phase * Mathf.PI) * upperWeight * .9f;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight); animator.SetIKPosition(AvatarIKGoal.LeftHand, point);
        }
    }
}
