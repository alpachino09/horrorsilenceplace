using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Silence.Interactions;
using Silence.Networking;
using Silence.Player;

namespace Silence.Tests
{
    public sealed class FarmSmokeTests
    {
        private const string FarmPath = "Assets/_Silence/Generated/Sprint01/Farm_Prototype.unity";
        private PlayerContext player;
        private OfflineWorldAuthority authority;

        [UnitySetUp]
        public IEnumerator LoadFreshFarm()
        {
            Assert.That(Application.CanStreamedLevelBeLoaded(FarmPath), Is.True,
                "Run SILENCE/01 Generate Farm Prototype and use the global Scene List in Build Profiles.");
            AsyncOperation load = SceneManager.LoadSceneAsync(FarmPath);
            float deadline = Time.realtimeSinceStartup + 30f;
            while (!load.isDone && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(load.isDone, Is.True, "Farm load timed out.");
            yield return null;
            player = Object.FindAnyObjectByType<PlayerContext>();
            authority = Object.FindAnyObjectByType<OfflineWorldAuthority>();
            Assert.That(player, Is.Not.Null);
            Assert.That(authority, Is.Not.Null);
            PlayerLook.Capture(false);
        }

        [Test]
        public void SceneHasOneOwnedCameraAndAudioListener()
        {
            Assert.That(player.IsLocalOwner, Is.True);
            Assert.That(SceneQuery.All<Camera>()
                .Count(c => c.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(SceneQuery.All<AudioListener>()
                .Count(l => l.isActiveAndEnabled), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PlayerLandsOnGround()
        {
            yield return new WaitForSeconds(1f);
            Assert.That(player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(player.transform.position.y, Is.InRange(-0.05f, 0.2f));
        }

        [Test]
        public void WalkRunAndDiagonalSpeedUseTheConfiguredEnvelope()
        {
            var motor = player.GetComponent<FirstPersonMotor>();
            motor.enabled = false;
            float Travel(Vector2 axis, bool run)
            {
                motor.Respawn();
                Vector3 start = player.transform.position;
                for (int i = 0; i < 60; i++) motor.Simulate(axis, run, false, false, 1f / 60f);
                Vector3 delta = player.transform.position - start;
                return new Vector2(delta.x, delta.z).magnitude;
            }
            Assert.That(Travel(Vector2.up, false), Is.EqualTo(player.Settings.walkSpeed).Within(0.15f));
            Assert.That(Travel(Vector2.up, true), Is.EqualTo(player.Settings.runSpeed).Within(0.15f));
            Assert.That(Travel(Vector2.one, false), Is.EqualTo(player.Settings.walkSpeed).Within(0.15f));
        }

        [Test]
        public void LowCeilingBlocksStandingButNotCrouching()
        {
            var motor = player.GetComponent<FirstPersonMotor>();
            motor.enabled = false;
            Teleport(new Vector3(5, 0.06f, -7));
            motor.Simulate(Vector2.zero, false, true, false, 0.02f);
            Assert.That(motor.IsCrouched, Is.True);
            motor.Simulate(Vector2.zero, false, false, false, 0.02f);
            Assert.That(motor.IsCrouched, Is.True, "Must not stand into the canopy.");
            Teleport(new Vector3(0, 0.06f, -7));
            motor.Simulate(Vector2.zero, false, false, false, 0.02f);
            Assert.That(motor.IsCrouched, Is.False);
        }

        [Test]
        public void ForeignActorAndOutOfRangeRequestsAreRejected()
        {
            var target = Object.FindAnyObjectByType<ItemPickup>();
            Assert.That(authority.RequestInteraction(player, target), Is.False);
            var foreign = new GameObject("UnregisteredActor");
            try
            {
                Assert.That(authority.RequestInteraction(foreign.AddComponent<PlayerContext>(), target), Is.False);
                Assert.That(player.Inventory.Items.Count, Is.Zero);
            }
            finally { Object.DestroyImmediate(foreign); }
        }

        [UnityTest]
        public IEnumerator PickupCannotDuplicateOrExceedThreeSlots()
        {
            var items = SceneQuery.All<ItemPickup>()
                .OrderBy(p => p.name).ToArray();
            Assert.That(items.Length, Is.EqualTo(4));
            for (int i = 0; i < items.Length; i++)
            {
                AimAt(items[i]);
                yield return new WaitForSeconds(0.2f);
                Assert.That(authority.RequestInteraction(player, items[i]), Is.EqualTo(i < 3));
                Assert.That(items[i].Consumed, Is.EqualTo(i < 3));
                Assert.That(items[i].GetComponent<Collider>().enabled, Is.EqualTo(i >= 3));
                Assert.That(items[i].GetComponent<Renderer>().enabled, Is.EqualTo(i >= 3));
                Assert.That(items[i].gameObject.activeSelf, Is.True, "Entity must survive for replication.");
                if (i == 0)
                {
                    yield return new WaitForSeconds(0.2f);
                    Assert.That(authority.RequestInteraction(player, items[i]), Is.False);
                    Assert.That(player.Inventory.Items.Count, Is.EqualTo(1));
                }
            }
            Assert.That(player.Inventory.Items.Count, Is.EqualTo(3));
        }

        [Test]
        public void WallBlocksInteractionAndRemovingItRestoresAccess()
        {
            var item = Object.FindAnyObjectByType<ItemPickup>();
            AimAt(item);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.transform.position = (player.View.transform.position + item.transform.position) / 2f;
                wall.transform.localScale = new Vector3(1.2f, 2, 0.15f);
                Physics.SyncTransforms();
                Assert.That(authority.RequestInteraction(player, item), Is.False);
                Assert.That(item.gameObject.activeSelf, Is.True);
                Assert.That(item.Consumed, Is.False);
            }
            finally { Object.DestroyImmediate(wall); }
            Physics.SyncTransforms();
            Assert.That(authority.RequestInteraction(player, item), Is.True);
        }

        private void AimAt(ItemPickup item)
        {
            Vector3 point = item.transform.position;
            Teleport(new Vector3(point.x, 0.1f, point.z - 1.3f));
            player.View.transform.LookAt(point);
            Physics.SyncTransforms();
        }
        private void Teleport(Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
