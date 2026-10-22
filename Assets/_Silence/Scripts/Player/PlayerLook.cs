using UnityEngine;
using Silence.UI;

namespace Silence.Player
{
    [RequireComponent(typeof(PlayerContext))]
    public sealed class PlayerLook : MonoBehaviour
    {
        private PlayerContext player;
        private float pitch;
        private void Awake() => player = GetComponent<PlayerContext>();

        private void Update()
        {
            if (!player.IsLocalOwner) return;
            if (player.Input.PausePressed) Capture(!player.Input.GameplayActive);
            else if (!player.Input.GameplayActive && player.Input.CapturePressed) Capture(true);
            if (!player.Input.GameplayActive) return;
            Vector2 delta = player.Input.MouseLook * PrototypePreferences.MouseSensitivity +
                player.Input.StickLook * (120f * Time.deltaTime);
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            player.View.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public static void Capture(bool capture)
        {
            Cursor.lockState = capture ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !capture;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (!focus && player != null && player.IsLocalOwner) Capture(false);
        }

        private void OnDisable()
        {
            if (player != null && player.IsLocalOwner) Capture(false);
        }
    }
}
