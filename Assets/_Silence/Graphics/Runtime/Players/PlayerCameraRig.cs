using UnityEngine;
using UnityEngine.Rendering.Universal;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Camera presentation is local. The existing eye Camera's transform continues to
    // drive server aim, look replication, interactions and the flashlight.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerContext)), DefaultExecutionOrder(1000)]
    public sealed class PlayerCameraRig : MonoBehaviour, IPlayerViewRig
    {
        [SerializeField] private Vector3 standingEyeOffset = new Vector3(0, -.04f, .16f);
        [SerializeField] private Vector3 crouchingEyeOffset = new Vector3(0, .02f, .22f);
        [SerializeField, Range(1, 5)] private float thirdPersonDistance = 2.8f;
        [SerializeField, Range(0, .7f)] private float shoulderOffset = .32f;
        [SerializeField, Range(.08f, .4f)] private float collisionRadius = .18f;
        [SerializeField] private LayerMask collisionMask = ~0;
        private PlayerContext actor;
        private CharacterController capsule;
        private Camera rendered;
        private AudioListener eyeListener, renderListener;
        private readonly RaycastHit[] hits = new RaycastHit[24];
        private readonly Collider[] overlaps = new Collider[24];
        private readonly CameraBoomModel boom = new CameraBoomModel();
        private bool thirdPerson, effectiveFirstPerson = true;
        public bool CameraOverride { get; set; }
        public bool ThirdPerson => thirdPerson;
        public Vector3 StandingEyeOffset => standingEyeOffset;
        public Vector3 CrouchingEyeOffset => crouchingEyeOffset;
        public Camera ActiveCamera => rendered != null && rendered.enabled ? rendered : null;
        public bool IsFirstPersonCamera(Camera camera) => camera != null && camera == ActiveCamera && effectiveFirstPerson;
        public void Configure(Vector3 standing, Vector3 crouching) { standingEyeOffset = standing; crouchingEyeOffset = crouching; }
        private void Awake() { actor = GetComponent<PlayerContext>(); capsule = GetComponent<CharacterController>(); if (actor.View != null) eyeListener = actor.View.GetComponent<AudioListener>(); }
        private void OnEnable() { if (actor == null) actor = GetComponent<PlayerContext>(); actor.ViewRig = this; boom.Reset(); }
        public void SetThirdPerson(bool value) { if (thirdPerson != value) { thirdPerson = value; boom.Reset(); } }
        private void Update()
        {
            if (actor.IsLocalOwner && actor.Vitals.Alive && !CameraOverride && actor.Input != null && actor.Input.enabled &&
                actor.Input.GameplayActive && actor.Input.TogglePerspectivePressed) SetThirdPerson(!thirdPerson);
        }
        private void EnsureCamera()
        {
            if (rendered != null) return;
            var root = new GameObject("Local player render camera"); root.transform.SetParent(transform, false);
            rendered = root.AddComponent<Camera>(); rendered.enabled = false; CopyCameraSettings(rendered); rendered.enabled = false;
            rendered.tag = "MainCamera";
            actor.View.enabled = false; if (eyeListener != null) eyeListener.enabled = false;
            renderListener = root.AddComponent<AudioListener>(); renderListener.enabled = false;
        }
        public void CopyCameraSettings(Camera destination)
        {
            destination.CopyFrom(actor.View); destination.nearClipPlane = .035f;
            var sourceData = actor.View.GetComponent<UniversalAdditionalCameraData>();
            var destinationData = destination.GetComponent<UniversalAdditionalCameraData>();
            if (destinationData == null) destinationData = destination.gameObject.AddComponent<UniversalAdditionalCameraData>();
            if (sourceData != null) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(sourceData), destinationData);
            destinationData.volumeTrigger = sourceData != null && sourceData.volumeTrigger != null ? sourceData.volumeTrigger : actor.View.transform;
        }
        private void DisableRender()
        { if (rendered != null) rendered.enabled = false; if (renderListener != null) renderListener.enabled = false; }
        public void DisableRendering() => DisableRender();
        private bool IsOwn(Collider collider) => collider != null && collider.transform.IsChildOf(transform);
        private float SafeDistance(Vector3 origin, Vector3 direction, float maximum, float radius)
        {
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, radius, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) return 0;
            for (int i = 0; i < overlapCount; i++) if (!IsOwn(overlaps[i])) return 0;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, maximum, collisionMask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return 0;
            float distance = maximum;
            for (int i = 0; i < count; i++) if (!IsOwn(hits[i].collider)) distance = Mathf.Min(distance, Mathf.Max(0, hits[i].distance - .035f));
            return distance;
        }
        public void FirstPersonPose(out Vector3 position, out Quaternion rotation)
        {
            float stand = actor.Settings.standingHeight - .16f, crouch = actor.Settings.crouchingHeight - .16f;
            float crouchBlend = 1 - Mathf.InverseLerp(crouch, stand, actor.View.transform.localPosition.y);
            Vector3 offset = Vector3.Lerp(standingEyeOffset, crouchingEyeOffset, crouchBlend);
            offset.z = PlayerViewRules.EyeForward(offset.z, capsule != null ? capsule.radius : .3f, .065f);
            Vector3 eye = actor.View.transform.position, adjusted = eye + transform.TransformDirection(offset);
            Vector3 toAdjusted = adjusted - eye; float length = toAdjusted.magnitude;
            if (length > .001f) adjusted = eye + toAdjusted / length * SafeDistance(eye, toAdjusted / length, length, .06f);
            position = adjusted; rotation = actor.View.transform.rotation;
        }
        private void LateUpdate()
        {
            if (actor.View == null || !actor.IsLocalOwner || CameraOverride || actor.Game != null && !actor.Vitals.Alive)
            { DisableRender(); return; }
            EnsureCamera();
            actor.View.enabled = false; if (eyeListener != null) eyeListener.enabled = false;
            FirstPersonPose(out Vector3 adjusted, out Quaternion rotation);
            effectiveFirstPerson = true;
            Vector3 position = adjusted;
            if (thirdPerson)
            {
                Vector3 pivot = adjusted;
                Vector3 extension = rotation * new Vector3(shoulderOffset, .10f, -thirdPersonDistance);
                float desired = extension.magnitude; Vector3 direction = extension / desired;
                float frustum = Mathf.Tan(rendered.fieldOfView * Mathf.Deg2Rad * .5f) * rendered.nearClipPlane * Mathf.Sqrt(1 + rendered.aspect * rendered.aspect) + .025f;
                float safe = SafeDistance(pivot, direction, desired, Mathf.Max(collisionRadius, frustum));
                float distance = boom.Sample(desired, safe, Time.unscaledDeltaTime);
                // A camera squeezed into the avatar would expose the neck again. Use
                // the natural eye view until enough space for the exterior view returns.
                if (distance > .48f)
                {
                    effectiveFirstPerson = false; position = pivot + direction * distance;
                    rotation = Quaternion.LookRotation(pivot + actor.View.transform.forward * 6 - position, Vector3.up);
                }
            }
            else boom.Reset();
            rendered.transform.SetPositionAndRotation(position, rotation);
            rendered.enabled = true; renderListener.enabled = true;
        }
        private void OnDisable()
        {
            DisableRender(); boom.Reset();
            if (actor != null && ReferenceEquals(actor.ViewRig, this))
            {
                actor.ViewRig = null;
                if (actor.View != null && actor.IsLocalOwner && actor.Vitals.Alive && !CameraOverride)
                { actor.View.enabled = true; if (eyeListener != null) eyeListener.enabled = true; }
            }
        }
        private void OnDestroy() { if (rendered != null) Destroy(rendered.gameObject); }
    }
}
