using System;

namespace Silence.Core
{
    public static class NoiseMath
    {
        // Gameplay envelope, not a claim of a physically calibrated dB model.
        public static float PerceivedIntensity(float intensity, float distance,
            float range, float obstruction, float masking)
        {
            RequireFinite(intensity, nameof(intensity));
            RequireFinite(distance, nameof(distance));
            RequireFinite(range, nameof(range));
            RequireFinite(obstruction, nameof(obstruction));
            RequireFinite(masking, nameof(masking));
            if (distance < 0f) throw new ArgumentOutOfRangeException(nameof(distance));
            if (range <= 0f) throw new ArgumentOutOfRangeException(nameof(range));
            float falloff = Clamp01(1f - distance / range);
            // Ambient noise must not make a nearby source perfectly silent.
            float limitedMasking = Math.Min(Clamp01(masking), 0.85f);
            return Clamp01(intensity) * falloff * falloff *
                   (1f - Clamp01(obstruction)) * (1f - limitedMasking);
        }

        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));

        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
