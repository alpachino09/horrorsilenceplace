using UnityEngine;
using Silence.Config;
using Silence.Core;
using Silence.Input;
using Silence.Networking;
using Silence.Gameplay;
using Silence.Interactions;

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
        public ActorVitals Vitals { get; } = new ActorVitals();
        public StaminaModel Stamina { get; } = new StaminaModel();
        public GameplayDirector Game { get; internal set; }
        public int SourceId { get; internal set; }
        public float FlashlightCharge { get; internal set; } = 1;
        public bool FlashlightOn { get; internal set; } = true;
        public FlashlightController Flashlight { get; internal set; }
        public float NoiseLevel { get; internal set; }
        public CarryableProp HeldProp { get; internal set; }
        public PlayerContext CarriedBy { get; internal set; }
        public PlayerContext HeldTeammate { get; internal set; }
        public void SetSpawn(Vector3 position, Quaternion rotation) { SpawnPosition = position; SpawnRotation = rotation; }
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
