using UnityEngine;
using UnityEngine.InputSystem;

namespace Silence.Input
{
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset template;
        private InputActionAsset instance;
        private InputAction move, look, lookStick, sprint, crouch, jump;
        private InputAction interact, flashlight, inventory, pause, capture;
        private InputAction drop, throwItem, doorControl, reloadFlashlight, holsterFlashlight, carefulWalk, togglePerspective;
        private readonly InputAction[] useSlots = new InputAction[5];
        public InputActionAsset Template => template;
        public Vector2 Move => move?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 MouseLook => look?.ReadValue<Vector2>() ?? Vector2.zero;
        public Vector2 StickLook => lookStick?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool Sprint => sprint != null && sprint.IsPressed();
        public bool CarefulWalk => carefulWalk != null && carefulWalk.IsPressed();
        public bool TogglePerspectivePressed => togglePerspective != null && togglePerspective.WasPressedThisFrame();
        public bool Crouch => crouch != null && crouch.IsPressed();
        public bool JumpPressed => jump != null && jump.WasPressedThisFrame();
        public bool InteractPressed => interact != null && interact.WasPressedThisFrame();
        public bool FlashlightPressed => flashlight != null && flashlight.WasPressedThisFrame();
        public bool ReloadFlashlightPressed => reloadFlashlight != null && reloadFlashlight.WasPressedThisFrame();
        public bool HolsterFlashlightPressed => holsterFlashlight != null && holsterFlashlight.WasPressedThisFrame();
        public bool InventoryPressed => inventory != null && inventory.WasPressedThisFrame();
        public bool PausePressed => pause != null && pause.WasPressedThisFrame();
        public bool CapturePressed => capture != null && capture.WasPressedThisFrame();
        public bool DropPressed => drop != null && drop.WasPressedThisFrame();
        public bool ThrowPressed => throwItem != null && throwItem.WasPressedThisFrame();
        public bool ThrowReleased => throwItem != null && throwItem.WasReleasedThisFrame();
        public bool DoorHeld => doorControl != null && doorControl.IsPressed();
        public bool UseSlotPressed(int slot) => slot >= 0 && slot < 5 && useSlots[slot] != null && useSlots[slot].WasPressedThisFrame();
        public string InteractionBindingLabel => interact != null ? interact.GetBindingDisplayString() : "E";
        public bool GameplayActive => Application.isFocused &&
            Cursor.lockState == CursorLockMode.Locked;

        public void Configure(InputActionAsset asset) => template = asset;

        private void OnEnable()
        {
            if (template == null)
            {
                Debug.LogError("SILENCE: Input Actions asset is missing.", this);
                enabled = false;
                return;
            }
            // Each future owned player has a separate action instance.
            instance = Instantiate(template);
            move = Find("Move"); look = Find("Look"); lookStick = Find("LookStick");
            sprint = Find("Sprint"); crouch = Find("Crouch"); jump = Find("Jump");
            interact = Find("Interact"); flashlight = Find("Flashlight");
            inventory = Find("Inventory"); pause = Find("Pause"); capture = Find("Capture");
            drop = Optional("Drop"); throwItem = Optional("Throw"); doorControl = Optional("DoorControl");
            reloadFlashlight = Optional("ReloadFlashlight"); holsterFlashlight = Optional("HolsterFlashlight");
            carefulWalk = Optional("CarefulWalk");
            togglePerspective = Optional("TogglePerspective");
            // Existing generated templates may predate the new action. Extend only
            // this player's private input instance, preserving the stored template.
            if (togglePerspective == null)
            {
                var gameplay = instance.FindActionMap("Gameplay", true);
                bool keyTaken = false;
                foreach (var binding in gameplay.bindings) if (binding.path == "<Keyboard>/v") keyTaken = true;
                togglePerspective = gameplay.AddAction("TogglePerspective", InputActionType.Button);
                if (!keyTaken) togglePerspective.AddBinding("<Keyboard>/v");
            }
            for (int i = 0; i < 5; i++) useSlots[i] = Optional("Use" + (i + 1));
            instance.Enable();
        }

        private InputAction Find(string name) => instance.FindAction("Gameplay/" + name, true);
        private InputAction Optional(string name) => instance.FindAction("Gameplay/" + name, false);

        private void OnDisable()
        {
            if (instance == null) return;
            instance.Disable();
            Destroy(instance);
            instance = null;
        }
    }
}
