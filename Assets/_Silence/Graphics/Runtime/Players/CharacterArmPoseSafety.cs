using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Final constraint after actions (700) and flashlight (750). Does not move gameplay targets.
    [DisallowMultipleComponent, RequireComponent(typeof(Animator)), DefaultExecutionOrder(780)]
    public sealed class CharacterArmPoseSafety : MonoBehaviour, IArmPoseConstraints, ICharacterIKCoordinator
    {
        [SerializeField] private PlayerContext actor;
        [SerializeField, Range(.08f, .30f)] private float torsoWidth = .18f;
        [SerializeField, Range(.07f, .25f)] private float torsoDepth = .13f;
        [SerializeField, Range(.02f, .08f)] private float palmRadius = .045f;
        private Animator animator;
        private PlayerAvatarPresentation presentation;
        private PlayerAvatarState state;
        private CharacterAnimationActions actions;
        private CharacterIKController feet;
        private FlashlightVisuals flashlight;
        private Transform hips, chest, leftHand, rightHand, leftShoulder, rightShoulder, leftElbow, rightElbow;
        private Transform leftThigh, rightThigh, leftKnee, rightKnee, leftFoot, rightFoot, head;
        private readonly BodyCapsule[] limbVolumes=new BodyCapsule[5];
        private Vector3 leftCorrection, rightCorrection;
        private float leftRelease, rightRelease, leftRearWeight, rightRearWeight;
        public int UnresolvedContacts { get; private set; }
        public bool CoordinatesIK => isActiveAndEnabled && animator!=null && animator.isHuman;
        public Vector2 TorsoDimensions => new Vector2(torsoWidth, torsoDepth);
        public void Configure(PlayerContext value, bool resetGoals)
        {
            actor = value;
            if (resetGoals && GetComponent<CharacterArmIKReset>() == null) gameObject.AddComponent<CharacterArmIKReset>();
        }
        public void ConfigureEnvelope(float width, float depth)
        { torsoWidth = Mathf.Clamp(width, .08f, .30f); torsoDepth = Mathf.Clamp(depth, .07f, .25f); }
        private void Start()
        {
            animator = GetComponent<Animator>();
            if (actor == null || !animator.isHuman) { enabled = false; return; }
            presentation = actor.GetComponent<PlayerAvatarPresentation>(); state = actor.GetComponent<PlayerAvatarState>();
            actions = GetComponent<CharacterAnimationActions>();
            feet=GetComponent<CharacterIKController>();flashlight=actor.GetComponent<FlashlightVisuals>();
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand); rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); rightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftThigh=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);rightThigh=animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            leftKnee=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);rightKnee=animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);head=animator.GetBoneTransform(HumanBodyBones.Head);
        }
        private bool Available => actor != null && actor.Vitals.Alive && actor.CarriedBy == null && hips != null && chest != null &&
            (actions == null || actions.AllowFeet);
        private Quaternion TorsoBasis()
        {
            Vector3 up = (chest.position-hips.position).normalized;
            Vector3 forward = Quaternion.Euler(0, presentation != null ? presentation.BodyYaw : actor.transform.eulerAngles.y, 0)*Vector3.forward;
            forward = Vector3.ProjectOnPlane(forward, up);
            return up.sqrMagnitude > .5f && forward.sqrMagnitude > .01f ? Quaternion.LookRotation(forward, up) : actor.transform.rotation;
        }
        private static RigVector Rig(Vector3 v) => new RigVector(v.x, v.y, v.z);
        private static Vector3 Unity(RigVector v) => new Vector3(v.X, v.Y, v.Z);
        public bool ConstrainArm(bool left, Vector3 requested, out Vector3 hand, out Vector3 elbow)
        {
            hand = requested; elbow = requested;
            if (!isActiveAndEnabled || !Available) return false;
            Transform shoulder = left ? leftShoulder : rightShoulder, mid = left ? leftElbow : rightElbow, wrist = left ? leftHand : rightHand;
            if (shoulder == null || mid == null || wrist == null) return false;
            Quaternion basis = TorsoBasis(), inverse = Quaternion.Inverse(basis); Vector3 origin = hips.position;
            Vector3 shoulderLocal = inverse*(shoulder.position-origin), midLocal = inverse*(mid.position-origin);
            float scale = Mathf.Abs(actor.transform.lossyScale.x);
            BodyCapsule Volume(Transform a,Transform b,float radius)=>a!=null && b!=null?new BodyCapsule(Rig(inverse*(a.position-origin)),Rig(inverse*(b.position-origin)),radius*scale):default;
            limbVolumes[0]=Volume(leftThigh,leftKnee,.09f);limbVolumes[1]=Volume(rightThigh,rightKnee,.09f);
            limbVolumes[2]=Volume(leftKnee,leftFoot,.06f);limbVolumes[3]=Volume(rightKnee,rightFoot,.06f);
            limbVolumes[4]=head!=null?new BodyCapsule(Rig(inverse*(head.position-origin)),Rig(inverse*(head.position-origin))+new RigVector(0,.08f*scale,0),.09f*scale):default;
            var torso = new TorsoEnvelope(.025f*scale, (chest.position-origin).magnitude-.055f*scale, torsoWidth*scale, torsoDepth*scale, .10f*scale);
            var solution = ArmAnatomyModel.Solve(Rig(shoulderLocal), Rig(midLocal), Rig(inverse*(requested-origin)),
                Vector3.Distance(shoulder.position, mid.position), Vector3.Distance(mid.position, wrist.position), shoulderLocal.x, torso, palmRadius*scale, .035f*scale, limbVolumes);
            hand = origin+basis*Unity(solution.Hand); elbow = origin+basis*Unity(solution.Elbow);
            if (solution.RemainingContact) UnresolvedContacts++;
            return solution.Corrected;
        }
        private void OnAnimatorIK(int layer)
        {
            if (!CoordinatesIK) return;
            if(!PlayerAnimatorIK.IsFinalPass(animator,layer))
            {
                // Earlier IK-enabled layers must not solve last frame's goals
                // before the final layer contributes this frame's actual pose.
                foreach(var goal in Goals){animator.SetIKPositionWeight(goal,0);animator.SetIKRotationWeight(goal,0);}
                animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,0);animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);animator.SetLookAtWeight(0);return;
            }
            // Suppressed contributors delegate to this single callback. Correctness
            // does not depend on Unity's ordering of separate OnAnimatorIK messages.
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,0);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,0);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,0);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,0);
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,0);animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,0);
            if(feet!=null && feet.isActiveAndEnabled)feet.ApplyAnimatorIK(layer);
            if(!actor.Vitals.Alive)return;
            if(actions!=null && actions.isActiveAndEnabled)actions.ApplyAnimatorIK(layer);
            var torch=flashlight!=null?flashlight.AnimationController:null;
            if(torch!=null && torch.isActiveAndEnabled)torch.ApplyAnimatorIK(layer);
            if(!Available)return;
            Apply(true, leftHand, ref leftCorrection, ref leftRelease, ref leftRearWeight);
            Apply(false, rightHand, ref rightCorrection, ref rightRelease, ref rightRearWeight);
        }
        private void Apply(bool left, Transform wrist, ref Vector3 correction, ref float release, ref float rearWeight)
        {
            if (wrist == null) return;
            AvatarIKGoal goal = left ? AvatarIKGoal.LeftHand : AvatarIKGoal.RightHand;
            float originalWeight = animator.GetIKPositionWeight(goal);
            Vector3 requested = Vector3.Lerp(wrist.position, animator.GetIKPosition(goal), originalWeight);
            Quaternion basis = TorsoBasis();
            Transform shoulder=left?leftShoulder:rightShoulder,mid=left?leftElbow:rightElbow;
            if(shoulder==null || mid==null)return;
            Vector3 local=Quaternion.Inverse(basis)*(wrist.position-hips.position);
            float side=Mathf.Sign(Vector3.Dot(shoulder.position-hips.position,basis*Vector3.right));
            bool bothBehind=leftHand!=null && rightHand!=null && Vector3.Dot(leftHand.position-hips.position,basis*Vector3.forward)<-.08f && Vector3.Dot(rightHand.position-hips.position,basis*Vector3.forward)<-.08f;
            float speed=state!=null && state.HasVelocity?state.Velocity.magnitude:0;
            bool rear=originalWeight<.001f && PlayerViewRules.BadArmPose(local.x*side,local.z,speed,bothBehind);
            rearWeight=Mathf.MoveTowards(rearWeight,rear?1:0,Time.deltaTime*8);
            if(rearWeight>0 && originalWeight<.001f)
            {
                float reach=Vector3.Distance(shoulder.position,mid.position)+Vector3.Distance(mid.position,wrist.position);
                Vector3 relaxed=shoulder.position+basis*new Vector3(side*.06f,-reach*.88f,.09f);
                requested=Vector3.Lerp(requested,relaxed,rearWeight);
            }
            bool constrained = ConstrainArm(left, requested, out Vector3 safe, out Vector3 pole);
            constrained|=rearWeight>0 && originalWeight<.001f;
            if (constrained) { correction = Quaternion.Inverse(basis)*(safe-requested); release = 1; }
            else { correction *= Mathf.Exp(-14*Time.deltaTime); release = Mathf.MoveTowards(release, 0, Time.deltaTime*8); }
            if (release <= .001f) return;
            Vector3 target = constrained ? safe : requested+basis*correction;
            if (!constrained) ConstrainArm(left, target, out target, out pole);
            animator.SetIKPosition(goal, target); animator.SetIKPositionWeight(goal, 1);
            AvatarIKHint hint = left ? AvatarIKHint.LeftElbow : AvatarIKHint.RightElbow;
            animator.SetIKHintPosition(hint, pole); animator.SetIKHintPositionWeight(hint, release*.85f);
        }
        private void OnDisable() { leftCorrection = rightCorrection = Vector3.zero; leftRelease = rightRelease = leftRearWeight = rightRearWeight = 0; }
        private static readonly AvatarIKGoal[] Goals={AvatarIKGoal.LeftFoot,AvatarIKGoal.RightFoot,AvatarIKGoal.LeftHand,AvatarIKGoal.RightHand};
    }
}
