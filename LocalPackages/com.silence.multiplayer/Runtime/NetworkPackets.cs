using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Silence.Core;

namespace Silence.Multiplayer
{
    public struct InputPacket : INetworkSerializable
    {
        public PlayerCommand Command;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Command.Sequence);
            serializer.SerializeValue(ref Command.JumpSequence);
            serializer.SerializeValue(ref Command.X);
            serializer.SerializeValue(ref Command.Z);
            serializer.SerializeValue(ref Command.Yaw);
            serializer.SerializeValue(ref Command.Pitch);
            serializer.SerializeValue(ref Command.Sprint);
            serializer.SerializeValue(ref Command.Crouch);
            serializer.SerializeValue(ref Command.Flashlight);
        }
    }

    public struct PlayerPose : INetworkSerializable, IEquatable<PlayerPose>
    {
        public Vector3 Position;
        public float Yaw, Pitch, Height;
        public bool Flashlight;
        public uint Tick;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Yaw);
            serializer.SerializeValue(ref Pitch);
            serializer.SerializeValue(ref Height);
            serializer.SerializeValue(ref Flashlight);
            serializer.SerializeValue(ref Tick);
        }
        public bool Equals(PlayerPose other) => Position.Equals(other.Position) && Yaw.Equals(other.Yaw) &&
            Pitch.Equals(other.Pitch) && Height.Equals(other.Height) && Flashlight == other.Flashlight && Tick == other.Tick;
    }

    public struct InventorySnapshot : INetworkSerializable, IEquatable<InventorySnapshot>
    {
        public byte Count, Capacity;
        public FixedString64Bytes First, Second, Third, Fourth, Fifth;
        public static InventorySnapshot Capture(InventoryModel inventory)
        {
            var snapshot = new InventorySnapshot { Count = (byte)inventory.Items.Count, Capacity = (byte)inventory.Capacity };
            if (snapshot.Count > 0) snapshot.First = inventory.Items[0];
            if (snapshot.Count > 1) snapshot.Second = inventory.Items[1];
            if (snapshot.Count > 2) snapshot.Third = inventory.Items[2];
            if (snapshot.Count > 3) snapshot.Fourth = inventory.Items[3];
            if (snapshot.Count > 4) snapshot.Fifth = inventory.Items[4];
            return snapshot;
        }
        public string[] ToArray()
        {
            if (Count > 5) throw new InvalidOperationException("Invalid inventory snapshot.");
            var items = new string[Count];
            if (Count > 0) items[0] = First.ToString();
            if (Count > 1) items[1] = Second.ToString();
            if (Count > 2) items[2] = Third.ToString();
            if (Count > 3) items[3] = Fourth.ToString();
            if (Count > 4) items[4] = Fifth.ToString();
            return items;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref Capacity);
            serializer.SerializeValue(ref First);
            serializer.SerializeValue(ref Second);
            serializer.SerializeValue(ref Third);
            serializer.SerializeValue(ref Fourth);
            serializer.SerializeValue(ref Fifth);
        }
        public bool Equals(InventorySnapshot other) => Count == other.Count && Capacity == other.Capacity && First.Equals(other.First) &&
            Second.Equals(other.Second) && Third.Equals(other.Third) && Fourth.Equals(other.Fourth) && Fifth.Equals(other.Fifth);
    }
}
