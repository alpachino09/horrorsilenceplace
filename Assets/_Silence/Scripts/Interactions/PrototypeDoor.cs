using UnityEngine;
using Silence.Player;

namespace Silence.Interactions
{
    // Sprint 01 toggle hinge. Hand-driven physical doors belong to Sprint 04.
    public sealed class PrototypeDoor : Interactable
    {
        [SerializeField] private float openingAngle = 100f;
        private bool open;
        private Quaternion closedRotation;
        private void Awake() => closedRotation = transform.localRotation;
        public override string GetPrompt(bool english) => english
            ? (open ? "Close" : "Open") : (open ? "Закрыть" : "Открыть");
        internal override bool Apply(PlayerContext player)
        {
            open = !open;
            return true;
        }
        private void Update()
        {
            Quaternion desired = closedRotation * Quaternion.Euler(0f, open ? openingAngle : 0f, 0f);
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation,
                desired, 110f * Time.deltaTime);
        }
    }
}
