using UnityEngine;

namespace Silence.Player
{
    [RequireComponent(typeof(PlayerContext))]
    public sealed class PlayerFlashlight : MonoBehaviour
    {
        [SerializeField] private Light lamp;
        private PlayerContext player;
        public void Configure(Light light) => lamp = light;
        private void Awake() => player = GetComponent<PlayerContext>();
        private void Update()
        {
            if (player.IsLocalOwner && player.Input.GameplayActive &&
                player.Input.FlashlightPressed && lamp != null)
                lamp.enabled = !lamp.enabled;
        }
    }
}
