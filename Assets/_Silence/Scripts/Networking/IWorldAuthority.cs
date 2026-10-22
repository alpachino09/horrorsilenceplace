using Silence.Interactions;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Networking
{
    // Transport adapters must resolve sender identity on the server, not trust
    // an actor ID or position supplied by a client. No transport is implemented yet.
    public interface IWorldAuthority
    {
        bool IsLocalOwner(PlayerContext actor);
        bool RequestInteraction(PlayerContext actor, Interactable target);
        void EmitNoise(PlayerContext actor, NoiseKind kind, float intensity);
    }
}
