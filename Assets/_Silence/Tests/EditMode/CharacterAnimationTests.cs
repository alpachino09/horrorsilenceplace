using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class CharacterAnimationTests
    {
        [Test] public void IdleHeadTurnDelaysOnlyBodyFacing()
        {
            var model = new CharacterFacingModel(); model.Sample(0, 0, .02f, false);
            var frame = model.Sample(35, 0, .02f, false);
            Assert.That(frame.Yaw, Is.EqualTo(0)); Assert.That(frame.TurnStarted, Is.False);
            frame = model.Sample(90, 0, .02f, false);
            Assert.That(frame.Yaw, Is.GreaterThan(0).And.LessThan(90)); Assert.That(frame.TurnStarted, Is.True);
        }
        [Test] public void TurnCrossingZeroDoesNotRotateTheLongWay()
        {
            var model = new CharacterFacingModel(); model.Sample(350, 2, .02f, false);
            var frame = model.Sample(10, 2, .02f, false);
            Assert.That(frame.Error, Is.EqualTo(20)); Assert.That(frame.Yaw, Is.GreaterThan(350).And.LessThanOrEqualTo(370));
        }
        [TestCase(45)] [TestCase(90)] [TestCase(135)] [TestCase(180)]
        public void MovingFacingConvergesWithoutInstantSnap(float target)
        {
            var model = new CharacterFacingModel(); model.Sample(0, 2, .02f, false);
            var first = model.Sample(target, 2, .02f, false); Assert.That(Math.Abs(first.Yaw), Is.LessThan(target));
            FacingFrame frame = default; for (int i = 0; i < 200; i++) frame = model.Sample(target, 2, .02f, false);
            Assert.That(CharacterFacingModel.Delta(frame.Yaw, target), Is.EqualTo(0).Within(.01f));
        }
        [Test] public void InvalidTimeResetsFacingInsteadOfProducingNaN()
        {
            var model = new CharacterFacingModel(); model.Sample(0, 0, .02f, false);
            model.Sample(float.NaN, 3, .02f, false);
            Assert.That(model.Sample(120, 0, .02f, false).Yaw, Is.EqualTo(120));
        }
        [Test] public void AFrameHitchDoesNotTurnTheModelToWorldZero()
        {
            var model = new CharacterFacingModel(); model.Sample(173, 2, .02f, false);
            Assert.That(model.Sample(173, 2, 1, false).Yaw, Is.EqualTo(173));
        }
        [Test] public void ReactionPriorityPreemptsMinorAction()
        {
            var gate = new AnimationActionGate(); Assert.That(gate.TryBegin(AnimationCueKind.Interact, 1, 1, true));
            Assert.That(gate.TryBegin(AnimationCueKind.Startle, 1.1, 1, true), Is.False);
            Assert.That(gate.TryBegin(AnimationCueKind.Hit, 1.2, .6, true));
            Assert.That(gate.TryBegin(AnimationCueKind.Trip, 1.3, 2, true), Is.False);
            Assert.That(gate.TryBegin(AnimationCueKind.Interact, 2, 1, true));
        }
        [Test] public void DeadActorCannotStartReactionAndInvalidDurationsCannotHoldGate()
        {
            var gate = new AnimationActionGate(); Assert.That(gate.TryBegin(AnimationCueKind.Hit, 1, 1, false), Is.False);
            Assert.That(gate.TryBegin(AnimationCueKind.Hit, 1, double.NaN, true), Is.False);
            Assert.That(gate.TryBegin((AnimationCueKind)255, 1, 1, true), Is.False);
            Assert.That(gate.TryBegin(AnimationCueKind.Trip, 1, 11, true), Is.False);
            Assert.That(gate.TryBegin(AnimationCueKind.Interact, 1, .5, true));
        }
        [Test] public void FootContactsDoNotEmitForInitialPoseOrStaticActor()
        {
            var feet = new FootContactModel();
            Assert.That(feet.Sample(1, 1, 0, true, 0, 1, out _), Is.False);
            Assert.That(feet.Sample(1, 0, .2f, true, 1, 2, out _), Is.False);
            Assert.That(feet.Sample(1, 0, .2f, true, 1, 2.2, out _), Is.False);
        }
        [Test] public void SlowWalkContactCanReleaseWhenIdleIsBlendedIntoGait()
        {
            // 25% walking mixed with 75% planted Idle must still release the swing foot.
            Assert.That(FootContactModel.RemoveIdleBlend(.75f, .25f), Is.EqualTo(0));
            Assert.That(FootContactModel.RemoveIdleBlend(1, .25f), Is.EqualTo(1));
            Assert.That(FootContactModel.RemoveIdleBlend(.5f, 1), Is.EqualTo(.5f));
        }
        [Test] public void IdleBlendContactRejectsNaNAndKeepsIdlePlanted()
        {
            Assert.That(FootContactModel.RemoveIdleBlend(1, 0), Is.EqualTo(1));
            Assert.That(FootContactModel.RemoveIdleBlend(float.NaN, .5f), Is.EqualTo(0));
            Assert.That(FootContactModel.RemoveIdleBlend(0, float.NaN), Is.EqualTo(0));
        }
        [Test] public void OnlyContactEdgesEmitAndEachContactEmitsOnce()
        {
            var feet = new FootContactModel(); feet.Sample(0, 0, .1f, true, 1, 1, out _);
            Assert.That(feet.Sample(1, 0, .2f, true, 1, 1.2, out bool left)); Assert.That(left);
            Assert.That(feet.Sample(1, 0, .2f, true, 1, 1.4, out _), Is.False);
            feet.Sample(0, 0, .2f, true, 1, 1.6, out _);
            Assert.That(feet.Sample(0, 1, .2f, true, 1, 1.8, out left)); Assert.That(left, Is.False);
        }
        [Test] public void ContactHysteresisRejectsBlendTreeJitter()
        {
            var feet = new FootContactModel(); feet.Sample(0, 0, .2f, true, 1, 1, out _);
            Assert.That(feet.Sample(.8f, 0, .2f, true, 1, 1.3, out _));
            foreach (float weight in new[] { .6f, .5f, .4f, .5f, .7f, .8f }) Assert.That(feet.Sample(weight, 0, .2f, true, 1, 1.6, out _), Is.False);
        }
        [Test] public void AirborneTeleportAndStationaryTurnCannotEmitSteps()
        {
            var feet = new FootContactModel(); feet.Sample(0, 0, .2f, true, 1, 1, out _);
            Assert.That(feet.Sample(1, 0, .2f, false, 1, 1.2, out _), Is.False);
            Assert.That(feet.Sample(1, 0, 5, true, 5, 1.4, out _), Is.False);
            Assert.That(feet.Sample(1, 0, 0, true, 0, 1.6, out _), Is.False);
        }
        [Test] public void TwoFeetAtOnceDoNotDoubleEmit()
        {
            var feet = new FootContactModel(); feet.Sample(0, 0, .2f, true, 1, 1, out _);
            Assert.That(feet.Sample(1, 1, .2f, true, 1, 1.2, out _));
            Assert.That(feet.Sample(1, 1, .2f, true, 1, 1.2, out _), Is.False);
        }
        [Test] public void GravelKeepsOldSurfaceIdentifiersAndHasDistinctNoise()
        {
            Assert.That((byte)SurfaceKind.Water, Is.EqualTo(6)); Assert.That((byte)SurfaceKind.Gravel, Is.EqualTo(7));
            Assert.That(StepProfile.For(SurfaceKind.Gravel, false, false).Intensity, Is.GreaterThan(StepProfile.For(SurfaceKind.Grass, false, false).Intensity));
            Assert.That(StepProfile.For(SurfaceKind.Gravel, true, false).Intensity, Is.LessThan(StepProfile.For(SurfaceKind.Gravel, false, false).Intensity));
        }
        [Test] public void OldAnimationWireLayoutCannotEnterAnUpdatedSession()
        {
            var lobby = new SessionAdmission();
            Assert.That(lobby.TryJoin(1, "SILENCE-GAME-3", out _, out _), Is.False);
            Assert.That(lobby.Count, Is.EqualTo(0));
            Assert.That(lobby.TryJoin(2, SessionAdmission.Protocol, out _, out _));
            Assert.That(lobby.Count, Is.EqualTo(1));
        }
    }
}
