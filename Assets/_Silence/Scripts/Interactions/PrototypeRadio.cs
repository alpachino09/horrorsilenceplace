using UnityEngine;
using Silence.Player;
using Silence.UI;

namespace Silence.Interactions
{
    public sealed class PrototypeRadio : Interactable
    {
        [SerializeField] private Light indicator;
        private bool on;
        public void Configure(Light light) => indicator = light;
        public override string GetPrompt(bool english) => english
            ? "Switch radio indicator" : "Переключить индикатор радио";
        internal override bool Apply(PlayerContext player)
        {
            on = !on;
            if (indicator != null) indicator.enabled = on;
            player.ShowMessage(PrototypePreferences.English
                ? (on ? "Indicator on." : "Indicator off.")
                : (on ? "Индикатор включён." : "Индикатор выключен."));
            return true;
        }
    }
}
