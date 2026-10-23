namespace Silence.Core
{
    public sealed class InteractionGate
    {
        private bool received;
        private uint lastSequence;
        private double nextAllowed;
        public bool TryBegin(uint sequence, double now)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0 ||
                (received && !PlayerCommand.IsNewer(sequence, lastSequence))) return false;
            // Consume even a rejected new sequence, so it cannot be replayed later.
            received = true;
            lastSequence = sequence;
            if (now < nextAllowed) return false;
            nextAllowed = now + 0.15;
            return true;
        }
    }
}
