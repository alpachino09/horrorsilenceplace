using UnityEngine;

namespace Silence.Gameplay
{
    public sealed class WorldAtmosphere : MonoBehaviour
    {
        public GameplayDirector game;
        public Light sun;
        private void Update()
        {
            if (game == null || game.Settings == null || game.Settings.chapters == null || game.Settings.chapters.Length != 7) return;
            float daylight = .35f + .2f * Mathf.Cos(Time.time * .003f);
            RenderSettings.fog = true; RenderSettings.fogDensity = .018f;
            RenderSettings.fogColor = game.Chapter.fogColor;
            RenderSettings.ambientLight = Color.Lerp(new Color(.04f, .05f, .08f), new Color(.25f, .27f, .29f), daylight);
            if (sun != null)
            {
                sun.intensity = daylight;
                sun.transform.rotation = Quaternion.Euler(12 + Mathf.Sin(Time.time * .003f) * 9, -35, 0);
                sun.color = new Color(1, .69f, .47f);
            }
        }
    }
}
