using UnityEngine;
using UnityEngine.AI;

namespace Silence.Creatures
{
    public sealed class RuntimeNavigation : MonoBehaviour
    {
        [SerializeField] private NavMeshData data;
        private NavMeshDataInstance instance;
        public void Configure(NavMeshData value) => data = value;
        private void OnEnable() { if (data != null) instance = NavMesh.AddNavMeshData(data); }
        private void OnDisable() { if (instance.valid) instance.Remove(); }
    }
}
