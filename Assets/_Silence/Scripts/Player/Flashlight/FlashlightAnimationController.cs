using System;
using System.Collections.Generic;
using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    // Humanoid IK plus authored phase curves. Contact markers must be calibrated against the real asset.
    [DisallowMultipleComponent, DefaultExecutionOrder(750)]
    public sealed class FlashlightAnimationController : MonoBehaviour
    {
        private FlashlightVisuals view;
        private Animator animator;
        private FirstPersonMotor motor;
        private Quaternion aim;
        private Vector3 rightPosition;
        private Quaternion rightRotation;
        private bool hasRightGoal;
        private float weight;
        private ILeftHandIKClaim otherHandIK;
        private IArmPoseConstraints armConstraints;
        private ICharacterIKCoordinator coordinator;
        private Vector3 tapLocal,capLocal,socketLocal;
        private Quaternion tapRotation,capRotation,socketRotation;
        private LocalRest cap,oldCells,newCells;
        private readonly List<Finger> fingers=new List<Finger>();
        private sealed class Finger { public Transform bone;public Quaternion basis;public bool applied,left,thumb;public float factor; }
        private sealed class LocalRest
        {
            public Transform value,parent;public Vector3 position;public Quaternion rotation;public bool active;
            public LocalRest(Transform t) { value=t;if(t!=null){parent=t.parent;position=t.localPosition;rotation=t.localRotation;active=t.gameObject.activeSelf;} }
            public void Restore() { if(value!=null){value.SetParent(parent,false);value.localPosition=position;value.localRotation=rotation;value.gameObject.SetActive(active);} }
        }
        public void Configure(FlashlightVisuals value)
        {
            view=value;animator=GetComponent<Animator>();motor=view.Actor.GetComponent<FirstPersonMotor>();aim=view.Actor.View.transform.rotation;
            otherHandIK=GetComponent<ILeftHandIKClaim>();
            armConstraints=GetComponent<IArmPoseConstraints>();
            coordinator=GetComponent<ICharacterIKCoordinator>();
            var art=view.Art;
            void Marker(Transform t,out Vector3 pos,out Quaternion rot)
            { Matrix4x4 m=art.transform.worldToLocalMatrix*t.localToWorldMatrix;pos=m.GetColumn(3);rot=m.rotation; }
            Marker(art.leftPalmTap,out tapLocal,out tapRotation);Marker(art.leftWristCap,out capLocal,out capRotation);Marker(art.batterySocket,out socketLocal,out socketRotation);
            cap=new LocalRest(art.cap);oldCells=new LocalRest(art.oldCells);newCells=new LocalRest(art.newCells);
            foreach(HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                string name=bone.ToString();bool finger=name.Contains("Thumb")||name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little");
                if(!finger || bone==HumanBodyBones.LastBone)continue;
                var t=animator.GetBoneTransform(bone);if(t!=null)fingers.Add(new Finger { bone=t,left=name.StartsWith("Left",StringComparison.Ordinal),thumb=name.Contains("Thumb"),factor=name.Contains("Intermediate")?1.2f:name.Contains("Distal")?.7f:1 });
            }
        }
        private static float Smooth(float a,float b,float t) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        private static float Tap(float t) { float Pulse(float at) => Mathf.Exp(-Mathf.Pow((t-at)/.022f,2));return Mathf.Max(Pulse(.51f),Mathf.Max(Pulse(.65f),Pulse(.79f))); }
        private void RestoreFingers() { foreach(var f in fingers)if(f.applied && f.bone!=null){f.bone.localRotation=f.basis;f.applied=false;} }
        private void Update()
        {
            RestoreFingers();if(view==null || view.Art==null)return;
            var frame=view.Controller.Frame;float phase=frame.duration>0?Mathf.Clamp01((float)((view.Controller.Clock-frame.startedAt)/frame.duration)):1;
            float targetWeight=!view.Controller.HasState || frame.state==FlashlightState.Holstered?0:1;
            weight=Mathf.MoveTowards(weight,targetWeight,Time.deltaTime*6);
            Transform camera=view.Actor.View.transform;float follow=1-Mathf.Exp(-18*Time.deltaTime);aim=Quaternion.Slerp(aim,camera.rotation,follow);
            float movement=motor!=null && motor.IsSprinting?1:0;
            Vector3 position=view.Profile.readyPosition+new Vector3(Mathf.Sin(Time.time*1.3f)*.0015f,Mathf.Sin(Time.time*1.7f)*.002f,0);
            position+=new Vector3(Mathf.Sin(Time.time*(movement>0?9:5))*.004f,-movement*.065f+Mathf.Cos(Time.time*10)*movement*.006f,-movement*.035f);
            Vector3 euler=view.Profile.aimEuler;
            if(frame.state==FlashlightState.Equipping || frame.state==FlashlightState.Holstering)
            {
                float raised=frame.state==FlashlightState.Equipping?Smooth(0,1,phase):1-Smooth(0,1,phase);
                position=Vector3.Lerp(new Vector3(.19f,-.58f,.10f),position,raised);euler+=new Vector3(50,15,-20)*(1-raised);
            }
            if(frame.state==FlashlightState.FailureReaction)
            {
                float lift=Smooth(0,.18f,phase)*(1-Smooth(.85f,1,phase));position+=new Vector3(-.04f,.105f,-.06f)*lift;
                euler+=new Vector3(18,-18,-12)*lift;position.z+=Tap(phase)*.006f;
            }
            if(frame.state==FlashlightState.ReloadingBatteries)
            {
                float lift=Smooth(0,.14f,phase)*(1-Smooth(.90f,1,phase));position+=new Vector3(-.035f,.095f,-.075f)*lift;euler+=new Vector3(12,-28,-15)*lift;
            }
            Vector3 target=camera.TransformPoint(position);Quaternion orientation=aim*Quaternion.Euler(euler);
            float blend=1-Mathf.Exp(-20*Time.deltaTime);
            rightPosition=hasRightGoal?Vector3.Lerp(rightPosition,target,blend):target;
            rightRotation=hasRightGoal?Quaternion.Slerp(rightRotation,orientation,blend):orientation;hasRightGoal=true;
        }
        private void OnAnimatorIK(int layer)
        { if(coordinator==null || !coordinator.CoordinatesIK)ApplyAnimatorIK(layer); }
        public void ApplyAnimatorIK(int layer)
        {
            if(view==null || view.Art==null || animator==null || !hasRightGoal || !PlayerAnimatorIK.IsFinalPass(animator,layer))return;
            var frame=view.Controller.Frame;float phase=frame.duration>0?Mathf.Clamp01((float)((view.Controller.Clock-frame.startedAt)/frame.duration)):1;
            Vector3 wristTarget=rightPosition+rightRotation*view.GripPosition;
            if(armConstraints!=null && armConstraints.ConstrainArm(false,wristTarget,out Vector3 safeWrist,out Vector3 rightPole))
            {
                rightPosition+=safeWrist-wristTarget;
                animator.SetIKHintPosition(AvatarIKHint.RightElbow,rightPole);animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow,weight*.85f);
            }
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);animator.SetIKRotationWeight(AvatarIKGoal.RightHand,weight);
            animator.SetIKPosition(AvatarIKGoal.RightHand,rightPosition+rightRotation*view.GripPosition);animator.SetIKRotation(AvatarIKGoal.RightHand,rightRotation*view.GripRotation);
            Vector3 left=Vector3.zero;Quaternion rotation=Quaternion.identity;float leftWeight=0;
            if(frame.state==FlashlightState.FailureReaction)
            {
                leftWeight=Smooth(.30f,.43f,phase)*(1-Smooth(.83f,.96f,phase));rotation=rightRotation*tapRotation;
                Vector3 contact=rightPosition+rightRotation*tapLocal+rotation*Vector3.forward*(.07f*(1-Tap(phase))+.002f);
                left=contact-rotation*view.Profile.leftPalmWristToContact;
            }
            if(frame.state==FlashlightState.ReloadingBatteries)
            {
                leftWeight=Smooth(.06f,.14f,phase)*(1-Smooth(.91f,1,phase));rotation=rightRotation*capRotation;
                Vector3 capPoint=rightPosition+rightRotation*capLocal;
                Vector3 pouch=view.Actor.transform.TransformPoint(new Vector3(-.17f,.87f,.04f));
                Vector3 insert=rightPosition+rightRotation*socketLocal;
                left=phase<.34f?capPoint:phase<.52f?Vector3.Lerp(capPoint,pouch,Smooth(.34f,.49f,phase)):phase<.72f?Vector3.Lerp(pouch,insert,Smooth(.52f,.67f,phase)):Vector3.Lerp(insert,capPoint,Smooth(.72f,.83f,phase));
                if(phase>.12f && phase<.28f)rotation*=Quaternion.AngleAxis(-FlashlightHandMotion.WristTurn(Mathf.InverseLerp(.12f,.28f,phase)),Vector3.forward);
                if(phase>.83f)rotation*=Quaternion.AngleAxis(FlashlightHandMotion.WristTurn(Mathf.InverseLerp(.83f,.95f,phase)),Vector3.forward);
            }
            if(leftWeight>0 || otherHandIK==null || !otherHandIK.OwnsLeftHand)
            { animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,leftWeight*weight);animator.SetIKRotationWeight(AvatarIKGoal.LeftHand,leftWeight*weight); }
            if(leftWeight>0){animator.SetIKPosition(AvatarIKGoal.LeftHand,left);animator.SetIKRotation(AvatarIKGoal.LeftHand,rotation);}
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow,leftWeight*weight*.5f);animator.SetIKHintPosition(AvatarIKHint.LeftElbow,view.Actor.View.transform.TransformPoint(new Vector3(-.42f,-.33f,.13f)));
        }
        private void LateUpdate()
        {
            if(view==null || view.Art==null)return;
            var frame=view.Controller.Frame;float phase=frame.duration>0?Mathf.Clamp01((float)((view.Controller.Clock-frame.startedAt)/frame.duration)):1;
            foreach(var f in fingers)
            {
                if(f.bone==null)continue;float curl=f.left?(frame.state==FlashlightState.ReloadingBatteries?.42f:0):view.Profile.fingerGrip;
                if(f.thumb && !f.left && frame.state==FlashlightState.FailureReaction)curl+=.20f*(Mathf.Exp(-Mathf.Pow((phase-.22f)/.025f,2))+Mathf.Exp(-Mathf.Pow((phase-.32f)/.025f,2)));
                f.basis=f.bone.localRotation;f.bone.localRotation=f.basis*Quaternion.Euler(60*curl*f.factor*weight,0,0);f.applied=true;
            }
            cap.Restore();oldCells.Restore();newCells.Restore();
            if(frame.state!=FlashlightState.ReloadingBatteries)return;
            var art=view.Art;Vector3 axis=art.capLocalAxis.normalized;
            float opened=Smooth(.22f,.34f,phase)*(1-Smooth(.76f,.85f,phase));
            cap.value.localPosition=cap.position+axis*(art.capTravel*opened);
            cap.value.localRotation=cap.rotation*Quaternion.AngleAxis(-720*(Smooth(.12f,.28f,phase)-Smooth(.83f,.95f,phase)),axis);
            if(phase>.34f && phase<.76f)
            {
                var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);cap.value.position=hand.TransformPoint(new Vector3(.015f,.015f,0));cap.value.rotation=hand.rotation*cap.rotation;
            }
            oldCells.value.gameObject.SetActive(phase<.46f);oldCells.value.localPosition=oldCells.position+axis*(art.capTravel*2*Smooth(.34f,.45f,phase));
            newCells.value.gameObject.SetActive(phase>=.50f && phase<.72f);
            if(phase>=.50f)
            {
                Vector3 pouch=view.Actor.transform.TransformPoint(new Vector3(-.17f,.87f,.04f));
                newCells.value.position=Vector3.Lerp(pouch,art.batterySocket.position,Smooth(.52f,.67f,phase));newCells.value.rotation=art.batterySocket.rotation;
            }
        }
        private void OnDisable() { RestoreFingers();cap?.Restore();oldCells?.Restore();newCells?.Restore(); }
    }
}
