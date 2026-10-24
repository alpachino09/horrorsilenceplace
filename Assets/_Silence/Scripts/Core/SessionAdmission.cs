using System;
using System.Collections.Generic;

namespace Silence.Core
{
    // Slots are reserved during approval, not after a delayed player spawn.
    public sealed class SessionAdmission
    {
        private readonly Dictionary<ulong, int> slots = new Dictionary<ulong, int>();
        public int Count => slots.Count;
        public bool Running { get; private set; }
        // AvatarMotionSnapshot replaced the old byte payload. Reject old builds even
        // if someone starts a preserved pre-upgrade scene with its old NGO config.
        public const string Protocol = "SILENCE-GAME-4";

        public bool TryJoin(ulong clientId, string protocol, out int slot, out string reason)
        {
            slot = -1;
            if (protocol != Protocol) { reason = "Different game version."; return false; }
            if (slots.ContainsKey(clientId)) { reason = "Client already admitted."; return false; }
            if (Running) { reason = "Round started. Join at the next lobby."; return false; }
            if (Count >= InteractionRules.MaximumPlayers) { reason = "Lobby full (4 players)."; return false; }
            for (int candidate = 0; candidate < InteractionRules.MaximumPlayers; candidate++)
                if (!slots.ContainsValue(candidate)) { slot = candidate; break; }
            slots.Add(clientId, slot);
            reason = string.Empty;
            return true;
        }

        public bool Contains(ulong clientId) => slots.ContainsKey(clientId);
        public bool Remove(ulong clientId) => slots.Remove(clientId);
        public void StartRound()
        {
            if (Count == 0) throw new InvalidOperationException("Cannot start an empty lobby.");
            Running = true;
        }
    }
}
