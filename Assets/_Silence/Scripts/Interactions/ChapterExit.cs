using UnityEngine;
using Silence.Player;
namespace Silence.Interactions
{
    public sealed class ChapterExit : Interactable
    {
        public override string GetPrompt(bool english) => english ? "Leave the area together" : "Покинуть локацию";
        internal override bool Apply(PlayerContext actor) => actor.Game != null && actor.Game.Exit(actor);
    }
}
