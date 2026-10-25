using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Silence.Player;
using Silence.Core;

namespace Silence.Graphics.Players
{
    // Per-camera visibility preserves the full model in mirrors, other views and shadows.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext))]
    public sealed class PlayerAvatarVisibility : MonoBehaviour
    {
        private sealed class Frame
        {
            public Camera camera;
            public ShadowCastingMode[] previous;
            public bool previousMaterialState;
        }
        [SerializeField] private Renderer[] headRenderers = Array.Empty<Renderer>();
        [SerializeField] private Renderer[] bodyRenderers = Array.Empty<Renderer>();
        private Material[][] exteriorMaterials, firstPersonMaterials;
        private readonly Dictionary<Material, Material> interiorSafeMaterials = new Dictionary<Material, Material>();
        private bool firstPersonMaterialState;
        private ShadowCastingMode[] baseline = Array.Empty<ShadowCastingMode>();
        private PlayerContext actor;
        private readonly CharacterRenderScopes<Frame> frames = new CharacterRenderScopes<Frame>();
        private readonly Stack<Frame> pool = new Stack<Frame>();
        private int renderFrame=-1;
        public IReadOnlyList<Renderer> HiddenRenderers => headRenderers;
        public IReadOnlyList<Renderer> BodyRenderers => bodyRenderers;
        public void ConfigureBody(Renderer[] renderers) { RestoreMaterials(); bodyRenderers = renderers ?? Array.Empty<Renderer>(); exteriorMaterials = null; firstPersonMaterials = null; }
        public void Configure(Renderer[] renderers)
        {
            Restore(); headRenderers = renderers ?? Array.Empty<Renderer>(); CaptureBaseline();
        }
        private void CaptureBaseline()
        {
            baseline = new ShadowCastingMode[headRenderers.Length];
            for (int i = 0; i < headRenderers.Length; i++) if (headRenderers[i] != null) baseline[i] = headRenderers[i].shadowCastingMode;
        }
        private void OnEnable()
        {
            actor = GetComponent<PlayerContext>(); CaptureBaseline();
            RenderPipelineManager.beginCameraRendering += BeginSrp;
            RenderPipelineManager.endCameraRendering += EndSrp;
            Camera.onPreCull += BeginBuiltin; Camera.onPostRender += EndBuiltin;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginSrp;
            RenderPipelineManager.endCameraRendering -= EndSrp;
            Camera.onPreCull -= BeginBuiltin; Camera.onPostRender -= EndBuiltin;
            Restore();
        }
        private void BeginSrp(ScriptableRenderContext context, Camera camera) => Begin(camera);
        private void EndSrp(ScriptableRenderContext context, Camera camera) => End(camera);
        private void BeginBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) Begin(camera); }
        private void EndBuiltin(Camera camera) { if (GraphicsSettings.currentRenderPipeline == null) End(camera); }
        private void Begin(Camera camera)
        {
            if (camera == null || actor == null || headRenderers.Length == 0) return;
            if(renderFrame!=Time.frameCount){if(frames.Count>0)Restore();renderFrame=Time.frameCount;}
            bool hide = actor.IsLocalOwner && (actor.ViewRig != null ? actor.ViewRig.IsFirstPersonCamera(camera) : camera == actor.View);
            var spectator = camera.GetComponentInParent<SpectatorView>();
            hide |= spectator != null && spectator.SpectatorCamera == camera && spectator.ObservedActor == actor;
            var frame = pool.Count > 0 ? pool.Pop() : new Frame(); frame.camera = camera;
            frame.previousMaterialState = firstPersonMaterialState; SetMaterialState(hide);
            if (frame.previous == null || frame.previous.Length != headRenderers.Length) frame.previous = new ShadowCastingMode[headRenderers.Length];
            for (int i = 0; i < headRenderers.Length; i++)
            {
                if (headRenderers[i] == null) continue;
                frame.previous[i] = headRenderers[i].shadowCastingMode;
                headRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : baseline[i];
            }
            frames.Push(camera,frame);
        }
        private void End(Camera camera)
        {
            // A disabled/destroyed camera may omit its matching callback; unwind to a known state.
            while (frames.TryPopFor(camera,out var frame,out bool matched))
            {
                SetMaterialState(frame.previousMaterialState);
                for (int i = 0; i < headRenderers.Length; i++) if (headRenderers[i] != null) headRenderers[i].shadowCastingMode = frame.previous[i];
                frame.camera = null; pool.Push(frame);
                if (matched) break;
            }
        }
        private void Restore()
        {
            for (int i = 0; i < Math.Min(headRenderers.Length, baseline.Length); i++) if (headRenderers[i] != null) headRenderers[i].shadowCastingMode = baseline[i];
            frames.Clear(); pool.Clear();
            RestoreMaterials();
        }
        private void SetMaterialState(bool firstPerson)
        {
            if (firstPerson == firstPersonMaterialState || bodyRenderers.Length == 0) return;
            if (exteriorMaterials == null)
            {
                exteriorMaterials = new Material[bodyRenderers.Length][]; firstPersonMaterials = new Material[bodyRenderers.Length][];
                for (int i = 0; i < bodyRenderers.Length; i++)
                {
                    if (bodyRenderers[i] == null) continue;
                    var original = bodyRenderers[i].sharedMaterials; exteriorMaterials[i] = original; var safe = new Material[original.Length];
                    for (int j = 0; j < original.Length; j++)
                    {
                        var material = original[j]; safe[j] = material;
                        if (material == null || !material.HasProperty("_Cull")) continue;
                        if (!interiorSafeMaterials.TryGetValue(material, out var clone))
                        {
                            clone = new Material(material) { name = material.name + " (local first person)", hideFlags = HideFlags.DontSave };
                            clone.SetFloat("_Cull", (float)CullMode.Back); interiorSafeMaterials.Add(material, clone);
                        }
                        safe[j] = clone;
                    }
                    firstPersonMaterials[i] = safe;
                }
            }
            var materials = firstPerson ? firstPersonMaterials : exteriorMaterials;
            for (int i = 0; i < bodyRenderers.Length; i++) if (bodyRenderers[i] != null && materials[i] != null) bodyRenderers[i].sharedMaterials = materials[i];
            firstPersonMaterialState = firstPerson;
        }
        private void RestoreMaterials() { if (firstPersonMaterialState) SetMaterialState(false); }
        private void OnDestroy()
        {
            RestoreMaterials(); foreach (var material in interiorSafeMaterials.Values) if (material != null) Destroy(material);
            interiorSafeMaterials.Clear();
        }
    }
}
