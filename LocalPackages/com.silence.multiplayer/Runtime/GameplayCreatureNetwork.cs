using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Creatures;
using System;

namespace Silence.Multiplayer
{
    public sealed class GameplayCreatureNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<ObjectPose> pose = new NetworkVariable<ObjectPose>();
        private readonly NetworkVariable<CreatureAnimationCue> animationCue = new NetworkVariable<CreatureAnimationCue>();
        private HearingCreature creature;
        private float next;
        public override void OnNetworkSpawn() => creature = GetComponent<HearingCreature>();
        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (Time.unscaledTime < next) return;
                next = Time.unscaledTime + .05f;
                pose.Value = new ObjectPose { Position = transform.position, Rotation = transform.rotation, Flags = (byte)(128 | (byte)creature.State) };
                var old = animationCue.Value;
                if (old.HeardSequence != creature.HeardSequence || old.AttackSequence != creature.AttackSequence ||
                    old.HasHeard != creature.HasHeardCue || old.HasAttack != creature.HasAttackCue)
                {
                    animationCue.Value = new CreatureAnimationCue
                    {
                        HeardSequence = creature.HeardSequence, AttackSequence = creature.AttackSequence,
                        HasHeard = creature.HasHeardCue, HasAttack = creature.HasAttackCue,
                        HeardPoint = creature.LastAcceptedSound, Strength = creature.LastAcceptedStrength,
                        HeardServerTime = creature.HasHeardCue ? NetworkManager.ServerTime.Time - Math.Max(0, Time.unscaledTimeAsDouble - creature.HeardAt) : 0,
                        AttackServerTime = creature.HasAttackCue ? NetworkManager.ServerTime.Time - Math.Max(0, Time.unscaledTimeAsDouble - creature.AttackedAt) : 0
                    };
                }
            }
            else
            {
                if ((pose.Value.Flags & 128) == 0) return;
                float blend = Vector3.Distance(transform.position, pose.Value.Position) > 5 ? 1 : 1 - Mathf.Exp(-15 * Time.deltaTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pose.Value.Position, blend),
                    Quaternion.Slerp(transform.rotation, pose.Value.Rotation, blend));
                creature.SetReplicaState((CreatureState)(pose.Value.Flags & 127));
                var cue = animationCue.Value;
                creature.SetReplicaCues(cue.HeardSequence, cue.HeardPoint, cue.Strength,
                    NetworkManager.ServerTime.Time - cue.HeardServerTime, cue.HasHeard,
                    cue.AttackSequence, NetworkManager.ServerTime.Time - cue.AttackServerTime, cue.HasAttack);
            }
        }
    }
    // Server-owned presentation data: the point is accepted acoustic memory, not a live player position.
    public struct CreatureAnimationCue : INetworkSerializable, IEquatable<CreatureAnimationCue>
    {
        public uint HeardSequence, AttackSequence;
        public Vector3 HeardPoint;
        public float Strength;
        public double HeardServerTime, AttackServerTime;
        public bool HasHeard, HasAttack;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref HeardSequence); serializer.SerializeValue(ref AttackSequence);
            serializer.SerializeValue(ref HeardPoint); serializer.SerializeValue(ref Strength);
            serializer.SerializeValue(ref HeardServerTime); serializer.SerializeValue(ref AttackServerTime);
            serializer.SerializeValue(ref HasHeard); serializer.SerializeValue(ref HasAttack);
        }
        public bool Equals(CreatureAnimationCue other) => HeardSequence == other.HeardSequence &&
            AttackSequence == other.AttackSequence && HeardPoint == other.HeardPoint && Strength == other.Strength &&
            HeardServerTime == other.HeardServerTime && AttackServerTime == other.AttackServerTime &&
            HasHeard == other.HasHeard && HasAttack == other.HasAttack;
    }
}
