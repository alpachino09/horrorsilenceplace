using UnityEngine;
using UnityEngine.Rendering;
using Silence.Gameplay;
using Silence.Core;

namespace Silence.Graphics
{
    [DefaultExecutionOrder(1000)]
    public sealed class VisualAtmosphere : MonoBehaviour
    {
        public GameplayDirector game;
        public Light sun;
        public float timeOfDay = 17.3f;
        public bool cycleTime;
        private Color previousFog, previousAmbient;
        private bool previousFogEnabled, wasActive;
        private AmbientMode previousMode;
        private Color previousSky,previousEquator,previousGround;
        private void Awake() { previousMode=RenderSettings.ambientMode; previousSky=RenderSettings.ambientSkyColor;previousEquator=RenderSettings.ambientEquatorColor;previousGround=RenderSettings.ambientGroundColor;previousFog = RenderSettings.fogColor; previousAmbient = RenderSettings.ambientLight; previousFogEnabled = RenderSettings.fog; }
        private void LateUpdate()
        {
            if (game == null || game.Campaign.Chapter != ChapterId.Farm)
            { if(wasActive){RestoreMode();wasActive=false;} return; }
            wasActive=true;
            if (cycleTime) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / 600, 24);
            float day = Mathf.SmoothStep(0, 1, Mathf.Clamp01(Mathf.Sin((timeOfDay-6)/24 * Mathf.PI*2)*2+.2f));
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler((timeOfDay-6)*15, -38, 0);
                sun.intensity = Mathf.Lerp(.12f, 1.7f, day); sun.color = Color.Lerp(new Color(.55f,.66f,1), new Color(1,.88f,.7f), day);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(.045f,.065f,.11f),new Color(.32f,.38f,.43f),day);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(.025f,.035f,.05f),new Color(.17f,.18f,.17f),day);
            RenderSettings.ambientGroundColor = new Color(.025f,.028f,.022f);
            // The local ray-marched fog handles depth/height; do not stack dense legacy distance fog.
            RenderSettings.fog = false;
        }
        private void RestoreMode()
        { RenderSettings.ambientMode=previousMode;RenderSettings.ambientSkyColor=previousSky;RenderSettings.ambientEquatorColor=previousEquator;RenderSettings.ambientGroundColor=previousGround; }
        private void OnDestroy() { RestoreMode(); RenderSettings.fogColor = previousFog; RenderSettings.ambientLight = previousAmbient; RenderSettings.fog = previousFogEnabled; }
    }
}
