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
        public byte Count;
        public FixedString64Bytes First, Second, Third;
        public static InventorySnapshot Capture(InventoryModel inventory)
        {
            var snapshot = new InventorySnapshot { Count = (byte)inventory.Items.Count };
            if (snapshot.Count > 0) snapshot.First = inventory.Items[0];
            if (snapshot.Count > 1) snapshot.Second = inventory.Items[1];
            if (snapshot.Count > 2) snapshot.Third = inventory.Items[2];
            return snapshot;
        }
        public string[] ToArray()
        {
            if (Count > InventoryModel.BaseCapacity) throw new InvalidOperationException("Invalid inventory snapshot.");
            var items = new string[Count];
            if (Count > 0) items[0] = First.ToString();
            if (Count > 1) items[1] = Second.ToString();
            if (Count > 2) items[2] = Third.ToString();
            return items;
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Count);
            serializer.SerializeValue(ref First);
            serializer.SerializeValue(ref Second);
            serializer.SerializeValue(ref Third);
        }
        public bool Equals(InventorySnapshot other) => Count == other.Count && First.Equals(other.First) &&
            Second.Equals(other.Second) && Third.Equals(other.Third);
    }
}
