using System;

namespace Silence.Core
{
    public enum AnimationCueKind : byte { Interact, Startle, Hit, Trip, Inspect }

    // Presentation scheduling never locks input, applies damage or moves the motor.
    public sealed class AnimationActionGate
    {
        private double until;
        private int priority;
        public void Reset() { until = 0; priority = 0; }
        public bool TryBegin(AnimationCueKind kind, double now, double duration, bool alive)
        {
            if (!alive || !Finite(now) || !Finite(duration) || now < 0 || duration <= 0 || duration > 10 ||
                kind < AnimationCueKind.Interact || kind > AnimationCueKind.Inspect) return false;
            int next = kind == AnimationCueKind.Hit ? 3 : kind == AnimationCueKind.Trip ? 2 : 1;
            if (now < until && next <= priority) return false;
            priority = next; until = now + duration; return true;
        }
        public bool Active(double now) => Finite(now) && now < until;
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }

    public readonly struct FacingFrame
    {
        public float Yaw { get; }
        public float Error { get; }
        public bool TurnStarted { get; }
        public FacingFrame(float yaw, float error, bool started) { Yaw = yaw; Error = error; TurnStarted = started; }
    }

    // Delays only the art's facing; camera, collision capsule and authoritative aim stay intact.
    public sealed class CharacterFacingModel
    {
        private bool ready, turning;
        private float yaw;
        public void Reset() { ready = turning = false; yaw = 0; }
        public FacingFrame Sample(float aimYaw, float speed, float seconds, bool crouched)
        {
            if (!Finite(aimYaw) || !Finite(speed) || !Finite(seconds) || seconds <= 0 || seconds > .25f || speed < 0)
            { Reset(); return new FacingFrame(Finite(aimYaw) ? aimYaw : 0, 0, false); }
            if (!ready) { yaw = aimYaw; ready = true; }
            float error = Delta(yaw, aimYaw);
            bool moving = speed > .15f;
            bool started = !turning && Math.Abs(error) > (moving ? 65 : 48);
            if (started) turning = true;
            float rate = moving ? 300 : crouched ? 90 : 115;
            if (moving || turning) yaw += Clamp(error, -rate * seconds, rate * seconds);
            if (Math.Abs(Delta(yaw, aimYaw)) < 4) turning = false;
            // A discontinuity/teleport never leaves the body more than 160 degrees behind.
            if (Math.Abs(Delta(yaw, aimYaw)) > 160) yaw = aimYaw - Math.Sign(error) * 160;
            return new FacingFrame(yaw, error, started);
        }
        public static float Delta(float from, float to)
        {
            float value = (to - from) % 360;
            if (value > 180) value -= 360;
            if (value < -180) value += 360;
            return value;
        }
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }

    // Hysteresis plus travelled distance prevents contact blending or repeated IK callbacks
    // from producing duplicate gameplay noise. Both feet cannot emit in the same instant.
    public sealed class FootContactModel
    {
        public static float RemoveIdleBlend(float blendedPlant, float movingWeight)
        {
            if (!Finite(blendedPlant) || !Finite(movingWeight)) return 0;
            if (movingWeight <= .01f) return 1;
            float weight = Math.Min(1, movingWeight);
            return Math.Max(0, Math.Min(1, (blendedPlant - (1 - weight)) / weight));
        }
        private bool leftDown, rightDown, ready;
        private double lastStep = -1;
        private float travel;
        public void Reset() { leftDown = rightDown = ready = false; lastStep = -1; travel = 0; }
        public bool Sample(float left, float right, float distance, bool grounded, float speed, double now, out bool leftFoot)
        {
            leftFoot = false;
            if (!Finite(left) || !Finite(right) || !Finite(distance) || !Finite(speed) ||
                double.IsNaN(now) || double.IsInfinity(now) || now < 0 || distance < 0 || distance > 2 || !grounded || speed < .12f)
            { Reset(); return false; }
            travel += distance;
            bool nextL = left > (leftDown ? .35f : .65f), nextR = right > (rightDown ? .35f : .65f);
            bool strikeL = ready && nextL && !leftDown, strikeR = ready && nextR && !rightDown;
            leftDown = nextL; rightDown = nextR; ready = true;
            if ((!strikeL && !strikeR) || travel < .12f || lastStep >= 0 && now - lastStep < .15) return false;
            leftFoot = strikeL; lastStep = now; travel = 0; return true;
        }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
    }
}
