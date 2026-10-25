using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Silence.Core;
using Silence.Player;
using Silence.Interactions;

namespace Silence.Graphics.Players
{
    // This component exists only in the isolated review scene. Its health/reaction
    // buttons are offline diagnostics, not gameplay commands or network authority.
    public sealed class HumanMotionLab : MonoBehaviour
    {
        public PlayerContext actor;
        public Camera observer;
        public AnimationClip[] originalBasicClips = Array.Empty<AnimationClip>();
        private Animator animator;
        private RuntimeAnimatorController controller;
        private PlayerAvatarPresentation presentation;
        private FirstPersonMotor motor;
        private PlayerLook look;
        private InteractionScanner scanner;
        private CharacterIKController ik;
        private CharacterAnimationActions actions;
        private FootstepAnimationBridge footsteps;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private bool thirdPerson, rawPreview, show = true;
        private int clipIndex;
        private double previewStart;
        private Vector2 scroll;
        private void Start()
        {
            if (actor == null || observer == null) { enabled = false; return; }
            presentation = actor.GetComponent<PlayerAvatarPresentation>(); animator = presentation.Model; controller = animator.runtimeAnimatorController;
            motor = actor.GetComponent<FirstPersonMotor>(); look = actor.GetComponent<PlayerLook>(); scanner = actor.GetComponent<InteractionScanner>();
            ik = animator.GetComponent<CharacterIKController>(); actions = animator.GetComponent<CharacterAnimationActions>(); footsteps = animator.GetComponent<FootstepAnimationBridge>();
            observer.enabled = false; actor.View.enabled = true; PlayerLook.Capture(false);
        }
        private void Update()
        {
            if (!rawPreview || !graph.IsValid()) return;
            double length = originalBasicClips[clipIndex].length;
            playable.SetTime((Time.timeAsDouble - previewStart) % Math.Max(.05, length)); graph.Evaluate(0);
        }
        private void LateUpdate()
        {
            if (actor == null || observer == null) return;
            if (actor.ViewRig != null && !rawPreview) { actor.ViewRig.CameraOverride = false; observer.enabled = false; return; }
            observer.enabled = thirdPerson || rawPreview; actor.View.enabled = !observer.enabled;
            if (!observer.enabled) return;
            Vector3 target = actor.transform.position + Vector3.up * .95f;
            observer.transform.position = target - actor.transform.forward * 3.7f + actor.transform.right * 2.2f + Vector3.up * .4f;
            observer.transform.LookAt(target); observer.fieldOfView = 42;
        }
        private void RawClip(int index)
        {
            if (originalBasicClips.Length == 0 || animator == null) return;
            if (graph.IsValid()) graph.Destroy();
            clipIndex = (index + originalBasicClips.Length) % originalBasicClips.Length;
            if (!rawPreview)
            {
                rawPreview = true;
                if (actor.ViewRig != null) actor.ViewRig.CameraOverride = true;
                motor.enabled = look.enabled = scanner.enabled = presentation.enabled = false;
                if (ik != null) ik.enabled = false; if (actions != null) actions.enabled = false; if (footsteps != null) footsteps.enabled = false;
                actor.Input.enabled = false; animator.runtimeAnimatorController = null; PlayerLook.Capture(false);
            }
            graph = PlayableGraph.Create("SILENCE original Basic Motions preview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, originalBasicClips[clipIndex]); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Original clip on our model", animator); output.SetSourcePlayable(playable); graph.Play(); previewStart = Time.timeAsDouble;
        }
        private void EndRaw()
        {
            if (graph.IsValid()) graph.Destroy();
            if (!rawPreview) return;
            rawPreview = false;
            if (actor != null && actor.ViewRig != null) actor.ViewRig.CameraOverride = false;
            if (animator != null) { animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.Rebind(); }
            if (actor != null && actor.Input != null) actor.Input.enabled = true;
            if (motor != null) motor.enabled = true; if (look != null) look.enabled = true;
            if (scanner != null) scanner.enabled = true; if (presentation != null) presentation.enabled = true;
            if (ik != null) ik.enabled = true; if (actions != null) actions.enabled = true; if (footsteps != null) footsteps.enabled = true;
        }
        private void Cue(AnimationCueKind kind) => actor.PublishAnimationCue(kind, actor.transform.position + actor.transform.right * 3 + Vector3.up * 1.3f);
        private void OnGUI()
        {
            if (actor == null || animator == null) return;
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(440, Screen.width - 24), show ? Mathf.Min(560, Screen.height - 24) : 42), GUI.skin.box);
            if (GUILayout.Button(show ? "Скрыть панель проверки" : "Показать панель проверки")) show = !show;
            if (!show) { GUILayout.EndArea(); return; }
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("SILENCE — отдельная площадка анимаций");
            GUILayout.Label("WASD · Shift: бег · Alt: осторожно · Ctrl: присесть · Space: прыжок · E: дверь/подбор. Esc освобождает мышь.");
            if (GUILayout.Button("Начать управление мышью")) { EndRaw(); PlayerLook.Capture(true); }
            bool external = actor.ViewRig != null ? actor.ViewRig.ThirdPerson : thirdPerson;
            if (GUILayout.Button(external ? "Камера от первого лица" : "Камера снаружи — посмотреть тело"))
            { if (actor.ViewRig != null) actor.ViewRig.SetThirdPerson(!external); else thirdPerson = !thirdPerson; }
            if (!rawPreview)
            {
                GUILayout.Label("Скорость: " + motor.PlanarVelocity.magnitude.ToString("F2") + " м/с · Присед: " + motor.IsCrouched + " · Здоровье: " + actor.Vitals.State);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Тест спотыкания")) Cue(AnimationCueKind.Trip);
                if (GUILayout.Button("Тест взгляда на шум")) Cue(AnimationCueKind.Startle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Тест ранения")) { actor.Vitals.Reset(); actor.Vitals.TryHit(Time.timeAsDouble, 0); Cue(AnimationCueKind.Hit); }
                if (GUILayout.Button("Тест смерти")) { actor.Vitals.Reset(); actor.Vitals.TryHit(Time.timeAsDouble, 0); actor.Vitals.TryHit(Time.timeAsDouble, 0); }
                if (GUILayout.Button("Сброс здоровья")) actor.Vitals.Reset();
                GUILayout.EndHorizontal();
                if (actor.Flashlight != null)
                    GUILayout.Label("Фонарик: " + actor.Flashlight.Frame.state + " · " + (actor.Flashlight.Frame.charge * 100).ToString("F0") + "% · запас: " + actor.Flashlight.Frame.spares + ". F / R / H. Только в этой лаборатории заряд рассчитан на 12 секунд.");
                else GUILayout.Label("Калиброванный тактический фонарик в этом player prefab не установлен. Его модель площадка не подменяет.");
            }
            GUILayout.Space(8);
            GUILayout.Label("Исходные 23 клипа на нашей модели (отдельно от игровой локомоции):");
            if (rawPreview)
            {
                GUILayout.Label("Сырой клип: " + originalBasicClips[clipIndex].name + ". IK и игровой контроллер временно выключены.");
                GUILayout.BeginHorizontal(); if (GUILayout.Button("←")) RawClip(clipIndex - 1); if (GUILayout.Button("→")) RawClip(clipIndex + 1); GUILayout.EndHorizontal();
                if (GUILayout.Button("Вернуться к игровой локомоции")) EndRaw();
            }
            else if (GUILayout.Button("Просмотреть все исходные клипы")) RawClip(0);
            GUILayout.Label("Площадка работает локально. Кооператив проверяй отдельными сборками Host и Client в своём мире. Вернуться в мир: останови Play → меню 25 → Вернуться в последний мир с анимациями.");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void OnDisable()
        {
            if (actor != null && animator != null) EndRaw(); else if (graph.IsValid()) graph.Destroy();
            if (actor != null && actor.ViewRig != null) actor.ViewRig.CameraOverride = false;
            if (observer != null) observer.enabled = false; if (actor != null && actor.View != null) actor.View.enabled = actor.ViewRig == null;
        }
    }
}
