using System;

namespace Silence.Core
{
    public enum FlashlightState : byte { Holstered, Equipping, Ready, SwitchingOn, SwitchingOff, BatteryDepleted, FailureReaction, ReloadingBatteries, Holstering }
    public enum FlashlightRequest : byte { Toggle, Reload, Holster }

    public sealed class FlashlightRules
    {
        public double BatterySeconds { get; }
        public double EquipSeconds { get; }
        public double HolsterSeconds { get; }
        public double ReactionSeconds { get; }
        public double ReloadSeconds { get; }
        public double InsertFraction { get; }
        public FlashlightRules(double batterySeconds = 720, double equipSeconds = .65, double holsterSeconds = .5,
            double reactionSeconds = 4.2, double reloadSeconds = 6.2, double insertFraction = .67)
        {
            if (!Finite(batterySeconds) || batterySeconds < 1 || !Finite(equipSeconds) || equipSeconds <= 0 ||
                !Finite(holsterSeconds) || holsterSeconds <= 0 || !Finite(reactionSeconds) || reactionSeconds < 3 || reactionSeconds > 5 ||
                !Finite(reloadSeconds) || reloadSeconds < 5 || reloadSeconds > 7 || !Finite(insertFraction) || insertFraction < .5 || insertFraction > .8)
                throw new ArgumentOutOfRangeException(nameof(batterySeconds));
            BatterySeconds = batterySeconds; EquipSeconds = equipSeconds; HolsterSeconds = holsterSeconds;
            ReactionSeconds = reactionSeconds; ReloadSeconds = reloadSeconds; InsertFraction = insertFraction;
        }
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    // Pure authoritative state machine. Inventory is the existing inventory, not another item store.
    public sealed class FlashlightModel
    {
        private readonly InventoryModel inventory;
        public FlashlightRules Rules { get; }
        public FlashlightState State { get; private set; }
        public double StartedAt { get; private set; }
        public double Duration { get; private set; }
        public double Charge { get; private set; }
        public uint Revision { get; private set; }
        public uint ReactionCount { get; private set; }
        public bool WantedOn { get; private set; }
        public bool BeamOn => powered && Charge > 0 && (State == FlashlightState.Ready || State == FlashlightState.SwitchingOff);
        public bool Busy => pendingReaction || (State != FlashlightState.Ready && State != FlashlightState.BatteryDepleted && State != FlashlightState.Holstered);
        private bool powered, pendingReaction, inserted, suspended, resumeEquipped;
        private double lastNow;
        public FlashlightModel(InventoryModel inventory, FlashlightRules rules, double now = 0)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules)); Reset(now);
        }
        public void Reset(double now)
        {
            if (!FlashlightRules.Finite(now) || now < 0) throw new ArgumentOutOfRangeException(nameof(now));
            lastNow = now; Charge = 1; powered = WantedOn = true; inserted = pendingReaction = suspended = resumeEquipped = false;
            Enter(FlashlightState.Ready, now, 0);
        }
        private void Enter(FlashlightState state, double now, double duration)
        { State = state; StartedAt = now; Duration = duration; Revision++; }
        private bool Clock(double now) => FlashlightRules.Finite(now) && now >= lastNow;
        public bool Request(FlashlightRequest request, double now)
        {
            if (!Clock(now) || suspended) return false;
            if (request == FlashlightRequest.Reload)
            {
                if ((State != FlashlightState.Ready && State != FlashlightState.BatteryDepleted) || pendingReaction || inventory.BatteryPacks <= 0 || Charge >= .999999) return false;
                inserted = false; powered = false; Enter(FlashlightState.ReloadingBatteries, now, Rules.ReloadSeconds); return true;
            }
            if (request == FlashlightRequest.Holster)
            {
                if (Busy) return false;
                if (State == FlashlightState.Holstered) Enter(FlashlightState.Equipping, now, Rules.EquipSeconds);
                else { powered = false; Enter(FlashlightState.Holstering, now, Rules.HolsterSeconds); }
                return true;
            }
            if (request != FlashlightRequest.Toggle || Busy) return false;
            if (State == FlashlightState.Holstered)
            { WantedOn = true; Enter(FlashlightState.Equipping, now, Rules.EquipSeconds); return true; }
            if (Charge <= 0) return false; // Empty attempts never replay the depletion reaction.
            WantedOn = !WantedOn;
            Enter(WantedOn ? FlashlightState.SwitchingOn : FlashlightState.SwitchingOff, now, WantedOn ? .16 : .12); return true;
        }
        public void SetSuspended(bool value, double now, bool resume = true)
        {
            if (!Clock(now)) return;
            if (value)
            {
                if (suspended) { if (!resume) resumeEquipped = false; return; }
                resumeEquipped = resume && State != FlashlightState.Holstered && State != FlashlightState.Holstering;
                suspended = true; pendingReaction = false; powered = false;
                Enter(FlashlightState.Holstered, now, 0);
            }
            else if (suspended)
            {
                suspended = false;
                if (resumeEquipped) Enter(FlashlightState.Equipping, now, Rules.EquipSeconds);
                resumeEquipped = false;
            }
        }
        public void Tick(double now, double seconds)
        {
            if (!Clock(now) || !FlashlightRules.Finite(seconds) || seconds < 0) return;
            // A caller cannot accelerate battery drain with a delta larger than elapsed server time.
            seconds = Math.Min(seconds, now - lastNow); lastNow = now;
            if (suspended) return;
            if (BeamOn && seconds > 0)
            {
                Charge = Math.Max(0, Charge - seconds / Rules.BatterySeconds);
                if (Charge < 1e-10) Charge = 0;
                if (Charge == 0)
                { powered = false; pendingReaction = true; Enter(FlashlightState.BatteryDepleted, now, .18); }
            }
            if (State == FlashlightState.ReloadingBatteries && !inserted && now >= StartedAt + Duration * Rules.InsertFraction)
            {
                inserted = true;
                if (!inventory.TryConsumeBatteryPack()) { Enter(Charge > 0 ? FlashlightState.Ready : FlashlightState.BatteryDepleted, now, 0); powered = WantedOn && Charge > 0; return; }
                Charge = 1; Revision++;
            }
            if (Duration <= 0 || now < StartedAt + Duration) return;
            double end = StartedAt + Duration;
            switch (State)
            {
                case FlashlightState.Equipping:
                case FlashlightState.SwitchingOn:
                case FlashlightState.SwitchingOff:
                case FlashlightState.ReloadingBatteries:
                    powered = WantedOn && Charge > 0; Enter(Charge > 0 ? FlashlightState.Ready : FlashlightState.BatteryDepleted, end, 0); break;
                case FlashlightState.Holstering: Enter(FlashlightState.Holstered, end, 0); break;
                case FlashlightState.BatteryDepleted:
                    if (pendingReaction) { pendingReaction = false; ReactionCount++; Enter(FlashlightState.FailureReaction, now, Rules.ReactionSeconds); }
                    break;
                case FlashlightState.FailureReaction: Enter(FlashlightState.BatteryDepleted, end, 0); break;
            }
        }
    }

    public static class FlashlightFlicker
    {
        private static uint Hash(uint value) { unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); } }
        public static float Sample(double charge, double now, uint seed, double lowThreshold = .20, double criticalThreshold = .10)
        {
            if (!FlashlightRules.Finite(charge) || !FlashlightRules.Finite(now) || now < 0 || charge <= 0) return 0;
            if (!FlashlightRules.Finite(lowThreshold) || !FlashlightRules.Finite(criticalThreshold) || criticalThreshold <= 0 || criticalThreshold >= lowThreshold || lowThreshold > 1 || charge >= lowThreshold) return 1;
            double interval = charge < criticalThreshold ? 2.7 : 4.7;
            uint cell = unchecked((uint)(now / interval)); uint hash = Hash(cell ^ seed);
            if ((hash & 255) > (charge < criticalThreshold ? 170u : 80u)) return 1;
            double offset = .30 + ((hash >> 8) & 1023) / 1023.0 * (interval - .7);
            double elapsed = now - Math.Floor(now / interval) * interval - offset;
            if (elapsed < 0 || elapsed > .14) return 1;
            return (float)(.28 + .72 * Math.Abs(elapsed - .07) / .07);
        }
    }
}
