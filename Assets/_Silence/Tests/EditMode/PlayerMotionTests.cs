using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class PlayerMotionTests
    {
        private static PlayerMotionFrame ConstantMotion(float x, float y, float z, int fps)
        {
            var model = new PlayerMotionModel(); PlayerMotionFrame result = default;
            for (int i = 0; i < fps; i++) result = model.Sample(x / fps, y / fps, z / fps, 1f / fps, true);
            return result;
        }
        [Test] public void WalkingSpeedIsIndependentOfRenderFrameRate()
        {
            var low = ConstantMotion(0, 0, 3, 30); var high = ConstantMotion(0, 0, 3, 120);
            Assert.That(low.Speed, Is.EqualTo(3).Within(.001));
            Assert.That(high.Speed, Is.EqualTo(low.Speed).Within(.001));
        }
        [TestCase(-2f, 0f)] [TestCase(2f, 0f)] [TestCase(0f, -3f)] [TestCase(2f, 3f)]
        public void AnimationPreservesStrafeAndBackwardDirections(float x, float z)
        {
            var result = ConstantMotion(x, 0, z, 60);
            Assert.That(result.Right, Is.EqualTo(x).Within(.001)); Assert.That(result.Forward, Is.EqualTo(z).Within(.001));
            Assert.That(result.Speed, Is.EqualTo(Math.Sqrt(x * x + z * z)).Within(.001));
        }
        [Test] public void StoppingDecaysMovementWithoutMovingThePlayer()
        {
            var model = new PlayerMotionModel(); PlayerMotionFrame result = default;
            for (int i = 0; i < 60; i++) model.Sample(0, 0, .05f, 1f / 60, true);
            for (int i = 0; i < 60; i++) result = model.Sample(0, 0, 0, 1f / 60, true);
            Assert.That(result.Speed, Is.LessThan(.001));
        }
        [Test] public void SpawnAndRepeatedGroundSamplesDoNotReplayLanding()
        {
            var model = new PlayerMotionModel();
            Assert.That(model.Sample(0, 0, 0, .02f, true).Landed, Is.False);
            Assert.That(model.Sample(0, 0, 0, .02f, false).Landed, Is.False);
            Assert.That(model.Sample(0, -.1f, 0, .02f, false).Landed, Is.False);
            Assert.That(model.Sample(0, 0, 0, .02f, true).Landed, Is.True);
            Assert.That(model.Sample(0, 0, 0, .02f, true).Landed, Is.False);
        }
        [Test] public void TeleportResetsMotionAndCannotCreateALandingEvent()
        {
            var model = new PlayerMotionModel(); model.Sample(0, .1f, .1f, .02f, false);
            var result = model.Sample(1000, 0, -1000, .02f, true);
            Assert.That(result.Speed, Is.Zero); Assert.That(result.Vertical, Is.Zero); Assert.That(result.Landed, Is.False);
            Assert.That(model.Sample(0, 0, 0, .02f, true).Landed, Is.False);
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-.02f)] [TestCase(0f)] [TestCase(.5f)]
        public void InvalidAndStaleDeltaTimeCannotPoisonAnimation(float seconds)
        {
            var model = new PlayerMotionModel(); model.Sample(0, .1f, .1f, .02f, false);
            var result = model.Sample(.1f, 0, .1f, seconds, true);
            Assert.That(result.Speed, Is.Zero); Assert.That(result.Vertical, Is.Zero); Assert.That(result.Landed, Is.False);
            Assert.That(model.Sample(0, 0, 0, .02f, true).Landed, Is.False);
        }
        [TestCase(float.NaN)] [TestCase(float.NegativeInfinity)] [TestCase(float.MaxValue)]
        public void CorruptDisplacementProducesFiniteIdleState(float displacement)
        {
            var result = new PlayerMotionModel().Sample(displacement, 0, 0, .02f, true);
            Assert.That(result.Speed, Is.Zero); Assert.That(result.Vertical, Is.Zero);
        }
        [Test] public void TinyDeltaTimeCannotOverflowVelocity()
        {
            var result = new PlayerMotionModel().Sample(1, 1, 1, float.Epsilon, true);
            Assert.That(result.Speed, Is.Zero); Assert.That(result.Vertical, Is.Zero);
        }
        [Test] public void FallingVelocityRemainsSignedAndFinite()
        {
            var result = ConstantMotion(0, -20, 0, 60);
            Assert.That(result.Vertical, Is.EqualTo(-20).Within(.001)); Assert.That(result.Speed, Is.Zero);
        }
    }
}
