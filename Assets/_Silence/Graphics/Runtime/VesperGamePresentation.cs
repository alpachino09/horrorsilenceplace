using System;
using System.Linq;
using UnityEngine;
using Silence.Audio;
using Silence.Core;
using Silence.Creatures;

namespace Silence.Graphics
{
    // Child skeleton only. NavMesh, damage, sound perception and the network root stay authoritative.
    [DisallowMultipleComponent, RequireComponent(typeof(HearingCreature)), DefaultExecutionOrder(550)]
    public sealed class VesperGamePresentation : MonoBehaviour
    {
        [Serializable] private sealed class Leg
        {
            public Transform upper, lower, end;
            public float phase, soleOffset;
            [NonSerialized] public bool planted, initialized;
            [NonSerialized] public Vector3 anchor;
        }
        [SerializeField] private Animator model;
        [SerializeField] private Transform neck;
        [SerializeField] private float walkSpeed = 1.8f, runSpeed = 6;
        [SerializeField] private Leg[] legs = Array.Empty<Leg>();
        private HearingCreature creature;
        private CreatureAudio sound;
        private Vector3 previous;
        private float speed, hearingWeight, aim;
        private bool hearing, started;
        private double closeHearingAt = double.NegativeInfinity;
        private int state;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        private static readonly int Run = Animator.StringToHash("Base Layer.Run");
        private static readonly int Recover = Animator.StringToHash("Base Layer.Recover");
        private static readonly int Attack = Animator.StringToHash("Base Layer.Attack");
        private static readonly int Playback = Animator.StringToHash("Playback");
        private static readonly int BreathRate = Animator.StringToHash("BreathRate");
        public Animator Model => model;

        public void Configure(Animator animator, float sourceWalkMetresPerSecond, float sourceRunMetresPerSecond)
        {
            model = animator;
            Transform Find(string name) => model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            neck = Find("Neck");
            legs = new Leg[4];
            string[] sides = { "L", "R", "L", "R" };
            float[] phases = { 0, .5f, .75f, .25f };
            float scaleSum = 0;
            for (int i = 0; i < 4; i++)
            {
                bool fore = i < 2; string side = sides[i];
                var end = Find((fore ? "Hand." : "Foot.") + side);
                if (end == null) throw new InvalidOperationException("Vesper skeleton is missing a foot: " + side);
                var upper = Find((fore ? "ForeUpper." : "HindUpper.") + side);
                var lower = Find((fore ? "ForeLower." : "HindLower.") + side);
                if (upper == null || lower == null) throw new InvalidOperationException("Vesper leg chain missing.");
                // Calibrate to claw soles, not FBX bounds that include the entire animation envelope.
                float unitScale = Vector3.Distance(upper.position, lower.position) / (fore ? 1.1406577f : 1.016022f);
                scaleSum += unitScale;
                legs[i] = new Leg { upper = upper, lower = lower, end = end, phase = phases[i],
                    soleOffset = (fore ? .273f : .403f) * unitScale };
            }
            walkSpeed = Mathf.Max(.1f, sourceWalkMetresPerSecond * scaleSum / 4);
            runSpeed = Mathf.Max(.1f, sourceRunMetresPerSecond * scaleSum / 4);
            model.applyRootMotion = false; model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        private void OnEnable()
        {
            creature = GetComponent<HearingCreature>(); sound = GetComponent<CreatureAudio>();
            previous = transform.position; speed = 0; state = 0; aim = 0; hearing = false; started = false; hearingWeight = 0;
            closeHearingAt = double.NegativeInfinity;
            foreach (var leg in legs) if (leg != null) leg.initialized = false;
            if (model != null) model.applyRootMotion = false;
            if (sound != null) sound.SetAnimationDriven(true);
        }
        private void OnDisable() { if (sound != null) sound.SetAnimationDriven(false); }
        private void LateUpdate()
        {
            if (model == null || model.runtimeAnimatorController == null || Time.deltaTime <= 0) return;
            Vector3 delta = transform.position - previous; previous = transform.position; delta.y = 0;
            bool teleport = delta.sqrMagnitude > 9;
            float measured = teleport ? 0 : delta.magnitude / Time.deltaTime;
            speed = teleport ? 0 : Mathf.Lerp(speed, measured, 1 - Mathf.Exp(-12 * Time.deltaTime));
            double now = Time.unscaledTimeAsDouble;
            double attackAge = now - creature.AttackedAt;
            bool attack = creature.HasAttackCue && CreatureMotionMath.IsRecent(attackAge, .9);
            int next = attack ? Attack : creature.State == CreatureState.Recover ? Recover :
                creature.State == CreatureState.Dormant || speed < .12f ? Idle :
                creature.State == CreatureState.Chase || speed > walkSpeed * 1.45f ? Run : Walk;
            if (next != state)
            {
                if (!model.HasState(0, next)) { Debug.LogError("SILENCE: Vesper Animator state missing. Run menu 19 and check the generated controller.", this); enabled = false; return; }
                model.CrossFadeInFixedTime(next, .12f, 0, attack ? (float)attackAge : 0);
                state = next;
            }
            // Per-state playback preserves breathing/listening timing during fast locomotion.
            model.speed = 1;
            model.SetFloat(Playback, Mathf.Clamp(speed / (next == Run ? runSpeed : walkSpeed), .3f, 1.7f));
            model.SetFloat(BreathRate, creature.State == CreatureState.Chase ? 1.55f : 1);
            double heardAge = now - creature.HeardAt;
            bool wanted = creature.HasHeardCue && (creature.State == CreatureState.Search || CreatureMotionMath.IsRecent(heardAge, 2.8));
            int ears = model.GetLayerIndex("Hearing");
            if (ears >= 0)
            {
                if (wanted != hearing)
                {
                    hearing = wanted;
                    if (!wanted) closeHearingAt = now;
                    string clip = wanted ? (!started && heardAge > 1.1 ? "ListenLoop" : "ListenStart") : "ListenEnd";
                    model.CrossFadeInFixedTime("Hearing." + clip, .10f, ears);
                }
                float target = wanted || now - closeHearingAt < .9 ? 1 : 0;
                hearingWeight = Mathf.MoveTowards(hearingWeight, target, Time.deltaTime * 4);
                model.SetLayerWeight(ears, hearingWeight);
            }
            started = true;
            int breath = model.GetLayerIndex("Breathing");
            if (breath >= 0) model.SetLayerWeight(breath, attack ? .35f : next == Run ? .65f : 1);
            if (neck != null)
            {
                Vector3 direction = creature.LastAcceptedSound - transform.position; direction.y = 0;
                float target = wanted && direction.sqrMagnitude > .01f ? Mathf.Clamp(Vector3.SignedAngle(transform.forward, direction, Vector3.up), -35, 35) : 0;
                aim = Mathf.Lerp(aim, target, 1 - Mathf.Exp(-4 * Time.deltaTime));
                neck.rotation = Quaternion.AngleAxis(aim * hearingWeight, Vector3.up) * neck.rotation;
            }
            GroundFeet(next, teleport);
            if (sound != null && breath >= 0)
                sound.SetBreathing(model.GetCurrentAnimatorStateInfo(breath).normalizedTime, next == Run ? .28f : .18f,
                    creature.State == CreatureState.Chase ? 1.55f : 1, neck != null ? neck.position : transform.position + Vector3.up);
        }
        private void GroundFeet(int motion, bool teleport)
        {
            bool moving = motion == Walk || motion == Run;
            float cycle = model.GetCurrentAnimatorStateInfo(0).normalizedTime;
            bool transitioning = model.IsInTransition(0);
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                if (leg == null || leg.upper == null || leg.lower == null || leg.end == null) continue;
                if (teleport || transitioning) leg.initialized = false;
                float phase = motion == Run ? (i < 2 ? i * .04f : .50f + (i - 2) * .04f) : leg.phase;
                float weight = moving ? CreatureMotionMath.PlantWeight(cycle, phase, motion == Run ? .40f : .64f) : 1;
                if (motion == Attack || motion == Recover) weight *= .4f;
                bool planted = weight > .55f;
                int count = Physics.RaycastNonAlloc(leg.end.position + Vector3.up * .5f, Vector3.down,
                    hits, leg.soleOffset + .95f, ~0, QueryTriggerInteraction.Ignore);
                bool found = false; RaycastHit best = default;
                for (int h = 0; h < count; h++)
                {
                    if (hits[h].collider.transform.IsChildOf(transform) || hits[h].normal.y < .65f) continue;
                    if (!found || hits[h].distance < best.distance) { best = hits[h]; found = true; }
                }
                if (!found) { leg.initialized = false; continue; }
                Vector3 target = best.point + Vector3.up * leg.soleOffset;
                if (planted && (!leg.initialized || !leg.planted))
                {
                    leg.anchor = target;
                    if (leg.initialized && moving && speed > .15f && sound != null) sound.PlayAnimatedStep(best.point, speed);
                }
                if (planted && leg.initialized && (leg.anchor - target).sqrMagnitude < .09f)
                    target = new Vector3(leg.anchor.x, target.y, leg.anchor.z);
                leg.planted = planted; leg.initialized = true;
                target = Vector3.Lerp(leg.end.position, target, weight);
                SolveLeg(leg, target, best.normal, weight);
            }
        }
        private static void SolveLeg(Leg leg, Vector3 target, Vector3 normal, float weight)
        {
            Vector3 a = leg.upper.position, b = leg.lower.position, c = leg.end.position;
            float upper = Vector3.Distance(a, b), lower = Vector3.Distance(b, c);
            Vector3 toward = target - a;
            if (upper < .001f || lower < .001f || toward.sqrMagnitude < .001f) return;
            Quaternion originalFoot = leg.end.rotation;
            float distance = Mathf.Clamp(toward.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            Vector3 direction = toward.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(b - a, direction);
            if (bend.sqrMagnitude < .0001f) bend = Vector3.ProjectOnPlane(leg.upper.forward, direction);
            if (bend.sqrMagnitude < .0001f) return;
            float x = (upper * upper + distance * distance - lower * lower) / (2 * distance);
            Vector3 elbow = a + direction * x + bend.normalized * Mathf.Sqrt(Mathf.Max(0, upper * upper - x * x));
            leg.upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * leg.upper.rotation;
            leg.lower.rotation = Quaternion.FromToRotation(leg.end.position - leg.lower.position,
                a + direction * distance - leg.lower.position) * leg.lower.rotation;
            leg.end.rotation = Quaternion.Slerp(originalFoot, Quaternion.FromToRotation(Vector3.up, normal) * originalFoot, weight);
        }
    }
}
