using System;

namespace Silence.Core
{
    public readonly struct NaturalArmPlan
    {
        public readonly RigVector Hand,Elbow;
        public readonly bool Required;
        public NaturalArmPlan(RigVector hand,RigVector elbow,bool required){Hand=hand;Elbow=elbow;Required=required;}
    }

    public static class NaturalArmPostureModel
    {
        // An IK envelope is not the visible mesh. Oversized sampled sleeves
        // must not make the torso volume extend far beyond the shoulder joints.
        public static float EffectiveTorsoWidth(float measured,float shoulderHalfSpan)
            => Math.Min(measured,Math.Max(.06f,Math.Abs(shoulderHalfSpan)*1.12f));
        public static float AttachmentWidth(float torsoWidth,float shoulderHalfSpan)
            => Math.Min(torsoWidth,Math.Max(.06f,Math.Abs(shoulderHalfSpan)*.88f));
        public static NaturalArmPlan Plan(RigVector shoulder,RigVector elbow,RigVector hand,
            float upper,float forearm,float side,float speed,float torsoWidth,float handRadius,float armRadius,bool alreadyCorrecting=false)
        {
            if(!shoulder.Finite || !elbow.Finite || !hand.Finite || !(upper>.02f && upper<1 && forearm>.02f && forearm<1))
                return new NaturalArmPlan(hand,elbow,false);
            float sign=side<0?-1:1,reach=upper+forearm;
            if(float.IsNaN(speed) || float.IsInfinity(speed))speed=0;
            float gait=Clamp(speed/3.6f),running=Clamp((speed-1.5f)/2.1f);
            float abduction=(18+24*gait-(alreadyCorrecting?4:0))*(float)Math.PI/180;
            float outward=(elbow.X-shoulder.X)*sign;
            float backLimit=(.05f+.23f*gait)*(alreadyCorrecting?.85f:1);
            bool spread=outward>upper*(float)Math.Sin(abduction)+.005f;
            bool crossed=hand.X*sign<.01f;
            bool rear=hand.Z-shoulder.Z < -backLimit;
            bool high=running<.1f && hand.Y>shoulder.Y-reach*.65f;
            float z=Max(-backLimit,Min(.07f+.23f*gait,hand.Z-shoulder.Z));
            if(rear && gait<.2f)z=.025f;
            float drop=(float)Math.Sqrt(Math.Max(.01f,reach*reach-z*z))*.96f;
            float low=shoulder.Y-drop;
            float movingY=Max(low,Min(shoulder.Y-reach*.30f,hand.Y));
            float y=low+(movingY-low)*running;
            float x=sign*Max(Math.Abs(shoulder.X)+.025f,torsoWidth+handRadius+.015f);
            var target=new RigVector(x,y,shoulder.Z+z);
            var pole=Pole(shoulder,upper,side,torsoWidth,armRadius,z);
            return new NaturalArmPlan(target,pole,spread || crossed || rear || high);
        }
        public static RigVector Pole(RigVector shoulder,float upper,float side,float torsoWidth,float armRadius,float swing)
        {
            float sign=side<0?-1:1;
            return new RigVector(sign*Max(Math.Abs(shoulder.X)+.025f,torsoWidth+armRadius+.005f),
                shoulder.Y-upper*.96f,shoulder.Z+.035f+swing*.25f);
        }
        private static float Clamp(float value)=>Max(0,Min(1,value));
        public static float Blend(float current,bool correcting,float seconds)
        {
            if(float.IsNaN(current) || float.IsInfinity(current))current=0;
            if(float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds<=0)return Clamp(current);
            float target=correcting?1:0;
            return Clamp(target+(Clamp(current)-target)*(float)Math.Exp(-Math.Min(seconds,.5f)/.10f));
        }
        private static float Min(float a,float b)=>Math.Min(a,b);
        private static float Max(float a,float b)=>Math.Max(a,b);
    }
}
