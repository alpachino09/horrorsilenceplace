using System;

namespace Silence.Core
{
    public enum SurfaceKind : byte { Dirt, Grass, Wood, Concrete, Glass, Metal, Water, Gravel }
    public readonly struct StepProfile
    {
        public readonly float Intensity, Range;
        public StepProfile(float intensity, float range) { Intensity = intensity; Range = range; }
        public static StepProfile For(SurfaceKind material, bool crouching, bool running)
        {
            float intensity, range;
            switch (material)
            {
                case SurfaceKind.Grass: intensity = .11f; range = 11; break;
                case SurfaceKind.Wood: intensity = .3f; range = 18; break;
                case SurfaceKind.Concrete: intensity = .22f; range = 16; break;
                case SurfaceKind.Glass: intensity = .8f; range = 30; break;
                case SurfaceKind.Metal: intensity = .6f; range = 25; break;
                case SurfaceKind.Water: intensity = .4f; range = 20; break;
                case SurfaceKind.Gravel: intensity = .34f; range = 19; break;
                default: intensity = .17f; range = 13; break;
            }
            return new StepProfile(NoiseMath.Clamp01(intensity * (crouching ? .24f : running ? 1.8f : 1)), range);
        }
    }
    public sealed class StepCycle
    {
        private float accumulated;
        public bool Advance(float distance, bool grounded, bool crouching, bool running)
        {
            if (!grounded || float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0 || distance > 3)
            { accumulated = 0; return false; }
            accumulated += distance;
            float stride = crouching ? .85f : running ? 1.8f : 1.35f;
            if (accumulated < stride) return false;
            accumulated %= stride; return true;
        }
    }
    public sealed class MicrophoneEnvelope
    {
        public float Floor { get; private set; } = .01f;
        public float Level { get; private set; }
        private double nextEmission;
        public static float Rms(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0;
            double sum = 0;
            foreach (float x in samples)
            {
                if (float.IsNaN(x) || float.IsInfinity(x)) continue;
                double bounded = Math.Max(-1, Math.Min(1, x)); sum += bounded * bounded;
            }
            return (float)Math.Sqrt(sum / samples.Length);
        }
        public void Calibrate(float rms)
        { if (!float.IsNaN(rms) && !float.IsInfinity(rms)) Floor = Math.Max(.002f, Math.Min(.2f, rms * 1.8f)); }
        public bool Sample(float rms, double now, float sensitivity, out byte metric)
        {
            metric = 0;
            if (float.IsNaN(rms) || float.IsInfinity(rms) || double.IsNaN(now) || double.IsInfinity(now) ||
                float.IsNaN(sensitivity) || float.IsInfinity(sensitivity) || now < 0) return false;
            float desired = NoiseMath.Clamp01((rms - Floor) * Math.Max(1, Math.Min(30, sensitivity)));
            Level += (desired - Level) * (desired > Level ? .5f : .15f);
            if (now < nextEmission || Level < .03f) return false;
            nextEmission = now + .1; metric = (byte)Math.Round(Level * 255); return true;
        }
    }
    public sealed class ActionBudget
    {
        private double lastTime, tokens = 8;
        public bool TrySpend(double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < lastTime) return false;
            tokens = Math.Min(8, tokens + (now - lastTime) * 45); lastTime = now;
            if (tokens < 1) return false;
            tokens--; return true;
        }
    }
}
