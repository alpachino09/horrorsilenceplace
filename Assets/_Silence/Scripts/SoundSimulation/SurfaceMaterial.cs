using UnityEngine;
using Silence.Core;

namespace Silence.SoundSimulation
{
    public sealed class SurfaceMaterial : MonoBehaviour
    {
        public SurfaceKind kind;
        [Range(0, .95f)] public float soundObstruction = .35f;
        public static SurfaceKind Below(Vector3 position)
        {
            if (Physics.Raycast(position + Vector3.up * .25f, Vector3.down, out RaycastHit hit, .7f,
                ~0, QueryTriggerInteraction.Ignore))
            {
                var surface = hit.collider.GetComponentInParent<SurfaceMaterial>();
                if (surface != null) return surface.kind;
            }
            return SurfaceKind.Dirt;
        }
    }
}
