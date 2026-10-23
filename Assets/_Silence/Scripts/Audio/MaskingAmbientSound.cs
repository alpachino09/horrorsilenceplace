using UnityEngine;
using Silence.SoundSimulation;

namespace Silence.Audio
{
    [RequireComponent(typeof(AcousticMaskingZone))]
    public sealed class MaskingAmbientSound : MonoBehaviour
    {
        private AudioClip clip;
        private void Awake()
        {
            var zone = GetComponent<AcousticMaskingZone>();
            var source = gameObject.AddComponent<AudioSource>();
            const int rate = 11025; var data = new float[rate * 2];
            for (int i = 0; i < data.Length; i++)
            {
                float time = i / (float)rate;
                data[i] = Mathf.Sin(time * Mathf.PI * 2 * 55) * .12f + Mathf.Sin(time * Mathf.PI * 2 * 93) * .06f;
            }
            clip = AudioClip.Create("Temporary ventilation hum", data.Length, 1, rate, false); clip.SetData(data, 0);
            source.clip = clip; source.loop = true; source.spatialBlend = 1; source.volume = zone.strength * .6f;
            source.rolloffMode = AudioRolloffMode.Linear; source.minDistance = 3; source.maxDistance = zone.size.magnitude * .6f;
            source.Play();
        }
        private void OnDestroy() { if (clip != null) Destroy(clip); }
    }
}
