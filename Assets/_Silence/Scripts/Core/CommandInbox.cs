using System;

namespace Silence.Core
{
    // One inbox per server-owned player. Packets never advance simulation time.
    public sealed class CommandInbox
    {
        public const double StaleAfterSeconds = 0.35;
        private const double Burst = 12, CommandsPerSecond = 60;
        private PlayerCommand latest;
        private bool received, jumpPending;
        private uint lastJump;
        private double lastReceive, refillAt, tokens = Burst;

        public bool TryReceive(PlayerCommand untrusted, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 ||
                !untrusted.TrySanitize(out PlayerCommand safe)) return false;
            if (received && (now < lastReceive || !PlayerCommand.IsNewer(safe.Sequence, latest.Sequence))) return false;
            tokens = Math.Min(Burst, tokens + Math.Max(0, now - refillAt) * CommandsPerSecond);
            refillAt = now;
            if (tokens < 1) return false;
            tokens -= 1;
            if (PlayerCommand.IsNewer(safe.JumpSequence, lastJump))
            {
                jumpPending = true;
                lastJump = safe.JumpSequence;
            }
            latest = safe;
            received = true;
            lastReceive = now;
            return true;
        }

        public PlayerCommand Consume(double now, out bool jump)
        {
            bool fresh = received && now >= lastReceive && now - lastReceive <= StaleAfterSeconds;
            jump = fresh && jumpPending;
            jumpPending = false;
            if (fresh) return latest;
            // Retain stance/orientation/lamp, stop all locomotion after lost input.
            var stopped = latest;
            stopped.X = stopped.Z = 0;
            stopped.Sprint = false;
            return stopped;
        }
    }
}
