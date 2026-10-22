using System;

namespace Silence.Core
{
    public static class InteractionRules
    {
        public const int MaximumPlayers = 4;

        public static bool CanApply(bool isAuthority, bool registeredActor,
            bool targetAvailable, float distance, float range, bool unobstructed)
        {
            return isAuthority && registeredActor && targetAvailable && unobstructed &&
                   !float.IsNaN(distance) && !float.IsInfinity(distance) &&
                   !float.IsNaN(range) && !float.IsInfinity(range) &&
                   distance >= 0f && range > 0f && distance <= range;
        }
    }
}
