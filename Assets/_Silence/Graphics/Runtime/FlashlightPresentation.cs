using UnityEngine;
using Silence.Player;

namespace Silence.Graphics
{
    // Offset the light, never the authoritative camera/raycast, controller or aim input.
    public sealed class FlashlightPresentation : MonoBehaviour
    {
        private PlayerContext actor;
        private Vector3 rest;
        private Light lamp;
        private void Awake() { actor = GetComponentInParent<PlayerContext>(); lamp = GetComponent<Light>(); rest = transform.localPosition; }
        private void LateUpdate()
        {
            if (actor == null || !actor.IsLocalOwner || lamp == null) return;
            Vector2 input = actor.Input.GameplayActive ? actor.Input.MouseLook : Vector2.zero;
            Quaternion desired = Quaternion.Euler(Mathf.Clamp(input.y*.025f,-.8f,.8f),Mathf.Clamp(-input.x*.025f,-1,1),0);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, desired, 1-Mathf.Exp(-12*Time.unscaledDeltaTime));
            transform.localPosition = rest; // No view bob, clipping offset or random shake.
        }
    }
}
