using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Creatures;

namespace Silence.Multiplayer
{
    public sealed class GameplayCreatureNetwork : NetworkBehaviour
    {
        private readonly NetworkVariable<ObjectPose> pose = new NetworkVariable<ObjectPose>();
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
            }
            else
            {
                if ((pose.Value.Flags & 128) == 0) return;
                float blend = Vector3.Distance(transform.position, pose.Value.Position) > 5 ? 1 : 1 - Mathf.Exp(-15 * Time.deltaTime);
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pose.Value.Position, blend),
                    Quaternion.Slerp(transform.rotation, pose.Value.Rotation, blend));
                creature.SetReplicaState((CreatureState)(pose.Value.Flags & 127));
            }
        }
    }
}
