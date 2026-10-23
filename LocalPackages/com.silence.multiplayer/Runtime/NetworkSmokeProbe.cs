using UnityEngine;
using Silence.Interactions;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Silence.Core;
using Silence.Player;
#endif

namespace Silence.Multiplayer
{
    // In release builds this is an inert component; no automation or warping API is compiled.
    public sealed class NetworkSmokeProbe : MonoBehaviour
    {
        [SerializeField] private ItemPickup contestedItem;
        public void Configure(ItemPickup item) => contestedItem = item;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        private sealed class Evidence
        {
            public string runId, role, unity, error;
            public bool passed, itemConsumed, cleanupConfirmed;
            public int expectedPlayers, observedPlayers, activeCameras, activeListeners,
                authoritativeItems = -1, localItems, inputOwners;
            public float movementMeters;
            public uint poseTicks;
        }
        private bool enabledByArgs, host, started, warped, requested, finished, capturedInventory;
        private string output, runId;
        private int expected = 2, maximumObserved, serverInventory = -1;
        private float launchedAt, roundAt, maxMovement;
        private Vector3 initialPosition;
        private uint firstPoseTick;
        private bool cleanup;
        internal void Abort(string reason) { if (enabledByArgs) Finish(reason); }

        private void Start()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = Argument(args, "--silence-role");
            if (role == null) { enabled = false; return; }
            host = role == "host";
            output = Argument(args, "--silence-evidence");
            runId = Argument(args, "--silence-run");
            if ((role != "host" && role != "client") || string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(runId))
            { Debug.LogError("SILENCE smoke: missing role/evidence/run arguments."); Application.Quit(2); return; }
            if (!int.TryParse(Argument(args, "--silence-players"), out expected) || expected < 1 || expected > 4)
            { Debug.LogError("SILENCE smoke: players must be 1–4."); Application.Quit(2); return; }
            ushort port = 7777;
            string portArg = Argument(args, "--silence-port");
            if (portArg != null && (!ushort.TryParse(portArg, out port) || port == 0))
            { Application.Quit(2); return; }
            enabledByArgs = true;
            launchedAt = Time.unscaledTime;
            if (contestedItem == null) { Finish("Smoke fixture is missing."); return; }
            NetworkSession.Instance.StartConnection(host, "127.0.0.1", port);
        }

        private void Update()
        {
            if (!enabledByArgs || finished) return;
            var session = NetworkSession.Instance;
            if (session == null || session.Returning) { Finish("Session stopped before verification."); return; }
            var players = SceneQuery.All<NetworkPlayer>()
                .Where(p => p.IsSpawned).ToArray();
            maximumObserved = Math.Max(maximumObserved, players.Length);
            if (host && !session.World.Running && players.Length == expected) session.World.BeginRound();
            var local = session.LocalPlayer;
            if (session.World.Running && local != null)
            {
                if (!started)
                {
                    started = true; roundAt = Time.unscaledTime;
                    initialPosition = local.transform.position; firstPoseTick = local.ReceivedPoseTick;
                }
                float elapsed = Time.unscaledTime - roundAt;
                if (elapsed < 4f)
                {
                    local.SetAutomationInput(new PlayerCommand { X = 1, Yaw = 0, Flashlight = true });
                    maxMovement = Mathf.Max(maxMovement, Vector3.Distance(initialPosition, local.transform.position));
                }
                else
                {
                    Vector3 direction = contestedItem.transform.position - local.Context.View.transform.position;
                    Quaternion look = Quaternion.LookRotation(direction);
                    float pitch = Mathf.DeltaAngle(0, look.eulerAngles.x);
                    local.SetAutomationInput(new PlayerCommand { Yaw = look.eulerAngles.y, Pitch = pitch, Flashlight = true });
                }
                if (host && elapsed > 6 && !warped)
                {
                    warped = true;
                    int index = 0;
                    foreach (var player in players.OrderBy(p => p.OwnerClientId))
                        player.WarpForSmoke(new Vector3(79.1f + index++ * .6f, .12f, -1.5f));
                }
                if (elapsed > 9 && !requested)
                {
                    requested = true;
                    local.RequestInteraction(contestedItem.GetComponent<NetworkInteractable>().NetworkObjectId);
                }
                if (host && elapsed > 12 && !capturedInventory)
                {
                    capturedInventory = true;
                    serverInventory = players.Sum(p => p.Context.Inventory.Items.Count);
                }
                if (!host && elapsed > 16) Finish(null);
                if (host && elapsed > 18 && expected > 1 && players.Length == 1 && session.Admission.Count == 1)
                    cleanup = true;
                if (host && elapsed > (expected > 1 ? 22 : 16))
                    Finish(expected > 1 && !cleanup ? "Disconnected player/slot cleanup was not observed." : null);
            }
            if (Time.unscaledTime - launchedAt > 55) Finish("Timed out before all required checks completed.");
        }

        private void Finish(string error)
        {
            if (finished) return;
            finished = true;
            var session = NetworkSession.Instance;
            var local = session != null ? session.LocalPlayer : null;
            var evidence = new Evidence
            {
                runId = runId, role = host ? "host" : "client", unity = Application.unityVersion,
                error = error ?? "", expectedPlayers = expected, observedPlayers = maximumObserved,
                movementMeters = maxMovement, authoritativeItems = serverInventory,
                localItems = local != null ? local.Context.Inventory.Items.Count : -1,
                poseTicks = local != null ? local.ReceivedPoseTick - firstPoseTick : 0,
                itemConsumed = contestedItem != null && contestedItem.Consumed, cleanupConfirmed = cleanup
            };
            evidence.activeCameras = SceneQuery.All<Camera>()
                .Count(c => c.enabled);
            evidence.activeListeners = SceneQuery.All<AudioListener>()
                .Count(l => l.enabled);
            evidence.inputOwners = SceneQuery.All<PlayerContext>()
                .Count(p => p.Input.enabled);
            evidence.passed = error == null && maximumObserved == expected && maxMovement > 1f && evidence.poseTicks > 30 &&
                evidence.activeCameras == 1 && evidence.activeListeners == 1 && evidence.inputOwners == 1 &&
                evidence.itemConsumed && (!host || serverInventory == 1);
            if (!evidence.passed && string.IsNullOrEmpty(evidence.error)) evidence.error = "One or more network assertions failed.";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                File.WriteAllText(output, JsonUtility.ToJson(evidence, true));
            }
            catch (Exception exception) { Debug.LogException(exception); Application.Quit(2); return; }
            Debug.Log("SILENCE smoke: " + (evidence.passed ? "PASS" : "FAIL") + " evidence=" + output);
            Application.Quit(evidence.passed ? 0 : 1);
        }
        private static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
#endif
    }
}
