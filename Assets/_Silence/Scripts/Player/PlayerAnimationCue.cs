using UnityEngine;
using Silence.Core;

namespace Silence.Player
{
    public readonly struct PlayerAnimationCue
    {
        public AnimationCueKind Kind { get; }
        public Vector3 Point { get; }
        public float Strength { get; }
        public PlayerAnimationCue(AnimationCueKind kind, Vector3 point, float strength)
        { Kind = kind; Point = point; Strength = strength; }
    }
}
