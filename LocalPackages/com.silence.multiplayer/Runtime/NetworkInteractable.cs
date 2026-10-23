using Unity.Netcode;
using UnityEngine;
using Silence.Interactions;
using Silence.Missions;

namespace Silence.Multiplayer
{
    // A server-written variable supplies initial state and subsequent changes.
    // No client RPC exposes world mutation.
    public sealed class NetworkInteractable : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> state = new NetworkVariable<bool>();
        private readonly NetworkVariable<ObjectPose> pose = new NetworkVariable<ObjectPose>();
        private float nextPose;
        private Interactable target;
        public Interactable Target => target != null ? target : target = GetComponent<Interactable>();
        public bool State => state.Value;

        public override void OnNetworkSpawn()
        {
            state.OnValueChanged += Changed;
            if (IsServer) Capture();
            else ApplyVisual(state.Value);
        }

        public override void OnNetworkDespawn() => state.OnValueChanged -= Changed;
        private void Changed(bool previous, bool current) { if (!IsServer) ApplyVisual(current); }

        internal void Capture()
        {
            if (!IsServer) return;
            if (Target is ItemPickup item) state.Value = item.Consumed;
            else if (Target is PrototypeDoor door) state.Value = door.IsOpen;
            else if (Target is PrototypeRadio radio) state.Value = radio.IsOn;
            else if (Target is PrototypeExit exit) state.Value = exit.Completed;
            CapturePose();
        }

        private void CapturePose()
        {
            var barrier = GetComponent<BreakableBarrier>();
            if (!(Target is ManualDoor) && !(Target is CarryableProp) && !(Target is ItemPickup) && barrier == null) return;
            pose.Value = new ObjectPose { Position = transform.position, Rotation = transform.rotation,
                Parameter = Target is ManualDoor door ? door.Angle : 0,
                Flags = (byte)(128 | (Target is CarryableProp prop && prop.Held ? 1 : 0) | (barrier != null && barrier.Broken ? 2 : 0)) };
        }
        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (Time.unscaledTime >= nextPose) { nextPose = Time.unscaledTime + .05f; Capture(); }
                return;
            }
            ObjectPose value = pose.Value;
            if ((value.Flags & 128) == 0) return;
            float blend = Vector3.Distance(transform.position, value.Position) > 3 ? 1 : 1 - Mathf.Exp(-20 * Time.deltaTime);
            if (Target is ManualDoor door) door.ApplyReplica(Mathf.Lerp(door.Angle, value.Parameter, blend));
            else
            {
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, value.Position, blend),
                    Quaternion.Slerp(transform.rotation, value.Rotation, blend));
                if (Target is CarryableProp prop) prop.SetReplicaHeld((value.Flags & 1) != 0);
            }
            var barrier = GetComponent<BreakableBarrier>();
            if (barrier != null) barrier.SetBroken((value.Flags & 2) != 0);
        }
        private void ApplyVisual(bool value)
        {
            if (Target is ItemPickup item) item.SetConsumed(value);
            else if (Target is PrototypeDoor door) door.SetOpen(value);
            else if (Target is PrototypeRadio radio) radio.SetOn(value);
            else if (Target is PrototypeExit exit) exit.SetCompleted(value);
        }
    }
}
