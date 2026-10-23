using System;
using System.Linq;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class GameplayModelTests
    {
        [Test] public void InjuryHasGraceThenDeathAndRequiresARealRevive()
        {
            var health = new ActorVitals();
            Assert.That(health.TryHit(1, 5), Is.True);
            Assert.That(health.State, Is.EqualTo(VitalState.Wounded));
            Assert.That(health.TryHit(2, 5), Is.False);
            Assert.That(health.TryHit(6, 5), Is.True);
            Assert.That(health.Alive, Is.False);
            Assert.That(health.Heal(), Is.False);
            Assert.That(health.Revive(7), Is.True);
            Assert.That(health.TryHit(8), Is.False);
            Assert.That(health.State, Is.EqualTo(VitalState.Wounded));
        }
        [Test] public void MedicineHealsWoundButNotAHealthyOrDeadActor()
        {
            var health = new ActorVitals();
            Assert.That(health.Heal(), Is.False);
            health.TryHit(0); Assert.That(health.Heal(), Is.True);
            Assert.That(health.State, Is.EqualTo(VitalState.Healthy));
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InvalidAttackTimeCannotMutateHealth(double time)
        { var health = new ActorVitals(); Assert.That(health.TryHit(time), Is.False); Assert.That(health.State, Is.EqualTo(VitalState.Healthy)); }
        [Test] public void StaminaSupportsLongSprintAndRecoversWhileWalking()
        {
            var stamina = new StaminaModel();
            for (int i = 0; i < 80; i++) stamina.Tick(.1f, true);
            Assert.That(stamina.Value, Is.InRange(.3f, .4f));
            for (int i = 0; i < 100; i++) stamina.Tick(.1f, false);
            Assert.That(stamina.Value, Is.EqualTo(1).Within(.001));
        }
        [TestCase(SurfaceKind.Dirt)] [TestCase(SurfaceKind.Grass)] [TestCase(SurfaceKind.Wood)]
        [TestCase(SurfaceKind.Concrete)] [TestCase(SurfaceKind.Glass)] [TestCase(SurfaceKind.Metal)] [TestCase(SurfaceKind.Water)]
        public void CrouchingIsQuieterThanWalkingAndRunning(SurfaceKind surface)
        {
            Assert.That(StepProfile.For(surface, true, false).Intensity, Is.LessThan(StepProfile.For(surface, false, false).Intensity));
            Assert.That(StepProfile.For(surface, false, true).Intensity, Is.GreaterThan(StepProfile.For(surface, false, false).Intensity));
        }
        [Test] public void MetalAndGlassAreLouderThanGrass()
        {
            Assert.That(StepProfile.For(SurfaceKind.Metal, false, false).Intensity,
                Is.GreaterThan(StepProfile.For(SurfaceKind.Grass, false, false).Intensity * 3));
            Assert.That(StepProfile.For(SurfaceKind.Glass, false, false).Range, Is.GreaterThan(20));
        }
        [Test] public void TeleportAndAirborneMovementDoNotGenerateFootsteps()
        {
            var steps = new StepCycle();
            Assert.That(steps.Advance(100, true, false, false), Is.False);
            Assert.That(steps.Advance(2, false, false, false), Is.False);
            Assert.That(steps.Advance(.8f, true, false, false), Is.False);
            Assert.That(steps.Advance(.8f, true, false, false), Is.True);
        }
        [Test] public void CreatureTracksHeardPositionAndCannotFollowSilentMovement()
        {
            var mind = new CreatureMind(DifficultyRules.For(Difficulty.Normal));
            Assert.That(mind.Hear(new Point3(4, 0, 8), .4f, 1), Is.True);
            Assert.That(mind.State, Is.EqualTo(CreatureState.Chase));
            mind.Tick(2, 5);
            Assert.That(mind.LastHeardPosition.X, Is.EqualTo(4));
            mind.Tick(9, 1);
            Assert.That(mind.State, Is.EqualTo(CreatureState.Search));
            mind.Tick(40, 1);
            Assert.That(mind.State, Is.EqualTo(CreatureState.Patrol));
            Assert.That(mind.HasMemory, Is.False);
        }
        [Test] public void InaudibleOrWeakerRecentNoiseDoesNotReplaceMemory()
        {
            var mind = new CreatureMind(DifficultyRules.For(Difficulty.Normal));
            Assert.That(mind.Hear(new Point3(0, 0, 0), .01f, 0), Is.False);
            mind.Hear(new Point3(5, 0, 0), .8f, 1);
            Assert.That(mind.Hear(new Point3(10, 0, 0), .06f, 1.1), Is.False);
            Assert.That(mind.LastHeardPosition.X, Is.EqualTo(5));
        }
        [Test] public void CreatureRecoveryPreventsInstantAttackLoop()
        {
            var mind = new CreatureMind(DifficultyRules.For(Difficulty.Normal));
            mind.Hear(new Point3(1, 0, 0), .6f, 0); mind.Recover(0, 6);
            mind.Hear(new Point3(2, 0, 0), 1, 1); mind.Tick(2, 1);
            Assert.That(mind.State, Is.EqualTo(CreatureState.Recover));
            mind.Tick(6, 2); Assert.That(mind.State, Is.EqualTo(CreatureState.Investigate));
        }
        [Test] public void CampaignCannotSkipObjectivesOrGoBackwards()
        {
            var campaign = new CampaignModel(); Assert.That(campaign.Advance(), Is.False);
            campaign.Collected("document"); Assert.That(campaign.ReceiveSignal(), Is.False);
            campaign.Collected("battery"); Assert.That(campaign.ReceiveSignal(), Is.True);
            Assert.That(campaign.Advance(), Is.True);
            Assert.That(campaign.Chapter, Is.EqualTo(ChapterId.Town));
            Assert.That(campaign.CanExit, Is.False);
        }
        [TestCase(true, true, Ending.Hope)] [TestCase(false, true, Ending.Uncertain)] [TestCase(true, false, Ending.Outbreak)]
        public void FinalChoiceProducesTheConfiguredEnding(bool help, bool research, Ending expected)
        {
            var campaign = new CampaignModel(); campaign.Restore(ChapterId.Laboratory, help, research);
            campaign.Collected("battery"); campaign.ReceiveSignal(); campaign.Collected("document"); campaign.Advance();
            Assert.That(campaign.Ending, Is.EqualTo(expected)); Assert.That(campaign.Advance(), Is.False);
        }
        [Test] public void RestartResetsMissionButKeepsStoryChoice()
        {
            var campaign = new CampaignModel(); campaign.Restore(ChapterId.Lake, true, false);
            campaign.Collected("battery"); campaign.RestartChapter();
            Assert.That(campaign.HelpedSurvivors, Is.True); Assert.That(campaign.Mission, Is.EqualTo(MissionFlags.None));
        }
        [Test] public void VoteRejectsOutsidersAndDuplicatesAndResolvesMajority()
        {
            var vote = new VoteModel(new ulong[] { 1, 2, 3 }, 0);
            Assert.That(vote.Cast(4, true), Is.False); Assert.That(vote.Cast(1, true), Is.True);
            Assert.That(vote.Cast(1, false), Is.False); vote.Cast(2, true); vote.Cast(3, false);
            Assert.That(vote.Tick(1), Is.True); Assert.That(vote.Result, Is.True);
        }
        [Test] public void DisconnectedVoterDoesNotBlockTheParty()
        {
            var vote = new VoteModel(new ulong[] { 1, 2 }, 0); vote.Cast(1, false); vote.Remove(2);
            Assert.That(vote.Tick(1), Is.True); Assert.That(vote.Result, Is.False);
        }
        [Test] public void VoteDeadlineAndTiesUseConservativeChoice()
        {
            var vote = new VoteModel(new ulong[] { 1, 2 }, 0); vote.Cast(1, true); vote.Cast(2, false);
            Assert.That(vote.Tick(1), Is.True); Assert.That(vote.Result, Is.True);
            Assert.That(new VoteModel(new ulong[] { 1 }, 0).Tick(21), Is.True);
        }
        [Test] public void BackpackUsesFiveSlotsAndSnapshotCapacityIsAtomic()
        {
            var inventory = new InventoryModel(); Assert.That(inventory.Expand(), Is.True);
            Assert.That(inventory.Expand(), Is.False);
            inventory.ApplySnapshot(5, new[] { "a", "b", "c", "d", "e" });
            Assert.Throws<ArgumentException>(() => inventory.ApplySnapshot(3, new[] { "a", "b", "c", "d" }));
            Assert.That(inventory.Items.Count, Is.EqualTo(5)); Assert.That(inventory.Capacity, Is.EqualTo(5));
            inventory.ApplySnapshot(3, new[] { "battery" }); Assert.That(inventory.Capacity, Is.EqualTo(3));
        }
        [Test] public void MicrophoneMetricsAreBoundedAndRateLimited()
        {
            Assert.That(MicrophoneEnvelope.Rms(Enumerable.Repeat(.5f, 512).ToArray()), Is.EqualTo(.5f).Within(.001));
            var envelope = new MicrophoneEnvelope(); envelope.Calibrate(.01f);
            Assert.That(envelope.Sample(.5f, 0, 12, out byte first), Is.True);
            Assert.That(first, Is.GreaterThan(0)); Assert.That(envelope.Sample(1, .01, 12, out _), Is.False);
            Assert.That(envelope.Sample(float.NaN, .2, 12, out _), Is.False);
            Assert.That(envelope.Sample(1, .2, 12, out byte next), Is.True); Assert.That(next, Is.LessThanOrEqualTo(255));
        }
        [Test] public void GameplayActionBurstCannotBypassServerBudget()
        {
            var budget = new ActionBudget();
            for (int i = 0; i < 8; i++) Assert.That(budget.TrySpend(0), Is.True);
            Assert.That(budget.TrySpend(0), Is.False); Assert.That(budget.TrySpend(1), Is.True);
            Assert.That(budget.TrySpend(.5), Is.False);
        }

        [Test] public void AllSevenChaptersRequireFreshObjectivesAndCarryStoryDecisions()
        {
            var campaign = new CampaignModel();
            for (int chapter = 0; chapter < 7; chapter++)
            {
                Assert.That((int)campaign.Chapter, Is.EqualTo(chapter));
                Assert.That(campaign.Advance(), Is.False);
                campaign.Collected("battery"); campaign.ReceiveSignal(); campaign.Collected("document");
                if (chapter == 1 || chapter == 4 || chapter == 5) campaign.ApplyChoice(true);
                Assert.That(campaign.Advance(), Is.True);
            }
            Assert.That(campaign.Ending, Is.EqualTo(Ending.Hope));
        }
        [Test] public void ExhaustedSprintMustRecoverBeforeRestarting()
        {
            var stamina = new StaminaModel();
            for (int i = 0; i < 12; i++) stamina.Tick(1, true);
            Assert.That(stamina.CanSprint, Is.False);
            stamina.Tick(1, false); stamina.Tick(1, false); stamina.Tick(.1f, false);
            Assert.That(stamina.CanSprint, Is.False);
            for (int i = 0; i < 4; i++) stamina.Tick(1, false);
            Assert.That(stamina.CanSprint, Is.True);
        }
        [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)] [TestCase(-1)]
        public void InvalidVoteClockCannotCloseAnUnfinishedVote(double time)
        {
            var vote = new VoteModel(new ulong[] { 1, 2 }, 0); vote.Cast(1, true);
            Assert.That(vote.Tick(time), Is.False); Assert.That(vote.Resolved, Is.False);
        }
        [Test] public void InfiniteRecoveryCannotFreezeTheCreature()
        {
            var mind = new CreatureMind(DifficultyRules.For(Difficulty.Normal));
            mind.Recover(0, double.PositiveInfinity); Assert.That(mind.State, Is.EqualTo(CreatureState.Patrol));
        }
        [TestCase(Difficulty.Easy)] [TestCase(Difficulty.Normal)] [TestCase(Difficulty.Hard)] [TestCase(Difficulty.Nightmare)]
        public void CreatureRemainsFasterThanAnUnencumberedRunningPlayer(Difficulty difficulty)
        { Assert.That(DifficultyRules.For(difficulty).ChaseSpeed, Is.GreaterThan(5.5f)); }
    }
}
