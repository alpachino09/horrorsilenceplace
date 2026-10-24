using System;
using System.Collections.Generic;
using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext)), DefaultExecutionOrder(650)]
    public sealed class PlayerAvatarFace : MonoBehaviour
    {
        private sealed class Target { public SkinnedMeshRenderer renderer; public string name; public int index; }
        [SerializeField] private bool automaticBlink = true, healthExpressions = true, eyeAim = true;
        [SerializeField] private SkinnedMeshRenderer[] surfaces = Array.Empty<SkinnedMeshRenderer>();
        private readonly List<Target> targets = new List<Target>();
        private PlayerContext actor;
        private static readonly HashSet<string> Driven = new HashSet<string> {
            "eyeBlinkLeft", "eyeBlinkRight", "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight",
            "browDownLeft", "browDownRight", "mouthFrownLeft", "mouthFrownRight" };
        public void Configure(SkinnedMeshRenderer[] renderers) { surfaces = renderers ?? Array.Empty<SkinnedMeshRenderer>(); Cache(); }
        private void OnEnable() { actor = GetComponent<PlayerContext>(); Cache(); }
        private void Cache()
        {
            targets.Clear();
            foreach (var surface in surfaces)
            {
                if (surface == null || surface.sharedMesh == null) continue;
                for (int i = 0; i < surface.sharedMesh.blendShapeCount; i++)
                {
                    string name = surface.sharedMesh.GetBlendShapeName(i); int dot = name.LastIndexOf('.'); if (dot >= 0) name = name.Substring(dot + 1);
                    if (Driven.Contains(name)) targets.Add(new Target { renderer = surface, name = name, index = i });
                }
            }
        }
        private void LateUpdate()
        {
            if (actor == null) return;
            double phase = (Time.unscaledTimeAsDouble + actor.SourceId * 1.137) % 4.7;
            float blink = phase < .16 ? Mathf.Sin((float)(phase / .16) * Mathf.PI) : 0;
            if (healthExpressions && !actor.Vitals.Alive) blink = 1;
            float pitch = actor.View != null ? Mathf.DeltaAngle(0, actor.View.transform.localEulerAngles.x) : 0;
            foreach (var target in targets)
            {
                if (target.renderer == null) continue;
                float weight;
                if (target.name.StartsWith("eyeBlink", StringComparison.Ordinal)) { if (!automaticBlink && (actor.Vitals.Alive || !healthExpressions)) continue; weight = blink; }
                else if (target.name.StartsWith("eyeLook", StringComparison.Ordinal)) { if (!eyeAim) continue; weight = Mathf.Clamp01((target.name.Contains("Up") ? -pitch : pitch) / 85); }
                else { if (!healthExpressions) continue; weight = actor.Vitals.State == VitalState.Wounded ? .18f : 0; }
                target.renderer.SetBlendShapeWeight(target.index, weight * 100);
            }
        }
        private void OnDisable() { foreach (var t in targets) if (t.renderer != null) t.renderer.SetBlendShapeWeight(t.index, 0); }
    }
}
