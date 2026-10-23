using UnityEngine;
namespace Silence.SoundSimulation
{
    public static class AcousticOcclusion
    {
        private static readonly RaycastHit[] hits = new RaycastHit[32];
        public static float Between(Vector3 source, Vector3 listener)
        {
            Vector3 delta = listener - source;
            int count = Physics.RaycastNonAlloc(source, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float transmission = 1;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].collider.GetComponentInParent<Silence.Player.PlayerContext>() != null) continue;
                var material = hits[i].collider.GetComponentInParent<SurfaceMaterial>();
                transmission *= 1 - (material != null ? material.soundObstruction : .2f);
            }
            return 1 - transmission;
        }
    }
}
