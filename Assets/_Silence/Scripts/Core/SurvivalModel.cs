using System;

namespace Silence.Core
{
    public enum VitalState : byte { Healthy, Wounded, Dead }
    public enum Difficulty : byte { Easy, Normal, Hard, Nightmare }
    public enum ActorAction : byte { UseSlot, Drop, Throw, DragDoor, Vote, Microphone }
    public enum ChapterId : byte { Farm, Town, Tunnels, Factory, Lake, RadioStation, Laboratory }
    public enum RoundPhase : byte { Lobby, Playing, Voting, Transition, Wipe, Completed }

    public sealed class ActorVitals
    {
        public VitalState State { get; private set; }
        private double nextHit;
        public bool Alive => State != VitalState.Dead;
        public bool TryHit(double now, double grace = 5)
        {
            if (!Finite(now) || now < 0 || !Finite(grace) || grace < 0 || !Alive || now < nextHit) return false;
            State = State == VitalState.Healthy ? VitalState.Wounded : VitalState.Dead;
            nextHit = now + grace;
            return true;
        }
        public bool Heal()
        {
            if (State != VitalState.Wounded) return false;
            State = VitalState.Healthy;
            return true;
        }
        public bool Revive(double now)
        {
            if (State != VitalState.Dead || !Finite(now) || now < 0) return false;
            State = VitalState.Wounded; nextHit = now + 8;
            return true;
        }
        public void Reset() { State = VitalState.Healthy; nextHit = 0; }
        public void ApplySnapshot(VitalState state)
        {
            if (state < VitalState.Healthy || state > VitalState.Dead) throw new ArgumentOutOfRangeException(nameof(state));
            State = state;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }

    public sealed class StaminaModel
    {
        public float Value { get; private set; } = 1;
        private float recoveryDelay;
        private bool exhausted;
        public bool CanSprint => !exhausted && Value > .08f;
        public void Tick(float seconds, bool sprinting)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 || seconds > 1)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (sprinting && CanSprint) { Value = Math.Max(0, Value - seconds / 12f); recoveryDelay = 1.2f; if (Value <= .08f) exhausted = true; }
            else if (recoveryDelay > 0) recoveryDelay = Math.Max(0, recoveryDelay - seconds);
            else Value = Math.Min(1, Value + seconds / 7f);
            if (Value >= .3f) exhausted = false;
        }
        public void Reset() { Value = 1; recoveryDelay = 0; exhausted = false; }
        public void ApplySnapshot(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            Value = NoiseMath.Clamp01(value);
        }
    }

    public readonly struct DifficultyRules
    {
        public float HearingThreshold { get; }
        public float ChaseThreshold { get; }
        public float SearchSeconds { get; }
        public float ChaseSpeed { get; }
        public float BatterySeconds { get; }
        public float AttackGrace { get; }
        private DifficultyRules(float hear, float chase, float search, float speed, float battery, float grace)
        { HearingThreshold = hear; ChaseThreshold = chase; SearchSeconds = search; ChaseSpeed = speed; BatterySeconds = battery; AttackGrace = grace; }
        public static DifficultyRules For(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return new DifficultyRules(.065f, .22f, 12, 6.3f, 900, 8);
                case Difficulty.Normal: return new DifficultyRules(.045f, .16f, 20, 6.8f, 720, 6);
                case Difficulty.Hard: return new DifficultyRules(.03f, .12f, 28, 7.3f, 540, 5);
                case Difficulty.Nightmare: return new DifficultyRules(.02f, .09f, 36, 7.7f, 420, 4);
                default: throw new ArgumentOutOfRangeException(nameof(difficulty));
            }
        }
    }
}
