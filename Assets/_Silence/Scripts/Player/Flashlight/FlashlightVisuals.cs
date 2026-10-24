using System.Collections.Generic;
using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    [DisallowMultipleComponent, RequireComponent(typeof(FlashlightController)), DefaultExecutionOrder(850)]
    public sealed class FlashlightVisuals : MonoBehaviour
    {
        [SerializeField] private FlashlightProfile profile;
        [SerializeField] private Animator bodyAnimator;
        private PlayerContext actor;
        private FlashlightController controller;
        private FlashlightArtRig art;
        private Light beam;
        private FlashlightAnimationController animationController;
        private FlashlightAudioController audioController;
        private readonly List<Material> lensMaterials = new List<Material>();
        public FlashlightProfile Profile => profile;
        public FlashlightArtRig Art => art;
        public FlashlightController Controller => controller;
        public Animator BodyAnimator => bodyAnimator;
        public PlayerContext Actor => actor;
        public Vector3 GripPosition { get; private set; }
        public Quaternion GripRotation { get; private set; }
        public void Configure(FlashlightProfile value,Animator animator) { profile=value;bodyAnimator=animator; }
        private void Start()
        {
            actor=GetComponent<PlayerContext>();controller=GetComponent<FlashlightController>();
            if(profile==null || profile.modelPrefab==null || bodyAnimator==null || !bodyAnimator.isHuman)
            { Debug.LogError("SILENCE: flashlight needs the real imported art profile and Humanoid Animator.",this);enabled=false;return; }
            Transform hand=bodyAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            if(hand==null) { enabled=false;return; }
            var instance=Instantiate(profile.modelPrefab,hand,false);instance.name="Tactical Flashlight";art=instance.GetComponent<FlashlightArtRig>();
            if(art==null || art.rightWristGrip==null || art.lensEmitter==null || art.leftPalmTap==null || art.leftWristCap==null || art.batterySocket==null || art.cap==null || art.oldCells==null || art.newCells==null)
            { Destroy(instance);Debug.LogError("SILENCE: missing calibrated flashlight markers.",this);enabled=false;return; }
            Matrix4x4 grip=art.transform.worldToLocalMatrix*art.rightWristGrip.localToWorldMatrix;
            GripPosition=grip.GetColumn(3);GripRotation=grip.rotation;
            Quaternion inverse=Quaternion.Inverse(GripRotation);art.transform.localRotation=inverse;art.transform.localPosition=-(inverse*GripPosition);
            beam=new GameObject("Lens Spot Light").AddComponent<Light>();beam.transform.SetParent(art.lensEmitter,false);
            beam.type=LightType.Spot;beam.range=profile.range;beam.spotAngle=profile.outerAngle;beam.innerSpotAngle=profile.outerAngle*profile.innerRatio;
            beam.color=profile.lightColor;beam.cookie=profile.cookie;beam.shadows=LightShadows.Soft;beam.shadowCustomResolution=512;beam.shadowNearPlane=.02f;beam.enabled=false;
            foreach(var renderer in art.emissiveLens ?? System.Array.Empty<Renderer>())
                if(renderer!=null)foreach(var material in renderer.materials) { material.EnableKeyword("_EMISSION");lensMaterials.Add(material); }
            animationController=bodyAnimator.gameObject.AddComponent<FlashlightAnimationController>();animationController.Configure(this);
            audioController=gameObject.AddComponent<FlashlightAudioController>();audioController.Configure(this);
        }
        private void LateUpdate()
        {
            if(art==null || controller==null) return;
            FlashlightFrame frame=controller.Frame;bool visible=controller.HasState && frame.state!=FlashlightState.Holstered;
            if(art.gameObject.activeSelf!=visible)art.gameObject.SetActive(visible);
            float intensity=frame.beam && visible ? FlashlightFlicker.Sample(frame.charge,controller.Clock,unchecked((uint)actor.SourceId+19417u),profile.lowChargeThreshold,profile.criticalChargeThreshold) : 0;
            beam.enabled=intensity>0;beam.intensity=profile.intensity*intensity;
            foreach(var material in lensMaterials)if(material!=null && material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",profile.lensEmission*intensity);
        }
        private void OnEnable() { if(animationController!=null)animationController.enabled=true;if(audioController!=null)audioController.enabled=true; }
        private void OnDisable() { if(beam!=null)beam.enabled=false;if(art!=null)art.gameObject.SetActive(false);if(animationController!=null)animationController.enabled=false;if(audioController!=null)audioController.enabled=false; }
        private void OnDestroy()
        { foreach(var material in lensMaterials)if(material!=null)Destroy(material);if(animationController!=null)Destroy(animationController);if(audioController!=null)Destroy(audioController);if(art!=null)Destroy(art.gameObject); }
    }
}
