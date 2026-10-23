using System;

namespace Silence.Core
{
    public struct PlayerCommand
    {
        public uint Sequence, JumpSequence;
        public float X, Z, Yaw, Pitch;
        public bool Sprint, Crouch, Flashlight;

        public bool TrySanitize(out PlayerCommand command)
        {
            command = this;
            if (!Finite(X) || !Finite(Z) || !Finite(Yaw) || !Finite(Pitch)) return false;
            command.X = Clamp(X, -1f, 1f);
            command.Z = Clamp(Z, -1f, 1f);
            float length = (float)Math.Sqrt(command.X * command.X + command.Z * command.Z);
            if (length > 1f) { command.X /= length; command.Z /= length; }
            command.Yaw = Yaw % 360f;
            command.Pitch = Clamp(Pitch, -85f, 85f);
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Clamp(float value, float low, float high) => Math.Max(low, Math.Min(high, value));
        public static bool IsNewer(uint candidate, uint previous) => unchecked((int)(candidate - previous)) > 0;
    }
}
