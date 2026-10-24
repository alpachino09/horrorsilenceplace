using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Silence.Core
{
    public sealed class InventoryModel
    {
        public const int BaseCapacity = 3;
        public const string FlashlightCellId = "flashlight_cells";
        public int BatteryPacks { get; private set; }
        public int BatteryPackCapacity { get; private set; }
        public bool BatteryPacksConfigured => BatteryPackCapacity > 0;
        private readonly List<string> items = new List<string>();
        public int Capacity { get; private set; }
        public ReadOnlyCollection<string> Items { get; }

        public InventoryModel(int capacity = BaseCapacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Items = items.AsReadOnly();
        }

        public bool TryAdd(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                throw new ArgumentException("An item ID is required.", nameof(itemId));
            if (itemId == FlashlightCellId) return TryAddBatteryPack();
            if (items.Count >= Capacity) return false;
            // Multiple items of the same definition are valid separate slots.
            items.Add(itemId);
            return true;
        }

        public bool TryRemoveAt(int index, out string itemId)
        {
            itemId = null;
            if (index < 0 || index >= items.Count) return false;
            itemId = items[index];
            items.RemoveAt(index);
            return true;
        }

        public bool TryConsume(string itemId)
        {
            if (itemId == FlashlightCellId) return TryConsumeBatteryPack();
            int index = items.IndexOf(itemId);
            return TryRemoveAt(index, out _);
        }
        // Equipment consumables belong to this inventory, alongside its three general slots.
        // They are not the mission's radio "battery" item and cannot satisfy that objective.
        public void ConfigureBatteryPacks(int maximum, int initial)
        {
            if (maximum < 1 || maximum > 12 || initial < 0 || initial > maximum) throw new ArgumentOutOfRangeException(nameof(maximum));
            BatteryPacks = BatteryPacksConfigured ? Math.Min(BatteryPacks, maximum) : initial;
            BatteryPackCapacity = maximum;
        }
        public void ApplyBatteryPacksSnapshot(int maximum, int count)
        {
            if (maximum < 1 || maximum > 12 || count < 0 || count > maximum) throw new ArgumentOutOfRangeException(nameof(count));
            BatteryPackCapacity = maximum; BatteryPacks = count;
        }
        public bool TryAddBatteryPack()
        { if (!BatteryPacksConfigured || BatteryPacks >= BatteryPackCapacity) return false; BatteryPacks++; return true; }
        public bool TryConsumeBatteryPack()
        { if (BatteryPacks <= 0) return false; BatteryPacks--; return true; }
        public bool Expand(int additionalSlots = 2)
        {
            if (additionalSlots <= 0 || Capacity + additionalSlots > 5) return false;
            Capacity += additionalSlots;
            return true;
        }
        public void ApplyCapacity(int capacity)
        {
            if (capacity < BaseCapacity || capacity > 5 || capacity < items.Count)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }
        public void ApplySnapshot(int capacity, IReadOnlyList<string> snapshot)
        {
            if (capacity < BaseCapacity || capacity > 5) throw new ArgumentOutOfRangeException(nameof(capacity));
            var validated = new InventoryModel(capacity);
            validated.ReplaceSnapshot(snapshot);
            Capacity = capacity; items.Clear(); items.AddRange(validated.Items);
        }

        public void ReplaceSnapshot(IReadOnlyList<string> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Count > Capacity) throw new ArgumentException("Snapshot exceeds capacity.", nameof(snapshot));
            // Validate first: a corrupt snapshot must not partially erase inventory.
            var replacement = new List<string>(snapshot.Count);
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(snapshot[i]))
                    throw new ArgumentException("Snapshot contains an empty item ID.", nameof(snapshot));
                replacement.Add(snapshot[i]);
            }
            items.Clear();
            items.AddRange(replacement);
        }
    }
}
