using Unity.Netcode;
using UnityEngine;
using Silence.Player;
using Silence.Core;

namespace Silence.Multiplayer
{
    // Server-confirmed state and reliable, server-originated presentation events.
    // The motor remains the sole movement authority; no bones are sent over the network.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext), typeof(PlayerAvatarState)), DefaultExecutionOrder(400)]
    public sealed class PlayerAvatarNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<AvatarMotionSnapshot> snapshot = new NetworkVariable<AvatarMotionSnapshot>();
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
            if (IsServer) actor.AnimationCue += SendCue;
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
            snapshot.Value = new AvatarMotionSnapshot {
                Flags = (byte)(128 | (!running || controller.enabled && controller.isGrounded ? 1 : 0) |
                    (motor.IsCrouched ? 2 : 0) | (actor.HeldProp != null || actor.HeldTeammate != null ? 4 : 0)),
                Velocity = running ? Vector3.ClampMagnitude(motor.PlanarVelocity, 15) : Vector3.zero,
                VerticalSpeed = running ? Mathf.Clamp(motor.VerticalVelocity, -80, 20) : 0 };
        }
        private void Apply()
        {
            var value = snapshot.Value; byte flags = value.Flags;
            visual.ApplyNetwork((flags & 128) != 0, (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, value.Velocity, value.VerticalSpeed);
        }
        private void SendCue(PlayerAnimationCue cue)
        { if (IsSpawned && IsServer) AnimationCueRpc((byte)cue.Kind, cue.Point, cue.Strength); }
        [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
        private void AnimationCueRpc(byte kind, Vector3 point, float strength)
        { if (!IsServer && actor != null) actor.ApplyAnimationCue(new PlayerAnimationCue((AnimationCueKind)kind, point, strength)); }
        public override void OnNetworkDespawn()
        { if (actor != null) actor.AnimationCue -= SendCue; if (visual != null) visual.EndNetwork(); }
    }

    public struct AvatarMotionSnapshot : INetworkSerializable, System.IEquatable<AvatarMotionSnapshot>
    {
        public byte Flags;
        public Vector3 Velocity;
        public float VerticalSpeed;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        { serializer.SerializeValue(ref Flags); serializer.SerializeValue(ref Velocity); serializer.SerializeValue(ref VerticalSpeed); }
        public bool Equals(AvatarMotionSnapshot other) => Flags == other.Flags && Velocity.Equals(other.Velocity) && VerticalSpeed.Equals(other.VerticalSpeed);
    }
}
