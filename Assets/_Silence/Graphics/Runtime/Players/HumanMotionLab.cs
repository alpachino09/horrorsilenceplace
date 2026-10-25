using System;
using System.Collections.Generic;
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
        private PlayableGraph graph;
        private AnimationClipPlayable playable;
        private bool thirdPerson, rawPreview, show = true;
        private int clipIndex;
        private double previewStart;
        private Vector2 scroll;
        private readonly List<Behaviour> suspended=new List<Behaviour>();
        private readonly List<bool> priorEnabled=new List<bool>();
        private CharacterReviewProbe probe;
        private int scenario,priorFps,priorVsync;
        private float priorTimeScale;
        private bool priorOverride,priorCaptured;
        private bool settingsCaptured;
        private void OnEnable(){priorFps=Application.targetFrameRate;priorVsync=QualitySettings.vSyncCount;settingsCaptured=true;}
        private void Start()
        {
            if (actor == null || observer == null) { enabled = false; return; }
            presentation = actor.GetComponent<PlayerAvatarPresentation>(); animator = presentation.Model; controller = animator.runtimeAnimatorController;
            motor = actor.GetComponent<FirstPersonMotor>(); look = actor.GetComponent<PlayerLook>(); scanner = actor.GetComponent<InteractionScanner>();
            observer.enabled = false; actor.View.enabled = true; PlayerLook.Capture(false);
            probe=GetComponent<CharacterReviewProbe>() ?? gameObject.AddComponent<CharacterReviewProbe>();probe.actor=actor;
        }
        private void Update()
        {
            if (!rawPreview || !graph.IsValid()) return;
            double length = originalBasicClips[clipIndex].length;
            playable.SetTime((Time.unscaledTimeAsDouble - previewStart) % Math.Max(.05, length)); graph.Evaluate(0);
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
                probe?.Session.Finish();priorTimeScale=Time.timeScale;priorCaptured=Cursor.lockState==CursorLockMode.Locked;priorOverride=actor.ViewRig!=null && actor.ViewRig.CameraOverride;
                if (actor.ViewRig != null) actor.ViewRig.CameraOverride = true;
                suspended.Clear();priorEnabled.Clear();
                void Pause(Behaviour component){if(component==null || suspended.Contains(component))return;suspended.Add(component);priorEnabled.Add(component.enabled);component.enabled=false;}
                Pause(motor);Pause(look);Pause(scanner);Pause(presentation);Pause(actor.Input);
                foreach(var component in animator.GetComponents<MonoBehaviour>())Pause(component);
                Pause(actor.GetComponent<FlashlightVisuals>());
                animator.runtimeAnimatorController = null; PlayerLook.Capture(false);Time.timeScale=0;
            }
            graph = PlayableGraph.Create("SILENCE current source clip preview"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, originalBasicClips[clipIndex]); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Original clip on our model", animator); output.SetSourcePlayable(playable); graph.Play(); previewStart = Time.unscaledTimeAsDouble;
        }
        private void EndRaw()
        {
            if (graph.IsValid()) graph.Destroy();
            if (!rawPreview) return;
            rawPreview = false;
            Time.timeScale=priorTimeScale;
            if (actor != null && actor.ViewRig != null) actor.ViewRig.CameraOverride = priorOverride;
            if (animator != null) { animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.Rebind(); }
            for(int i=suspended.Count-1;i>=0;i--)if(suspended[i]!=null)suspended[i].enabled=priorEnabled[i];
            suspended.Clear();priorEnabled.Clear();PlayerLook.Capture(priorCaptured);
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
            GUILayout.Label("Клипы текущего контроллера: "+originalBasicClips.Length+" (просмотр отдельно от игровой локомоции):");
            if (rawPreview)
            {
                GUILayout.Label("Сырой клип: " + originalBasicClips[clipIndex].name + ". IK и игровой контроллер временно выключены.");
                GUILayout.BeginHorizontal(); if (GUILayout.Button("←")) RawClip(clipIndex - 1); if (GUILayout.Button("→")) RawClip(clipIndex + 1); GUILayout.EndHorizontal();
                if (GUILayout.Button("Вернуться к игровой локомоции")) EndRaw();
            }
            else if (GUILayout.Button("Просмотреть все исходные клипы")) RawClip(0);
            if(!rawPreview && probe!=null)
            {
                GUILayout.Space(8);GUILayout.Label("Проверка 15 сценариев: запись не означает успешную визуальную приёмку.");
                scenario=GUILayout.SelectionGrid(scenario,CharacterReviewSession.Names,2);
                var current=probe.Session.cases[scenario];GUILayout.Label("Статус: "+current.status+" · кадров: "+current.measurements.samples);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("Записать сценарий"))probe.Begin(scenario);
                if(GUILayout.Button("Закончить запись"))probe.Session.Finish();GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUI.enabled=current.status!=CharacterReviewStatus.Recording && current.status!=CharacterReviewStatus.Unavailable && current.measurements.samples>0;
                if(GUILayout.Button("Осмотрел: нормально"))probe.Session.Mark(scenario,true,"Подтверждено пользователем в Unity.");
                if(GUILayout.Button("Есть дефект"))probe.Session.Mark(scenario,false,"Пользователь обнаружил дефект; приложи изображение/описание.");GUILayout.EndHorizontal();
                GUI.enabled=true;
                var stats=current.measurements;GUILayout.Label("Локти max: "+stats.maxArmSpread.ToString("F1")+"° · сдвиг кисти/кадр: "+stats.maxHandStep.ToString("F3")+" м · скольжение опорной стопы max: "+stats.maxFootSlip.ToString("F2")+" м/с");
                GUILayout.BeginHorizontal();
                foreach(int fps in new[]{15,30,60})if(GUILayout.Button(fps+" FPS")){QualitySettings.vSyncCount=0;Application.targetFrameRate=fps;}
                if(GUILayout.Button("Вернуть FPS")){QualitySettings.vSyncCount=priorVsync;Application.targetFrameRate=priorFps;}GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();if(GUILayout.Button("Сохранить отчёт"))try{probe.Save();}catch(Exception e){Debug.LogException(e);}
                if(GUILayout.Button("Скопировать отчёт"))GUIUtility.systemCopyBuffer=probe.Json();GUILayout.EndHorizontal();
                if(!string.IsNullOrEmpty(probe.Note))GUILayout.Label(probe.Note);
            }
            GUILayout.Label("Кооператив проверяй отдельными Host/Client. Вернуться в свой мир: останови Play → меню 29 → Вернуться из площадки в игровой мир.");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }
        private void OnDisable()
        {
            if (actor != null && animator != null) EndRaw(); else if (graph.IsValid()) graph.Destroy();
            if(settingsCaptured){QualitySettings.vSyncCount=priorVsync;Application.targetFrameRate=priorFps;settingsCaptured=false;}
            if (actor != null && actor.ViewRig != null) actor.ViewRig.CameraOverride = false;
            if (observer != null) observer.enabled = false; if (actor != null && actor.View != null) actor.View.enabled = actor.ViewRig == null;
        }
    }
}
