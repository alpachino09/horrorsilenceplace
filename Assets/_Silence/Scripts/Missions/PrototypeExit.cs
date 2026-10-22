using Silence.Interactions;
using Silence.Player;
using Silence.UI;

namespace Silence.Missions
{
    public sealed class PrototypeExit : Interactable
    {
        public bool Completed { get; private set; }
        public override string GetPrompt(bool english) => english
            ? "Finish the prototype walkthrough" : "Завершить маршрут прототипа";
        internal override bool Apply(PlayerContext player)
        {
            Completed = true;
            player.ShowMessage(PrototypePreferences.English
                ? "Prototype route complete." : "Маршрут прототипа завершён.");
            return true;
        }
    }
}
