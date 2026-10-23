using System;
using Unity.Netcode;
using UnityEngine;

namespace Silence.Multiplayer
{
    public struct ObjectPose : INetworkSerializable, IEquatable<ObjectPose>
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Parameter;
        public byte Flags;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Position); s.SerializeValue(ref Rotation); s.SerializeValue(ref Parameter); s.SerializeValue(ref Flags); }
        public bool Equals(ObjectPose other) => Position.Equals(other.Position) && Rotation.Equals(other.Rotation) && Parameter.Equals(other.Parameter) && Flags == other.Flags;
    }
    public struct ActorStatus : INetworkSerializable, IEquatable<ActorStatus>
    {
        public byte Health;
        public float Charge, Stamina, Noise;
        public ulong HeldObject;
        public int CarriedBy;
        public bool Carried;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Health); s.SerializeValue(ref Charge); s.SerializeValue(ref Stamina); s.SerializeValue(ref Noise);
            s.SerializeValue(ref HeldObject); s.SerializeValue(ref CarriedBy); s.SerializeValue(ref Carried);
        }
        public bool Equals(ActorStatus other) => Health == other.Health && Charge.Equals(other.Charge) && Stamina.Equals(other.Stamina) &&
            Noise.Equals(other.Noise) && HeldObject == other.HeldObject && CarriedBy == other.CarriedBy && Carried == other.Carried;
    }
    public struct CampaignStatus : INetworkSerializable, IEquatable<CampaignStatus>
    {
        public byte Chapter, Mission, Phase, Ending, Difficulty;
        public float Seconds;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Chapter); s.SerializeValue(ref Mission); s.SerializeValue(ref Phase); s.SerializeValue(ref Ending); s.SerializeValue(ref Difficulty); s.SerializeValue(ref Seconds); }
        public bool Equals(CampaignStatus other) => Chapter == other.Chapter && Mission == other.Mission && Phase == other.Phase &&
            Ending == other.Ending && Difficulty == other.Difficulty && Seconds.Equals(other.Seconds);
    }
    public struct NoisePacket : INetworkSerializable
    {
        public Vector3 Position;
        public float Intensity, Range;
        public int Source;
        public byte Kind, Surface;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        { s.SerializeValue(ref Position); s.SerializeValue(ref Intensity); s.SerializeValue(ref Range); s.SerializeValue(ref Source); s.SerializeValue(ref Kind); s.SerializeValue(ref Surface); }
    }
}
