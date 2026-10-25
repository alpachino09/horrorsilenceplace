using UnityEngine;

namespace Silence.Player
{
    public sealed class SpectatorView : MonoBehaviour
    {
        private PlayerContext actor;
        private Camera spectator;
        private AudioListener listener;
        public Camera SpectatorCamera => spectator;
        public PlayerContext ObservedActor { get; private set; }
        private void Awake() => actor = GetComponent<PlayerContext>();
        private void LateUpdate()
        {
            if (!actor.IsLocalOwner || actor.Game == null) return;
            bool dead = !actor.Vitals.Alive;
            if (!dead)
            {
                ObservedActor = null;
                if (spectator != null) { spectator.enabled = false; listener.enabled = false; }
                bool legacyEye = actor.ViewRig == null;
                actor.View.enabled = legacyEye; actor.View.GetComponent<AudioListener>().enabled = legacyEye;
                return;
            }
            if (spectator == null)
            {
                actor.ViewRig?.DisableRendering();
                var root = new GameObject("Spectator view"); root.transform.SetParent(transform, false);
                spectator = root.AddComponent<Camera>(); listener = root.AddComponent<AudioListener>();
                spectator.CopyFrom(actor.View);
                actor.ViewRig?.CopyCameraSettings(spectator);
            }
            actor.View.enabled = false; actor.View.GetComponent<AudioListener>().enabled = false;
            spectator.enabled = listener.enabled = true;
            PlayerContext target = null;
            foreach (var other in actor.Game.Actors) if (other != actor && other.Vitals.Alive) { target = other; break; }
            var nextObserved = target != null ? target : actor;
            if (ObservedActor != nextObserved)
            { ObservedActor = nextObserved; ObservedActor.ViewRig?.CopyCameraSettings(spectator); spectator.enabled = true; }
            Transform eye = target != null ? target.View.transform : actor.View.transform;
            if (ObservedActor.ViewRig != null)
            { ObservedActor.ViewRig.FirstPersonPose(out Vector3 position, out Quaternion rotation); spectator.transform.SetPositionAndRotation(position, rotation); }
            else spectator.transform.SetPositionAndRotation(eye.position, eye.rotation);
        }
    }
}
