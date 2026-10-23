using System;
using NUnit.Framework;
using Silence.Core;

namespace Silence.Tests
{
    public sealed class NetworkRulesTests
    {
        [Test]
        public void LobbyReservesFourDistinctSlotsAndRejectsFifth()
        {
            var lobby = new SessionAdmission();
            for (ulong i = 0; i < 4; i++)
            {
                Assert.That(lobby.TryJoin(i, SessionAdmission.Protocol, out int slot, out _), Is.True);
                Assert.That(slot, Is.EqualTo((int)i));
            }
            Assert.That(lobby.TryJoin(5, SessionAdmission.Protocol, out _, out _), Is.False);
            Assert.That(lobby.Count, Is.EqualTo(4));
        }

        [Test]
        public void DisconnectReleasesSlotAndRoundBlocksRejoin()
        {
            var lobby = new SessionAdmission();
            lobby.TryJoin(10, SessionAdmission.Protocol, out _, out _);
            lobby.TryJoin(11, SessionAdmission.Protocol, out _, out _);
            Assert.That(lobby.Remove(10), Is.True);
            Assert.That(lobby.TryJoin(12, SessionAdmission.Protocol, out int slot, out _), Is.True);
            Assert.That(slot, Is.Zero);
            lobby.StartRound();
            lobby.Remove(12);
            Assert.That(lobby.TryJoin(13, SessionAdmission.Protocol, out _, out _), Is.False);
        }

        [Test]
        public void DuplicateAndDifferentProtocolDoNotReserveSlots()
        {
            var lobby = new SessionAdmission();
            Assert.That(lobby.TryJoin(1, "old", out _, out _), Is.False);
            Assert.That(lobby.Count, Is.Zero);
            lobby.TryJoin(1, SessionAdmission.Protocol, out _, out _);
            Assert.That(lobby.TryJoin(1, SessionAdmission.Protocol, out _, out _), Is.False);
            Assert.That(lobby.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => new SessionAdmission().StartRound());
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void NonFiniteInputIsRejected(float value)
        {
            Assert.That(new PlayerCommand { X = value }.TrySanitize(out _), Is.False);
            Assert.That(new PlayerCommand { Z = value }.TrySanitize(out _), Is.False);
            Assert.That(new PlayerCommand { Yaw = value }.TrySanitize(out _), Is.False);
            Assert.That(new PlayerCommand { Pitch = value }.TrySanitize(out _), Is.False);
        }

        [Test]
        public void MovementIsBoundedAndDiagonalNormalized()
        {
            Assert.That(new PlayerCommand { X = float.MaxValue, Z = float.MaxValue,
                Yaw = -721, Pitch = 10000 }.TrySanitize(out var safe), Is.True);
            Assert.That(safe.X * safe.X + safe.Z * safe.Z, Is.EqualTo(1).Within(0.00001));
            Assert.That(safe.Pitch, Is.EqualTo(85));
            Assert.That(safe.Yaw, Is.EqualTo(-1));
        }

        [Test]
        public void InboxRejectsReplayAndAllowsSequenceWrap()
        {
            var inbox = new CommandInbox();
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = uint.MaxValue, X = 1 }, 0), Is.True);
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = uint.MaxValue, X = -1 }, .01), Is.False);
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = 0, X = .5f }, .02), Is.True);
            Assert.That(inbox.Consume(.03, out _).X, Is.EqualTo(.5f));
        }

        [Test]
        public void InputTimeoutStopsMovementAndJumpIsConsumedOnce()
        {
            var inbox = new CommandInbox();
            inbox.TryReceive(new PlayerCommand { X = 1, JumpSequence = 1, Crouch = true, Flashlight = true }, 0);
            inbox.Consume(.01, out bool first);
            inbox.TryReceive(new PlayerCommand { Sequence = 1, X = 1, JumpSequence = 1, Crouch = true, Flashlight = true }, .02);
            inbox.Consume(.03, out bool duplicate);
            var stale = inbox.Consume(1, out bool expired);
            Assert.That(first, Is.True);
            Assert.That(duplicate || expired, Is.False);
            Assert.That(stale.X, Is.Zero);
            Assert.That(stale.Crouch && stale.Flashlight, Is.True);
        }

        [Test]
        public void PacketBurstIsLimitedWithoutAdvancingWorldTime()
        {
            var inbox = new CommandInbox();
            for (uint i = 0; i < 12; i++) Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = i }, 0), Is.True);
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = 12 }, 0), Is.False);
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = 13 }, 1), Is.True);
            Assert.That(inbox.TryReceive(new PlayerCommand { Sequence = 14 }, .5), Is.False);
        }

        [Test]
        public void InteractionCooldownConsumesRejectedSequence()
        {
            var gate = new InteractionGate();
            Assert.That(gate.TryBegin(1, 0), Is.True);
            Assert.That(gate.TryBegin(2, .01), Is.False);
            Assert.That(gate.TryBegin(2, 1), Is.False);
            Assert.That(gate.TryBegin(3, 1), Is.True);
            Assert.That(gate.TryBegin(1, 2), Is.False);
        }

        [Test]
        public void InvalidSnapshotPreservesPreviousInventory()
        {
            var inventory = new InventoryModel();
            inventory.TryAdd("battery");
            Assert.Throws<ArgumentException>(() => inventory.ReplaceSnapshot(new[] { "medicine", "" }));
            Assert.Throws<ArgumentException>(() => inventory.ReplaceSnapshot(new[] { "a", "b", "c", "d" }));
            Assert.That(inventory.Items, Is.EqualTo(new[] { "battery" }));
            inventory.ReplaceSnapshot(new[] { "document", "battery", "battery" });
            Assert.That(inventory.Items, Is.EqualTo(new[] { "document", "battery", "battery" }));
        }
    }
}
