using Unity.Netcode;
using UnityEngine;
using Silence.Player;

namespace Silence.Multiplayer
{
    // Attached only to a copied player prefab. No RPCs or changes to motor/interaction authority.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext), typeof(PlayerAvatarState)), DefaultExecutionOrder(400)]
    public sealed class PlayerAvatarNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<byte> snapshot = new NetworkVariable<byte>();
        private PlayerContext actor;
        private PlayerAvatarState visual;
        private FirstPersonMotor motor;
        private CharacterController controller;
        private float next;
        public override void OnNetworkSpawn()
        {
            actor = GetComponent<PlayerContext>(); visual = GetComponent<PlayerAvatarState>();
            motor = GetComponent<FirstPersonMotor>(); controller = GetComponent<CharacterController>();
            visual.BeginNetwork(); next = 0;
            if (IsServer) Publish();
            Apply();
        }
        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer && Time.unscaledTime >= next) { next = Time.unscaledTime + .05f; Publish(); }
            Apply();
        }
        private void Publish()
        {
            bool running = NetworkSession.Instance != null && NetworkSession.Instance.World != null && NetworkSession.Instance.World.Running;
            snapshot.Value = (byte)(128 | (!running || controller.enabled && controller.isGrounded ? 1 : 0) |
                (motor.IsCrouched ? 2 : 0) | (actor.HeldProp != null || actor.HeldTeammate != null ? 4 : 0));
        }
        private void Apply()
        {
            byte value = snapshot.Value;
            visual.ApplyNetwork((value & 128) != 0, (value & 1) != 0, (value & 2) != 0, (value & 4) != 0);
        }
        public override void OnNetworkDespawn() { if (visual != null) visual.EndNetwork(); }
    }
}
