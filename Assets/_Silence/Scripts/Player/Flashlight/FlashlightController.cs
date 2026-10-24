using System;
using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    public interface IFlashlightTransport
    {
        bool HasAuthority { get; }
        double ServerNow { get; }
        void Send(FlashlightRequest request);
    }
    [Serializable]
    public struct FlashlightFrame
    {
        public FlashlightState state;
        public uint revision;
        public double startedAt;
        public float duration, charge;
        public bool beam, wantedOn;
        public byte spares, capacity;
    }
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext)), DefaultExecutionOrder(150)]
    public sealed class FlashlightController : MonoBehaviour
    {
        [SerializeField, Min(1)] private float batterySeconds = 720;
        [SerializeField, Range(3,5)] private float reactionSeconds = 4.2f;
        [SerializeField, Range(5,7)] private float reloadSeconds = 6.2f;
        [SerializeField, Range(1,12)] private int maximumSparePacks = 4;
        [SerializeField, Range(0,12)] private int startingSparePacks = 2;
        [SerializeField, Range(0,1)] private float buttonNoise = .035f, palmTapNoise = .12f, batteryNoise = .05f;
        private PlayerContext actor;
        private FirstPersonMotor motor;
        private FlashlightModel model;
        private IFlashlightTransport transport;
        private FlashlightFrame frame;
        private bool receivedRemote;
        private double noiseClock, noiseStart = -1;
        private FlashlightState noiseState;
        private VitalState previousHealth;
        public FlashlightFrame Frame => frame;
        public bool HasState => transport != null ? HasAuthority || receivedRemote : actor != null && actor.Authority != null;
        public double Clock => transport != null ? transport.ServerNow : Time.timeAsDouble;
        public bool HasAuthority => transport != null ? transport.HasAuthority : actor != null && actor.Authority != null && actor.Authority.CanSimulate(actor);
        public float InsertFraction => .67f;
        private void Awake()
        {
            actor = GetComponent<PlayerContext>(); motor = GetComponent<FirstPersonMotor>();
            if (actor.Flashlight != null && actor.Flashlight != this) throw new InvalidOperationException("Duplicate flashlight controller.");
            actor.Inventory.ConfigureBatteryPacks(maximumSparePacks, Mathf.Min(startingSparePacks,maximumSparePacks));
            model = new FlashlightModel(actor.Inventory,new FlashlightRules(batterySeconds,reactionSeconds:reactionSeconds,reloadSeconds:reloadSeconds),Clock);
            actor.Flashlight = this; previousHealth = actor.Vitals.State; Refresh();
        }
        public void Bind(IFlashlightTransport value)
        {
            transport = value; receivedRemote = false;
            if (model != null && value.HasAuthority) { model.Reset(Clock); Refresh(); }
        }
        public void ClearRemoteState() => receivedRemote = false;
        private void OnEnable() { if (actor != null) actor.Flashlight = this; }
        private void OnDisable() { if (actor != null && actor.Flashlight == this) actor.Flashlight = null; }
        public void ResetForChapter()
        { if (!HasAuthority || model == null) return; model.Reset(Clock); previousHealth=actor.Vitals.State; Refresh(); }
        private bool Available => actor.Authority != null && actor.Authority.CanSimulate(actor) && actor.Vitals.Alive && actor.CarriedBy == null && actor.HeldProp == null && actor.HeldTeammate == null && (actor.Game == null || actor.Game.CanAct(actor));
        public bool Request(FlashlightRequest request)
        {
            if (!HasAuthority) { transport?.Send(request); return transport != null; }
            if (!Available || model == null) return false;
            if (request == FlashlightRequest.Reload && motor != null && motor.IsSprinting) return false;
            bool accepted = model.Request(request,Clock); Refresh();
            if (accepted && request == FlashlightRequest.Toggle && (frame.state == FlashlightState.SwitchingOn || frame.state == FlashlightState.SwitchingOff)) Emit(buttonNoise,5);
            if (!accepted && request == FlashlightRequest.Reload && actor.IsLocalOwner && actor.Inventory.BatteryPacks == 0)
                actor.ShowMessage(Silence.UI.PrototypePreferences.English ? "No spare battery packs." : "Нет запасных батареек.");
            return accepted;
        }
        private void Update()
        {
            if (actor.IsLocalOwner && actor.Input != null && actor.Input.enabled && actor.Input.GameplayActive)
            {
                if (actor.Input.FlashlightPressed) Request(FlashlightRequest.Toggle);
                if (actor.Input.ReloadFlashlightPressed) Request(FlashlightRequest.Reload);
                if (actor.Input.HolsterFlashlightPressed) Request(FlashlightRequest.Holster);
            }
            if (!HasAuthority || model == null) return;
            bool damaged = previousHealth != actor.Vitals.State; previousHealth = actor.Vitals.State;
            bool sprintReload = model.State == FlashlightState.ReloadingBatteries && motor != null && motor.IsSprinting;
            if (damaged || sprintReload) model.SetSuspended(true,Clock,actor.Vitals.Alive);
            model.SetSuspended(!Available,Clock,actor.Vitals.Alive);
            model.Tick(Clock,Time.deltaTime); Refresh(); SimulateNoise();
        }
        private void Emit(float intensity,float range)
        { if (intensity>0) actor.Game?.Emit(actor,actor.View.transform.position,intensity,range,Silence.SoundSimulation.NoiseKind.Interaction,SurfaceKind.Metal); }
        private void SimulateNoise()
        {
            double now=Clock;
            if (frame.state!=noiseState || frame.startedAt!=noiseStart) { noiseState=frame.state;noiseStart=frame.startedAt;noiseClock=noiseStart; }
            void Cue(float fraction,float intensity,float range)
            { double at=noiseStart+frame.duration*fraction;if(noiseClock<at && now>=at)Emit(intensity,range); }
            if(frame.state==FlashlightState.FailureReaction)
            { Cue(.22f,buttonNoise,5);Cue(.32f,buttonNoise,5);Cue(.51f,palmTapNoise,12);Cue(.65f,palmTapNoise,12);Cue(.79f,palmTapNoise,12); }
            if(frame.state==FlashlightState.ReloadingBatteries)
            { Cue(.14f,batteryNoise,6);Cue(.38f,batteryNoise,6);Cue(.67f,batteryNoise,6);Cue(.85f,batteryNoise,6); }
            noiseClock=now;
        }
        private void Refresh()
        {
            frame = new FlashlightFrame { state=model.State, revision=model.Revision, startedAt=model.StartedAt, duration=(float)model.Duration,
                charge=(float)model.Charge, beam=model.BeamOn, wantedOn=model.WantedOn,
                spares=(byte)actor.Inventory.BatteryPacks, capacity=(byte)actor.Inventory.BatteryPackCapacity };
            actor.FlashlightCharge=frame.charge; actor.FlashlightOn=frame.beam;
        }
        public void ApplyRemote(FlashlightFrame value)
        {
            if (HasAuthority || value.capacity < 1 || value.capacity > 12 || value.spares > value.capacity ||
                float.IsNaN(value.charge) || float.IsInfinity(value.charge) || value.charge < 0 || value.charge > 1 ||
                double.IsNaN(value.startedAt) || double.IsInfinity(value.startedAt) || value.startedAt < 0 ||
                float.IsNaN(value.duration) || float.IsInfinity(value.duration) || value.duration < 0 || value.duration > 10 ||
                (byte)value.state > (byte)FlashlightState.Holstering) return;
            frame=value; receivedRemote=true; actor.Inventory.ApplyBatteryPacksSnapshot(value.capacity,value.spares);
            actor.FlashlightCharge=value.charge; actor.FlashlightOn=value.beam;
        }
    }
}
