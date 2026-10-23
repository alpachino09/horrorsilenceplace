using UnityEngine;
using Silence.Core;
using Silence.Gameplay;
using Silence.Interactions;

namespace Silence.Player
{
    [DefaultExecutionOrder(50)]
    public sealed class ActorGameplayInput : MonoBehaviour
    {
        private PlayerContext actor;
        private InteractionScanner scanner;
        private bool armed;
        private float throwAt, nextDrag;
        public int SelectedSlot { get; set; }
        private void Awake() { actor = GetComponent<PlayerContext>(); scanner = GetComponent<InteractionScanner>(); }
        private void Update()
        {
            if (actor == null || !actor.IsLocalOwner || actor.Game == null || !actor.Game.CanAct(actor) || !actor.Input.GameplayActive)
            { armed = false; return; }
            for (int i = 0; i < 5; i++) if (actor.Input.UseSlotPressed(i)) { SelectedSlot = i; Send(ActorAction.UseSlot, i); }
            if (actor.Input.DropPressed) Send(ActorAction.Drop, SelectedSlot);
            if (actor.Input.ThrowPressed && actor.HeldProp != null) { armed = true; throwAt = Time.unscaledTime; }
            if (actor.Input.ThrowReleased && armed)
            { armed = false; Send(ActorAction.Throw, 0, Mathf.Clamp01((Time.unscaledTime - throwAt) / 1.2f)); }
            if (actor.Input.DoorHeld && scanner.Current is ManualDoor && Time.unscaledTime > nextDrag)
            {
                nextDrag = Time.unscaledTime + 1f / 30;
                float rate = Mathf.Clamp(actor.Input.MouseLook.x * .045f + actor.Input.StickLook.x, -1, 1);
                Send(ActorAction.DragDoor, 0, rate, scanner.Current);
            }
        }
        public bool Send(ActorAction action, int slot = 0, float amount = 0, Interactable target = null) =>
            actor.Authority is IGameplayCommands commands && commands.RequestAction(actor, action, slot, amount, target);
    }
}
