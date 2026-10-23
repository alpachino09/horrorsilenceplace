using UnityEngine;
using Silence.Core;
using Silence.Creatures;

namespace Silence.Audio
{
    [RequireComponent(typeof(HearingCreature))]
    public sealed class CreatureAudio : MonoBehaviour
    {
        private HearingCreature creature;
        private AudioSource source;
        private AudioClip step, breath;
        private Vector3 previous;
        private float distance;
        private CreatureState state;
        private void Awake()
        {
            creature = GetComponent<HearingCreature>(); source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = 2; source.maxDistance = 32;
            source.rolloffMode = AudioRolloffMode.Linear;
            step = WorldAudio.Make("Creature step", .3f, 48, .7f, 401);
            breath = WorldAudio.Make("Creature breath", .8f, 65, .8f, 402); previous = transform.position;
        }
        private void LateUpdate()
        {
            float travelled = Vector3.Distance(transform.position, previous); previous = transform.position;
            if (travelled < 3) distance += travelled; else distance = 0;
            if (creature.State == CreatureState.Chase && state != CreatureState.Chase) source.PlayOneShot(breath, .65f);
            state = creature.State;
            if (distance < 1.4f) return;
            distance %= 1.4f; source.PlayOneShot(step, .5f);
            // Playback is separate from the hearing simulation; creatures do not chase their own sounds.
        }
        private void OnDestroy() { if (step != null) Destroy(step); if (breath != null) Destroy(breath); }
    }
}
