using UnityEngine;

namespace Silence.Graphics
{
    public enum GraphicsPreset { Medium, High, Ultra }
    [CreateAssetMenu(menuName = "SILENCE/Graphics Quality")]
    public sealed class GraphicsQualityProfile : ScriptableObject
    {
        public GraphicsPreset preset;
        [Range(.6f, 1.5f)] public float renderScale = 1;
        [Range(15, 130)] public float shadowDistance = 45;
        public int shadowResolution = 2048;
        [Range(0, 4)] public int msaaSamples = 2;
        [Range(.2f, 2)] public float lodBias = 1;
        [Range(.1f, 1)] public float vegetationDensity = .55f;
        [Range(40, 220)] public float viewDistance = 140;
        [Range(8, 32)] public int fogSteps = 12;
        public bool reflectionProbes = true;
        public bool particles = true;
        public bool bloom = true;
        public float exposure = .25f;
    }
}
