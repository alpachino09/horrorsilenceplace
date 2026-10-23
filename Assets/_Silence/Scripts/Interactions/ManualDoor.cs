using UnityEngine;
using Silence.Gameplay;
using Silence.Player;
using Silence.UI;
using Silence.SoundSimulation;

namespace Silence.Interactions
{
    public sealed class ManualDoor : Interactable
    {
        [SerializeField] private GameplayDirector game;
        private Quaternion closed;
        private float angle, target, dragRate, dragUntil, nextNoise;
        private PlayerContext operatorActor;
        public float Angle => angle;
        private void Awake() => closed = transform.localRotation;
        public void Configure(GameplayDirector director) => game = director;
        public override bool CanInteract(PlayerContext actor) => base.CanInteract(actor) &&
            (GetComponent<BreakableBarrier>() == null || !GetComponent<BreakableBarrier>().Broken);
        public override string GetPrompt(bool english) => english
            ? "E: open/close · hold right mouse and drag slowly" : "E: открыть/закрыть · удерживайте ПКМ и ведите мышь";
        internal override bool Apply(PlayerContext player)
        {
            if (game == null || !game.IsAuthority || !game.CanAct(player)) return false;
            operatorActor = player; target = angle > 45 ? 0 : 100; dragUntil = 0;
            return true;
        }
        public void Drag(float rate, PlayerContext actor)
        {
            if (game == null || !game.IsAuthority) return;
            operatorActor = actor; dragRate = rate; dragUntil = Time.unscaledTime + .15f;
        }
        private void Update()
        {
            if (game == null || !game.IsAuthority || game.Phase != Silence.Core.RoundPhase.Playing) return;
            float before = angle;
            if (Time.unscaledTime < dragUntil) { angle = Mathf.Clamp(angle + dragRate * 110 * Time.deltaTime, 0, 100); target = angle; }
            else angle = Mathf.MoveTowards(angle, target, 100 * Time.deltaTime);
            transform.localRotation = closed * Quaternion.Euler(0, angle, 0);
            float speed = Mathf.Abs(angle - before) / Mathf.Max(.001f, Time.deltaTime);
            if (speed > 1 && Time.time > nextNoise)
            {
                nextNoise = Time.time + .15f;
                game.Emit(operatorActor, transform.position + Vector3.up, Mathf.Lerp(.025f, .5f, Mathf.Clamp01(speed / 110)), 20, NoiseKind.Interaction);
            }
            if (speed > 75 && (angle == 0 || angle == 100) && Mathf.Abs(angle - before) > .01f)
                game.Emit(operatorActor, transform.position + Vector3.up, .75f, 28, NoiseKind.Impact);
        }
        public void ApplyReplica(float value)
        { angle = target = Mathf.Clamp(value, 0, 100); transform.localRotation = closed * Quaternion.Euler(0, angle, 0); }
        public void ResetDoor() { GetComponent<BreakableBarrier>()?.SetBroken(false); angle = target = dragRate = dragUntil = 0; transform.localRotation = closed; }
    }
}
