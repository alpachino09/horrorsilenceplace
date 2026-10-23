using UnityEngine;
namespace Silence.SoundSimulation
{
    public sealed class AcousticMaskingZone : MonoBehaviour
    {
        public Vector3 size = new Vector3(12, 8, 12);
        [Range(0, .85f)] public float strength = .4f;
        public bool Contains(Vector3 point)
        {
            Vector3 local = transform.InverseTransformPoint(point);
            return Mathf.Abs(local.x) <= size.x / 2 && Mathf.Abs(local.y) <= size.y / 2 && Mathf.Abs(local.z) <= size.z / 2;
        }
    }
}
