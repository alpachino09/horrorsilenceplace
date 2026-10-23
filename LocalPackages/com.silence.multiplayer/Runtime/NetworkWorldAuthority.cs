using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Interactions;
using Silence.Networking;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Multiplayer
{
    public sealed class NetworkWorldAuthority : NetworkBehaviour, IWorldAuthority
    {
        private readonly Dictionary<PlayerContext, NetworkPlayer> players = new Dictionary<PlayerContext, NetworkPlayer>();
        private readonly Dictionary<ulong, InteractionGate> gates = new Dictionary<ulong, InteractionGate>();
        private readonly NetworkVariable<bool> running = new NetworkVariable<bool>();
        private readonly NetworkVariable<int> playerCount = new NetworkVariable<int>();
        public bool Running => IsSpawned && running.Value;
        public int PlayerCount => playerCount.Value;

        internal void Register(NetworkPlayer player)
        {
            players.Add(player.Context, player);
            // NGO spawns the host player before sweeping in-scene NetworkObjects.
            // The player's role is initialized here; this world's IsServer may
            // still be false until its own OnNetworkSpawn. Use the spawned actor.
            if (player.IsServer) gates[player.OwnerClientId] = new InteractionGate();
            InteractionDiagnostics.Trace(this, "REGISTER", "actor=" + player.OwnerClientId +
                " playerServer=" + player.IsServer + " worldServer=" + IsServer +
                " gate=" + gates.ContainsKey(player.OwnerClientId));
            player.Context.Bind(this);
        }
        internal void Unregister(NetworkPlayer player)
        {
            players.Remove(player.Context);
            gates.Remove(player.OwnerClientId);
        }
        private void Update()
        {
            if (IsSpawned && IsServer) playerCount.Value = NetworkManager.ConnectedClientsIds.Count;
        }
        public void BeginRound()
        {
            if (!IsServer || !IsSpawned || Running) return;
            NetworkSession.Instance.Admission.StartRound();
            running.Value = true;
        }
        public bool IsLocalOwner(PlayerContext actor) => actor != null &&
            players.TryGetValue(actor, out var player) && player.IsOwner;
        public bool CanSimulate(PlayerContext actor) => IsServer && Running &&
            actor != null && players.ContainsKey(actor);

        public bool RequestInteraction(PlayerContext actor, Interactable target)
        {
            if (!Running || !IsLocalOwner(actor) || target == null)
            {
                InteractionDiagnostics.Trace(this, "REQUEST_REJECT", "running=" + Running +
                    " owner=" + IsLocalOwner(actor) + " target=" + (target != null ? target.name : "none"));
                return false;
            }
            var networked = target.GetComponent<NetworkInteractable>();
            if (networked == null || !networked.IsSpawned)
            {
                InteractionDiagnostics.Trace(this, "REQUEST_REJECT", "target=" + target.name +
                    " networkComponent=" + (networked != null) + " spawned=" + (networked != null && networked.IsSpawned));
                return false;
            }
            InteractionDiagnostics.Trace(this, "REQUEST", "target=" + target.name + " entity=" + networked.NetworkObjectId);
            players[actor].RequestInteraction(networked.NetworkObjectId);
            return true; // Queued; only a server response confirms success.
        }

        internal void ApplyInteraction(NetworkPlayer player, ulong sender, ulong targetId, uint sequence)
        {
            InteractionDiagnostics.Trace(this, "SERVER_RECEIVE", "sender=" + sender + " owner=" + player.OwnerClientId +
                " target=" + targetId + " sequence=" + sequence + " server=" + IsServer + " running=" + Running);
            if (!IsServer || !Running || player.OwnerClientId != sender)
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "role/round/sender"); return; }
            if (!players.TryGetValue(player.Context, out var registered) || registered != player)
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "unregistered actor"); return; }
            if (!gates.TryGetValue(sender, out var gate))
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "missing interaction gate"); return; }
            if (!gate.TryBegin(sequence, Time.unscaledTimeAsDouble))
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "cooldown or replay"); return; }
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var entity))
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "target not spawned"); return; }
            var state = entity.GetComponent<NetworkInteractable>();
            if (state == null)
            { InteractionDiagnostics.Trace(this, "SERVER_REJECT", "missing NetworkInteractable"); return; }
            var target = state.Target;
            var actor = player.Context;
            Transform eye = actor.View.transform;
            Physics.SyncTransforms();
            bool hitSomething = Physics.Raycast(eye.position, eye.forward, out RaycastHit hit,
                actor.Settings.interactionRange, ~0, QueryTriggerInteraction.Ignore);
            bool clear = hitSomething && hit.collider.GetComponentInParent<Interactable>() == target;
            if (target == null || !InteractionRules.CanApply(true, true, target.CanInteract(actor),
                hitSomething ? hit.distance : float.PositiveInfinity, actor.Settings.interactionRange, clear))
            {
                InteractionDiagnostics.Trace(this, "SERVER_REJECT", "ray/availability/range target=" +
                    (target != null ? target.name : "none") + " hit=" + (hitSomething ? hit.collider.name : "none") +
                    " distance=" + (hitSomething ? hit.distance.ToString("F3") : "none") +
                    " range=" + actor.Settings.interactionRange + " clear=" + clear);
                return;
            }
            bool applied = target.Apply(actor);
            InteractionDiagnostics.Trace(this, "APPLY", "target=" + target.name + " success=" + applied +
                (target is PrototypeDoor door ? " doorOpen=" + door.IsOpen + " rotation=" + door.transform.localEulerAngles : "") +
                " timeScale=" + Time.timeScale);
            // Inventory-full feedback must also reach a remote owner.
            player.SendFeedback(applied);
            if (!applied) return;
            state.Capture();
            player.PublishInventory();
            EmitNoise(actor, NoiseKind.Interaction, .2f);
        }

        public void EmitNoise(PlayerContext actor, NoiseKind kind, float intensity)
        {
            if (!CanSimulate(actor) || float.IsNaN(intensity) || float.IsInfinity(intensity)) return;
            var noise = GetComponent<NoiseSystem>();
            if (noise == null) return;
            noise.Publish(new NoiseEvent(actor.transform.position, Mathf.Clamp01(intensity), 12f, kind,
                unchecked((int)players[actor].NetworkObjectId), NetworkManager.ServerTime.Time));
        }
    }
}
