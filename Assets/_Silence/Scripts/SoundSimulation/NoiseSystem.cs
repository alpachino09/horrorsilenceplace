using System;
using UnityEngine;

namespace Silence.SoundSimulation
{
    public sealed class NoiseSystem : MonoBehaviour
    {
        // Numeric simulation event only. No sound playback, hearing or AI yet.
        public event Action<NoiseEvent> Emitted;
        internal void Publish(NoiseEvent noise) => Emitted?.Invoke(noise);
    }
}
