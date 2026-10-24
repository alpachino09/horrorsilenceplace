using UnityEngine;
using Silence.Core;
using Silence.SoundSimulation;

namespace Silence.Player
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerContext))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        [SerializeField] private bool animationMotion;
        private CharacterController controller;
        private PlayerContext player;
        private float verticalSpeed;
        private readonly StepCycle stepCycle = new StepCycle();
        private readonly Collider[] overlaps = new Collider[32];
        private Vector3 smoothedVelocity;
        private float eyeVelocity, contactHeartbeat = -100;
        private float lastStep = -100;
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalVelocity => verticalSpeed;
        public bool IsCrouched { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool AnimationMotion => animationMotion;
        public void ConfigureAnimationMotion(bool value) => animationMotion = value;
        public void ResetMotion()
        { smoothedVelocity = PlanarVelocity = Vector3.zero; verticalSpeed = eyeVelocity = 0; contactHeartbeat = -100; }
        // An evaluated Animator renews this lease. A disabled/missing Animator automatically
        // returns the existing distance-based footstep source to service.
        public void AnimationContactHeartbeat() => contactHeartbeat = Time.unscaledTime;
        public void EmitAnimationStep(Vector3 point)
        {
            if (!animationMotion || player.Authority == null || !player.Authority.CanSimulate(player) ||
                player.Game == null || !controller.enabled || !controller.isGrounded || PlanarVelocity.sqrMagnitude < .02f ||
                !player.Game.CanAct(player)) return;
            EmitStep(point);
        }
        private void EmitStep(Vector3 point)
        {
            if (player.Game == null || !player.Game.IsAuthority || Time.time - lastStep < .13f) return;
            lastStep = Time.time;
            SurfaceKind material = SurfaceMaterial.Below(point);
            StepProfile step = StepProfile.For(material, IsCrouched, IsSprinting);
            player.Game.Emit(player, point + Vector3.up * .15f, step.Intensity, step.Range, NoiseKind.Footstep, material);
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            player = GetComponent<PlayerContext>();
        }

        private void Update()
        {
            if (!player.IsLocalOwner) return;
            var input = player.Input;
            bool active = input.GameplayActive;
            Simulate(active ? input.Move * (input.CarefulWalk ? .45f : 1) : Vector2.zero, active && input.Sprint && !input.CarefulWalk,
                active && input.Crouch, active && input.JumpPressed, Time.deltaTime);
        }

        // Shared simulation step: offline Update or the authoritative network tick.
        // Client prediction/reconciliation is not implemented by this method.
        public void Simulate(Vector2 axis, bool sprint, bool crouch, bool jump, float deltaTime)
        {
            if (player.Authority == null || !player.Authority.CanSimulate(player) ||
                deltaTime <= 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime)) return;
            if (!player.Vitals.Alive || player.Game != null && (!player.Game.CanAct(player) || player.CarriedBy != null))
            { ResetMotion(); return; }
            Vector3 before = transform.position;
            if (player.Game != null)
            {
                sprint &= player.Stamina.CanSprint;
                player.Stamina.Tick(Mathf.Min(deltaTime, 1), sprint && !crouch && axis.sqrMagnitude > .01f);
            }
            var config = player.Settings;
            if (IsCrouched && !crouch && !CanStand()) crouch = true;
            IsCrouched = crouch;
            IsSprinting = sprint && !crouch && axis.sqrMagnitude > .01f;
            float height = crouch ? config.crouchingHeight : config.standingHeight;
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
            float eyeHeight = animationMotion ? Mathf.SmoothDamp(player.View.transform.localPosition.y,
                height - .16f, ref eyeVelocity, .11f, Mathf.Infinity, deltaTime) : height - .16f;
            player.View.transform.localPosition = Vector3.up * eyeHeight;

            axis = Vector2.ClampMagnitude(axis, 1f);
            float speed = crouch ? config.crouchSpeed :
                sprint ? config.runSpeed : config.walkSpeed;
            if (player.HeldProp != null || player.HeldTeammate != null) speed *= .65f;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jump && controller.isGrounded && !crouch)
                verticalSpeed = Mathf.Sqrt(config.jumpHeight * -2f * config.gravity);
            verticalSpeed += config.gravity * deltaTime;
            Vector3 velocity = (transform.right * axis.x + transform.forward * axis.y) * speed;
            if (animationMotion)
            {
                float acceleration = axis.sqrMagnitude < .01f ? 18 : crouch ? 7 : 12;
                smoothedVelocity = Vector3.MoveTowards(smoothedVelocity, velocity, acceleration * deltaTime);
                velocity = smoothedVelocity;
            }
            velocity.y = verticalSpeed;
            bool groundedBefore = controller.isGrounded; float fallingSpeed = verticalSpeed;
            CollisionFlags flags = controller.Move(velocity * deltaTime);
            PlanarVelocity = Vector3.ProjectOnPlane(transform.position - before, Vector3.up) / deltaTime;
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
            if (player.Game != null && !groundedBefore && controller.isGrounded && fallingSpeed < -4)
            {
                SurfaceKind surface = SurfaceMaterial.Below(transform.position);
                player.Game.Emit(player, transform.position + Vector3.up * .15f,
                    Mathf.Clamp(-fallingSpeed / 15, .2f, .8f), 24, NoiseKind.Impact, surface);
            }
            bool distanceStep = stepCycle.Advance(Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude,
                controller.isGrounded, crouch, sprint);
            if (player.Game != null && distanceStep && (!animationMotion || Time.unscaledTime - contactHeartbeat > .25f))
                EmitStep(transform.position);

            // Prototype safety only; not the campaign death/restart system.
            if (transform.position.y < -8f) Respawn();
        }

        private bool CanStand()
        {
            float radius = controller.radius - 0.02f;
            Vector3 bottom = transform.position + Vector3.up * (controller.radius + 0.03f);
            Vector3 top = transform.position + Vector3.up *
                (player.Settings.standingHeight - controller.radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlaps,
                ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(transform)) return false;
            return true;
        }

        public void Respawn()
        {
            if (player.Authority == null || !player.Authority.CanSimulate(player)) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(player.SpawnPosition, player.SpawnRotation);
            controller.enabled = true;
            ResetMotion();
        }
    }
}
