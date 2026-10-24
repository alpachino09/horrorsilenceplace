using System;
using UnityEngine;
using Silence.Core;
using Silence.SoundSimulation;

namespace Silence.Audio
{
    [RequireComponent(typeof(NoiseSystem))]
    public sealed class WorldAudio : MonoBehaviour
    {
        private AudioSource[] pool;
        private AudioClip[] steps;
        private AudioClip impact, hinge, pain, windClip;
        private int next;
        private NoiseSystem noise;
        private void Awake()
        {
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++)
            {
                var child = new GameObject("WorldSound_" + i); child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1;
                source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 1; source.maxDistance = 30;
                pool[i] = source;
            }
            steps = new AudioClip[Enum.GetValues(typeof(SurfaceKind)).Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = Make("Footstep_" + (SurfaceKind)i, .22f, 55 + i * 70, .7f, 100 + i);
            impact = Make("Impact", .5f, 170, .85f, 201);
            hinge = Make("Hinge", .25f, 380, .3f, 202);
            pain = Make("PainBreath", .65f, 95, .7f, 203);
            var windObject = new GameObject("Wind"); windObject.transform.SetParent(transform, false);
            var wind = windObject.AddComponent<AudioSource>(); wind.spatialBlend = 0; wind.loop = true;
            wind.clip = windClip = Make("Wind", 4, 32, .1f, 300); wind.volume = .15f; wind.Play();
            noise = GetComponent<NoiseSystem>(); noise.Emitted += Play;
        }
        private void Play(NoiseEvent sound)
        {
            if (sound.Kind == NoiseKind.VoiceMetric) return; // Numerical microphone metric is never voice playback.
            var source = pool[next++ % pool.Length]; source.transform.position = sound.Position;
            source.maxDistance = sound.Range; source.volume = Mathf.Clamp01(sound.Intensity * .65f + .05f);
            source.pitch = sound.Kind == NoiseKind.Footstep ? 1 : .9f;
            int surface = (int)sound.Surface;
            source.clip = sound.Kind == NoiseKind.Footstep ? steps[surface >= 0 && surface < steps.Length ? surface : 0] : sound.Kind == NoiseKind.Impact
                ? impact : sound.Kind == NoiseKind.Pain ? pain : hinge;
            source.Play();
        }
        internal static AudioClip Make(string name, float seconds, float frequency, float roughness, int seed)
        {
            const int rate = 22050; int count = (int)(rate * seconds);
            float[] data = new float[count]; var random = new System.Random(seed); float low = 0;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                low += ((float)(random.NextDouble() * 2 - 1) - low) * .15f;
                float envelope = name == "Wind" ? .6f + .3f * Mathf.Sin(t * Mathf.PI / 2) : Mathf.Exp(-t * 14) * Mathf.Min(1, t * 120);
                data[i] = (low * roughness + Mathf.Sin(t * frequency * Mathf.PI * 2) * (1 - roughness) * .3f) * envelope;
            }
            var clip = AudioClip.Create(name, count, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        private void OnDestroy()
        {
            if (noise != null) noise.Emitted -= Play;
            if (steps != null) foreach (var clip in steps) if (clip != null) Destroy(clip);
            if (windClip != null) Destroy(windClip);
            if (impact != null) Destroy(impact); if (hinge != null) Destroy(hinge); if (pain != null) Destroy(pain);
        }
    }
}
