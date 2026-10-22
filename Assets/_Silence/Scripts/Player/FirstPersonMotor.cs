using UnityEngine;

namespace Silence.Player
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerContext))]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        private CharacterController controller;
        private PlayerContext player;
        private float verticalSpeed;
        private readonly Collider[] overlaps = new Collider[32];
        public bool IsCrouched { get; private set; }

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
            Simulate(active ? input.Move : Vector2.zero, active && input.Sprint,
                active && input.Crouch, active && input.JumpPressed, Time.deltaTime);
        }

        // Explicit tick seam for PlayMode tests and a later prediction adapter.
        // Transport/server reconciliation is not implemented by this method.
        public void Simulate(Vector2 axis, bool sprint, bool crouch, bool jump, float deltaTime)
        {
            if (!player.IsLocalOwner || deltaTime <= 0f || float.IsNaN(deltaTime) ||
                float.IsInfinity(deltaTime)) return;
            var config = player.Settings;
            if (IsCrouched && !crouch && !CanStand()) crouch = true;
            IsCrouched = crouch;
            float height = crouch ? config.crouchingHeight : config.standingHeight;
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
            player.View.transform.localPosition = Vector3.up * (height - 0.16f);

            axis = Vector2.ClampMagnitude(axis, 1f);
            float speed = crouch ? config.crouchSpeed :
                sprint ? config.runSpeed : config.walkSpeed;
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            if (jump && controller.isGrounded && !crouch)
                verticalSpeed = Mathf.Sqrt(config.jumpHeight * -2f * config.gravity);
            verticalSpeed += config.gravity * deltaTime;
            Vector3 velocity = (transform.right * axis.x + transform.forward * axis.y) * speed;
            velocity.y = verticalSpeed;
            CollisionFlags flags = controller.Move(velocity * deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;

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
            if (!player.IsLocalOwner) return;
            controller.enabled = false;
            transform.SetPositionAndRotation(player.SpawnPosition, player.SpawnRotation);
            controller.enabled = true;
            verticalSpeed = 0f;
        }
    }
}
