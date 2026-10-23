using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Interactions;
using Silence.Networking;
using Silence.Player;
using Silence.SoundSimulation;
using Silence.Gameplay;

namespace Silence.Multiplayer
{
    public sealed class NetworkWorldAuthority : NetworkBehaviour, IWorldAuthority, IGameplayCommands
    {
        private readonly Dictionary<PlayerContext, NetworkPlayer> players = new Dictionary<PlayerContext, NetworkPlayer>();
        private readonly Dictionary<ulong, InteractionGate> gates = new Dictionary<ulong, InteractionGate>();
        private readonly Dictionary<ulong, ActionBudget> actionBudgets = new Dictionary<ulong, ActionBudget>();
        private readonly Dictionary<ulong, uint> actionSequences = new Dictionary<ulong, uint>();
        private readonly Dictionary<ulong, double> microphoneTimes = new Dictionary<ulong, double>();
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
            if (player.IsServer) actionBudgets[player.OwnerClientId] = new ActionBudget();
            var game = GetComponent<GameplayDirector>();
            if (game != null) { game.SetAuthority(player.IsServer); game.Register(player.Context, unchecked((int)player.NetworkObjectId)); }
            InteractionDiagnostics.Trace(this, "REGISTER", "actor=" + player.OwnerClientId +
                " playerServer=" + player.IsServer + " worldServer=" + IsServer +
                " gate=" + gates.ContainsKey(player.OwnerClientId));
            player.Context.Bind(this);
        }
        internal void Unregister(NetworkPlayer player)
        {
            GetComponent<GameplayDirector>()?.Unregister(player.Context);
            players.Remove(player.Context);
            gates.Remove(player.OwnerClientId);
            actionBudgets.Remove(player.OwnerClientId); actionSequences.Remove(player.OwnerClientId); microphoneTimes.Remove(player.OwnerClientId);
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
            GetComponent<GameplayDirector>()?.BeginRound();
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
            if (actor.Game != null && !actor.Game.CanAct(actor)) return;
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
            float previousMessageTime = actor.MessageUntil;
            bool applied = target.Apply(actor);
            InteractionDiagnostics.Trace(this, "APPLY", "target=" + target.name + " success=" + applied +
                (target is PrototypeDoor door ? " doorOpen=" + door.IsOpen + " rotation=" + door.transform.localEulerAngles : "") +
                " timeScale=" + Time.timeScale);
            // Inventory-full feedback must also reach a remote owner.
            if (actor.Game == null) player.SendFeedback(applied);
            else player.SendGameplayFeedback(applied, actor.MessageUntil != previousMessageTime ? actor.Message : "");
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

        public bool RequestAction(PlayerContext actor, ActorAction action, int slot, float amount, Interactable target = null)
        {
            if (!Running || !IsLocalOwner(actor) || actor.Game == null) return false;
            ulong targetId = 0;
            if (target != null)
            {
                var entity = target.GetComponent<NetworkObject>();
                if (entity == null || !entity.IsSpawned) return false;
                targetId = entity.NetworkObjectId;
            }
            players[actor].RequestGameplayAction(action, slot, amount, targetId); return true;
        }
        internal void ApplyAction(NetworkPlayer player, ulong sender, uint sequence, ActorAction action, int slot, float amount, ulong targetId)
        {
            if (!IsServer || !Running || player.OwnerClientId != sender || !players.ContainsKey(player.Context) ||
                !actionBudgets.TryGetValue(sender, out var budget) || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            if (actionSequences.TryGetValue(sender, out uint previous) && !PlayerCommand.IsNewer(sequence, previous)) return;
            actionSequences[sender] = sequence;
            double now = Time.unscaledTimeAsDouble;
            if (!budget.TrySpend(now)) return;
            if (action == ActorAction.Microphone)
            {
                if (microphoneTimes.TryGetValue(sender, out double allowed) && now < allowed) return;
                microphoneTimes[sender] = now + .1;
            }
            Interactable target = null;
            if (action == ActorAction.DragDoor)
            {
                if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var entity)) return;
                target = entity.GetComponent<Interactable>();
                var actor = player.Context;
                Physics.SyncTransforms();
                Transform eye = actor.View.transform;
                if (target == null || !Physics.Raycast(eye.position, eye.forward, out RaycastHit hit,
                    actor.Settings.interactionRange, ~0, QueryTriggerInteraction.Ignore) ||
                    hit.collider.GetComponentInParent<Interactable>() != target) return;
            }
            float previousMessageTime = player.Context.MessageUntil;
            player.Context.Game?.Action(player.Context, action, slot, amount, target);
            if (player.Context.MessageUntil != previousMessageTime) player.SendGameplayFeedback(false, player.Context.Message);
            player.PublishInventory();
        }
    }
}
