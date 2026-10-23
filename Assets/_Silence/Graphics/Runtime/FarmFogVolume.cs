using UnityEngine;
using Silence.Core;
using Silence.Gameplay;
using Silence.Player;

namespace Silence.Graphics
{
    [DefaultExecutionOrder(1100)]
    public sealed class FarmFogVolume : MonoBehaviour
    {
        public GameplayDirector game;
        public Material fogMaterial;
        public int StepCount = 12;
        private MeshRenderer visual;
        private PlayerContext[] actors;
        private float nextActors;
        private void Awake() => visual = GetComponent<MeshRenderer>();
        private void LateUpdate()
        {
            if (visual == null || fogMaterial == null) return;
            visual.enabled = game != null && game.Campaign.Chapter == ChapterId.Farm;
            if (!visual.enabled) return;
            if (actors == null || Time.unscaledTime >= nextActors) { actors = SceneQuery.All<PlayerContext>(); nextActors = Time.unscaledTime + 1; }
            Light flashlight = null;
            foreach (var actor in actors) if (actor != null && actor.IsLocalOwner) { flashlight = actor.View.GetComponentInChildren<Light>(true); break; }
            fogMaterial.SetInt("_StepCount", Mathf.Clamp(StepCount, 8, 32));
            fogMaterial.SetVector("_BoundsMin", transform.position-transform.lossyScale*.5f);
            fogMaterial.SetVector("_BoundsMax", transform.position+transform.lossyScale*.5f);
            if (flashlight != null && flashlight.enabled)
            {
                fogMaterial.SetVector("_FlashPosition", flashlight.transform.position);
                fogMaterial.SetVector("_FlashDirection", flashlight.transform.forward);
                fogMaterial.SetVector("_FlashParams", new Vector4(Mathf.Cos(flashlight.spotAngle*.5f*Mathf.Deg2Rad), flashlight.range, .2f, 1));
            }
            else fogMaterial.SetVector("_FlashParams", Vector4.zero);
        }
    }
}
