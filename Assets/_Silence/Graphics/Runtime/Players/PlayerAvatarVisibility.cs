using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Silence.Player;

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
        }
        [SerializeField] private Renderer[] headRenderers = Array.Empty<Renderer>();
        private ShadowCastingMode[] baseline = Array.Empty<ShadowCastingMode>();
        private PlayerContext actor;
        private readonly Stack<Frame> frames = new Stack<Frame>();
        private readonly Stack<Frame> pool = new Stack<Frame>();
        public IReadOnlyList<Renderer> HiddenRenderers => headRenderers;
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
            bool hide = camera == actor.View && actor.IsLocalOwner;
            var spectator = camera.GetComponentInParent<SpectatorView>();
            hide |= spectator != null && spectator.SpectatorCamera == camera && spectator.ObservedActor == actor;
            var frame = pool.Count > 0 ? pool.Pop() : new Frame(); frame.camera = camera;
            if (frame.previous == null || frame.previous.Length != headRenderers.Length) frame.previous = new ShadowCastingMode[headRenderers.Length];
            for (int i = 0; i < headRenderers.Length; i++)
            {
                if (headRenderers[i] == null) continue;
                frame.previous[i] = headRenderers[i].shadowCastingMode;
                headRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : baseline[i];
            }
            frames.Push(frame);
        }
        private void End(Camera camera)
        {
            // A disabled/destroyed camera may omit its matching callback; unwind to a known state.
            while (frames.Count > 0)
            {
                var frame = frames.Pop();
                for (int i = 0; i < headRenderers.Length; i++) if (headRenderers[i] != null) headRenderers[i].shadowCastingMode = frame.previous[i];
                bool matched = frame.camera == camera; frame.camera = null; pool.Push(frame);
                if (matched) break;
            }
        }
        private void Restore()
        {
            for (int i = 0; i < Math.Min(headRenderers.Length, baseline.Length); i++) if (headRenderers[i] != null) headRenderers[i].shadowCastingMode = baseline[i];
            frames.Clear(); pool.Clear();
        }
    }
}
