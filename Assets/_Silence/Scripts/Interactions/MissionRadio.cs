using UnityEngine;
using Silence.Player;
namespace Silence.Interactions
{
    public sealed class MissionRadio : Interactable
    {
        public override string GetPrompt(bool english) => english ? "Receive the signal" : "Принять сигнал";
        internal override bool Apply(PlayerContext actor) => actor.Game != null && actor.Game.Radio(actor);
    }
}
