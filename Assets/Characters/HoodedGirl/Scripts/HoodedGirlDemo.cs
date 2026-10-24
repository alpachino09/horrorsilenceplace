using System;
using System.Collections.Generic;
using UnityEngine;

namespace Silence.HoodedGirl
{
    public sealed class HoodedGirlDemo : MonoBehaviour
    {
        public Animator model;
        public Camera viewCamera;
        private bool closeView;
        private int pose;
        private readonly List<Target> targets = new List<Target>();
        private sealed class Target { public SkinnedMeshRenderer renderer; public int index; public string name; }
        private static readonly string[] Labels = { "Нейтрально", "Моргание", "Открыть рот", "Страх", "Боль", "Удивление", "Напряжение" };
        private static readonly Dictionary<string, float>[] Poses = {
            new Dictionary<string,float>(),
            new Dictionary<string,float>{{"eyeBlinkLeft",1},{"eyeBlinkRight",1}},
            new Dictionary<string,float>{{"jawOpen",.8f}},
            new Dictionary<string,float>{{"browInnerUp",.75f},{"browOuterUpLeft",.35f},{"browOuterUpRight",.35f},{"eyeWideLeft",.5f},{"eyeWideRight",.5f},{"jawOpen",.22f},{"mouthStretchLeft",.22f},{"mouthStretchRight",.22f}},
            new Dictionary<string,float>{{"browDownLeft",.7f},{"browDownRight",.7f},{"eyeSquintLeft",.6f},{"eyeSquintRight",.6f},{"noseSneerLeft",.35f},{"noseSneerRight",.35f},{"mouthFrownLeft",.35f},{"mouthFrownRight",.35f},{"jawOpen",.16f}},
            new Dictionary<string,float>{{"browInnerUp",.8f},{"browOuterUpLeft",.7f},{"browOuterUpRight",.7f},{"eyeWideLeft",.65f},{"eyeWideRight",.65f},{"jawOpen",.5f},{"mouthFunnel",.25f}},
            new Dictionary<string,float>{{"browDownLeft",.25f},{"browDownRight",.25f},{"mouthPressLeft",.7f},{"mouthPressRight",.7f},{"mouthRollLower",.2f}}
        };
        private void Start()
        {
            if (model == null) { enabled = false; return; }
            model.applyRootMotion = false;
            foreach (var renderer in model.transform.root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    string name = renderer.sharedMesh.GetBlendShapeName(i); int dot = name.LastIndexOf('.'); if (dot >= 0) name = name.Substring(dot + 1);
                    targets.Add(new Target { renderer = renderer, index = i, name = name });
                }
        }
        private void LateUpdate()
        {
            foreach (var target in targets) if (target.renderer != null) target.renderer.SetBlendShapeWeight(target.index, Poses[pose].TryGetValue(target.name, out float weight) ? weight * 100 : 0);
            if (viewCamera == null || model == null) return;
            Transform root = model.transform.root;
            Vector3 targetPoint = root.position + Vector3.up * .88f;
            if (closeView)
            {
                Transform head = model.isHuman ? model.GetBoneTransform(HumanBodyBones.Head) : null;
                targetPoint = head != null ? head.position + root.up * .055f + root.forward * .04f : root.position + root.up * 1.60f;
            }
            viewCamera.transform.position = targetPoint + (closeView ? root.forward * .72f + root.right * .035f : root.forward * 3.7f + root.right * 1.8f + root.up * .30f);
            viewCamera.transform.LookAt(targetPoint); viewCamera.fieldOfView = closeView ? 28 : 30;
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(720, Screen.width - 24), 200), GUI.skin.box);
            GUILayout.Label("HoodedGirl — просмотр модели и анимаций");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Labels.Length; i++) if (GUILayout.Button(Labels[i])) pose = i;
            GUILayout.EndHorizontal(); GUILayout.BeginHorizontal();
            foreach (string state in new[] { "Idle", "Walk", "Run", "CrouchIdle", "LookAround", "UseItem" })
                if (GUILayout.Button(state)) model.CrossFadeInFixedTime(state, .12f);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(closeView ? "Показать целиком" : "Лицо крупно")) closeView = !closeView;
            GUILayout.EndArea();
        }
    }
}
