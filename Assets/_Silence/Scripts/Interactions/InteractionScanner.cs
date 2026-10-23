using UnityEngine;
using Silence.Player;

namespace Silence.Interactions
{
    [RequireComponent(typeof(PlayerContext))]
    public sealed class InteractionScanner : MonoBehaviour
    {
        private PlayerContext player;
        public Interactable Current { get; private set; }
        private void Awake() => player = GetComponent<PlayerContext>();
        private void LateUpdate()
        {
            Current = null;
            if (player == null || player.Input == null) return;
            bool pressed = player.Input.InteractPressed;
            InteractionDiagnostics.ProbeKey(player, pressed);
            if (!player.IsLocalOwner || !player.Input.GameplayActive) return;
            Transform eye = player.View.transform;
            if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit,
                player.Settings.interactionRange, ~0, QueryTriggerInteraction.Ignore))
            {
                var target = hit.collider.GetComponentInParent<Interactable>();
                if (target != null && target.CanInteract(player)) Current = target;
            }
            if (pressed) InteractionDiagnostics.Trace(this, "SCAN", "target=" +
                (Current != null ? Current.name : "none") + " collider=" +
                (hit.collider != null ? hit.collider.name : "none"));
            if (Current != null && pressed)
                player.Authority.RequestInteraction(player, Current);
        }
    }
}
