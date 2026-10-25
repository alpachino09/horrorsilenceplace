using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class ArmAnatomyTests
    {
        private static readonly TorsoEnvelope Torso = new TorsoEnvelope(.02f, .35f, .17f, .12f, .08f);
        [TestCase(-1)] [TestCase(1)] public void HandAtTorsoCentreIsProjectedToItsOwnSide(float side)
        {
            var p = Torso.Clear(new RigVector(0,.2f,0),.045f,side);
            Assert.That(p.X*side,Is.GreaterThan(.21f)); Assert.That(Torso.Contains(p,.045f),Is.False);
        }
        [Test] public void AlreadyClearHandIsNotDisplaced()
        { var p=new RigVector(.3f,.1f,.2f); Assert.That((Torso.Clear(p,.045f,1)-p).Length,Is.EqualTo(0)); }
        [Test] public void CapsuleIncludesAbdomenAndUpperChest()
        { Assert.That(Torso.Contains(new RigVector(.05f,.03f,0),.03f)); Assert.That(Torso.Contains(new RigVector(.05f,.34f,0),.03f)); }
        [Test] public void GoalBeyondReachDoesNotStretchEitherBone()
        {
            var shoulder=new RigVector(.24f,.4f,0); var r=ArmAnatomyModel.Solve(shoulder,new RigVector(.30f,.15f,.05f),new RigVector(5,3,2),.28f,.26f,1,Torso,.045f,.03f);
            Assert.That((r.Elbow-shoulder).Length,Is.EqualTo(.28f).Within(.00001f));
            Assert.That((r.Hand-r.Elbow).Length,Is.EqualTo(.26f).Within(.00001f)); Assert.That(r.Corrected);
        }
        [Test] public void CoincidentGoalProducesFiniteBentArm()
        {
            var shoulder=new RigVector(.24f,.4f,0); var r=ArmAnatomyModel.Solve(shoulder,new RigVector(.3f,.15f,0),shoulder,.28f,.26f,1,Torso,.045f,.03f);
            Assert.That(r.Hand.Finite && r.Elbow.Finite); Assert.That((r.Hand-shoulder).Length,Is.GreaterThan(.1f));
        }
        [Test] public void MirroringTheEntirePoseMirrorsTheSolution()
        {
            RigVector Mirror(RigVector p)=>new RigVector(-p.X,p.Y,p.Z);
            var s=new RigVector(.24f,.4f,0); var e=new RigVector(.28f,.14f,.05f); var h=new RigVector(.02f,.12f,0);
            var a=ArmAnatomyModel.Solve(s,e,h,.28f,.26f,1,Torso,.045f,.03f);
            var b=ArmAnatomyModel.Solve(Mirror(s),Mirror(e),Mirror(h),.28f,.26f,-1,Torso,.045f,.03f);
            Assert.That((Mirror(a.Hand)-b.Hand).Length,Is.LessThan(.00001f)); Assert.That((Mirror(a.Elbow)-b.Elbow).Length,Is.LessThan(.00001f));
        }
        [Test] public void RandomReachAndTorsoGoalsKeepMeasuredBoneLengths()
        {
            var random=new Random(1729); var s=new RigVector(.24f,.40f,0); var e=new RigVector(.3f,.15f,.05f);
            for(int i=0;i<1000;i++)
            {
                var h=new RigVector((float)random.NextDouble()*1.4f-.4f,(float)random.NextDouble()*1.5f-.6f,(float)random.NextDouble()*1.4f-.7f);
                var r=ArmAnatomyModel.Solve(s,e,h,.28f,.26f,1,Torso,.045f,.03f);
                Assert.That(r.Hand.Finite && r.Elbow.Finite);
                Assert.That((r.Elbow-s).Length,Is.EqualTo(.28f).Within(.00002f)); Assert.That((r.Hand-r.Elbow).Length,Is.EqualTo(.26f).Within(.00002f));
            }
        }
        [Test] public void DegenerateLimbIsReportedWithoutChangingTarget()
        {
            var h=new RigVector(.2f,.1f,.1f); var r=ArmAnatomyModel.Solve(h,h,h,0,.2f,1,Torso,.04f,.03f);
            Assert.That(r.RemainingContact); Assert.That(r.Corrected,Is.False); Assert.That((r.Hand-h).Length,Is.EqualTo(0));
        }
        [Test] public void InvalidGoalDoesNotBecomeAnIKCommand()
        {
            var r=ArmAnatomyModel.Solve(new RigVector(.2f,.4f,0),new RigVector(.3f,.15f,0),new RigVector(float.NaN,0,0),.28f,.26f,1,Torso,.04f,.03f);
            Assert.That(r.Corrected,Is.False); Assert.That(r.RemainingContact);
        }
        [Test] public void ChestCentreGoalIsRedirectedOutsideTheMeasuredTorso()
        {
            var r=ArmAnatomyModel.Solve(new RigVector(.24f,.4f,0),new RigVector(.30f,.15f,.05f),new RigVector(.02f,.12f,0),.28f,.26f,1,Torso,.045f,.03f);
            Assert.That(r.Corrected);Assert.That(Torso.Contains(r.Hand,.045f),Is.False);Assert.That(r.RemainingContact,Is.False);
        }
        [Test] public void FlashlightRegripsStayWithinHumanWristRangeAndReturnToNeutral()
        {
            float previous=0;
            for(int i=0;i<=1000;i++)
            {
                float angle=FlashlightHandMotion.WristTurn(i/1000f);
                Assert.That(Math.Abs(angle),Is.LessThanOrEqualTo(40));Assert.That(Math.Abs(angle-previous),Is.LessThan(1));previous=angle;
            }
            Assert.That(FlashlightHandMotion.WristTurn(0),Is.EqualTo(0));Assert.That(FlashlightHandMotion.WristTurn(1),Is.EqualTo(0).Within(.0001f));
            Assert.That(FlashlightHandMotion.WristTurn(float.NaN),Is.EqualTo(0));
        }
        [Test] public void KneeClearanceUsesTheBentLimbNotAStandingHeightAssumption()
        {
            var knee=new BodyCapsule(new RigVector(-.1f,-.2f,.1f),new RigVector(-.1f,.05f,.4f),.09f);
            var p=new RigVector(-.1f,.05f,.4f);var clear=knee.Clear(p,.045f,-1);
            Assert.That((clear-p).Length,Is.EqualTo(.135f).Within(.00001f));Assert.That(clear.X,Is.LessThan(p.X));
        }
    }
}
