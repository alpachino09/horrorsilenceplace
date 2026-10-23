using UnityEngine;
using UnityEngine.AI;
using Silence.Config;
using Silence.Core;
using Silence.Gameplay;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Creatures
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class HearingCreature : MonoBehaviour
    {
        private GameplayDirector game;
        private NavMeshAgent agent;
        private CreatureMind mind;
        private ChapterDefinition chapter;
        private AcousticMaskingZone[] masking;
        private int patrol;
        private double nextAttack;
        private float nextPath;
        private Vector3 lastDestination;
        private readonly Collider[] contacts = new Collider[16];
        public CreatureState State => mind != null ? mind.State : replicaState;
        private CreatureState replicaState;
        public uint HeardSequence { get; private set; }
        public uint AttackSequence { get; private set; }
        public Vector3 LastAcceptedSound { get; private set; }
        public float LastAcceptedStrength { get; private set; }
        public double HeardAt { get; private set; } = double.NegativeInfinity;
        public double AttackedAt { get; private set; } = double.NegativeInfinity;
        public bool HasHeardCue => !double.IsNegativeInfinity(HeardAt);
        public bool HasAttackCue => !double.IsNegativeInfinity(AttackedAt);
        public void SetAuthority(bool authority, GameplayDirector director)
        {
            if (game != null && game.Noise != null) game.Noise.Emitted -= Hear;
            game = director; agent = GetComponent<NavMeshAgent>(); agent.enabled = authority;
            if (authority && game.Noise != null) game.Noise.Emitted += Hear;
            masking = SceneQuery.All<AcousticMaskingZone>();
        }
        public void BeginChapter(ChapterDefinition definition, DifficultyRules rules)
        {
            if (game == null || !game.IsAuthority) return;
            chapter = definition; mind = new CreatureMind(rules); patrol = 0; nextPath = 0; nextAttack = Time.unscaledTimeAsDouble + 5;
            HeardAt = AttackedAt = double.NegativeInfinity;
            if (NavMesh.SamplePosition(chapter.creatureSpawn, out NavMeshHit hit, 8, NavMesh.AllAreas))
            {
                if (!agent.enabled) { transform.position = hit.position; agent.enabled = true; }
                agent.Warp(hit.position); agent.ResetPath();
            }
            else { agent.enabled = false; Debug.LogError("SILENCE: creature spawn has no baked navigation."); }
        }
        private void Hear(NoiseEvent sound)
        {
            if (sound.SourceId == -2 || game == null || !game.IsAuthority || game.Phase != RoundPhase.Playing || mind == null) return;
            Vector3 ear = transform.position + Vector3.up * 1.5f;
            float distance = Vector3.Distance(ear, sound.Position);
            if (distance > sound.Range) return;
            float maskingAmount = chapter.ambientMasking;
            foreach (var zone in masking)
                if (zone != null && (zone.Contains(ear) || zone.Contains(sound.Position))) maskingAmount = Mathf.Max(maskingAmount, zone.strength);
            float perceived = NoiseMath.PerceivedIntensity(sound.Intensity, distance, sound.Range,
                AcousticOcclusion.Between(sound.Position, ear), maskingAmount);
            if (mind.Hear(new Point3(sound.Position.x, sound.Position.y, sound.Position.z), perceived, Time.unscaledTimeAsDouble))
            {
                LastAcceptedSound = sound.Position; LastAcceptedStrength = perceived;
                HeardAt = Time.unscaledTimeAsDouble; unchecked { HeardSequence++; }
            }
        }
        private void Update()
        {
            if (game == null || !game.IsAuthority || mind == null || !agent.enabled || !agent.isOnNavMesh) return;
            if (game.Phase != RoundPhase.Playing) { agent.isStopped = true; return; }
            agent.isStopped = mind.State == CreatureState.Recover;
            Vector3 memory = new Vector3(mind.LastHeardPosition.X, mind.LastHeardPosition.Y, mind.LastHeardPosition.Z);
            mind.Tick(Time.unscaledTimeAsDouble, Vector3.Distance(transform.position, memory));
            if (mind.State == CreatureState.Recover) return;
            agent.isStopped = false;
            agent.speed = mind.State == CreatureState.Chase ? game.Rules.ChaseSpeed : mind.State == CreatureState.Patrol ? 1.8f : 3.7f;
            if (mind.State == CreatureState.Patrol)
            {
                if (chapter.patrolPoints.Length > 0)
                {
                    if (!agent.pathPending && agent.remainingDistance < 1) patrol = (patrol + 1) % chapter.patrolPoints.Length;
                    Destination(chapter.patrolPoints[patrol]);
                }
            }
            else if (mind.State == CreatureState.Search)
            {
                float phase = Time.time * .35f;
                Vector3 search = memory + new Vector3(Mathf.Cos(phase), 0, Mathf.Sin(phase)) * 2;
                if (NavMesh.SamplePosition(search, out NavMeshHit hit, 2, NavMesh.AllAreas)) Destination(hit.position);
            }
            else Destination(memory);
            if (mind.HasMemory && mind.State != CreatureState.Patrol)
            {
                Vector3 toward = memory - transform.position;
                if (Physics.SphereCast(transform.position + Vector3.up * 1.2f, .25f, toward.normalized,
                    out RaycastHit obstruction, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                {
                    var barrier = obstruction.collider.GetComponentInParent<Silence.Interactions.BreakableBarrier>();
                    if (barrier != null && barrier.TryBreak(game)) mind.Recover(Time.unscaledTimeAsDouble, 1.5);
                }
            }
            // Contact attack only: this never supplies a live player's position to navigation.
            if (Time.unscaledTimeAsDouble < nextAttack) return;
            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, 1.1f, contacts, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var actor = contacts[i].GetComponentInParent<PlayerContext>();
                if (actor == null || !actor.Vitals.Alive || actor.Game != game) continue;
                Vector3 start = transform.position + Vector3.up * 1.3f;
                if (Physics.Linecast(start, actor.View.transform.position, out RaycastHit blocked, ~0, QueryTriggerInteraction.Ignore) &&
                    blocked.collider.GetComponentInParent<PlayerContext>() != actor) continue;
                if (!game.Damage(actor)) continue;
                AttackedAt = Time.unscaledTimeAsDouble; unchecked { AttackSequence++; }
                nextAttack = Time.unscaledTimeAsDouble + game.Rules.AttackGrace;
                mind.Recover(Time.unscaledTimeAsDouble, game.Rules.AttackGrace);
                break;
            }
        }
        private void Destination(Vector3 point)
        {
            if (Time.unscaledTime < nextPath && (point - lastDestination).sqrMagnitude < .25f) return;
            nextPath = Time.unscaledTime + .25f; lastDestination = point; agent.SetDestination(point);
        }
        public void SetReplicaState(CreatureState state) => replicaState = state;
        public void SetReplicaCues(uint heard, Vector3 point, float strength, double heardAge, bool hasHeard,
            uint attacked, double attackAge, bool hasAttack)
        {
            if (game != null && game.IsAuthority) return;
            HeardSequence = heard; LastAcceptedSound = point; LastAcceptedStrength = strength;
            AttackSequence = attacked;
            HeardAt = hasHeard ? Time.unscaledTimeAsDouble - System.Math.Max(0, heardAge) : double.NegativeInfinity;
            AttackedAt = hasAttack ? Time.unscaledTimeAsDouble - System.Math.Max(0, attackAge) : double.NegativeInfinity;
        }
        private void OnDestroy() { if (game != null && game.Noise != null) game.Noise.Emitted -= Hear; }
    }
}
