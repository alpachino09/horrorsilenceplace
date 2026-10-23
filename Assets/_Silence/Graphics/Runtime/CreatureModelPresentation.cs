using UnityEngine;
using Silence.Core;
using Silence.Creatures;

namespace Silence.Graphics
{
    // Presentation only: the existing server creature owns movement, hearing and damage.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HearingCreature))]
    [DefaultExecutionOrder(500)]
    public sealed class CreatureModelPresentation : MonoBehaviour
    {
        [SerializeField] private Animator model;
        [SerializeField] private float walkMetresPerSecond = 1.8f;
        [SerializeField] private float runMetresPerSecond = 6;
        private HearingCreature creature;
        private Vector3 previous;
        private float speed;
        private int stateHash;
        private static readonly int Idle = Animator.StringToHash("Base Layer.Idle");
        private static readonly int Walk = Animator.StringToHash("Base Layer.Walk");
        private static readonly int Run = Animator.StringToHash("Base Layer.Run");
        private static readonly int Listen = Animator.StringToHash("Base Layer.Listen");
        private static readonly int Recover = Animator.StringToHash("Base Layer.Recover");
        public Animator Model => model;
        public void Configure(Animator value, float walkSpeed, float runSpeed)
        { model = value; walkMetresPerSecond = Mathf.Max(.1f, walkSpeed); runMetresPerSecond = Mathf.Max(.1f, runSpeed); }
        private void OnEnable()
        {
            creature = GetComponent<HearingCreature>(); previous = transform.position; speed = 0; stateHash = 0;
            if (model != null) model.applyRootMotion = false;
        }
        private void LateUpdate()
        {
            if (model == null || model.runtimeAnimatorController == null || Time.deltaTime <= 0) return;
            Vector3 delta = transform.position - previous; previous = transform.position;
            delta.y = 0;
            float measured = delta.magnitude > 5 ? 0 : delta.magnitude / Time.deltaTime;
            speed = Mathf.Lerp(speed, measured, 1 - Mathf.Exp(-12 * Time.deltaTime));
            int next;
            float playback = 1;
            if (creature.State == CreatureState.Dormant) next = Idle;
            else if (creature.State == CreatureState.Recover) next = Recover;
            else if (speed > .12f)
            {
                bool chasing = creature.State == CreatureState.Chase;
                next = chasing ? Run : Walk;
                playback = Mathf.Clamp(speed / (chasing ? runMetresPerSecond : walkMetresPerSecond), .65f, 1.5f);
            }
            else next = creature.State == CreatureState.Investigate || creature.State == CreatureState.Search ? Listen : Idle;
            if (next != stateHash) { model.CrossFadeInFixedTime(next, .15f, 0); stateHash = next; }
            model.speed = playback;
            // Recover is not treated as a successful attack: it also occurs after breaking barriers.
            // Attack animation needs an explicit server event when the selected model is integrated.
        }
    }
}
