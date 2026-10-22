using Silence.Player;

namespace Silence.Interactions
{
    public interface IInteractable
    {
        bool CanInteract(PlayerContext player);
        string GetPrompt(bool english);
    }
}
