using System;

namespace Silence.Core
{
    public readonly struct PlayerMotionFrame
    {
        public float Right { get; }
        public float Forward { get; }
        public float Vertical { get; }
        public float Speed => (float)Math.Sqrt(Right * Right + Forward * Forward);
        public bool Landed { get; }
        public PlayerMotionFrame(float right, float forward, float vertical, bool landed)
        { Right = right; Forward = forward; Vertical = vertical; Landed = landed; }
    }

    // Presentation only: consumes displacement without authoring movement or game sounds.
    public sealed class PlayerMotionModel
    {
        private float right, forward, vertical;
        private bool hasGroundSample, wasGrounded;
        public void Reset()
        { right = forward = vertical = 0; hasGroundSample = wasGrounded = false; }

        public PlayerMotionFrame Sample(float localX, float worldY, float localZ, float seconds, bool grounded)
        {
            if (!Finite(localX) || !Finite(worldY) || !Finite(localZ) || !Finite(seconds) ||
                seconds <= 0 || seconds > .25f ||
                (double)localX * localX + (double)worldY * worldY + (double)localZ * localZ > 9)
            { Reset(); return default; }

            // Also reject impossible velocities from a tiny delta time, before float overflow.
            double x = localX / (double)seconds, y = worldY / (double)seconds, z = localZ / (double)seconds;
            if (x * x + z * z > 400 || Math.Abs(y) > 80)
            { Reset(); return default; }

            float blend = (float)(1 - Math.Exp(-12 * seconds));
            right += ((float)x - right) * blend;
            forward += ((float)z - forward) * blend;
            vertical += ((float)y - vertical) * blend;
            bool landed = hasGroundSample && !wasGrounded && grounded;
            hasGroundSample = true; wasGrounded = grounded;
            return new PlayerMotionFrame(right, forward, vertical, landed);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
