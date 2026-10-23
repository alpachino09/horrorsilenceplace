using System;
using UnityEngine;

namespace Silence.SoundSimulation
{
    public sealed class NoiseSystem : MonoBehaviour
    {
        // Numeric simulation; audio playback and hearing subscribe independently.
        public event Action<NoiseEvent> Emitted;
        internal void Publish(NoiseEvent noise) => Emitted?.Invoke(noise);
    }
}
