using UnityEngine;
using Silence.Player;

namespace Silence.Networking
{
    [DefaultExecutionOrder(-500)]
    public sealed class FarmBootstrap : MonoBehaviour
    {
        [SerializeField] private OfflineWorldAuthority authority;
        [SerializeField] private PlayerContext player;
        public void Configure(OfflineWorldAuthority service, PlayerContext actor)
        {
            authority = service; player = actor;
        }
        private void Awake()
        {
            if (authority == null || player == null || player.Settings == null ||
                player.Input == null || player.View == null)
            {
                Debug.LogError("SILENCE: generated scene references are incomplete.", this);
                enabled = false;
                return;
            }
            authority.Register(player);
        }
    }
}
