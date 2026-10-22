using UnityEngine;
using Silence.Inventory;
using Silence.Player;
using Silence.UI;

namespace Silence.Interactions
{
    public sealed class ItemPickup : Interactable
    {
        [SerializeField] private ItemDefinition definition;
        private bool consumed;
        public ItemDefinition Definition => definition;
        public void Configure(ItemDefinition item) => definition = item;
        public override bool CanInteract(PlayerContext player) =>
            base.CanInteract(player) && !consumed && definition != null;
        public override string GetPrompt(bool english) => english
            ? "Take: " + definition.labelEn : "Взять: " + definition.labelRu;
        internal override bool Apply(PlayerContext player)
        {
            if (!CanInteract(player)) return false;
            if (!player.Inventory.TryAdd(definition.id))
            {
                player.ShowMessage(PrototypePreferences.English
                    ? "Inventory full (3 slots)." : "Инвентарь заполнен (3 слота).");
                return false;
            }
            consumed = true;
            gameObject.SetActive(false);
            player.ShowMessage(PrototypePreferences.English
                ? "Taken: " + definition.labelEn : "Взято: " + definition.labelRu);
            return true;
        }
    }
}
