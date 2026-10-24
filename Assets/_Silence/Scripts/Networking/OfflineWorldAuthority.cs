using UnityEngine;
using Silence.Core;
using Silence.Interactions;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Networking
{
    public sealed class OfflineWorldAuthority : MonoBehaviour, IWorldAuthority
    {
        [SerializeField] private NoiseSystem noiseSystem;
        private PlayerContext localPlayer;
        private float nextInteractionTime;
        public void Configure(NoiseSystem system) => noiseSystem = system;
        public void Register(PlayerContext player)
        {
            if (player == null || (localPlayer != null && localPlayer != player))
                throw new System.InvalidOperationException("Offline supports one registered player.");
            localPlayer = player;
            player.Bind(this);
        }
        public bool IsLocalOwner(PlayerContext actor) => actor != null && actor == localPlayer;
        public bool CanSimulate(PlayerContext actor) => IsLocalOwner(actor);
        public bool RequestInteraction(PlayerContext actor, Interactable target)
        {
            if (!IsLocalOwner(actor) || target == null || Time.time < nextInteractionTime)
                return false;
            Transform eye = actor.View.transform;
            bool hitSomething = Physics.Raycast(eye.position, eye.forward, out RaycastHit hit,
                actor.Settings.interactionRange, ~0, QueryTriggerInteraction.Ignore);
            bool unobstructed = hitSomething &&
                hit.collider.GetComponentInParent<Interactable>() == target;
            if (!InteractionRules.CanApply(true, actor == localPlayer, target.CanInteract(actor),
                hitSomething ? hit.distance : float.PositiveInfinity,
                actor.Settings.interactionRange, unobstructed)) return false;
            nextInteractionTime = Time.time + 0.15f;
            if (!target.Apply(actor)) return false;
            actor.PublishAnimationCue(AnimationCueKind.Interact, hit.point);
            EmitNoise(actor, NoiseKind.Interaction, 0.2f);
            return true;
        }
        public void EmitNoise(PlayerContext actor, NoiseKind kind, float intensity)
        {
            if (!IsLocalOwner(actor) || noiseSystem == null ||
                float.IsNaN(intensity) || float.IsInfinity(intensity)) return;
            // One registered actor per offline session; no Unity object ID is needed.
            noiseSystem.Publish(new NoiseEvent(actor.transform.position,
                Mathf.Clamp01(intensity), 12f, kind, 1, Time.timeAsDouble));
        }
    }
}
