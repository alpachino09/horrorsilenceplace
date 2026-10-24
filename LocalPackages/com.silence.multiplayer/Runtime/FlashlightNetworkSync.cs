using System;
using Unity.Netcode;
using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Multiplayer
{
    public struct FlashlightPacket : INetworkSerializable, IEquatable<FlashlightPacket>
    {
        public byte state, spares, capacity, flags;
        public ushort charge;
        public uint revision;
        public double startedAt;
        public float duration;
        public void NetworkSerialize<T>(BufferSerializer<T> s) where T:IReaderWriter
        {
            s.SerializeValue(ref state); s.SerializeValue(ref spares); s.SerializeValue(ref capacity); s.SerializeValue(ref flags);
            s.SerializeValue(ref charge); s.SerializeValue(ref revision); s.SerializeValue(ref startedAt); s.SerializeValue(ref duration);
        }
        public bool Equals(FlashlightPacket p) => state==p.state && spares==p.spares && capacity==p.capacity && flags==p.flags && charge==p.charge && revision==p.revision && startedAt.Equals(p.startedAt) && duration.Equals(p.duration);
        public static FlashlightPacket From(FlashlightFrame f) => new FlashlightPacket { state=(byte)f.state,spares=f.spares,capacity=f.capacity,flags=(byte)((f.beam?1:0)|(f.wantedOn?2:0)),charge=(ushort)Mathf.RoundToInt(f.charge*1000),revision=f.revision,startedAt=f.startedAt,duration=f.duration };
        public FlashlightFrame Frame => new FlashlightFrame { state=(FlashlightState)state,spares=spares,capacity=capacity,beam=(flags&1)!=0,wantedOn=(flags&2)!=0,charge=charge/1000f,revision=revision,startedAt=startedAt,duration=duration };
    }
    [DisallowMultipleComponent, RequireComponent(typeof(FlashlightController)), DefaultExecutionOrder(200)]
    public sealed class FlashlightNetworkSync : NetworkBehaviour, IFlashlightTransport
    {
        private readonly NetworkVariable<FlashlightPacket> snapshot=new NetworkVariable<FlashlightPacket>();
        private FlashlightController controller;
        private double nextPublish, nextRequest;
        bool IFlashlightTransport.HasAuthority => IsSpawned && IsServer;
        public double ServerNow => IsSpawned ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        public override void OnNetworkSpawn()
        {
            controller=GetComponent<FlashlightController>();controller.Bind(this);snapshot.OnValueChanged+=Changed;
            if(IsServer) Publish(); else controller.ApplyRemote(snapshot.Value.Frame);
        }
        private void Update()
        {
            if(!IsSpawned || !IsServer) return;
            FlashlightPacket current=FlashlightPacket.From(controller.Frame);
            bool changed=current.revision!=snapshot.Value.revision || current.spares!=snapshot.Value.spares || current.capacity!=snapshot.Value.capacity;
            if(changed || ServerNow>=nextPublish) { Publish(); nextPublish=ServerNow+.2; }
        }
        private void Publish() { var value=FlashlightPacket.From(controller.Frame);if(!value.Equals(snapshot.Value)) snapshot.Value=value; }
        private void Changed(FlashlightPacket previous, FlashlightPacket current) { if(!IsServer)controller.ApplyRemote(current.Frame); }
        public void Send(FlashlightRequest request) { if(IsSpawned && IsOwner) RequestRpc((byte)request); }
        [Rpc(SendTo.Server,InvokePermission=RpcInvokePermission.Owner)]
        private void RequestRpc(byte request,RpcParams rpc=default)
        {
            if(!IsServer || rpc.Receive.SenderClientId!=OwnerClientId || request>(byte)FlashlightRequest.Holster || ServerNow<nextRequest) return;
            nextRequest=ServerNow+.075;controller.Request((FlashlightRequest)request);Publish();
        }
        public override void OnNetworkDespawn() { snapshot.OnValueChanged-=Changed;if(controller!=null)controller.ClearRemoteState(); }
    }
}
