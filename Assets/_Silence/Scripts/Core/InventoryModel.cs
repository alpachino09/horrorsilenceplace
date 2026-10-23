using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Silence.Core
{
    public sealed class InventoryModel
    {
        public const int BaseCapacity = 3;
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
            int index = items.IndexOf(itemId);
            return TryRemoveAt(index, out _);
        }
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
