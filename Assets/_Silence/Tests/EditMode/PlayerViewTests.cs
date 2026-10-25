using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class PlayerViewTests
    {
        [Test] public void NewlyAppearingWallRetractsCameraImmediately()
        {
            var boom = new CameraBoomModel(); Assert.That(boom.Sample(3, 3, .02f), Is.EqualTo(3));
            Assert.That(boom.Sample(3, .2f, .02f), Is.EqualTo(.2f));
        }
        [Test] public void CameraExtendsSmoothlyAfterLeavingWall()
        {
            var boom = new CameraBoomModel(); boom.Sample(3, .2f, .02f);
            float value = boom.Sample(3, 3, .02f); Assert.That(value, Is.GreaterThan(.2f).And.LessThan(3));
        }
        [TestCase(0)] [TestCase(.02f)] [TestCase(1)] [TestCase(5)]
        public void CameraNeverExceedsCurrentSafeDistance(float safe)
        {
            var boom = new CameraBoomModel(); boom.Sample(3, 3, .02f);
            Assert.That(boom.Sample(3, safe, .02f), Is.LessThanOrEqualTo(safe).And.LessThanOrEqualTo(3));
        }
        [Test] public void PausedOrInvalidTimeStillRetractsAtObstruction()
        {
            var boom = new CameraBoomModel(); boom.Sample(3, 3, .02f);
            Assert.That(boom.Sample(3, .1f, 0), Is.EqualTo(.1f));
            Assert.That(boom.Sample(3, .05f, float.NaN), Is.EqualTo(.05f));
        }
        [Test] public void InvalidPhysicsResultRetractsInsteadOfPropagatingNaN()
        { var boom = new CameraBoomModel(); Assert.That(boom.Sample(3, float.NaN, .02f), Is.EqualTo(0)); }
        [Test] public void NaturalEyeOffsetRespectsCapsuleClearance()
        {
            Assert.That(PlayerViewRules.EyeForward(.4f, .3f, .07f), Is.EqualTo(.23f).Within(.0001f));
            Assert.That(PlayerViewRules.EyeForward(.16f, .3f, .07f), Is.EqualTo(.16f));
            Assert.That(PlayerViewRules.EyeForward(float.NaN, .3f, .07f), Is.EqualTo(0));
        }
        [Test] public void RearClaspedIdleHandsAreCorrected()
        { Assert.That(PlayerViewRules.BadArmPose(.02f, -.24f, 0, true)); }
        [Test] public void CrossedHandsTriggerCorrection()
        { Assert.That(PlayerViewRules.BadArmPose(-.1f, .05f, 1, false)); }
        [Test] public void NearRearClaspIsCorrectedWithoutFlatteningWideIdleArms()
        {
            Assert.That(PlayerViewRules.BadArmPose(.02f, -.12f, 0, true));
            Assert.That(PlayerViewRules.BadArmPose(.27f, -.10f, 0, true), Is.False);
        }
        [Test] public void FrontOfBodyInjuryPoseMayCrossTheMidline()
        { Assert.That(PlayerViewRules.BadArmPose(-.1f, .25f, 0, false), Is.False); }
        [Test] public void OrdinaryBackwardRunArmSwingIsPreserved()
        { Assert.That(PlayerViewRules.BadArmPose(.25f, -.25f, 5, false), Is.False); }
        [Test] public void RelaxedOrForwardHandsArePreserved()
        { Assert.That(PlayerViewRules.BadArmPose(.27f, .05f, 0, false), Is.False); }
    }
}
