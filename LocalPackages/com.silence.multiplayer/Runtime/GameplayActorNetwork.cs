using System.Linq;
using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Player;
using Silence.Interactions;

namespace Silence.Multiplayer
{
    public sealed class GameplayActorNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<ActorStatus> status = new NetworkVariable<ActorStatus>();
        private PlayerContext actor;
        private NetworkPlayer player;
        private float next;
        public override void OnNetworkSpawn()
        {
            actor = GetComponent<PlayerContext>(); player = GetComponent<NetworkPlayer>();
            var extra = transform.Find("TeammateTarget");
            if (extra != null) extra.GetComponent<Collider>().enabled = !IsOwner && !IsServer;
            status.OnValueChanged += Changed;
            if (IsServer) Publish(); else Apply(status.Value);
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && Time.unscaledTime >= next) { next = Time.unscaledTime + .1f; Publish(); player.PublishInventory(); }
            else if (!IsServer) Apply(status.Value);
        }
        private void Publish()
        {
            status.Value = new ActorStatus { Health = (byte)actor.Vitals.State, Charge = actor.FlashlightCharge,
                Stamina = actor.Stamina.Value, Noise = actor.NoiseLevel,
                HeldObject = actor.HeldProp != null ? actor.HeldProp.GetComponent<NetworkObject>().NetworkObjectId : 0,
                Carried = actor.CarriedBy != null, CarriedBy = actor.CarriedBy != null ? actor.CarriedBy.SourceId : 0 };
        }
        private void Changed(ActorStatus previous, ActorStatus current) { if (!IsServer) Apply(current); }
        private void Apply(ActorStatus value)
        {
            actor.Vitals.ApplySnapshot((VitalState)value.Health); actor.FlashlightCharge = value.Charge;
            actor.Stamina.ApplySnapshot(value.Stamina); actor.NoiseLevel = value.Noise;
            if (value.HeldObject != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(value.HeldObject, out var entity))
                actor.HeldProp = entity.GetComponent<CarryableProp>();
            else actor.HeldProp = null;
            actor.CarriedBy = value.Carried && actor.Game != null ? actor.Game.Actors.FirstOrDefault(a => a.SourceId == value.CarriedBy) : null;
        }
        public override void OnNetworkDespawn() => status.OnValueChanged -= Changed;
    }
}
