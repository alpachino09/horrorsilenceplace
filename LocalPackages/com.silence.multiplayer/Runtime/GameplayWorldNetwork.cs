using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Gameplay;
using Silence.SoundSimulation;

namespace Silence.Multiplayer
{
    public sealed class GameplayWorldNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<CampaignStatus> status = new NetworkVariable<CampaignStatus>();
        private GameplayDirector game;
        private float nextState;
        public override void OnNetworkSpawn()
        {
            game = GetComponent<GameplayDirector>(); game.SetAuthority(IsServer);
            status.OnValueChanged += Changed;
            if (IsServer)
            {
                game.StateChanged += Publish; game.ChapterReset += CaptureObjects;
                game.Noise.Emitted += RelayNoise; Publish();
            }
            else Apply(status.Value);
        }
        private void Update() { if (IsSpawned && IsServer && Time.unscaledTime >= nextState) { nextState = Time.unscaledTime + .1f; Publish(); } }
        private void Publish()
        {
            if (!IsSpawned || !IsServer) return;
            status.Value = new CampaignStatus { Chapter = (byte)game.Campaign.Chapter, Mission = (byte)game.Campaign.Mission,
                Phase = (byte)game.Phase, Ending = (byte)game.Campaign.Ending, Difficulty = (byte)game.Settings.difficulty, Seconds = game.PhaseSeconds };
        }
        private void CaptureObjects() { foreach (var item in SceneQuery.All<NetworkInteractable>()) if (item.IsSpawned) item.Capture(); }
        private void Changed(CampaignStatus previous, CampaignStatus current) { if (!IsServer) Apply(current); }
        private void Apply(CampaignStatus value)
        {
            game.Settings.difficulty = (Difficulty)value.Difficulty;
            game.ApplyReplica((ChapterId)value.Chapter, (MissionFlags)value.Mission, (RoundPhase)value.Phase, (Ending)value.Ending, value.Seconds);
        }
        private void RelayNoise(NoiseEvent sound) => NoiseRpc(new NoisePacket { Position = sound.Position, Intensity = sound.Intensity,
            Range = sound.Range, Source = sound.SourceId, Kind = (byte)sound.Kind, Surface = (byte)sound.Surface });
        [Rpc(SendTo.NotServer, InvokePermission = RpcInvokePermission.Server, Delivery = RpcDelivery.Unreliable)]
        private void NoiseRpc(NoisePacket sound)
        {
            if (game == null || IsServer) return;
            game.Noise.Publish(new NoiseEvent(sound.Position, sound.Intensity, sound.Range, (NoiseKind)sound.Kind,
                sound.Source, NetworkManager.ServerTime.Time, (SurfaceKind)sound.Surface));
        }
        public override void OnNetworkDespawn()
        {
            status.OnValueChanged -= Changed;
            if (game != null) { game.StateChanged -= Publish; game.ChapterReset -= CaptureObjects; game.Noise.Emitted -= RelayNoise; }
        }
    }
}
