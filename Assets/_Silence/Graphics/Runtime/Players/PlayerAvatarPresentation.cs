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
        public Animator Model => model;
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
            model.applyRootMotion = false;
            if (model.isHuman) { head = model.GetBoneTransform(HumanBodyBones.Head); neck = model.GetBoneTransform(HumanBodyBones.Neck); }
            breathingLayer = model.GetLayerIndex("Breathing"); hasBreathRate = HasParameter("BreathRate", AnimatorControllerParameterType.Float);
        }
        private void Update() => RestoreLook();
        private void OnDisable() => RestoreLook();
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
        private void LateUpdate()
        {
            if (model == null || !model.enabled) return;
            Vector3 delta = transform.position - previous; previous = transform.position;
            visualState.CaptureOffline();
            if (!visualState.Ready || Time.deltaTime <= 0) { motion.Reset(); return; }
            Vector3 local = transform.InverseTransformDirection(delta);
            PlayerMotionFrame frame = motion.Sample(local.x, delta.y, local.z, Time.deltaTime, visualState.Grounded);
            model.SetFloat("MoveX", frame.Right); model.SetFloat("MoveZ", frame.Forward);
            model.SetFloat("Speed", frame.Speed); model.SetFloat("VerticalSpeed", frame.Vertical);
            float pitch = actor.View != null ? Mathf.Clamp(Mathf.DeltaAngle(0, actor.View.transform.localEulerAngles.x), -85, 85) : 0;
            model.SetFloat("LookPitch", pitch);
            model.SetBool("Grounded", visualState.Grounded); model.SetBool("Crouched", visualState.Crouched);
            model.SetBool("Carrying", visualState.Carrying); model.SetBool("Wounded", actor.Vitals.State == VitalState.Wounded);
            model.SetBool("Dead", !actor.Vitals.Alive);
            if (breathingLayer >= 0) model.SetLayerWeight(breathingLayer, actor.Vitals.Alive ? 1 : 0);
            if (hasBreathRate) model.SetFloat("BreathRate", actor.Settings != null && frame.Speed > actor.Settings.walkSpeed * 1.2f ? 1.6f : 1);
            if (canLand && frame.Landed && actor.Vitals.Alive) model.SetTrigger("Land");
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
