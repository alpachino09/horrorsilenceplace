using UnityEngine;

namespace Silence.SoundSimulation
{
    public enum NoiseKind { Interaction, Footstep, Impact, VoiceMetric }
    public readonly struct NoiseEvent
    {
        public Vector3 Position { get; }
        public float Intensity { get; }
        public float Range { get; }
        public NoiseKind Kind { get; }
        public int SourceId { get; }
        public double Timestamp { get; }
        public NoiseEvent(Vector3 position, float intensity, float range,
            NoiseKind kind, int sourceId, double timestamp)
        {
            Position = position; Intensity = intensity; Range = range;
            Kind = kind; SourceId = sourceId; Timestamp = timestamp;
        }
    }
    public interface INoiseEmitter { void EmitNoise(NoiseKind kind, float intensity); }
    public interface INoiseListener { void OnNoise(NoiseEvent noise); }
}
