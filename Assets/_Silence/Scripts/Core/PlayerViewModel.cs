using System;

namespace Silence.Core
{
    // Presentation-only collision response. A newly obstructed boom retracts in the
    // same frame; smoothing can extend it only as far as the fresh physics result.
    public sealed class CameraBoomModel
    {
        private bool ready;
        private float distance;
        public void Reset() { ready = false; distance = 0; }
        public float Sample(float desired, float safe, float seconds)
        {
            if (!Finite(desired) || !Finite(safe) || desired < 0 || safe < 0) { Reset(); return 0; }
            float limit = Math.Min(desired, safe);
            if (!ready) { distance = limit; ready = true; }
            if (!Finite(seconds) || seconds <= 0) return distance = Math.Min(distance, limit);
            if (limit < distance) return distance = limit;
            float blend = (float)(1 - Math.Exp(-9 * Math.Min(seconds, .25f)));
            return distance = Math.Min(limit, distance + (limit - distance) * blend);
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    public static class PlayerViewRules
    {
        public static float EyeForward(float requested, float capsuleRadius, float clearance)
        {
            if (!Finite(requested) || !Finite(capsuleRadius) || !Finite(clearance)) return 0;
            return Math.Max(0, Math.Min(requested, capsuleRadius - Math.Max(.03f, clearance)));
        }
        public static bool BadArmPose(float signedLateral, float forward, float speed, bool bothBehind)
        {
            if (!Finite(signedLateral) || !Finite(forward) || !Finite(speed)) return false;
            bool crossed = signedLateral < -.025f && forward < .06f;
            bool behind = forward < -.18f && (speed < .25f || bothBehind);
            bool rearClasp = speed < .25f && bothBehind && forward < -.08f && signedLateral < .16f;
            return crossed || behind || rearClasp;
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
