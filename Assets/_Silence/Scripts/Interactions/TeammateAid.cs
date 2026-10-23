using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Interactions
{
    [RequireComponent(typeof(PlayerContext))]
    public sealed class TeammateAid : Interactable
    {
        private PlayerContext teammate;
        private void Awake() => teammate = GetComponent<PlayerContext>();
        public override bool CanInteract(PlayerContext actor) => base.CanInteract(actor) && teammate != actor &&
            teammate != null && teammate.Vitals.State != VitalState.Healthy &&
            (teammate.CarriedBy == null || teammate.CarriedBy == actor);
        public override string GetPrompt(bool english) => english
            ? (teammate.Vitals.Alive ? "Aid / carry teammate" : "Revive with a rescue kit")
            : (teammate.Vitals.Alive ? "Помочь / перенести товарища" : "Применить набор спасения");
        internal override bool Apply(PlayerContext actor) => actor.Game != null && actor.Game.Assist(actor, teammate);
    }
}
