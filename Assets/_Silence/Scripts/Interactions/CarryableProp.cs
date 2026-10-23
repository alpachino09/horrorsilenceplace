using UnityEngine;
using Silence.Gameplay;
using Silence.Player;
using Silence.SoundSimulation;

namespace Silence.Interactions
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class CarryableProp : Interactable
    {
        public float Mass => body != null ? body.mass : 1;
        public bool Held => holder != null || replicaHeld;
        public bool ReplicaHeld => replicaHeld;
        private Rigidbody body;
        private GameplayDirector game;
        private PlayerContext holder, lastActor;
        private Vector3 origin;
        private Quaternion originRotation;
        private bool remembered, replicaHeld;
        private float nextImpact;
        private readonly RaycastHit[] holdHits = new RaycastHit[16];
        private CharacterController ignoredController;
        private float restoreCollisionAt;
        private void Awake() { body = GetComponent<Rigidbody>(); Remember(); }
        private void Remember()
        { if (remembered) return; origin = transform.position; originRotation = transform.rotation; remembered = true; }
        public void SetAuthority(bool authority, GameplayDirector director)
        { game = director; if (body == null) body = GetComponent<Rigidbody>(); Remember(); body.isKinematic = !authority; }
        public override bool CanInteract(PlayerContext actor) => base.CanInteract(actor) && !Held && actor.Game != null;
        public override string GetPrompt(bool english) => english ? "Carry · G: place · hold/release LMB: throw" : "Взять · G: положить · удержать/отпустить ЛКМ: бросить";
        internal override bool Apply(PlayerContext actor) => actor.Game != null && actor.Game.Grab(actor, this);
        public void Grab(PlayerContext actor)
        {
            RestoreCollision(); holder = lastActor = actor; body.isKinematic = true;
            ignoredController = actor.GetComponent<CharacterController>();
            Physics.IgnoreCollision(GetComponent<Collider>(), ignoredController, true); restoreCollisionAt = float.PositiveInfinity;
        }
        public void Release(float force)
        {
            if (holder == null) return;
            var previous = holder; holder = null; body.isKinematic = false;
            body.linearVelocity = previous.transform.forward * (force > 0 ? Mathf.Lerp(2, 7, force) : .15f);
            restoreCollisionAt = Time.time + .3f;
        }
        private void RestoreCollision()
        {
            if (ignoredController != null) Physics.IgnoreCollision(GetComponent<Collider>(), ignoredController, false);
            ignoredController = null;
        }
        private void FixedUpdate()
        {
            if (ignoredController != null && holder == null && Time.time >= restoreCollisionAt) RestoreCollision();
            if (game == null || !game.IsAuthority || holder == null) return;
            Transform eye = holder.View.transform;
            Vector3 target;
            int count = Physics.SphereCastNonAlloc(eye.position, .3f, eye.forward, holdHits, 1.1f, ~0, QueryTriggerInteraction.Ignore);
            float available = count == holdHits.Length ? .35f : 1.1f;
            for (int i = 0; i < count; i++)
            {
                var hit = holdHits[i];
                if (hit.collider == GetComponent<Collider>() || hit.transform.IsChildOf(holder.transform)) continue;
                available = Mathf.Min(available, Mathf.Max(.35f, hit.distance - .2f));
            }
            target = eye.position + eye.forward * available - Vector3.up * .25f;
            body.MovePosition(target); body.MoveRotation(Quaternion.Euler(0, holder.transform.eulerAngles.y, 0));
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (game == null || !game.IsAuthority || holder != null || Time.time < nextImpact) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < .5f) return;
            nextImpact = Time.time + .25f;
            var material = GetComponent<SurfaceMaterial>();
            float factor = material != null && material.kind == Silence.Core.SurfaceKind.Metal ? 1 : .6f;
            game.Emit(lastActor, transform.position, Mathf.Clamp01(speed / 6 * factor), 30, NoiseKind.Impact);
        }
        private void OnDestroy() => RestoreCollision();
        public void SetReplicaHeld(bool held) => replicaHeld = held;
        public void ResetProp()
        {
            RestoreCollision(); nextImpact = Time.time + .75f;
            if (holder != null) { holder.HeldProp = null; holder.Inventory.TryConsume("held_prop"); holder = null; }
            if (body == null) body = GetComponent<Rigidbody>();
            body.isKinematic = true; transform.SetPositionAndRotation(origin, originRotation);
            body.isKinematic = game == null || !game.IsAuthority;
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
        }
    }
}
