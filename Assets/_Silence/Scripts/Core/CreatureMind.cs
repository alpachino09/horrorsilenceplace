using System;

namespace Silence.Core
{
    public enum CreatureState : byte { Dormant, Patrol, Investigate, Search, Chase, Attack, Recover }
    public readonly struct Point3
    {
        public readonly float X, Y, Z;
        public Point3(float x, float y, float z) { X = x; Y = y; Z = z; }
    }

    // No actor reference, sight test, or current-player-position input exists here.
    public sealed class CreatureMind
    {
        public CreatureState State { get; private set; } = CreatureState.Patrol;
        public Point3 LastHeardPosition { get; private set; }
        public float Confidence { get; private set; }
        public bool HasMemory { get; private set; }
        private readonly DifficultyRules rules;
        private double heardAt, searchUntil, recoverUntil;
        public CreatureMind(DifficultyRules balance) => rules = balance;

        public bool Hear(Point3 position, float perceived, double now)
        {
            if (!Finite(position.X) || !Finite(position.Y) || !Finite(position.Z) || !Finite(perceived) ||
                double.IsNaN(now) || double.IsInfinity(now) || now < 0 || perceived < rules.HearingThreshold) return false;
            float decayed = HasMemory ? Confidence * (float)Math.Exp(-Math.Max(0, now - heardAt) / 3) : 0;
            if (HasMemory && perceived < decayed * .6f) return false;
            LastHeardPosition = position; Confidence = NoiseMath.Clamp01(perceived); heardAt = now; HasMemory = true;
            if (State != CreatureState.Recover && State != CreatureState.Attack)
                State = perceived >= rules.ChaseThreshold ? CreatureState.Chase : CreatureState.Investigate;
            return true;
        }
        public void Tick(double now, float distanceToMemory)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0) return;
            if (State == CreatureState.Recover || State == CreatureState.Attack)
            {
                if (now < recoverUntil) return;
                State = HasMemory ? CreatureState.Investigate : CreatureState.Patrol;
            }
            if (!HasMemory) return;
            if ((State == CreatureState.Chase || State == CreatureState.Investigate) &&
                (distanceToMemory < .8f || now - heardAt > 7))
            { State = CreatureState.Search; searchUntil = now + rules.SearchSeconds; }
            if (State == CreatureState.Search && now >= searchUntil)
            { HasMemory = false; Confidence = 0; State = CreatureState.Patrol; }
        }
        public void Recover(double now, double seconds)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 || seconds < 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            State = CreatureState.Recover; recoverUntil = now + seconds;
        }
        public void Reset() { State = CreatureState.Patrol; HasMemory = false; Confidence = 0; heardAt = searchUntil = recoverUntil = 0; }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
