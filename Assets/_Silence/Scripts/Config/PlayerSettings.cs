using UnityEngine;

namespace Silence.Config
{
    [CreateAssetMenu(menuName = "SILENCE/Player Settings")]
    public sealed class PlayerSettings : ScriptableObject
    {
        [Min(0.1f)] public float walkSpeed = 3f;
        [Min(0.1f)] public float runSpeed = 5.5f;
        [Min(0.1f)] public float crouchSpeed = 1.4f;
        [Min(0.6f)] public float standingHeight = 1.8f;
        [Min(0.6f)] public float crouchingHeight = 1.05f;
        [Min(0f)] public float jumpHeight = 0.65f;
        public float gravity = -22f;
        [Range(1f, 5f)] public float interactionRange = 2.5f;

        private void OnValidate()
        {
            standingHeight = Mathf.Max(1.4f, standingHeight);
            crouchingHeight = Mathf.Clamp(crouchingHeight, 0.7f, standingHeight);
            gravity = Mathf.Min(-0.1f, gravity);
        }
    }
}
