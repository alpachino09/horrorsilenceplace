using System;
using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext), typeof(PlayerAvatarState)), DefaultExecutionOrder(550)]
    public sealed class PlayerAvatarPresentation : MonoBehaviour
    {
        [SerializeField] private Animator model;
        [SerializeField] private PlayerAnimationProfile animationProfile;
        [SerializeField] private Transform facingRoot;
        private PlayerContext actor;
        private PlayerAvatarState visualState;
        private Vector3 previous;
        private readonly PlayerMotionModel motion = new PlayerMotionModel();
        private bool canLand;
        private Transform head, neck;
        private Quaternion headBase, neckBase;
        private bool lookApplied;
        private int breathingLayer = -1;
        private bool hasBreathRate;
        private readonly CharacterFacingModel facing = new CharacterFacingModel();
        private readonly System.Collections.Generic.HashSet<string> parameters = new System.Collections.Generic.HashSet<string>();
        private Quaternion facingRest;
        private Vector3 previousDirection;
        private float nextPivot;
        public Animator Model => model;
        public Transform FacingRoot => facingRoot;
        public PlayerAnimationProfile AnimationProfile => animationProfile;
        public PlayerMotionFrame MotionFrame { get; private set; }
        public float BodyYaw { get; private set; }
        public void ConfigureAnimation(PlayerAnimationProfile profile, Transform visualRoot)
        { animationProfile = profile; facingRoot = visualRoot; }
        public void Configure(Animator value)
        {
            model = value != null ? value : throw new ArgumentNullException(nameof(value));
            model.applyRootMotion = false;
            model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        private void OnEnable()
        {
            actor = GetComponent<PlayerContext>(); visualState = GetComponent<PlayerAvatarState>();
            previous = transform.position; motion.Reset();
            facing.Reset(); if (facingRoot != null) facingRest = facingRoot.localRotation;
        }
        private void Start()
        {
            if (model == null || model.runtimeAnimatorController == null)
            { Debug.LogError("SILENCE: Player avatar Animator is not configured.", this); enabled = false; return; }
            foreach (string name in PlayerAvatarDefinition.FloatParameters)
                if (!HasParameter(name, AnimatorControllerParameterType.Float)) { Fail(name); return; }
            foreach (string name in PlayerAvatarDefinition.BoolParameters)
                if (!HasParameter(name, AnimatorControllerParameterType.Bool)) { Fail(name); return; }
            canLand = HasParameter("Land", AnimatorControllerParameterType.Trigger);
            foreach (var parameter in model.parameters) parameters.Add(parameter.name);
            model.applyRootMotion = false;
            if (model.isHuman && animationProfile == null) { head = model.GetBoneTransform(HumanBodyBones.Head); neck = model.GetBoneTransform(HumanBodyBones.Neck); }
            breathingLayer = model.GetLayerIndex("Breathing"); hasBreathRate = HasParameter("BreathRate", AnimatorControllerParameterType.Float);
            if (animationProfile != null && model.HasState(0, Animator.StringToHash("Base Layer.Idle")))
            {
                model.Play("Base Layer.Idle", 0, Mathf.Repeat(actor.SourceId * .173f, 1));
                model.SetFloat("IdleVariant", Mathf.Repeat(actor.SourceId * .38197f, 1));
            }
        }
        private void Update() { RestoreLook(); Animate(); }
        private void OnDisable()
        { RestoreLook(); if (facingRoot != null) facingRoot.localRotation = facingRest; }
        private void RestoreLook()
        {
            if (!lookApplied) return;
            if (neck != null) neck.localRotation = neckBase;
            if (head != null) head.localRotation = headBase;
            lookApplied = false;
        }
        private bool HasParameter(string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in model.parameters) if (parameter.name == name && parameter.type == type) return true;
            return false;
        }
        private void Fail(string name)
        { Debug.LogError("SILENCE: Player Animator parameter missing or wrong type: " + name, this); enabled = false; }
        private void Animate()
        {
            if (model == null || !model.enabled) return;
            Vector3 delta = transform.position - previous; previous = transform.position;
            visualState.CaptureOffline();
            if (!visualState.Ready || Time.deltaTime <= 0) { motion.Reset(); return; }
            Vector3 velocityDelta = animationProfile != null && visualState.HasVelocity ? visualState.Velocity * Time.deltaTime : delta;
            if (parameters.Contains("Turn")) model.ResetTrigger("Turn");
            if (parameters.Contains("Pivot")) model.ResetTrigger("Pivot");
            BodyYaw = transform.eulerAngles.y;
            if (animationProfile != null && facingRoot != null)
            {
                FacingFrame turn = facing.Sample(transform.eulerAngles.y, velocityDelta.magnitude / Time.deltaTime, Time.deltaTime, visualState.Crouched);
                BodyYaw = turn.Yaw;
                facingRoot.localRotation = Quaternion.AngleAxis(Mathf.DeltaAngle(transform.eulerAngles.y, BodyYaw), Vector3.up) * facingRest;
                if (parameters.Contains("TurnAngle")) model.SetFloat("TurnAngle", turn.Error);
                if (parameters.Contains("TurnMirrored") && turn.TurnStarted) model.SetBool("TurnMirrored", turn.Error < 0);
                if (turn.TurnStarted && parameters.Contains("Turn")) model.SetTrigger("Turn");
            }
            Vector3 local = Quaternion.Inverse(Quaternion.Euler(0, BodyYaw, 0)) * velocityDelta;
            PlayerMotionFrame frame = motion.Sample(local.x, animationProfile != null && visualState.HasVelocity ? visualState.VerticalSpeed * Time.deltaTime : delta.y,
                local.z, Time.deltaTime, visualState.Grounded);
            MotionFrame = frame;
            float damping = animationProfile != null ? animationProfile.parameterDamping : 0;
            float directionScale = animationProfile != null && frame.Speed > .01f && frame.Speed < animationProfile.walkMetresPerSecond ? animationProfile.walkMetresPerSecond / frame.Speed : 1;
            model.SetFloat("MoveX", frame.Right * directionScale, damping, Time.deltaTime); model.SetFloat("MoveZ", frame.Forward * directionScale, damping, Time.deltaTime);
            model.SetFloat("Speed", frame.Speed, damping, Time.deltaTime); model.SetFloat("VerticalSpeed", frame.Vertical);
            float pitch = actor.View != null ? Mathf.Clamp(Mathf.DeltaAngle(0, actor.View.transform.localEulerAngles.x), -85, 85) : 0;
            model.SetFloat("LookPitch", pitch);
            model.SetBool("Grounded", visualState.Grounded); model.SetBool("Crouched", visualState.Crouched);
            model.SetBool("Carrying", visualState.Carrying); model.SetBool("Wounded", actor.Vitals.State == VitalState.Wounded);
            model.SetBool("Dead", !actor.Vitals.Alive);
            if (breathingLayer >= 0) model.SetLayerWeight(breathingLayer, actor.Vitals.Alive ? 1 : 0);
            if (hasBreathRate) model.SetFloat("BreathRate", actor.Settings != null && frame.Speed > actor.Settings.walkSpeed * 1.2f ? 1.6f : 1);
            if (canLand && frame.Landed && actor.Vitals.Alive) model.SetTrigger("Land");
            if (animationProfile != null)
            {
                if (parameters.Contains("GaitRate"))
                {
                    float reference = visualState.Crouched ? animationProfile.crouchMetresPerSecond : visualState.Carrying ? animationProfile.walkMetresPerSecond : actor.Vitals.State == VitalState.Wounded ? 1.1f : frame.Speed < animationProfile.walkMetresPerSecond ? animationProfile.walkMetresPerSecond :
                        frame.Speed < animationProfile.runMetresPerSecond ? Mathf.Lerp(animationProfile.walkMetresPerSecond, animationProfile.runMetresPerSecond,
                            Mathf.InverseLerp(animationProfile.walkMetresPerSecond, animationProfile.runMetresPerSecond, frame.Speed)) : frame.Speed;
                    model.SetFloat("GaitRate", frame.Speed < .1f ? 1 : Mathf.Clamp(frame.Speed / Mathf.Max(.1f, reference), .15f, 3));
                }
                if (parameters.Contains("HoldingFlashlight")) model.SetBool("HoldingFlashlight", actor.Flashlight != null && actor.Flashlight.HasState && actor.Flashlight.Frame.state != FlashlightState.Holstered);
                if (parameters.Contains("FlashlightState")) model.SetInteger("FlashlightState", actor.Flashlight != null ? (int)actor.Flashlight.Frame.state : 0);
                Vector3 direction = velocityDelta.sqrMagnitude > .00001f ? velocityDelta.normalized : Vector3.zero;
                if (frame.Speed > 1 && previousDirection.sqrMagnitude > .1f && Vector3.Dot(direction, previousDirection) < -.65f && Time.time >= nextPivot && parameters.Contains("Pivot"))
                {
                    model.SetTrigger("Pivot"); nextPivot = Time.time + 1.1f;
                    if (parameters.Contains("TurnMirrored")) model.SetBool("TurnMirrored", Vector3.Cross(previousDirection, direction).y < 0);
                }
                if (frame.Speed > 1 && direction.sqrMagnitude > .1f) previousDirection = direction;
            }
        }
        private void LateUpdate()
        {
            if (model == null || actor == null) return;
            float pitch = actor.View != null ? Mathf.Clamp(Mathf.DeltaAngle(0, actor.View.transform.localEulerAngles.x), -85, 85) : 0;
            if (head != null && actor.Vitals.Alive)
            {
                headBase = head.localRotation; if (neck != null) neckBase = neck.localRotation;
                float angle = Mathf.Clamp(pitch, -35, 35);
                if (neck != null) neck.rotation = Quaternion.AngleAxis(angle * .3f, transform.right) * neck.rotation;
                head.rotation = Quaternion.AngleAxis(angle * (neck != null ? .7f : 1), transform.right) * head.rotation;
                lookApplied = true;
            }
        }
    }
}
