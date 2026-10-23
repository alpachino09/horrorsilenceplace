using UnityEngine;
using UnityEngine.AI;
using Silence.Gameplay;
using Silence.SoundSimulation;

namespace Silence.Interactions
{
    public sealed class BreakableBarrier : MonoBehaviour
    {
        public bool Broken { get; private set; }
        public bool creatureMayBreak = true;
        public void SetBroken(bool value)
        {
            if (Broken == value) return;
            Broken = value;
            foreach (var shape in GetComponentsInChildren<Collider>(true)) shape.enabled = !value;
            foreach (var visual in GetComponentsInChildren<Renderer>(true)) visual.enabled = !value;
            foreach (var obstacle in GetComponentsInChildren<NavMeshObstacle>(true)) obstacle.enabled = !value;
        }
        public bool TryBreak(GameplayDirector game)
        {
            if (!creatureMayBreak || Broken || game == null || !game.IsAuthority) return false;
            SetBroken(true);
            game.Noise.Publish(new NoiseEvent(transform.position + Vector3.up, .9f, 32,
                NoiseKind.Impact, -2, Time.unscaledTimeAsDouble));
            return true;
        }
    }
}
