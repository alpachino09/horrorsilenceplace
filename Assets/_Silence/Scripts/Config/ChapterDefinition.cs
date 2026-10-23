using UnityEngine;
using Silence.Core;

namespace Silence.Config
{
    [CreateAssetMenu(menuName = "SILENCE/Chapter")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        public ChapterId id;
        public string titleRu, titleEn;
        [TextArea] public string noteRu, noteEn;
        [TextArea] public string radioRu, radioEn;
        public Vector3 center;
        public Vector3 creatureSpawn;
        public Vector3[] spawnPoints;
        public Vector3[] patrolPoints;
        public bool hasVote;
        public string voteRu, voteEn;
        [Range(0, .85f)] public float ambientMasking;
        public Color fogColor = new Color(.12f, .16f, .18f);
    }
}
