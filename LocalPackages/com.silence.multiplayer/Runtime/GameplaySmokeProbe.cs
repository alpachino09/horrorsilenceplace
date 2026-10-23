using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Silence.Core;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Interactions;
using Silence.Player;

namespace Silence.Multiplayer
{
    // Explicit development-only checks. No passing result exists until Unity actually executes this coroutine.
    public sealed class GameplaySmokeProbe : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable] private sealed class Entry { public string name; public bool passed; }
        [Serializable] private sealed class Report
        {
            public string runId, utc, unity, scope = "Native solo gameplay checks; not a multiplayer transport test";
            public bool completed;
            public List<Entry> checks = new List<Entry>();
            public string error;
        }
        private Report report;
        private string output;
        private IEnumerator Start()
        {
            output = Environment.GetEnvironmentVariable("SILENCE_GAMEPLAY_CHECKS");
            if (!string.IsNullOrEmpty(output)) Environment.SetEnvironmentVariable("SILENCE_GAMEPLAY_CHECKS", null);
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--silence-gameplay-checks") output = args[i + 1];
            if (string.IsNullOrEmpty(output)) yield break;
            report = new Report { runId = Guid.NewGuid().ToString("N"), utc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            IEnumerator test = Run();
            while (true)
            {
                bool more;
                try { more = test.MoveNext(); }
                catch (Exception exception) { report.error = exception.ToString(); break; }
                if (!more) { report.completed = true; break; }
                yield return test.Current;
            }
            try
            {
                string path = Path.GetFullPath(output); Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                int failures = report.checks.Count(c => !c.passed) + (!report.completed ? 1 : 0);
                string message = "SILENCE Gameplay checks — Failures: " + failures + " | " + path;
                if (failures == 0) Debug.Log(message); else Debug.LogError(message + "\n" + report.error);
            }
            catch (Exception exception) { Debug.LogError("SILENCE could not write gameplay evidence: " + exception); }
            PlayerLook.Capture(false);
        }
        private void Check(bool passed, string name)
        {
            report.checks.Add(new Entry { name = name, passed = passed });
            if (!passed) throw new InvalidOperationException(name);
        }
        private IEnumerator Run()
        {
            var session = GetComponent<NetworkSession>(); var game = session.World.GetComponent<GameplayDirector>();
            Check(game != null && game.Settings.chapters.Length == 7, "Campaign configured with seven chapters");
            game.SaveEnabled = false; game.ContinueSave = false;
            Check(session.StartConnection(true, "127.0.0.1", 17777), "Solo host starts");
            float timeout = Time.unscaledTime + 15;
            while ((session.LocalPlayer == null || !session.World.IsSpawned) && Time.unscaledTime < timeout) yield return null;
            Check(session.LocalPlayer != null && session.World.IsSpawned, "Network world and local player spawned");
            var actor = session.LocalPlayer.Context;
            session.World.BeginRound(); yield return null;
            Check(game.Phase == RoundPhase.Playing && actor.Game == game, "Round starts and actor is registered");
            Check(SceneQuery.All<Camera>().Count(c => c.isActiveAndEnabled) == 1 &&
                SceneQuery.All<AudioListener>().Count(c => c.isActiveAndEnabled) == 1, "One active camera/listener");
            var agent = game.Creature.GetComponent<UnityEngine.AI.NavMeshAgent>();
            Check(agent.enabled && agent.isOnNavMesh, "Creature is on baked navigation");
            Vector3 noise = game.Creature.transform.position + game.Creature.transform.forward * 2;
            game.Emit(actor, noise, 1, 30, Silence.SoundSimulation.NoiseKind.Impact);
            Check(game.Creature.State == CreatureState.Chase || game.Creature.State == CreatureState.Investigate, "Creature reacts to a simulated noise");
            game.Creature.enabled = false; // Keep subsequent mission checks deterministic; this does not test evasion or balance.
            var door = SceneQuery.All<ManualDoor>().First();
            game.Action(actor, ActorAction.DragDoor, 0, 1, door);
            float until = Time.unscaledTime + .1f; while (Time.unscaledTime < until) yield return null;
            Check(door.Angle > 0, "Manual door moves on the authority"); door.ResetDoor();
            var prop = SceneQuery.All<CarryableProp>().First(p => Vector3.Distance(p.transform.position, actor.transform.position) < 50);
            Check(game.Grab(actor, prop) && actor.Inventory.Items.Contains("held_prop"), "Prop occupies an inventory slot");
            Check(game.Action(actor, ActorAction.Throw, 0, .8f, null) && actor.HeldProp == null && !prop.GetComponent<Rigidbody>().isKinematic,
                "Throw releases a dynamic rigidbody and frees the slot");
            Check(game.Damage(actor) && actor.Vitals.State == VitalState.Wounded, "First hit wounds");
            actor.Inventory.TryAdd("medicine");
            Check(game.Action(actor, ActorAction.UseSlot, actor.Inventory.Items.Count - 1, 0, null) && actor.Vitals.State == VitalState.Healthy,
                "Medicine heals and is consumed");
            until = Time.unscaledTime + game.Rules.AttackGrace + .1f; while (Time.unscaledTime < until) yield return null;
            Check(game.Damage(actor), "Actor can be wounded again after grace");
            until = Time.unscaledTime + game.Rules.AttackGrace + .1f; while (Time.unscaledTime < until) yield return null;
            Check(game.Damage(actor) && !actor.Vitals.Alive, "Second unhealed hit kills");
            yield return null; Check(game.Phase == RoundPhase.Wipe, "Full party death starts a chapter restart");
            until = Time.unscaledTime + 4.2f; while (Time.unscaledTime < until) yield return null;
            Check(game.Phase == RoundPhase.Playing && actor.Vitals.State == VitalState.Healthy, "Chapter restart restores actor health");
            for (int chapter = 0; chapter < 7; chapter++)
            {
                Check((int)game.Campaign.Chapter == chapter && !game.Campaign.CanExit, "Chapter " + chapter + " starts with fresh objectives");
                if (chapter > 0) Check(agent.isOnNavMesh, "Chapter " + chapter + " creature navigation exists");
                ItemPickup Find(string id) => SceneQuery.All<ItemPickup>().First(p => p.Definition.id == id && !p.Consumed &&
                    Vector3.Distance(p.transform.position, game.Chapter.center) < 55);
                Check(Find("battery").Apply(actor), "Chapter " + chapter + " battery can be collected");
                Check(game.Radio(actor), "Chapter " + chapter + " radio consumes a battery");
                Check(Find("document").Apply(actor), "Chapter " + chapter + " document can be collected");
                Check(game.Exit(actor), "Chapter " + chapter + " completed objectives allow exit");
                if (game.Phase == RoundPhase.Voting) Check(game.Action(actor, ActorAction.Vote, 1, 0, null), "Chapter " + chapter + " story vote accepted");
                until = Time.unscaledTime + 2.5f; while (Time.unscaledTime < until) yield return null;
                if (chapter < 6) Check(Vector3.Distance(actor.transform.position, game.Chapter.center) < 40, "Chapter " + chapter + " transition warps player");
            }
            Check(game.Phase == RoundPhase.Completed && game.Campaign.Ending == Ending.Hope, "Seven-chapter path reaches the configured ending");
        }
#endif
    }
}
