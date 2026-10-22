using UnityEngine;
using Silence.Player;
using Silence.Interactions;
using Silence.Inventory;

namespace Silence.UI
{
    [RequireComponent(typeof(PlayerContext), typeof(InteractionScanner))]
    public sealed class PrototypeHud : MonoBehaviour
    {
        [SerializeField] private ItemDefinition[] definitions;
        private PlayerContext player;
        private InteractionScanner scanner;
        private bool inventoryVisible;
        public void Configure(ItemDefinition[] items) => definitions = items;
        private void Awake()
        {
            player = GetComponent<PlayerContext>();
            scanner = GetComponent<InteractionScanner>();
        }
        private void Update()
        {
            if (player.IsLocalOwner && player.Input.InventoryPressed)
                inventoryVisible = !inventoryVisible;
        }
        private string Text(string ru, string en) => PrototypePreferences.English ? en : ru;
        private void OnGUI()
        {
            if (player == null || !player.IsLocalOwner) return;
            if (scanner.Current != null && player.Input.GameplayActive)
                GUI.Box(new Rect(Screen.width / 2f - 250, Screen.height - 90, 500, 38),
                    player.Input.InteractionBindingLabel + ": " + scanner.Current.GetPrompt(PrototypePreferences.English));
            if (Time.unscaledTime < player.MessageUntil)
                GUI.Box(new Rect(20, 20, Screen.width - 40, 45), player.Message);
            if (inventoryVisible)
                for (int i = 0; i < player.Inventory.Capacity; i++)
                {
                    string label = i < player.Inventory.Items.Count
                        ? ItemName(player.Inventory.Items[i]) : Text("Пусто", "Empty");
                    GUI.Box(new Rect(20 + i * 150, Screen.height - 180, 145, 60), label);
                }
            if (player.Input.GameplayActive) return;
            Rect panel = new Rect(Screen.width / 2f - 260, Screen.height / 2f - 180, 520, 360);
            GUILayout.BeginArea(panel, GUI.skin.box);
            GUILayout.Label(Text("SILENCE — прототип фермы", "SILENCE — farm prototype"));
            GUILayout.Label("WASD / Stick · Shift · Ctrl · Space · E · F · Tab · Esc");
            GUILayout.Label(Text("До 3 предметов. Выход — ворота напротив места появления.",
                "Up to 3 items. Exit: gate opposite the spawn."));
            if (GUILayout.Button(Text("Продолжить", "Resume"))) PlayerLook.Capture(true);
            if (GUILayout.Button(Text("Язык: русский", "Language: English")))
                PrototypePreferences.English = !PrototypePreferences.English;
            GUILayout.Label(Text("Чувствительность мыши", "Mouse sensitivity"));
            float sensitivity = GUILayout.HorizontalSlider(
                PrototypePreferences.MouseSensitivity, 0.02f, 0.4f);
            if (!Mathf.Approximately(sensitivity, PrototypePreferences.MouseSensitivity))
                PrototypePreferences.MouseSensitivity = sensitivity;
            bool sync = GUILayout.Toggle(QualitySettings.vSyncCount > 0, "VSync");
            if (sync != (QualitySettings.vSyncCount > 0)) QualitySettings.vSyncCount = sync ? 1 : 0;
            GUILayout.Label(Text("Дальность теней", "Shadow distance"));
            float shadowDistance = GUILayout.HorizontalSlider(QualitySettings.shadowDistance, 15f, 70f);
            if (!Mathf.Approximately(shadowDistance, QualitySettings.shadowDistance))
                QualitySettings.shadowDistance = shadowDistance;
            GUILayout.EndArea();
        }
        private string ItemName(string id)
        {
            if (definitions != null)
                foreach (var item in definitions)
                    if (item != null && item.id == id)
                        return PrototypePreferences.English ? item.labelEn : item.labelRu;
            return id;
        }
    }
}
