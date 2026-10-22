using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Silence.Core
{
    public sealed class InventoryModel
    {
        public const int BaseCapacity = 3;
        private readonly List<string> items = new List<string>();
        public int Capacity { get; }
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
    }
}
