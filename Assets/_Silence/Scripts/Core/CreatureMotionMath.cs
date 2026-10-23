using System;

namespace Silence.Core
{
    // Timing only. This class never selects targets or changes the creature mind.
    public static class CreatureMotionMath
    {
        public static bool IsRecent(double age, double duration) =>
            !double.IsNaN(age) && !double.IsInfinity(age) && age >= 0 && age <= duration;

        public static float PlantWeight(float cycle, float offset, float stance)
        {
            if (float.IsNaN(cycle) || float.IsInfinity(cycle) || float.IsNaN(offset) || float.IsInfinity(offset) ||
                float.IsNaN(stance) || float.IsInfinity(stance) || stance <= 0 || stance >= 1) return 0;
            double phase = cycle + offset; phase -= Math.Floor(phase);
            if (phase >= stance) return 0;
            float edge = Math.Min((float)phase, stance - (float)phase);
            float t = Math.Max(0, Math.Min(1, edge / .075f));
            return t * t * (3 - 2 * t);
        }
    }
}
