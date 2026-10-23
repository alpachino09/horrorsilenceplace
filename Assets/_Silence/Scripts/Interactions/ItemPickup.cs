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
        private Vector3 spawnPosition;
        private bool remembered;
        public bool Consumed => consumed;
        public ItemDefinition Definition => definition;
        public void Configure(ItemDefinition item) => definition = item;
        public override bool CanInteract(PlayerContext player) =>
            base.CanInteract(player) && !consumed && definition != null;
        public override string GetPrompt(bool english) => english
            ? "Take: " + definition.labelEn : "Взять: " + definition.labelRu;
        internal override bool Apply(PlayerContext player)
        {
            if (!CanInteract(player)) return false;
            bool added = definition.id == "backpack" ? player.Inventory.Expand() : player.Inventory.TryAdd(definition.id);
            if (!added)
            {
                player.ShowMessage(PrototypePreferences.English
                    ? "No free space (" + player.Inventory.Capacity + " slots)." : "Нет свободного места (" + player.Inventory.Capacity + " слотов).");
                return false;
            }
            SetConsumed(true);
            player.ShowMessage(PrototypePreferences.English
                ? "Taken: " + definition.labelEn : "Взято: " + definition.labelRu);
            player.Game?.Collected(player, definition);
            return true;
        }
        public void RememberSpawn() { if (remembered) return; spawnPosition = transform.position; remembered = true; }
        public void ResetPickup() { RememberSpawn(); transform.position = spawnPosition; SetConsumed(false); }
        public void PlaceAt(Vector3 position) { transform.position = position; SetConsumed(false); }

        internal void SetConsumed(bool value)
        {
            consumed = value;
            // Keep the entity alive so late snapshots cannot resurrect a pickup.
            foreach (var visual in GetComponentsInChildren<Renderer>(true)) visual.enabled = !value;
            foreach (var shape in GetComponentsInChildren<Collider>(true)) shape.enabled = !value;
        }
    }
}
