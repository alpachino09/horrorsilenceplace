using UnityEngine;

namespace Silence.Graphics.Players
{
    [CreateAssetMenu(menuName = "SILENCE/Player Animation Profile")]
    public sealed class PlayerAnimationProfile : ScriptableObject
    {
        public RuntimeAnimatorController controller;
        [Min(.1f)] public float walkMetresPerSecond = 1.65f, runMetresPerSecond = 3.6f, sprintMetresPerSecond = 5.5f, crouchMetresPerSecond = 1.4f;
        [Range(.03f, .3f)] public float parameterDamping = .1f;
        [Range(0, 1)] public float footIKWeight = .85f, headLookWeight = .55f;
        [Range(.02f, .3f)] public float maximumPelvisOffset = .16f;
        [Range(.01f, .15f)] public float soleThickness = .045f;
        [Range(5, 40)] public float ikDistance = 22;
        public LayerMask groundMask = ~0;
        public float interactionSeconds = .8f, startleSeconds = .6f, hitSeconds = .65f, tripSeconds = 2.083333f;
        [TextArea] public string provenance;
    }
}
