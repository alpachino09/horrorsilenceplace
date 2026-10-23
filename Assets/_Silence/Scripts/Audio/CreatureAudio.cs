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
        private bool animationDriven;
        private AudioSource breathing;
        private AudioSource animatedSteps;
        private AudioClip breathLoop;
        public void SetAnimationDriven(bool value) { animationDriven = value; if (!value && breathing != null) breathing.Stop(); }
        public void PlayAnimatedStep(Vector3 point, float speed)
        {
            if (animatedSteps == null || step == null) return;
            animatedSteps.transform.position = point;
            animatedSteps.PlayOneShot(step, Mathf.Clamp(.30f + speed * .035f, .3f, .65f));
        }
        public void SetBreathing(float phase, float volume, float playback, Vector3 point)
        {
            if (breathing == null || breathLoop == null) return;
            float fraction = Mathf.Repeat(phase, 1);
            if (!breathing.isPlaying) { breathing.time = fraction * breathLoop.length; breathing.Play(); }
            float error = Mathf.Abs(breathing.time / breathLoop.length - fraction);
            if (error > .10f && error < .90f) breathing.time = fraction * breathLoop.length;
            breathing.volume = volume;
            breathing.pitch = playback; breathing.transform.position = point;
        }
        private void Awake()
        {
            creature = GetComponent<HearingCreature>(); source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = 2; source.maxDistance = 32;
            source.rolloffMode = AudioRolloffMode.Linear;
            step = WorldAudio.Make("Creature step", .3f, 48, .7f, 401);
            breath = WorldAudio.Make("Creature breath", .8f, 65, .8f, 402); previous = transform.position;
            var breathObject = new GameObject("Creature breathing audio"); breathObject.transform.SetParent(transform, false);
            breathing = breathObject.AddComponent<AudioSource>(); breathing.playOnAwake = false; breathing.loop = true;
            breathing.spatialBlend = 1; breathing.minDistance = 1.5f; breathing.maxDistance = 22;
            var stepObject = new GameObject("Creature foot audio"); stepObject.transform.SetParent(transform, false);
            animatedSteps = stepObject.AddComponent<AudioSource>(); animatedSteps.playOnAwake = false;
            animatedSteps.spatialBlend = 1; animatedSteps.minDistance = 2; animatedSteps.maxDistance = 32;
            const int rate = 22050; var samples = new float[(int)(rate * 4.6f)]; var random = new System.Random(7602);
            float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, phase = t / 4.6f;
                low += ((float)random.NextDouble() * 2 - 1 - low) * .16f;
                float envelope = Mathf.Pow(Mathf.Max(0, Mathf.Sin(phase * Mathf.PI * 2)), .65f) +
                    .55f * Mathf.Pow(Mathf.Max(0, -Mathf.Sin(phase * Mathf.PI * 2)), 1.5f);
                samples[i] = (low * .40f + Mathf.Sin(t * 55 * Mathf.PI * 2) * .065f) * envelope;
            }
            breathLoop = AudioClip.Create("Vesper procedural breathing placeholder", samples.Length, 1, rate, false);
            breathLoop.SetData(samples, 0); breathing.clip = breathLoop;
        }
        private void LateUpdate()
        {
            if (animationDriven) { previous = transform.position; return; }
            float travelled = Vector3.Distance(transform.position, previous); previous = transform.position;
            if (travelled < 3) distance += travelled; else distance = 0;
            if (creature.State == CreatureState.Chase && state != CreatureState.Chase) source.PlayOneShot(breath, .65f);
            state = creature.State;
            if (distance < 1.4f) return;
            distance %= 1.4f; source.PlayOneShot(step, .5f);
            // Playback is separate from the hearing simulation; creatures do not chase their own sounds.
        }
        private void OnDestroy() { if (step != null) Destroy(step); if (breath != null) Destroy(breath); if (breathLoop != null) Destroy(breathLoop); }
    }
}
