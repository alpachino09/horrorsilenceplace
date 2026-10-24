using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class AnimationClipPreparation
    {
        [Serializable] public sealed class Measurements
        {
            public string clip, source;
            public float duration, measuredStanceSpeed, leftContactFraction, rightContactFraction;
            public bool humanoid, loop, nativePoseSampled;
        }
        // Sampling occurs only in the user's actual Unity Editor on a private preview
        // scene; the open world and original .anim files are never mutated.
        public static AnimationClip Clone(AnimationClip source, Animator target, string path, bool loop, bool moving, out Measurements measurements)
        {
            if (source == null || source.legacy || !source.isHumanMotion || target.avatar == null || !target.avatar.isValid || !target.avatar.isHuman)
                throw new InvalidOperationException("Нужны реальный Humanoid-клип и действительный Avatar: " + (source != null ? source.name : "null"));
            var clip = Object.Instantiate(source); clip.name = source.name;
            AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop; settings.loopBlend = loop;
            // Extract planar travel/turns into root motion, then deliberately discard it
            // via applyRootMotion=false. Baking travelling clips into the pose instead
            // would let the hips walk metres away from the collision capsule.
            settings.loopBlendOrientation = false; settings.loopBlendPositionXZ = false;
            settings.loopBlendPositionY = source.name != "Jumping Up" && source.name != "Jumping Down" && source.name != "Fall";
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = false;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, path);
            measurements = Sample(clip, target, loop, moving);
            measurements.source = AssetDatabase.GetAssetPath(source);
            EditorUtility.SetDirty(clip);
            return clip;
        }
        private static Measurements Sample(AnimationClip clip, Animator source, bool loop, bool moving)
        {
            Scene preview = EditorSceneManager.NewPreviewScene(); GameObject instance = null; PlayableGraph graph = default;
            try
            {
                instance = Object.Instantiate(source.gameObject); instance.name = "SILENCE isolated clip sampler";
                SceneManager.MoveGameObjectToScene(instance, preview); instance.hideFlags = HideFlags.HideAndDontSave;
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true)) component.enabled = false;
                instance.transform.SetPositionAndRotation(Vector3.zero, source.transform.rotation);
                instance.transform.localScale = source.transform.lossyScale;
                var animator = instance.GetComponent<Animator>(); animator.runtimeAnimatorController = null;
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind();
                graph = PlayableGraph.Create("SILENCE clip contact audit"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                var output = AnimationPlayableOutput.Create(graph, "Humanoid", animator); output.SetSourcePlayable(playable); graph.Play();
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (left == null || right == null) throw new InvalidOperationException("Avatar не содержит обе стопы.");
                int count = Mathf.Clamp(Mathf.CeilToInt(clip.length * 60), 30, 1200);
                var l = new Vector3[count + 1]; var r = new Vector3[count + 1];
                Vector3 firstBody = Vector3.zero;
                for (int i = 0; i <= count; i++)
                {
                    playable.SetTime(clip.length * i / count); graph.Evaluate(0);
                    l[i] = left.position; r[i] = right.position;
                    if (!Finite(l[i]) || !Finite(r[i])) throw new InvalidOperationException("Некорректная поза в клипе: " + clip.name);
                    if (i == 0) firstBody = animator.bodyPosition;
                    if (moving && loop && Vector3.ProjectOnPlane(animator.bodyPosition - firstBody, Vector3.up).magnitude > .5f)
                        throw new InvalidOperationException("Клип уводит тело от капсулы: " + clip.name + ". Проверь извлечение Root Motion/Avatar. Исходная сцена сохранена.");
                }
                float lowL = l.Min(p => p.y), lowR = r.Min(p => p.y);
                float rangeL = l.Max(p => p.y) - lowL, rangeR = r.Max(p => p.y) - lowR;
                if (moving && (rangeL < .008f || rangeR < .008f))
                    throw new InvalidOperationException("Клип не дал движущуюся Humanoid-позу при проверке: " + clip.name + ". Проверь Rig/Avatar. Сцена не изменена.");
                var lc = new AnimationCurve(); var rc = new AnimationCurve(); var speeds = new List<float>();
                float stepTime = clip.length / count, totalL = 0, totalR = 0;
                for (int i = 0; i <= count; i++)
                {
                    // Contact is inferred from actual retargeted ankle height, not an invented
                    // sin/cos phase. Each foot releases during swing; standing stays planted.
                    float lw = moving || rangeL > .04f ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lowL + .015f, lowL + Mathf.Max(.045f, rangeL * .3f), l[i].y)) : 1;
                    float rw = moving || rangeR > .04f ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lowR + .015f, lowR + Mathf.Max(.045f, rangeR * .3f), r[i].y)) : 1;
                    lc.AddKey(stepTime * i, lw); rc.AddKey(stepTime * i, rw); totalL += lw; totalR += rw;
                    if (moving && i > 0)
                    {
                        void Add(Vector3 delta, float weight) { float speed = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude / stepTime; if (weight > .65f && speed > .15f && speed < 12) speeds.Add(speed); }
                        Add(l[i] - l[i - 1], lw); Add(r[i] - r[i - 1], rw);
                    }
                }
                for (int i = 0; i < lc.length; i++) { AnimationUtility.SetKeyLeftTangentMode(lc, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(lc, i, AnimationUtility.TangentMode.Linear); }
                for (int i = 0; i < rc.length; i++) { AnimationUtility.SetKeyLeftTangentMode(rc, i, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(rc, i, AnimationUtility.TangentMode.Linear); }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "LeftFootPlant"), lc);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), "RightFootPlant"), rc);
                speeds.Sort(); float measured = speeds.Count > 4 ? speeds[speeds.Count / 2] : 0;
                if (moving && (measured < .15f || totalL < 1 || totalR < 1)) throw new InvalidOperationException("Не удалось измерить контакт/скорость: " + clip.name);
                // Align locomotion clips on the first left-foot strike before blending gaits.
                if (loop && moving)
                {
                    for (int i = 1; i < lc.length; i++)
                        if (lc.keys[i].value >= .65f && lc.keys[i - 1].value < .65f)
                        { var s = AnimationUtility.GetAnimationClipSettings(clip); s.cycleOffset = lc.keys[i].time / clip.length; AnimationUtility.SetAnimationClipSettings(clip, s); break; }
                }
                return new Measurements { clip = clip.name, duration = clip.length, humanoid = clip.isHumanMotion, loop = loop, nativePoseSampled = true,
                    measuredStanceSpeed = measured, leftContactFraction = totalL / (count + 1), rightContactFraction = totalR / (count + 1) };
            }
            finally { if (graph.IsValid()) graph.Destroy(); if (instance != null) Object.DestroyImmediate(instance); EditorSceneManager.ClosePreviewScene(preview); }
        }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}
