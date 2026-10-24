using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    [DisallowMultipleComponent]
    public sealed class FlashlightAudioController : MonoBehaviour
    {
        private FlashlightVisuals view;
        private AudioSource source;
        private FlashlightState previousState;
        private double start=-1, previousTime;
        public void Configure(FlashlightVisuals value)
        {
            view=value;source=new GameObject("Flashlight Audio").AddComponent<AudioSource>();source.transform.SetParent(view.Art.transform,false);source.playOnAwake=false;source.spatialBlend=1;source.rolloffMode=AudioRolloffMode.Logarithmic;
            source.minDistance=.7f;source.maxDistance=18;source.dopplerLevel=0;source.volume=view.Profile.soundVolume;
            previousState=view.Controller.Frame.state;start=view.Controller.Frame.startedAt;previousTime=view.Controller.Clock;
        }
        private void Play(AudioClip clip) { if(clip!=null) source.PlayOneShot(clip); }
        private void Update()
        {
            if(view==null || source==null) return;
            var f=view.Controller.Frame;double now=view.Controller.Clock;
            var oldState=previousState;bool changed=f.state!=previousState || f.startedAt!=start;
            if(changed)
            {
                previousState=f.state;start=f.startedAt;
                // Late joiners enter the current pose without playing old historical sounds.
                previousTime=Mathf.Abs((float)(now-start))<.35f ? start-.001 : now;
                if(now-start<.35)
                {
                    if(f.state==FlashlightState.SwitchingOn || f.state==FlashlightState.SwitchingOff)Play(view.Profile.buttonClick);
                    if(f.state==FlashlightState.Equipping)Play(view.Profile.equip);
                    if(f.state==FlashlightState.Holstering)Play(view.Profile.holster);
                }
            }
            void Cue(float fraction,AudioClip clip)
            { double at=start+f.duration*fraction;if(previousTime<at && now>=at)Play(clip); }
            if(f.state==FlashlightState.FailureReaction)
            { Cue(.22f,view.Profile.buttonClick);Cue(.32f,view.Profile.buttonClick);Cue(.51f,view.Profile.palmTap);Cue(.65f,view.Profile.palmTap);Cue(.79f,view.Profile.palmTap); }
            if(f.state==FlashlightState.ReloadingBatteries)
            { Cue(.14f,view.Profile.unscrewCap);Cue(.38f,view.Profile.removeCells);Cue(.67f,view.Profile.insertCells);Cue(.85f,view.Profile.screwCap); }
            if(changed && f.state==FlashlightState.Ready && f.beam && oldState==FlashlightState.ReloadingBatteries)Play(view.Profile.buttonClick);
            previousTime=now;

        }
        private void OnDisable() { if(source!=null)source.Stop(); }
        private void OnDestroy() { if(source!=null)Destroy(source.gameObject); }
    }
}
