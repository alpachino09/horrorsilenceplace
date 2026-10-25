using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Silence.Core;
using Silence.Player;

namespace Silence.Graphics.Players
{
    // Review-scene diagnostics only. Never applies damage, IK, input or network commands.
    [DefaultExecutionOrder(950)]
    public sealed class CharacterReviewProbe : MonoBehaviour
    {
        public PlayerContext actor;
        public CharacterReviewSession Session { get; private set; }=new CharacterReviewSession();
        private Animator animator;
        private PlayerAvatarState state;
        private CharacterArmPoseSafety safety;
        private FlashlightVisuals torch;
        private Transform hips,chest,leftShoulder,rightShoulder,leftElbow,rightElbow,leftHand,rightHand,leftFoot,rightFoot;
        private Vector3 previousLeft,previousRight;
        private float[] lengths;
        private bool sampled;
        private int previousUnresolved;
        private readonly RaycastHit[] hits=new RaycastHit[12];
        private readonly Foot leftPlant=new Foot(),rightPlant=new Foot();
        private readonly List<string> materialIssues=new List<string>();
        private sealed class Foot { public Transform surface;public Vector3 point;public bool planted; }
        public string Note { get; private set; }
        private void Start()
        {
            if(actor==null){enabled=false;return;}
            var view=actor.GetComponent<PlayerAvatarPresentation>();animator=view!=null?view.Model:null;
            if(animator==null || !animator.isHuman){enabled=false;return;}
            state=actor.GetComponent<PlayerAvatarState>();safety=animator.GetComponent<CharacterArmPoseSafety>();torch=actor.GetComponent<FlashlightVisuals>();
            hips=animator.GetBoneTransform(HumanBodyBones.Hips);chest=animator.GetBoneTransform(HumanBodyBones.Chest);
            leftShoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);rightShoulder=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftElbow=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);rightElbow=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if(hips==null || chest==null || leftShoulder==null || rightShoulder==null || leftElbow==null || rightElbow==null || leftHand==null || rightHand==null){enabled=false;return;}
            lengths=Lengths();
            var shared=new List<Material>();
            foreach(var renderer in animator.GetComponentsInChildren<Renderer>(true))
            {
                shared.Clear();renderer.GetSharedMaterials(shared);
                foreach(var material in shared)
                {
                    if(material==null){materialIssues.Add("Missing material: "+renderer.name);continue;}
                    if(CharacterSurfaceRules.Classify(renderer.name,material.name)!=CharacterSurfaceRole.Solid)continue;
                    bool opaque=material.HasProperty("_Surface") && material.GetFloat("_Surface")==0 && material.HasProperty("_ZWrite") && material.GetFloat("_ZWrite")==1;
                    opaque&=!material.HasProperty("_BaseColor") || material.GetColor("_BaseColor").a>=.999f;
                    if(!opaque || material.renderQueue>=3000)materialIssues.Add("Opaque/depth review: "+renderer.name+" / "+material.name);
                }
            }
        }
        private float[] Lengths()=>new[]{Vector3.Distance(leftShoulder.position,leftElbow.position),Vector3.Distance(leftElbow.position,leftHand.position),Vector3.Distance(rightShoulder.position,rightElbow.position),Vector3.Distance(rightElbow.position,rightHand.position)};
        public void Begin(int index)
        {
            bool flash=index>=6 && index<=9,remote=index==13;
            bool available=!remote && (!flash || torch!=null && torch.Art!=null);
            string reason=remote?"Эта площадка offline. Нужны Host/Client и камера реального другого игрока.":!available?"Нет установленной калиброванной 3D-модели фонарика; источник света не заменяет проверку хвата.":"";
            Session.Begin(index,available,reason);sampled=false;leftPlant.planted=rightPlant.planted=false;previousUnresolved=safety!=null?safety.UnresolvedContacts:0;Note=reason;
        }
        private float Slip(Transform foot,Foot plant,bool left,float dt)
        {
            if(foot==null || state==null || !state.Grounded || PlayerFootContacts.Read(animator,left)<.7f){plant.planted=false;return 0;}
            int count=Physics.RaycastNonAlloc(foot.position+Vector3.up*.2f,Vector3.down,hits,.45f,~0,QueryTriggerInteraction.Ignore);
            Transform surface=null;float nearest=float.PositiveInfinity;
            if(count<hits.Length)for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(actor.transform) && hits[i].distance<nearest){nearest=hits[i].distance;surface=hits[i].collider.transform;}
            if(surface==null){plant.planted=false;return 0;}
            float speed=plant.planted && plant.surface==surface?Vector3.Distance(foot.position,surface.TransformPoint(plant.point))/dt:0;
            plant.surface=surface;plant.point=surface.InverseTransformPoint(foot.position);plant.planted=true;return speed;
        }
        private void LateUpdate()
        {
            if(Session.active<0 || animator==null || lengths==null || Time.deltaTime<=0)return;
            float dt=Time.deltaTime;var l=leftHand.position;var r=rightHand.position;
            float step=sampled?Mathf.Max(Vector3.Distance(previousLeft,l),Vector3.Distance(previousRight,r)):0;previousLeft=l;previousRight=r;sampled=true;
            Vector3 right=(rightShoulder.position-leftShoulder.position).normalized;
            float Angle(Transform shoulder,Transform elbow)
            {var d=(elbow.position-shoulder.position).normalized;float lateral=Mathf.Abs(Vector3.Dot(d,right));return Mathf.Asin(Mathf.Clamp01(lateral))*Mathf.Rad2Deg;}
            float error=0;
            // No temporary length array in the per-frame measurement path.
            float Check(float actual,float baseline)=>baseline>.001f?Mathf.Abs(actual-baseline)/baseline:0;
            error=Mathf.Max(Check(Vector3.Distance(leftShoulder.position,leftElbow.position),lengths[0]),Check(Vector3.Distance(leftElbow.position,leftHand.position),lengths[1]));
            error=Mathf.Max(error,Mathf.Max(Check(Vector3.Distance(rightShoulder.position,rightElbow.position),lengths[2]),Check(Vector3.Distance(rightElbow.position,rightHand.position),lengths[3])));
            float slip=Mathf.Max(Slip(leftFoot,leftPlant,true,dt),Slip(rightFoot,rightPlant,false,dt));int current=safety!=null?safety.UnresolvedContacts:0;
            Session.cases[Session.active].measurements.Add(Time.unscaledDeltaTime,step,slip,Mathf.Max(Angle(leftShoulder,leftElbow),Angle(rightShoulder,rightElbow)),error,current-previousUnresolved);previousUnresolved=current;
        }
        [Serializable] private sealed class Export
        {
            public string version="0.6.0",utc,unity,scene;
            public bool nativeUserRun=true,cloudVisualAcceptance=false,triangleIntersectionTest=false;
            public bool calibratedFlashlightPresent;
            public int calibratedFingerBones;
            public string[] initialMaterialIssues;
            public CharacterReviewSession session;
        }
        public string Json()=>JsonUtility.ToJson(new Export{utc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,scene=gameObject.scene.path,calibratedFlashlightPresent=torch!=null && torch.Art!=null,
            calibratedFingerBones=torch!=null && torch.AnimationController!=null?torch.AnimationController.CalibratedFingerCount:0,initialMaterialIssues=materialIssues.ToArray(),session=Session},true);
        public string Save()
        {
            string folder=Application.isEditor?"Library/Silence":Path.Combine(Application.persistentDataPath,"Silence");Directory.CreateDirectory(folder);
            string path=Path.Combine(folder,"CharacterReviewLatest.json");File.WriteAllText(path,Json());Note="Отчёт: "+Path.GetFullPath(path);return path;
        }
        private void OnDisable()=>Session.Finish();
    }
}
