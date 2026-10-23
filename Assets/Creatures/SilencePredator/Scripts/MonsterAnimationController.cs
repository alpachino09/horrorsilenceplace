using UnityEngine;
namespace Silence.Predator
{
    [DisallowMultipleComponent]
    public sealed class MonsterAnimationController : MonoBehaviour
    {
        public Animator animator;
        public bool demoControls;
        private static readonly string[] DemoClips = {"Idle","IdleBreathing","Walk","Stalk","Run","Sprint","ListenStart","ListenEnd","AttackBite","AttackClaw","Lunge","Alert","Search","Investigate","Stunned","Recover"};
        private string active = "Idle";
        private void Awake() { if (animator != null) animator.applyRootMotion = false; }
        public bool Play(string clip, float playback = 1)
        {
            if (animator == null || animator.runtimeAnimatorController == null ||
                !animator.HasState(0, Animator.StringToHash("Base Layer." + clip))) return false;
            animator.speed = Mathf.Clamp(playback, .1f, 3);
            animator.CrossFadeInFixedTime("Base Layer." + clip, .12f, 0); active = clip; return true;
        }
        // Pass only an already perceived sound from the authoritative hearing system.
        // This class never finds players, applies damage or moves the network root.
        public void SetHearing(float perceived) { if (perceived > .05f) Play("ListenStart"); else Play("ListenEnd"); }
        public void SetMotion(float metresPerSecond, bool chasing)
        {
            string clip = metresPerSecond < .1f ? "Idle" : chasing ? "Run" : "Walk";
            if (clip != active) Play(clip);
            if (animator != null) animator.speed = metresPerSecond < .1f ? 1 : Mathf.Clamp(metresPerSecond / (chasing ? 2.23f : .67f), .5f, 3);
        }
        private void OnGUI()
        {
            if (!demoControls) return;
            GUILayout.BeginArea(new Rect(16,16,225,Screen.height-32), GUI.skin.box);
            GUILayout.Label("SILENCE / Vesper — preview");
            foreach (string clip in DemoClips) if (GUILayout.Button(clip)) Play(clip);
            GUILayout.EndArea();
        }
    }
}
