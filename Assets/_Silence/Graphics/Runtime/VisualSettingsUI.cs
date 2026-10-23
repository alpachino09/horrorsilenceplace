using UnityEngine;
using Silence.Player;
using Silence.UI;

namespace Silence.Graphics
{
    public sealed class VisualSettingsUI : MonoBehaviour
    {
        public GraphicsQualityManager quality;
        private PlayerContext local;
        private float nextLookup;
        private void Update()
        {
            if (local != null || Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime+1;
            foreach (var actor in SceneQuery.All<PlayerContext>()) if (actor.IsLocalOwner) { local = actor; break; }
        }
        private void OnGUI()
        {
            if (quality == null || quality.Active == null || local == null || local.Input.GameplayActive || local.Game == null || !local.Game.Started) return;
            GUILayout.BeginArea(new Rect(Screen.width-225,18,210,160),GUI.skin.box);
            GUILayout.Label(PrototypePreferences.English ? "Graphics" : "Графика");
            int chosen=GUILayout.SelectionGrid((int)quality.Active.preset,new[]{"Medium","High","Ultra"},1);
            if (chosen!=(int)quality.Active.preset) quality.Apply((GraphicsPreset)chosen);
            bool effects=GUILayout.Toggle(quality.EffectsEnabled,PrototypePreferences.English ? "Grain / vignette" : "Зерно / виньетка");
            if (effects!=quality.EffectsEnabled) quality.SetCameraEffects(effects);
            GUILayout.EndArea();
        }
    }
}
