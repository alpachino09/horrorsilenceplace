using System;

namespace Silence.Core
{
    public enum CharacterReviewStatus { NotRun,Recording,NeedsVisualReview,Passed,Failed,Unavailable }
    [Serializable] public sealed class CharacterReviewStats
    {
        public int samples,invalidSamples,unresolvedConstraintCalls;
        public float seconds,maxFrameSeconds,maxHandStep,maxFootSlip,maxArmSpread,maxBoneLengthError;
        public void Add(float dt,float handStep,float footSlip,float armSpread,float boneError,int unresolved)
        {
            bool Finite(float x)=>!float.IsNaN(x) && !float.IsInfinity(x);
            if(!Finite(dt) || !Finite(handStep) || !Finite(footSlip) || !Finite(armSpread) || !Finite(boneError) || dt<=0 || handStep<0 || footSlip<0 || boneError<0)
            {invalidSamples++;return;}
            samples++;seconds+=dt;maxFrameSeconds=Math.Max(maxFrameSeconds,dt);maxHandStep=Math.Max(maxHandStep,handStep);
            maxFootSlip=Math.Max(maxFootSlip,footSlip);maxArmSpread=Math.Max(maxArmSpread,armSpread);maxBoneLengthError=Math.Max(maxBoneLengthError,boneError);
            unresolvedConstraintCalls+=Math.Max(0,unresolved);
        }
    }
    [Serializable] public sealed class CharacterReviewCase
    {
        public string name,note="";
        public CharacterReviewStatus status;
        public bool manuallyConfirmed;
        public CharacterReviewStats measurements=new CharacterReviewStats();
    }
    [Serializable] public sealed class CharacterReviewSession
    {
        public string kind="User-run Unity measurements; approximate envelopes, not triangle self-intersection proof";
        public CharacterReviewCase[] cases;
        public int active=-1;
        public static readonly string[] Names={"Покой","Медленная ходьба","Бег","Резкая остановка","Повороты 90° / 180°","Присед и ходьба",
            "Удержание фонарика","Направления фонарика","Замена батареек","Разрядка фонарика","Поднятие предмета","У стены","Переходы анимаций","Камера другого игрока","Низкая частота кадров"};
        public CharacterReviewSession()
        {
            cases=new CharacterReviewCase[Names.Length];for(int i=0;i<cases.Length;i++)cases[i]=new CharacterReviewCase{name=Names[i]};
        }
        public void Finish()
        {
            if(active>=0){var c=cases[active];if(c.status==CharacterReviewStatus.Recording)c.status=c.measurements.samples>0?CharacterReviewStatus.NeedsVisualReview:CharacterReviewStatus.NotRun;}
            active=-1;
        }
        public void Begin(int index,bool available,string reason="")
        {
            if(index<0 || index>=cases.Length)throw new ArgumentOutOfRangeException(nameof(index));Finish();
            var c=cases[index];c.measurements=new CharacterReviewStats();c.manuallyConfirmed=false;c.note=reason;
            c.status=available?CharacterReviewStatus.Recording:CharacterReviewStatus.Unavailable;active=available?index:-1;
        }
        public bool Mark(int index,bool passed,string note)
        {
            if(index<0 || index>=cases.Length)return false;var c=cases[index];
            if(c.status==CharacterReviewStatus.Unavailable || c.status==CharacterReviewStatus.Recording || c.measurements.samples==0)return false;
            c.status=passed?CharacterReviewStatus.Passed:CharacterReviewStatus.Failed;c.manuallyConfirmed=true;c.note=note ?? "";return true;
        }
    }
    public static class FingerCurlModel
    {
        public static RigVector BendAxis(RigVector direction,RigVector inward)
        {
            if(!direction.Finite || !inward.Finite)return default;
            var cross=new RigVector(direction.Y*inward.Z-direction.Z*inward.Y,direction.Z*inward.X-direction.X*inward.Z,direction.X*inward.Y-direction.Y*inward.X);
            return cross.Length>.00001f?cross.Normalized:default;
        }
        public static float Degrees(bool thumb,int segment,float grip)
        {
            if(float.IsNaN(grip) || float.IsInfinity(grip) || segment<0 || segment>2)return 0;
            float maximum=thumb?(segment==1?35:25):(segment==0?55:segment==1?75:45);
            return maximum*Math.Max(0,Math.Min(1,grip));
        }
    }
}
