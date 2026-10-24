using UnityEngine;
using Silence.Player;

namespace Silence.Graphics.Players
{
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(680)]
    public sealed class CharacterIKController : MonoBehaviour
    {
        [SerializeField] private PlayerContext actor;
        [SerializeField] private PlayerAnimationProfile profile;
        private Animator animator;
        private PlayerAvatarState state;
        private PlayerAvatarPresentation presentation;
        private CharacterAnimationActions actions;
        private Transform left, right, chest;
        private Transform viewer;
        private readonly RaycastHit[] hits = new RaycastHit[12];
        private float pelvis;
        private Vector3 lean, previousVelocity;
        private readonly Plant leftPlant = new Plant(), rightPlant = new Plant();
        private sealed class Plant { public bool down; public Transform surface; public Vector3 anchor; }
        public void Configure(PlayerContext value, PlayerAnimationProfile settings) { actor = value; profile = settings; }
        private void Start()
        {
            animator = GetComponent<Animator>();
            if (actor == null || profile == null || !animator.isHuman || animator.avatar == null || !animator.avatar.isValid)
            { Debug.LogError("SILENCE: character IK requires a valid Humanoid Avatar and animation profile.", this); enabled = false; return; }
            state = actor.GetComponent<PlayerAvatarState>(); presentation = actor.GetComponent<PlayerAvatarPresentation>(); actions = GetComponent<CharacterAnimationActions>();
            left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            var camera = Camera.main; viewer = camera != null ? camera.transform : null;
        }
        private bool Ground(Transform foot, out RaycastHit closest)
        {
            closest = default; float nearest = float.PositiveInfinity;
            int count = Physics.RaycastNonAlloc(foot.position + Vector3.up * .32f, Vector3.down, hits, .65f, profile.groundMask, QueryTriggerInteraction.Ignore);
            // A full buffer may hide the real ground: fall back to the authored pose.
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!hits[i].collider.transform.IsChildOf(actor.transform) && hits[i].distance < nearest && Vector3.Angle(hits[i].normal, Vector3.up) < 55)
                { closest = hits[i]; nearest = hits[i].distance; }
            return nearest < float.PositiveInfinity;
        }
        private void OnAnimatorIK(int layer)
        {
            if (animator == null || !PlayerAnimatorIK.IsFinalPass(animator, layer)) return;
            // Clear last frame's goals before the interaction and flashlight owners write
            // their claims later in this same IK pass (execution order 700 / 750).
            foreach (var goal in Goals)
            { animator.SetIKPositionWeight(goal, 0); animator.SetIKRotationWeight(goal, 0); }
            animator.SetLookAtWeight(0);
            if (profile == null || state == null || !state.Ready || !actor.Vitals.Alive || Time.deltaTime <= 0) return;
            bool close = actor.IsLocalOwner || viewer == null || Vector3.SqrMagnitude(viewer.position - actor.transform.position) < profile.ikDistance * profile.ikDistance;
            bool grounded = close && state.Grounded && (actions == null || actions.AllowFeet);
            float leftWeight = grounded ? PlayerFootContacts.Read(animator, true) * profile.footIKWeight : 0;
            float rightWeight = grounded ? PlayerFootContacts.Read(animator, false) * profile.footIKWeight : 0;
            RaycastHit l = default, r = default;
            bool hitL = left != null && leftWeight > .01f && Ground(left, out l);
            bool hitR = right != null && rightWeight > .01f && Ground(right, out r);
            float targetPelvis = 0;
            if (hitL) targetPelvis = Mathf.Min(targetPelvis, (l.point.y + profile.soleThickness - left.position.y) * leftWeight);
            if (hitR) targetPelvis = Mathf.Min(targetPelvis, (r.point.y + profile.soleThickness - right.position.y) * rightWeight);
            pelvis = Mathf.Lerp(pelvis, Mathf.Clamp(targetPelvis, -profile.maximumPelvisOffset, profile.maximumPelvisOffset), 1 - Mathf.Exp(-14 * Time.deltaTime));
            if (grounded) animator.bodyPosition += Vector3.up * pelvis;
            Foot(AvatarIKGoal.LeftFoot, left, leftPlant, hitL, l, leftWeight);
            Foot(AvatarIKGoal.RightFoot, right, rightPlant, hitR, r, rightWeight);
            if (!close) return;
            Vector3 velocity = state.Velocity;
            Vector3 acceleration = (velocity - previousVelocity) / Mathf.Max(.01f, Time.deltaTime); previousVelocity = velocity;
            Vector3 localAcceleration = actor.transform.InverseTransformDirection(acceleration);
            Vector3 desiredLean = new Vector3(Mathf.Clamp(localAcceleration.z * .22f, -3, 4), 0, Mathf.Clamp(-localAcceleration.x * .25f, -4, 4));
            lean = Vector3.Lerp(lean, desiredLean, 1 - Mathf.Exp(-8 * Time.deltaTime));
            if (chest != null && (actions == null || actions.AllowFeet))
                animator.SetBoneLocalRotation(HumanBodyBones.Chest, chest.localRotation * Quaternion.Euler(lean));
            if (actor.View != null)
            {
                Vector3 target = actions != null && actions.HasLookTarget ? actions.LookTarget : actor.View.transform.position + actor.View.transform.forward * 4;
                Vector3 direction = target - animator.bodyPosition;
                // Clamp yaw/pitch relative to the art's delayed facing, rather than twisting the spine.
                Quaternion facing = Quaternion.Euler(0, presentation != null ? presentation.BodyYaw : actor.transform.eulerAngles.y, 0);
                Vector3 local = Quaternion.Inverse(facing) * direction;
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65, 65);
                float pitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -35, 35);
                animator.SetLookAtWeight(profile.headLookWeight, .12f, .65f, .35f, .65f);
                animator.SetLookAtPosition(animator.bodyPosition + facing * Quaternion.Euler(pitch, yaw, 0) * Vector3.forward * 4);
            }
        }
        private static readonly AvatarIKGoal[] Goals = { AvatarIKGoal.LeftFoot, AvatarIKGoal.RightFoot, AvatarIKGoal.LeftHand, AvatarIKGoal.RightHand };
        private void Foot(AvatarIKGoal goal, Transform bone, Plant plant, bool hit, RaycastHit ground, float weight)
        {
            float applied = hit ? weight : 0;
            animator.SetIKPositionWeight(goal, applied); animator.SetIKRotationWeight(goal, applied * .75f);
            if (!hit || weight < .3f) { plant.down = false; return; }
            Vector3 desired = ground.point + Vector3.up * profile.soleThickness;
            if (!plant.down && weight > .6f)
            { plant.down = true; plant.surface = ground.collider.transform; plant.anchor = plant.surface.InverseTransformPoint(desired); }
            if (plant.down && plant.surface != null)
            {
                Vector3 anchor = plant.surface.TransformPoint(plant.anchor);
                // Release a planted foot after a teleport, collider change or excessive reach.
                if (plant.surface == ground.collider.transform && Vector3.Distance(anchor, bone.position) < .28f)
                    desired = Vector3.Lerp(desired, anchor, Mathf.InverseLerp(.3f, .75f, weight));
                else plant.down = false;
            }
            Vector3 correction = Vector3.ClampMagnitude(desired - bone.position, profile.maximumPelvisOffset + .08f);
            animator.SetIKPosition(goal, bone.position + correction);
            // Imported foot bones need not use local Y as their sole normal. Mecanim's
            // current IK goal rotation carries the Avatar's bone-axis correction.
            Quaternion slope = Quaternion.FromToRotation(Vector3.up, ground.normal) * animator.GetIKRotation(goal);
            animator.SetIKRotation(goal, slope);
        }
        private void OnDisable() { leftPlant.down = rightPlant.down = false; pelvis = 0; previousVelocity = lean = Vector3.zero; }
    }
}
