using UnityEngine;

namespace Silence.Player
{
    public sealed class SpectatorView : MonoBehaviour
    {
        private PlayerContext actor;
        private Camera spectator;
        private AudioListener listener;
        private void Awake() => actor = GetComponent<PlayerContext>();
        private void LateUpdate()
        {
            if (!actor.IsLocalOwner || actor.Game == null) return;
            bool dead = !actor.Vitals.Alive;
            if (!dead)
            {
                if (spectator != null) { spectator.enabled = false; listener.enabled = false; }
                actor.View.enabled = true; actor.View.GetComponent<AudioListener>().enabled = true;
                return;
            }
            if (spectator == null)
            {
                var root = new GameObject("Spectator view"); root.transform.SetParent(transform, false);
                spectator = root.AddComponent<Camera>(); listener = root.AddComponent<AudioListener>();
                spectator.CopyFrom(actor.View);
            }
            actor.View.enabled = false; actor.View.GetComponent<AudioListener>().enabled = false;
            spectator.enabled = listener.enabled = true;
            PlayerContext target = null;
            foreach (var other in actor.Game.Actors) if (other != actor && other.Vitals.Alive) { target = other; break; }
            Transform eye = target != null ? target.View.transform : actor.View.transform;
            spectator.transform.SetPositionAndRotation(eye.position, eye.rotation);
        }
    }
}
