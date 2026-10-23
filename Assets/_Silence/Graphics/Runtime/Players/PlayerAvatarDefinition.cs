using UnityEngine;

namespace Silence.Graphics.Players
{
    [CreateAssetMenu(menuName = "SILENCE/Player Avatar Definition", fileName = "PlayerAvatar")]
    public sealed class PlayerAvatarDefinition : ScriptableObject
    {
        [Tooltip("Art-only prefab or imported FBX with one Animator; no gameplay components.")]
        public GameObject modelPrefab;
        public RuntimeAnimatorController controller;
        public bool requireHumanoid = true;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;
        [Tooltip("Renderer paths relative to modelPrefab: head, eyes, hair, headwear. Keep torso and arms separate.")]
        public string[] firstPersonHiddenRenderers = System.Array.Empty<string>();
        public string referenceUrl = "https://www.fab.com/listings/edf670ba-1967-4b84-bbea-544847c9b6e2";

        public static readonly string[] FloatParameters = { "MoveX", "MoveZ", "Speed", "VerticalSpeed", "LookPitch" };
        public static readonly string[] BoolParameters = { "Grounded", "Crouched", "Carrying", "Wounded", "Dead" };
    }
}
