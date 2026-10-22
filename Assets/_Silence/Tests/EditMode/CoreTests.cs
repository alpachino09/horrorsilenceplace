using System;
using System.Collections.Generic;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class InventoryTests
    {
        [Test]
        public void FourthItemIsRejectedWithoutLosingExistingItems()
        {
            var inventory = new InventoryModel();
            Assert.That(inventory.Capacity, Is.EqualTo(3));
            Assert.That(inventory.TryAdd("battery"), Is.True);
            Assert.That(inventory.TryAdd("medicine"), Is.True);
            Assert.That(inventory.TryAdd("document"), Is.True);
            Assert.That(inventory.TryAdd("fourth"), Is.False);
            Assert.That(inventory.Items, Is.EqualTo(new[] { "battery", "medicine", "document" }));
        }
        [Test]
        public void RemovingAnItemFreesExactlyOneSlot()
        {
            var inventory = new InventoryModel(1);
            inventory.TryAdd("battery");
            Assert.That(inventory.TryRemoveAt(0, out string removed), Is.True);
            Assert.That(removed, Is.EqualTo("battery"));
            Assert.That(inventory.TryAdd("medicine"), Is.True);
            Assert.That(inventory.TryAdd("document"), Is.False);
        }
        [TestCase(-1)]
        [TestCase(0)]
        public void InvalidCapacityIsRejected(int capacity) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new InventoryModel(capacity));
        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidItemCannotConsumeASlot(string id)
        {
            var inventory = new InventoryModel();
            Assert.Throws<ArgumentException>(() => inventory.TryAdd(id));
            Assert.That(inventory.Items.Count, Is.Zero);
        }
        [TestCase(-1)]
        [TestCase(1)]
        public void InvalidRemovalDoesNotModifyInventory(int index)
        {
            var inventory = new InventoryModel();
            inventory.TryAdd("battery");
            Assert.That(inventory.TryRemoveAt(index, out string removed), Is.False);
            Assert.That(removed, Is.Null);
            Assert.That(inventory.Items, Is.EqualTo(new[] { "battery" }));
        }
        [Test]
        public void TwoBatteriesUseTwoSlots()
        {
            var inventory = new InventoryModel();
            inventory.TryAdd("battery"); inventory.TryAdd("battery");
            Assert.That(inventory.Items.Count, Is.EqualTo(2));
        }
        [Test]
        public void PublicCollectionCannotBypassCapacity()
        {
            var inventory = new InventoryModel();
            Assert.Throws<NotSupportedException>(() => ((IList<string>)inventory.Items).Add("injected"));
        }
    }

    public sealed class NoiseMathTests
    {
        [TestCase(0f, 1f)]
        [TestCase(5f, 0.25f)]
        [TestCase(10f, 0f)]
        [TestCase(15f, 0f)]
        public void RangeEnvelopeHasExpectedEndpoints(float distance, float expected) =>
            Assert.That(NoiseMath.PerceivedIntensity(1, distance, 10, 0, 0), Is.EqualTo(expected).Within(0.00001));
        [Test]
        public void ObstructionAndMaskingReduceIntensityIndependently()
        {
            float dry = NoiseMath.PerceivedIntensity(0.8f, 2, 10, 0, 0);
            Assert.That(NoiseMath.PerceivedIntensity(0.8f, 2, 10, 0.5f, 0), Is.EqualTo(dry * 0.5f).Within(0.00001));
            Assert.That(NoiseMath.PerceivedIntensity(0.8f, 2, 10, 0.5f, 0.5f), Is.EqualTo(dry * 0.25f).Within(0.00001));
        }
        [Test]
        public void InvalidEnvelopeFactorsCannotAmplifyOrInvertNoise()
        {
            Assert.That(NoiseMath.PerceivedIntensity(5, 0, 10, -2, -1), Is.EqualTo(1));
            Assert.That(NoiseMath.PerceivedIntensity(-2, 0, 10, 0, 0), Is.Zero);
            Assert.That(NoiseMath.PerceivedIntensity(1, 0, 10, 3, 0), Is.Zero);
        }
        [Test]
        public void IncreasingDistanceNeverIncreasesIntensity()
        {
            float previous = 1f;
            for (int i = 0; i <= 120; i++)
            {
                float current = NoiseMath.PerceivedIntensity(1, i / 10f, 10, 0, 0);
                Assert.That(current, Is.LessThanOrEqualTo(previous));
                previous = current;
            }
        }
        [Test]
        public void AmbientMaskingCannotCompletelySilenceANearbySource()
        {
            Assert.That(NoiseMath.PerceivedIntensity(1, 0, 10, 0, 1), Is.EqualTo(0.15f).Within(0.00001));
            Assert.That(NoiseMath.PerceivedIntensity(1, 0, 10, 0, 100), Is.GreaterThan(0f));
        }
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidDistanceIsRejected(float distance) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseMath.PerceivedIntensity(1, distance, 10, 0, 0));
        [TestCase(0f)]
        [TestCase(-10f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRangeIsRejected(float range) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseMath.PerceivedIntensity(1, 0, range, 0, 0));
        [Test]
        public void NonfiniteFactorsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseMath.PerceivedIntensity(float.NaN, 0, 10, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseMath.PerceivedIntensity(1, 0, 10, float.PositiveInfinity, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => NoiseMath.PerceivedIntensity(1, 0, 10, 0, float.NaN));
        }
    }

    public sealed class InteractionRulesTests
    {
        [TestCase(true, true, true, 2f, true, true)]
        [TestCase(true, true, true, 2.5f, true, true)]
        [TestCase(false, true, true, 1f, true, false)]
        [TestCase(true, false, true, 1f, true, false)]
        [TestCase(true, true, false, 1f, true, false)]
        [TestCase(true, true, true, 1f, false, false)]
        [TestCase(true, true, true, 2.51f, true, false)]
        [TestCase(true, true, true, -1f, true, false)]
        [TestCase(true, true, true, float.NaN, true, false)]
        [TestCase(true, true, true, float.PositiveInfinity, true, false)]
        public void AuthorityIdentityRangeAndVisibilityAreAllRequired(bool authority,
            bool actor, bool target, float distance, bool clear, bool expected) =>
            Assert.That(InteractionRules.CanApply(authority, actor, target, distance, 2.5f, clear), Is.EqualTo(expected));
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRangeCannotAuthorizeInteraction(float range) =>
            Assert.That(InteractionRules.CanApply(true, true, true, 0, range, true), Is.False);
    }
}
