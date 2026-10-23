using UnityEngine;
using Silence.Config;
using Silence.Core;
using Silence.Input;
using Silence.Networking;

namespace Silence.Player
{
    public sealed class PlayerContext : MonoBehaviour
    {
        [SerializeField] private PlayerSettings settings;
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private Camera view;
        public PlayerSettings Settings => settings;
        public PlayerInputReader Input => input;
        public Camera View => view;
        public InventoryModel Inventory { get; } = new InventoryModel();
        public IWorldAuthority Authority { get; private set; }
        public bool IsLocalOwner => Authority != null && Authority.IsLocalOwner(this);
        public Vector3 SpawnPosition { get; private set; }
        public Quaternion SpawnRotation { get; private set; }
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public void ShowMessage(string text)
        {
            Message = text;
            MessageUntil = Time.unscaledTime + 5f;
        }

        public void Configure(PlayerSettings config, PlayerInputReader reader, Camera camera)
        {
            settings = config; input = reader; view = camera;
        }

        public void Bind(IWorldAuthority authority)
        {
            Authority = authority;
            SpawnPosition = transform.position;
            SpawnRotation = transform.rotation;
            bool local = IsLocalOwner;
            // Remote eyes remain transforms for server raycasts and replicated lamps.
            view.gameObject.SetActive(true);
            view.enabled = local;
            var listener = view.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = local;
            input.enabled = local;
        }
    }
}
