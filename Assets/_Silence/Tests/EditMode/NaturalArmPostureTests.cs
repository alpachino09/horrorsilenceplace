using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class NaturalArmPostureTests
    {
        private static RigVector Mirror(RigVector p)=>new RigVector(-p.X,p.Y,p.Z);
        private static NaturalArmPlan Plan(RigVector s,RigVector e,RigVector h,float speed=0,float side=1)
            => NaturalArmPostureModel.Plan(s,e,h,.25f,.228f,side,speed,.184f,.045f,.035f);
        [Test] public void WideHandsOnHipsPoseIsLoweredAndItsElbowMovesTowardTheBody()
        {
            var s=new RigVector(.166f,.473f,0);var e=new RigVector(.371f,.329f,0);var h=new RigVector(.271f,.125f,0);
            var plan=Plan(s,e,h);Assert.That(plan.Required);
            var torso=new TorsoEnvelope(.025f,.352f,.184f,.173f,.10f);
            var attachment=new TorsoEnvelope(torso.Bottom,torso.Top,NaturalArmPostureModel.AttachmentWidth(torso.Width,s.X),torso.Depth,torso.Cap);
            var limbs=new[]{new BodyCapsule(new RigVector(.085f,-.005f,0),new RigVector(.085f,-.39f,.02f),.09f)};
            var result=ArmAnatomyModel.Solve(s,e,plan.Hand,.25f,.228f,1,torso,.045f,.035f,limbs,attachment,plan.Elbow);
            Assert.That(result.Hand.Y,Is.LessThan(.1f));Assert.That(result.Elbow.X-s.X,Is.LessThan(.10f));
            Assert.That(result.RemainingContact,Is.False);
            Assert.That((result.Elbow-s).Length,Is.EqualTo(.25f).Within(.00001f));
            Assert.That((result.Hand-result.Elbow).Length,Is.EqualTo(.228f).Within(.00001f));
        }
        [Test] public void NormalRelaxedIdleFromTheDeliveredRigIsNotReplaced()
        {
            var plan=Plan(new RigVector(.1663f,.4728f,0),new RigVector(.2212f,.2283f,.0041f),new RigVector(.2710f,.0069f,.0219f));
            Assert.That(plan.Required,Is.False);
        }
        [Test] public void TheTorsoAttachmentDoesNotPushAValidCloseUpperArmOutward()
        {
            var s=new RigVector(.1663f,.4728f,0);var e=new RigVector(.2212f,.2283f,.0041f);var h=new RigVector(.2710f,.0069f,.0219f);
            var torso=new TorsoEnvelope(.025f,.352f,.184f,.173f,.10f);
            var attachment=new TorsoEnvelope(torso.Bottom,torso.Top,NaturalArmPostureModel.AttachmentWidth(torso.Width,s.X),torso.Depth,torso.Cap);
            var r=ArmAnatomyModel.Solve(s,e,h,(e-s).Length,(h-e).Length,1,torso,.045f,.035f,null,attachment);
            // Corrected=false means the runtime leaves this authored pose alone.
            // The geometric output may differ below 2 mm due to its minimum bend.
            Assert.That(r.Corrected,Is.False);Assert.That(r.RemainingContact,Is.False);Assert.That((r.Hand-h).Length,Is.LessThan(.002f));
        }
        [Test] public void BothArmsHaveMirroredNaturalGoals()
        {
            var s=new RigVector(.166f,.473f,0);var e=new RigVector(.371f,.329f,0);var h=new RigVector(.271f,.125f,0);
            var a=Plan(s,e,h);var b=Plan(Mirror(s),Mirror(e),Mirror(h),0,-1);
            Assert.That(a.Required && b.Required);Assert.That((Mirror(a.Hand)-b.Hand).Length,Is.LessThan(.00001f));Assert.That((Mirror(a.Elbow)-b.Elbow).Length,Is.LessThan(.00001f));
        }
        [Test] public void WalkSwingIsPreservedInsteadOfBeingReplacedByAProceduralOscillation()
        {
            var s=new RigVector(.166f,.473f,0);var e=new RigVector(.371f,.329f,0);
            var front=Plan(s,e,new RigVector(.271f,.125f,.16f),1.65f);var back=Plan(s,e,new RigVector(.271f,.125f,-.13f),1.65f);
            Assert.That(front.Hand.Z,Is.EqualTo(.16f).Within(.00001f));Assert.That(back.Hand.Z,Is.EqualTo(-.13f).Within(.00001f));
        }
        [Test] public void RunningAllowsItsAuthoredElbowBendAndForwardHandMotion()
        {
            var s=new RigVector(.166f,.473f,0);var e=new RigVector(.25f,.25f,.05f);var h=new RigVector(.24f,.3f,.25f);
            Assert.That(Plan(s,e,h,5.5f).Required,Is.False);
        }
        [Test] public void ClaspedOrCrossedHandsStillGetAReachableGoalOnTheirOwnSide()
        {
            var plan=Plan(new RigVector(.166f,.473f,0),new RigVector(.2f,.25f,-.05f),new RigVector(-.02f,.05f,-.16f));
            Assert.That(plan.Required);Assert.That(plan.Hand.X,Is.GreaterThan(.22f));Assert.That(plan.Hand.Z,Is.GreaterThan(0));
        }
        [Test] public void AnOversizedSleeveEnvelopeCannotForceTheArmPastItsShoulderSpan()
        {
            float width=NaturalArmPostureModel.EffectiveTorsoWidth(.3f,.166f);
            Assert.That(width,Is.EqualTo(.18592f).Within(.00001f));
            Assert.That(NaturalArmPostureModel.EffectiveTorsoWidth(.15f,.2f),Is.EqualTo(.15f));
        }
        [Test] public void RandomWidePosesDoNotStretchBonesOrGenerateNonfiniteGoals()
        {
            var random=new Random(51);var s=new RigVector(.166f,.473f,0);var e=new RigVector(.371f,.329f,0);
            var torso=new TorsoEnvelope(.025f,.352f,.184f,.173f,.10f);var attachment=new TorsoEnvelope(.025f,.352f,.146f,.173f,.10f);
            for(int i=0;i<500;i++)
            {
                var h=new RigVector(.1f+(float)random.NextDouble()*.4f,(float)random.NextDouble()*.5f-.1f,(float)random.NextDouble()*.8f-.4f);
                var p=Plan(s,e,h,(float)random.NextDouble()*5.5f);
                var r=ArmAnatomyModel.Solve(s,e,p.Hand,.25f,.228f,1,torso,.045f,.035f,null,attachment,p.Elbow);
                Assert.That(r.Hand.Finite && r.Elbow.Finite);Assert.That((r.Elbow-s).Length,Is.EqualTo(.25f).Within(.00002f));Assert.That((r.Hand-r.Elbow).Length,Is.EqualTo(.228f).Within(.00002f));
            }
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidSpeedCannotTurnThePoseIntoNaN(float speed)
        {var p=Plan(new RigVector(.166f,.473f,0),new RigVector(.371f,.329f,0),new RigVector(.271f,.125f,0),speed);Assert.That(p.Hand.Finite && p.Elbow.Finite);}
    }
}
