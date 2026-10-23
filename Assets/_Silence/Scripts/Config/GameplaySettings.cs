using UnityEngine;
using Silence.Core;

namespace Silence.Config
{
    [CreateAssetMenu(menuName = "SILENCE/Gameplay Settings")]
    public sealed class GameplaySettings : ScriptableObject
    {
        public Difficulty difficulty = Difficulty.Normal;
        public ChapterDefinition[] chapters;
        [Min(1)] public float maximumCarryMass = 16;
        public bool enableCampaign = true;
    }
}
