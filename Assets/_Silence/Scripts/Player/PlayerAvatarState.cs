using UnityEngine;

namespace Silence.Player
{
    // Network bridge supplies confirmed visual state; offline play reads the existing motor.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext), typeof(FirstPersonMotor))]
    public sealed class PlayerAvatarState : MonoBehaviour
    {
        private PlayerContext actor;
        private FirstPersonMotor motor;
        private CharacterController controller;
        private bool networkControlled;
        public bool Ready { get; private set; }
        public bool Grounded { get; private set; }
        public bool Crouched { get; private set; }
        public bool Carrying { get; private set; }
        public bool HasVelocity { get; private set; }
        public Vector3 Velocity { get; private set; }
        public float VerticalSpeed { get; private set; }
        private void Awake()
        { actor = GetComponent<PlayerContext>(); motor = GetComponent<FirstPersonMotor>(); controller = GetComponent<CharacterController>(); }
        public void BeginNetwork() { networkControlled = true; Ready = HasVelocity = false; }
        public void EndNetwork() { networkControlled = false; Ready = HasVelocity = false; Velocity = Vector3.zero; }
        public void ApplyNetwork(bool ready, bool grounded, bool crouched, bool carrying, Vector3 velocity, float verticalSpeed)
        {
            if (!networkControlled) return;
            Ready = ready; Grounded = grounded; Crouched = crouched; Carrying = carrying;
            HasVelocity = true; Velocity = velocity; VerticalSpeed = verticalSpeed;
        }
        public void CaptureOffline()
        {
            if (networkControlled || actor == null) return;
            Ready = actor.Authority != null;
            Grounded = controller != null && controller.enabled && controller.isGrounded;
            Crouched = motor != null && motor.IsCrouched;
            Carrying = actor.HeldProp != null || actor.HeldTeammate != null;
            HasVelocity = motor != null && motor.AnimationMotion;
            Velocity = motor != null ? motor.PlanarVelocity : Vector3.zero;
            VerticalSpeed = motor != null ? motor.VerticalVelocity : 0;
        }
    }
}
