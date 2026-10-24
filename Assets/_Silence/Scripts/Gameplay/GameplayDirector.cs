using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Silence.Config;
using Silence.Core;
using Silence.Creatures;
using Silence.Interactions;
using Silence.Inventory;
using Silence.Player;
using Silence.Save;
using Silence.SoundSimulation;
using Silence.UI;

namespace Silence.Gameplay
{
    public interface IGameplayCommands
    {
        bool RequestAction(PlayerContext actor, ActorAction action, int slot, float amount, Interactable target = null);
    }

    [DefaultExecutionOrder(-100)]
    public sealed class GameplayDirector : MonoBehaviour
    {
        [SerializeField] private GameplaySettings settings;
        [SerializeField] private HearingCreature creature;
        private readonly List<PlayerContext> actors = new List<PlayerContext>();
        private readonly Dictionary<PlayerContext, string[]> checkpoint = new Dictionary<PlayerContext, string[]>();
        private readonly Dictionary<PlayerContext, int> checkpointPacks = new Dictionary<PlayerContext, int>();
        private readonly Dictionary<PlayerContext, int> checkpointCapacities = new Dictionary<PlayerContext, int>();
        private readonly Dictionary<PlayerContext, float> nextPain = new Dictionary<PlayerContext, float>();
        private ItemPickup[] pickups;
        private CarryableProp[] props;
        private ManualDoor[] doors;
        private NoiseSystem noise;
        private bool voteCompleted;
        private double phaseUntil;
        private VoteModel vote;
        public bool IsAuthority { get; private set; }
        public bool Started { get; private set; }
        public RoundPhase Phase { get; private set; }
        public CampaignModel Campaign { get; } = new CampaignModel();
        public IReadOnlyList<PlayerContext> Actors => actors;
        public GameplaySettings Settings => settings;
        public HearingCreature Creature => creature;
        public DifficultyRules Rules => DifficultyRules.For(settings.difficulty);
        public ChapterDefinition Chapter => settings.chapters[(int)Campaign.Chapter];
        public NoiseSystem Noise => noise;
        public float PhaseSeconds => Mathf.Max(0, (float)(phaseUntil - Time.unscaledTimeAsDouble));
        public int VoteCount => vote?.VotedCount ?? 0;
        public bool ContinueSave { get; set; }
        public bool SaveEnabled { get; set; } = true;
        public event Action StateChanged;
        public event Action ChapterReset;
        public string LastNote { get; private set; }

        public void Configure(GameplaySettings config, HearingCreature enemy) { settings = config; creature = enemy; }
        private void Awake() => noise = GetComponent<NoiseSystem>();
        public void SetAuthority(bool value)
        {
            IsAuthority = value;
            if (creature != null) creature.SetAuthority(value, this);
            foreach (var prop in SceneQuery.All<CarryableProp>()) prop.SetAuthority(value, this);
        }
        public void Register(PlayerContext actor, int sourceId)
        {
            if (actors.Contains(actor)) return;
            actors.Add(actor); actor.Game = this; actor.SourceId = sourceId;
        }
        public void Unregister(PlayerContext actor)
        {
            if (IsAuthority)
            {
                DropHeld(actor, 0); DropTeammate(actor);
                if (actor.CarriedBy != null) DropTeammate(actor.CarriedBy);
                for (int i = actor.Inventory.Items.Count - 1; i >= 0; i--) TryDropSlot(actor, i);
                vote?.Remove(unchecked((ulong)actor.SourceId));
            }
            actors.Remove(actor); checkpoint.Remove(actor); checkpointCapacities.Remove(actor); checkpointPacks.Remove(actor); nextPain.Remove(actor);
        }
        public bool CanAct(PlayerContext actor) => Started && Phase == RoundPhase.Playing && actor.Vitals.Alive;
        public void BeginRound()
        {
            if (!IsAuthority || Started || settings == null || settings.chapters == null || settings.chapters.Length != 7) return;
            pickups = SceneQuery.All<ItemPickup>(); props = SceneQuery.All<CarryableProp>(); doors = SceneQuery.All<ManualDoor>();
            foreach (var pickup in pickups) pickup.RememberSpawn();
            foreach (var prop in props) prop.SetAuthority(true, this);
            if (ContinueSave && CampaignSaveStore.TryLoad(out var save))
            {
                Campaign.Restore((ChapterId)save.chapter, save.helped, save.research);
                for (int i = 0; i < actors.Count && i < save.players.Length; i++)
                {
                    actors[i].Inventory.ApplySnapshot(save.players[i].capacity, save.players[i].items);
                    if (save.version >= 2 && save.players[i].batteryPackCapacity > 0 && actors[i].Inventory.BatteryPacksConfigured)
                        actors[i].Inventory.ApplyBatteryPacksSnapshot(actors[i].Inventory.BatteryPackCapacity, Mathf.Min(save.players[i].batteryPacks, actors[i].Inventory.BatteryPackCapacity));
                }
            }
            Started = true;
            CaptureCheckpoint(); StartChapter();
        }
        private void CaptureCheckpoint()
        {
            foreach (var actor in actors)
            {
                checkpoint[actor] = actor.Inventory.Items.Where(id => id != "held_prop" && id != "document").ToArray();
                checkpointCapacities[actor] = actor.Inventory.Capacity; checkpointPacks[actor] = actor.Inventory.BatteryPacks;
            }
        }
        private void StartChapter()
        {
            foreach (var actor in actors) { DropHeld(actor, 0); DropTeammate(actor); }
            Campaign.RestartChapter(); LastNote = null; nextPain.Clear(); voteCompleted = false; vote = null;
            Phase = RoundPhase.Playing;
            foreach (var pickup in pickups) pickup.ResetPickup();
            foreach (var prop in props) prop.ResetProp();
            foreach (var door in doors) door.ResetDoor();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                actor.Vitals.Reset(); actor.Stamina.Reset(); actor.FlashlightCharge = 1;
                actor.Inventory.ApplySnapshot(checkpointCapacities[actor], checkpoint[actor]);
                if (actor.Inventory.BatteryPacksConfigured) actor.Inventory.ApplyBatteryPacksSnapshot(actor.Inventory.BatteryPackCapacity,checkpointPacks[actor]);
                actor.Flashlight?.ResetForChapter();
                Warp(actor, Chapter.spawnPoints[i % Chapter.spawnPoints.Length]);
            }
            creature?.BeginChapter(Chapter, Rules);
            SaveCheckpoint(); ChapterReset?.Invoke(); StateChanged?.Invoke();
        }
        private void Update()
        {
            if (!IsAuthority || !Started) return;
            double now = Time.unscaledTimeAsDouble;
            if (Phase == RoundPhase.Wipe && now >= phaseUntil) { StartChapter(); return; }
            if (Phase == RoundPhase.Transition && now >= phaseUntil)
            {
                Campaign.Advance();
                if (Campaign.Ending != Ending.None) { Phase = RoundPhase.Completed; StateChanged?.Invoke(); return; }
                CaptureCheckpoint(); StartChapter(); return;
            }
            if (Phase == RoundPhase.Voting)
            {
                if (vote.Tick(now)) { Campaign.ApplyChoice(vote.Result); voteCompleted = true; StartTransition(); }
                return;
            }
            if (Phase != RoundPhase.Playing) return;
            if (actors.Count > 0 && actors.All(a => !a.Vitals.Alive))
            { Phase = RoundPhase.Wipe; phaseUntil = now + 4; StateChanged?.Invoke(); return; }
            foreach (var actor in actors)
            {
                actor.NoiseLevel = Mathf.Max(0, actor.NoiseLevel - Time.deltaTime * .65f);
                if (actor.CarriedBy != null) actor.transform.position = actor.CarriedBy.transform.position - actor.CarriedBy.transform.forward * .5f + Vector3.up * .8f;
                if (!actor.Vitals.Alive) continue;
                if (actor.Flashlight == null && actor.FlashlightOn && actor.FlashlightCharge > 0)
                    actor.FlashlightCharge = Mathf.Max(0, actor.FlashlightCharge - Time.deltaTime / Rules.BatterySeconds);
                if (actor.Vitals.State == VitalState.Wounded && (!nextPain.TryGetValue(actor, out float at) || Time.time >= at))
                { nextPain[actor] = Time.time + 9; Emit(actor, actor.transform.position + Vector3.up, .18f, 12, NoiseKind.Pain); }
                Vector3 relative = actor.transform.position - Chapter.center;
                if (Mathf.Abs(relative.x) > 58 || Mathf.Abs(relative.z) > 58 || relative.y < -6)
                    Warp(actor, Chapter.spawnPoints[0]);
            }
        }
        public void Collected(PlayerContext actor, ItemDefinition definition)
        {
            if (!IsAuthority) return;
            Campaign.Collected(definition.id);
            if (definition.id == "document")
            {
                LastNote = PrototypePreferences.English ? Chapter.noteEn : Chapter.noteRu;
                actor.ShowMessage(LastNote);
            }
            StateChanged?.Invoke();
        }
        public bool Radio(PlayerContext actor)
        {
            if (!CanAct(actor) || !IsAuthority) return false;
            if ((Campaign.Mission & MissionFlags.Signal) != 0) return true;
            var batteryOwner = actors.FirstOrDefault(a => a.Inventory.Items.Contains("battery"));
            if (batteryOwner == null) { actor.ShowMessage(Text("Нужна батарейка для радио.", "The radio needs a battery.")); return false; }
            batteryOwner.Inventory.TryConsume("battery"); Campaign.Collected("battery"); Campaign.ReceiveSignal();
            actor.ShowMessage(Text(Chapter.radioRu, Chapter.radioEn));
            Emit(actor, actor.transform.position + Vector3.up, .45f, 22, NoiseKind.Interaction);
            StateChanged?.Invoke(); return true;
        }
        public bool Exit(PlayerContext actor)
        {
            if (!IsAuthority || !CanAct(actor)) return false;
            if (!Campaign.CanExit)
            { actor.ShowMessage(Text("Сначала найдите батарейку, примите сигнал и заберите документ.", "Find a battery, receive the signal and recover the document first.")); return false; }
            if (Chapter.hasVote && !voteCompleted)
            {
                vote = new VoteModel(actors.Where(a => a.Vitals.Alive).Select(a => unchecked((ulong)a.SourceId)), Time.unscaledTimeAsDouble);
                phaseUntil = Time.unscaledTimeAsDouble + 20; Phase = RoundPhase.Voting; StateChanged?.Invoke(); return true;
            }
            StartTransition(); return true;
        }
        private void StartTransition()
        {
            foreach (var actor in actors)
            {
                DropHeld(actor, 0); DropTeammate(actor);
                while (actor.Inventory.TryConsume("document")) { }
            }
            Phase = RoundPhase.Transition; phaseUntil = Time.unscaledTimeAsDouble + 2;
            StateChanged?.Invoke();
        }
        public bool Action(PlayerContext actor, ActorAction action, int slot, float amount, Interactable target)
        {
            if (!IsAuthority || !actors.Contains(actor) || float.IsNaN(amount) || float.IsInfinity(amount)) return false;
            if (action == ActorAction.Vote && Phase == RoundPhase.Voting && actor.Vitals.Alive)
                return vote.Cast(unchecked((ulong)actor.SourceId), slot != 0);
            if (!CanAct(actor)) return false;
            switch (action)
            {
                case ActorAction.UseSlot: return UseSlot(actor, slot);
                case ActorAction.Drop: return actor.CarriedBy != null ? DropTeammate(actor.CarriedBy) : actor.HeldProp != null ? DropHeld(actor, 0) : actor.HeldTeammate != null ? DropTeammate(actor) : TryDropSlot(actor, slot);
                case ActorAction.Throw: return DropHeld(actor, Mathf.Clamp01(amount));
                case ActorAction.DragDoor:
                    if (target is ManualDoor door) { door.Drag(Mathf.Clamp(amount, -1, 1), actor); return true; }
                    return false;
                case ActorAction.Microphone:
                    Emit(actor, actor.View.transform.position, Mathf.Clamp01(amount), 20, NoiseKind.VoiceMetric); return true;
                default: return false;
            }
        }
        private bool UseSlot(PlayerContext actor, int slot)
        {
            if (slot < 0 || slot >= actor.Inventory.Items.Count) return false;
            string id = actor.Inventory.Items[slot];
            bool used = false;
            if (id == "battery" && actor.Flashlight != null)
            { actor.ShowMessage(Text("Эта батарейка для радио. Для фонарика используйте R и запасные комплекты.", "Radio battery. Reload flashlight with R and spare packs.")); return false; }
            if (id == "medicine") used = actor.Vitals.Heal();
            if (id == "battery" && actor.FlashlightCharge < .95f)
            {
                if ((Campaign.Mission & MissionFlags.Signal) == 0 && actors.Sum(a => a.Inventory.Items.Count(i => i == "battery")) <= 1)
                { actor.ShowMessage(Text("Оставьте одну батарейку для радио.", "Keep one battery for the radio.")); return false; }
                actor.FlashlightCharge = 1; used = true;
            }
            if (id == "rescue_kit")
            {
                var dead = actors.FirstOrDefault(a => !a.Vitals.Alive && Vector3.Distance(a.transform.position, actor.transform.position) < 2 && CanReach(actor, a));
                used = dead != null && dead.Vitals.Revive(Time.unscaledTimeAsDouble);
            }
            if (!used) return false;
            actor.Inventory.TryRemoveAt(slot, out _); StateChanged?.Invoke(); return true;
        }
        public bool Grab(PlayerContext actor, CarryableProp prop)
        {
            if (!IsAuthority || !CanAct(actor) || actor.CarriedBy != null || (actor.HeldProp != null || actor.HeldTeammate != null) || prop.Held || prop.Mass > settings.maximumCarryMass ||
                !actor.Inventory.TryAdd("held_prop")) return false;
            actor.HeldProp = prop; prop.Grab(actor); return true;
        }
        private bool DropHeld(PlayerContext actor, float charge)
        {
            if (actor.HeldProp == null) return false;
            actor.HeldProp.Release(charge); actor.HeldProp = null; actor.Inventory.TryConsume("held_prop"); return true;
        }
        private bool TryDropSlot(PlayerContext actor, int slot)
        {
            if (pickups == null || slot < 0 || slot >= actor.Inventory.Items.Count) return false;
            string id = actor.Inventory.Items[slot];
            var pickup = pickups.FirstOrDefault(p => p.Consumed && p.Definition != null && p.Definition.id == id);
            if (pickup == null) return false;
            Vector3 position = actor.transform.position + actor.transform.forward * .8f + Vector3.up * .3f;
            if (Physics.Linecast(actor.View.transform.position, position, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                position = hit.point - actor.transform.forward * .2f;
            actor.Inventory.TryRemoveAt(slot, out _); pickup.PlaceAt(position); return true;
        }
        public bool Assist(PlayerContext actor, PlayerContext teammate)
        {
            if (!IsAuthority || !CanAct(actor) || actor.CarriedBy != null || teammate == null || actor == teammate || !actors.Contains(teammate) || !CanReach(actor, teammate)) return false;
            if (!teammate.Vitals.Alive)
            {
                if (!actor.Inventory.Items.Contains("rescue_kit") || !teammate.Vitals.Revive(Time.unscaledTimeAsDouble)) return false;
                actor.Inventory.TryConsume("rescue_kit"); StateChanged?.Invoke(); return true;
            }
            if (teammate.Vitals.State != VitalState.Wounded) return false;
            if (actor.Inventory.Items.Contains("medicine") && teammate.Vitals.Heal())
            { actor.Inventory.TryConsume("medicine"); StateChanged?.Invoke(); return true; }
            if (actor.HeldTeammate == teammate) return DropTeammate(actor);
            if (actor.HeldProp != null || actor.HeldTeammate != null || teammate.CarriedBy != null || teammate.HeldTeammate != null || !actor.Inventory.TryAdd("held_prop")) return false;
            actor.HeldTeammate = teammate; teammate.CarriedBy = actor;
            teammate.GetComponent<CharacterController>().enabled = false; StateChanged?.Invoke(); return true;
        }
        private static bool CanReach(PlayerContext actor, PlayerContext teammate)
        {
            if (Vector3.Distance(actor.transform.position, teammate.transform.position) > 2.5f) return false;
            return !Physics.Linecast(actor.View.transform.position, teammate.View.transform.position,
                out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) || hit.collider.GetComponentInParent<PlayerContext>() == teammate;
        }
        private bool DropTeammate(PlayerContext actor)
        {
            if (actor.HeldTeammate == null) return false;
            var teammate = actor.HeldTeammate;
            teammate.CarriedBy = null; actor.HeldTeammate = null; actor.Inventory.TryConsume("held_prop");
            Vector3 position = actor.transform.position;
            foreach (Vector3 offset in new[] { actor.transform.right, -actor.transform.right, -actor.transform.forward })
            {
                Vector3 candidate = actor.transform.position + offset * 1.2f;
                if (!Physics.CheckCapsule(candidate + Vector3.up * .35f, candidate + Vector3.up * 1.4f,
                    .3f, ~0, QueryTriggerInteraction.Ignore)) { position = candidate; break; }
            }
            teammate.transform.position = position;
            teammate.GetComponent<CharacterController>().enabled = true; StateChanged?.Invoke(); return true;
        }
        public bool Damage(PlayerContext actor)
        {
            if (!IsAuthority || !CanAct(actor) || !actor.Vitals.TryHit(Time.unscaledTimeAsDouble, Rules.AttackGrace)) return false;
            DropHeld(actor, 0); DropTeammate(actor);
            if (actor.CarriedBy != null) DropTeammate(actor.CarriedBy);
            actor.PublishAnimationCue(AnimationCueKind.Hit, creature != null ? creature.transform.position : actor.transform.position + actor.transform.forward);
            Emit(actor, actor.View.transform.position, .65f, 28, NoiseKind.Pain);
            StateChanged?.Invoke(); return true;
        }
        public void Emit(PlayerContext actor, Vector3 position, float intensity, float range, NoiseKind kind,
            SurfaceKind surface = SurfaceKind.Dirt)
        {
            if (!IsAuthority || Phase != RoundPhase.Playing || noise == null) return;
            noise.Publish(new NoiseEvent(position, Mathf.Clamp01(intensity), range, kind,
                actor != null ? actor.SourceId : -1, Time.unscaledTimeAsDouble, surface));
            if (actor != null) actor.NoiseLevel = Mathf.Max(actor.NoiseLevel, Mathf.Clamp01(intensity));
        }
        public void ApplyReplica(ChapterId chapter, MissionFlags mission, RoundPhase phase, Ending ending, float seconds)
        {
            if (IsAuthority) return;
            if (Campaign.Chapter != chapter) Campaign.Restore(chapter, false, false);
            ReplicaMission = mission; ReplicaEnding = ending; Phase = phase; Started = phase != RoundPhase.Lobby;
            phaseUntil = Time.unscaledTimeAsDouble + seconds;
        }
        public MissionFlags ReplicaMission { get; private set; }
        public Ending ReplicaEnding { get; private set; }
        public MissionFlags VisibleMission => IsAuthority ? Campaign.Mission : ReplicaMission;
        public Ending VisibleEnding => IsAuthority ? Campaign.Ending : ReplicaEnding;
        public void Warp(PlayerContext actor, Vector3 position)
        {
            var cc = actor.GetComponent<CharacterController>();
            bool enabledBefore = cc.enabled; cc.enabled = false; actor.transform.position = position; cc.enabled = enabledBefore;
            actor.SetSpawn(position, actor.transform.rotation);
            actor.GetComponent<FirstPersonMotor>()?.ResetMotion();
        }
        private void SaveCheckpoint()
        {
            if (!SaveEnabled) return;
            try { CampaignSaveStore.Save(Campaign, actors); }
            catch (Exception e) { Debug.LogWarning("SILENCE: checkpoint could not be saved: " + e.Message); }
        }
        private static string Text(string ru, string en) => PrototypePreferences.English ? en : ru;
    }
}
