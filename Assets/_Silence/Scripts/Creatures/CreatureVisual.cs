using UnityEngine;
using Silence.Core;

namespace Silence.Creatures
{
    public sealed class CreatureVisual : MonoBehaviour
    {
        [SerializeField] private Transform[] limbs;
        private HearingCreature creature;
        private Quaternion[] rests;
        public void Configure(Transform[] bones) => limbs = bones;
        private void Awake()
        {
            creature = GetComponent<HearingCreature>();
            rests = new Quaternion[limbs.Length];
            for (int i = 0; i < limbs.Length; i++) rests[i] = limbs[i].localRotation;
        }
        private void LateUpdate()
        {
            float speed = creature.State == CreatureState.Chase ? 12 : 4;
            for (int i = 0; i < limbs.Length; i++)
                limbs[i].localRotation = rests[i] * Quaternion.Euler(Mathf.Sin(Time.time * speed + i * Mathf.PI) * 16, 0, 0);
        }
    }
}
