using UnityEngine;
using Silence.Player;

namespace Silence.Interactions
{
    public abstract class Interactable : MonoBehaviour, IInteractable
    {
        public virtual bool CanInteract(PlayerContext player) => isActiveAndEnabled;
        public abstract string GetPrompt(bool english);
        // Only the authority implementation calls this, after validation.
        internal abstract bool Apply(PlayerContext player);
    }
}
