using Unity.Netcode;
using Silence.Interactions;
using Silence.Missions;

namespace Silence.Multiplayer
{
    // A server-written variable supplies initial state and subsequent changes.
    // No client RPC exposes world mutation.
    public sealed class NetworkInteractable : NetworkBehaviour
    {
        private readonly NetworkVariable<bool> state = new NetworkVariable<bool>();
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
