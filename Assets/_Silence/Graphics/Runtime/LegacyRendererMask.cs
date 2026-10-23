using UnityEngine;

namespace Silence.Graphics
{
    [DefaultExecutionOrder(2000)]
    public sealed class LegacyRendererMask : MonoBehaviour
    {
        [SerializeField] private Renderer[] replacedRenderers;
        public void Configure(Renderer[] renderers) { replacedRenderers=renderers;Hide(); }
        private void Awake()=>Hide();
        // Pickup reset and barrier replication toggle child renderers. Hide only the old visuals afterwards.
        private void LateUpdate()=>Hide();
        private void Hide() { if(replacedRenderers!=null)foreach(var renderer in replacedRenderers)if(renderer!=null)renderer.enabled=false; }
    }
}
