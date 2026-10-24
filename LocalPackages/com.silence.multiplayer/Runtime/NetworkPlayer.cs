using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using Silence.Core;
using Silence.Interactions;
using Silence.Player;
using Silence.UI;

namespace Silence.Multiplayer
{
    [RequireComponent(typeof(PlayerContext), typeof(NetworkObject))]
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        private readonly NetworkVariable<PlayerPose> pose = new NetworkVariable<PlayerPose>();
        private readonly NetworkVariable<InventorySnapshot> inventory = new NetworkVariable<InventorySnapshot>(
            default, NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
        private readonly CommandInbox inbox = new CommandInbox();
        private NetworkWorldAuthority world;
        private FirstPersonMotor motor;
        private CharacterController controller;
        private InteractionScanner scanner;
        private PrototypeHud hud;
        private Light lamp;
        private Transform body;
        private PlayerCommand command;
        private float yaw, pitch;
        private uint interactionSequence, serverTick;
        private uint gameplaySequence;
        private bool lampOn = true;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
        private bool automated;
        private PlayerCommand automationCommand;
        internal void SetAutomationInput(PlayerCommand value) { automated = true; automationCommand = value; }
        internal void WarpForSmoke(Vector3 position)
        {
            if (!IsServer) return;
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            PublishPose();
        }
#endif
        public PlayerContext Context { get; private set; }
        public uint ReceivedPoseTick => pose.Value.Tick;
        public uint SuccessfulInteractions { get; private set; }

        public override void OnNetworkSpawn()
        {
            Context = GetComponent<PlayerContext>();
            world = NetworkSession.Instance.World;
            motor = GetComponent<FirstPersonMotor>();
            controller = GetComponent<CharacterController>();
            scanner = GetComponent<InteractionScanner>();
            hud = GetComponent<PrototypeHud>();
            lamp = Context.View.GetComponentInChildren<Light>(true);
            body = transform.Find("Body_Graybox");
            motor.enabled = false; // Only the server's fixed network tick calls Simulate.
            GetComponent<PlayerLook>().enabled = false;
            GetComponent<PlayerFlashlight>().enabled = false;
            controller.enabled = IsServer;
            world.Register(this);
            scanner.enabled = hud.enabled = Context.Input.enabled = false;
            yaw = transform.eulerAngles.y;
            command = new PlayerCommand { Yaw = yaw, Flashlight = true };
            if (body != null)
            {
                var visual = body.GetComponent<Renderer>();
                visual.shadowCastingMode = IsOwner ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", Color.HSVToRGB((OwnerClientId * .23f) % 1f, .55f, .8f));
                visual.SetPropertyBlock(block);
            }
            inventory.OnValueChanged += InventoryChanged;
            if (IsServer) { PublishPose(); PublishInventory(); }
            else if (IsOwner) Context.Inventory.ApplySnapshot(inventory.Value.Capacity == 0 ? 3 : inventory.Value.Capacity, inventory.Value.ToArray());
            if (!IsServer && pose.Value.Height > 0) ApplyPose(pose.Value, true);
            NetworkManager.NetworkTickSystem.Tick += NetworkTick;
            if (IsOwner) NetworkSession.Instance.SetLocalPlayer(this);
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null && NetworkManager.NetworkTickSystem != null)
                NetworkManager.NetworkTickSystem.Tick -= NetworkTick;
            inventory.OnValueChanged -= InventoryChanged;
            if (world != null && Context != null) world.Unregister(this);
            if (Context != null)
            {
                Context.Input.enabled = false;
                Context.View.enabled = false;
                var listener = Context.View.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
            if (IsOwner) PlayerLook.Capture(false);
        }

        private void Update()
        {
            if (!IsSpawned) return;
            bool controls = IsOwner && world.Running && (Context.Game == null || Context.Game.CanAct(Context));
            Context.Input.enabled = controls;
            scanner.enabled = controls;
            hud.enabled = controls && Context.Game == null;
            if (!controls) return;
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            if (automated)
            {
                uint sequence = command.Sequence;
                command = automationCommand;
                command.Sequence = sequence;
                yaw = command.Yaw; pitch = command.Pitch;
                return;
            }
#endif
            var input = Context.Input;
            if (input.PausePressed) PlayerLook.Capture(!input.GameplayActive);
            else if (!input.GameplayActive && input.CapturePressed) PlayerLook.Capture(true);
            bool active = input.GameplayActive;
            if (active)
            {
                Vector2 look = input.MouseLook * PrototypePreferences.MouseSensitivity + input.StickLook * (120f * Time.deltaTime);
                if (Context.Game != null && input.DoorHeld && scanner.Current is ManualDoor) look = Vector2.zero;
                yaw = Mathf.Repeat(yaw + look.x, 360f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                if (input.JumpPressed) command.JumpSequence++;
                if (Context.Flashlight == null && input.FlashlightPressed) lampOn = !lampOn;
            }
            Vector2 axis = active ? input.Move : Vector2.zero;
            if (active && input.CarefulWalk) axis *= .45f;
            command.X = axis.x; command.Z = axis.y;
            command.Sprint = active && input.Sprint && !input.CarefulWalk;
            command.Crouch = active && input.Crouch;
            command.Yaw = yaw; command.Pitch = pitch; command.Flashlight = lampOn;
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!IsServer && pose.Value.Height > 0) ApplyPose(pose.Value, false);
            // Eye look is immediate locally; world position is authoritative and interpolated.
            if (IsOwner && world.Running)
            {
                if (!IsServer) transform.rotation = Quaternion.Euler(0, yaw, 0);
                Context.View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
                if (lamp != null) lamp.enabled = Context.Flashlight == null && lampOn && Context.FlashlightCharge > 0 && Context.Vitals.Alive;
            }
        }

        private void NetworkTick()
        {
            if (!IsSpawned) return;
            if (IsOwner && world.Running)
            {
                command.Sequence++;
                SubmitInputRpc(new InputPacket { Command = command });
            }
            if (!IsServer) return;
            if (world.Running)
            {
                PlayerCommand accepted = inbox.Consume(Time.unscaledTimeAsDouble, out bool jump);
                transform.rotation = Quaternion.Euler(0, accepted.Yaw, 0);
                Context.View.transform.localRotation = Quaternion.Euler(accepted.Pitch, 0, 0);
                lampOn = accepted.Flashlight;
                if (Context.Flashlight == null) Context.FlashlightOn = lampOn && Context.FlashlightCharge > 0 && Context.Vitals.Alive;
                if (lamp != null) lamp.enabled = Context.Flashlight == null && Context.FlashlightOn;
                motor.Simulate(new Vector2(accepted.X, accepted.Z), accepted.Sprint, accepted.Crouch,
                    jump, 1f / NetworkManager.NetworkConfig.TickRate);
            }
            PublishPose();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitInputRpc(InputPacket packet, RpcParams rpc = default)
        {
            if (!IsServer || !world.Running || rpc.Receive.SenderClientId != OwnerClientId) return;
            if (inbox.TryReceive(packet.Command, Time.unscaledTimeAsDouble) &&
                packet.Command.TrySanitize(out var safe))
            {
                // A reliable interaction sent next uses this validated aim, even between ticks.
                transform.rotation = Quaternion.Euler(0, safe.Yaw, 0);
                Context.View.transform.localRotation = Quaternion.Euler(safe.Pitch, 0, 0);
            }
        }

        internal void RequestInteraction(ulong target)
        {
            InteractionDiagnostics.Trace(this, "RPC_SEND", "owner=" + OwnerClientId + " isOwner=" + IsOwner +
                " server=" + IsServer + " target=" + target + " sequence=" + (interactionSequence + 1));
            // Reliable ordered input arrives before the associated interaction request.
            command.Sequence++;
            SubmitInputRpc(new InputPacket { Command = command });
            InteractRpc(target, ++interactionSequence);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void InteractRpc(ulong target, uint sequence, RpcParams rpc = default)
        {
            InteractionDiagnostics.Trace(this, "RPC_RECEIVE", "sender=" + rpc.Receive.SenderClientId +
                " target=" + target + " sequence=" + sequence);
            world.ApplyInteraction(this, rpc.Receive.SenderClientId, target, sequence);
        }

        internal void PublishInventory()
        {
            if (IsServer) inventory.Value = InventorySnapshot.Capture(Context.Inventory);
        }
        internal void RequestGameplayAction(ActorAction action, int slot, float amount, ulong target)
        {
            command.Sequence++;
            SubmitInputRpc(new InputPacket { Command = command });
            GameplayActionRpc(++gameplaySequence, (byte)action, slot, amount, target);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        private void GameplayActionRpc(uint sequence, byte action, int slot, float amount, ulong target, RpcParams rpc = default) =>
            world.ApplyAction(this, rpc.Receive.SenderClientId, sequence, (ActorAction)action, slot, amount, target);
        private void InventoryChanged(InventorySnapshot previous, InventorySnapshot current)
        {
            if (!IsServer && IsOwner) Context.Inventory.ApplySnapshot(current.Capacity == 0 ? 3 : current.Capacity, current.ToArray());
        }
        internal void SendFeedback(bool applied) => FeedbackRpc(applied);
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void FeedbackRpc(bool applied)
        {
            if (applied) SuccessfulInteractions++;
            Context.ShowMessage(PrototypePreferences.English
                ? (applied ? "Interaction confirmed." : "Inventory full.")
                : (applied ? "Взаимодействие подтверждено." : "Инвентарь заполнен."));
        }
        internal void SendGameplayFeedback(bool applied, string message) => GameplayFeedbackRpc(applied, message ?? "");
        [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
        private void GameplayFeedbackRpc(bool applied, string message)
        {
            if (applied) SuccessfulInteractions++;
            if (!string.IsNullOrEmpty(message)) Context.ShowMessage(message);
            else if (!applied) Context.ShowMessage(PrototypePreferences.English ? "Cannot do that now. Check your pockets." : "Сейчас это не получится. Проверьте карманы.");
        }
        private void PublishPose()
        {
            SetBodyHeight(controller.height);
            pose.Value = new PlayerPose { Position = transform.position, Yaw = transform.eulerAngles.y,
                Pitch = Context.View.transform.localEulerAngles.x, Height = controller.height,
                Flashlight = Context.Flashlight != null ? Context.FlashlightOn : lampOn && Context.FlashlightCharge > 0 && Context.Vitals.Alive, Tick = ++serverTick };
        }
        private void ApplyPose(PlayerPose state, bool snap)
        {
            float blend = snap || Vector3.Distance(transform.position, state.Position) > 3f
                ? 1f : 1f - Mathf.Exp(-18f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, state.Position, blend);
            if (!IsOwner) transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.Euler(0, state.Yaw, 0), blend);
            Vector3 eye = Context.View.transform.localPosition;
            eye.y = Mathf.Lerp(eye.y, state.Height - .16f, blend);
            Context.View.transform.localPosition = eye;
            if (!IsOwner) Context.View.transform.localRotation = Quaternion.Euler(state.Pitch, 0, 0);
            if (lamp != null && !IsOwner) lamp.enabled = Context.Flashlight == null && state.Flashlight;
            SetBodyHeight(state.Height);
        }
        private void SetBodyHeight(float height)
        {
            if (body == null) return;
            body.localPosition = Vector3.up * (height * .5f);
            body.localScale = new Vector3(.6f, height * .5f, .6f);
        }
        private void OnApplicationFocus(bool focused) { if (!focused && IsOwner) PlayerLook.Capture(false); }
    }
}
