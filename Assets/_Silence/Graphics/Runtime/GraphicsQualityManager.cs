using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Silence.Player;

namespace Silence.Graphics
{
    // Local rendering choices only. No colliders, noise, authority, AI, or world seeds are changed.
    public sealed class GraphicsQualityManager : MonoBehaviour
    {
        [SerializeField] private GraphicsQualityProfile[] profiles;
        [SerializeField] private Volume volume;
        [SerializeField] private Terrain terrain;
        [SerializeField] private FarmFogVolume fog;
        private UniversalRenderPipelineAsset original, local;
        private bool originalQualityWasNull;
        private float oldLod, oldShadow;
        private GraphicsQualityProfile active;
        private PlayerContext[] actors;
        private float nextActorLookup;
        public GraphicsQualityProfile Active => active;
        public bool EffectsEnabled { get; private set; }
        public void Configure(GraphicsQualityProfile[] values, Volume post, Terrain ground, FarmFogVolume atmosphere)
        { profiles = values; volume = post; terrain = ground; fog = atmosphere; }
        private void Awake()
        {
            oldLod = QualitySettings.lodBias; oldShadow = QualitySettings.shadowDistance;
            originalQualityWasNull = QualitySettings.renderPipeline == null;
            original = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (original == null) { enabled = false; Debug.LogError("SILENCE visual rebuild requires URP."); return; }
            // Clone at runtime: never persist changes to the owner's shared pipeline asset.
            local = Instantiate(original); local.name = "SILENCE local graphics settings"; QualitySettings.renderPipeline = local;
            Apply((GraphicsPreset)Mathf.Clamp(PlayerPrefs.GetInt("Silence.Graphics.Preset", 0), 0, 2));
        }
        public void Apply(GraphicsPreset value)
        {
            if (local == null || profiles == null || profiles.Length != 3) return;
            active = profiles[(int)value]; if (active == null) return;
            local.renderScale = active.renderScale; local.msaaSampleCount = active.msaaSamples >= 8 ? 8 : active.msaaSamples >= 4 ? 4 : active.msaaSamples >= 2 ? 2 : 1;
            local.shadowDistance = active.shadowDistance; local.mainLightShadowmapResolution = active.shadowResolution;
            local.supportsCameraDepthTexture = true; local.supportsHDR = true;
            QualitySettings.lodBias = active.lodBias; QualitySettings.shadowDistance = active.shadowDistance;
            if (terrain != null) { terrain.detailObjectDensity = active.vegetationDensity; terrain.detailObjectDistance = active.preset == GraphicsPreset.Medium ? 32 : 55; }
            if (fog != null) fog.StepCount = active.fogSteps;
            foreach (var probe in GetComponentsInChildren<ReflectionProbe>(true))
            { probe.enabled = active.reflectionProbes; probe.resolution = active.preset == GraphicsPreset.Ultra ? 512 : active.preset == GraphicsPreset.High ? 256 : 128; }
            foreach (var particles in GetComponentsInChildren<ParticleSystem>(true))
            { var emission = particles.emission; emission.enabled = active.particles; }
            if (volume != null && volume.profile.TryGet(out Bloom bloom)) bloom.active = active.bloom;
            if (volume != null && volume.profile.TryGet(out ColorAdjustments color)) color.postExposure.value = active.exposure;
            PlayerPrefs.SetInt("Silence.Graphics.Preset", (int)value);
        }
        private void LateUpdate()
        {
            if (active == null) return;
            if (local != null) local.shadowDistance = Mathf.Clamp(QualitySettings.shadowDistance, 15, 130);
            if (actors == null || Time.unscaledTime >= nextActorLookup) { actors = SceneQuery.All<PlayerContext>(); nextActorLookup = Time.unscaledTime + 1; }
            foreach (var actor in actors)
            {
                if (actor == null || !actor.IsLocalOwner) continue;
                actor.View.farClipPlane = active.viewDistance;
                if (actor.View.TryGetComponent(out UniversalAdditionalCameraData data))
                {
                    data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                }
            }
        }
        public void SetCameraEffects(bool enabledEffects)
        {
            EffectsEnabled = enabledEffects;
            if (volume == null) return;
            if (volume.profile.TryGet(out FilmGrain grain)) grain.active = enabledEffects;
            if (volume.profile.TryGet(out Vignette vignette)) vignette.active = enabledEffects;
            // Motion blur and chromatic aberration deliberately remain disabled by default.
        }
        private void OnDestroy()
        {
            if (local == null) return;
            if (QualitySettings.renderPipeline == local) QualitySettings.renderPipeline = originalQualityWasNull ? null : original;
            QualitySettings.lodBias = oldLod; QualitySettings.shadowDistance = oldShadow; Destroy(local);
        }
    }
}
