using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Silence.Creatures;
using Silence.Graphics;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class CreatureArtWorkshop : EditorWindow
    {
        private GameObject model;
        private AnimationClip idle, walk, run, listen, recover;
        private float height = 2, yaw, walkSpeed = 1.8f, runSpeed = 6;
        private string result = "";
        [MenuItem("SILENCE/16 Creature Art Workshop")]
        public static void Open() => GetWindow<CreatureArtWorkshop>("SILENCE Monster");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("SILENCE — внешний вид монстра", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Импортируй приобретённую модель в Unity, затем выбери её здесь. Нужны настоящий скелет и анимации для этого скелета. Инструмент создаёт отдельную копию открытой игровой сцены.", MessageType.Info);
            model = (GameObject)EditorGUILayout.ObjectField("Модель / prefab", model, typeof(GameObject), false);
            idle = Clip("Стоит / дыхание", idle); walk = Clip("Ходьба на месте", walk); run = Clip("Бег на месте", run);
            listen = Clip("Прислушивается (опция)", listen); recover = Clip("Восстановление (опция)", recover);
            height = EditorGUILayout.Slider("Высота (м)", height, 1.6f, 2.2f);
            yaw = EditorGUILayout.Slider("Разворот модели", yaw, -180, 180);
            walkSpeed = EditorGUILayout.FloatField("Скорость в walk-клипе", walkSpeed);
            runSpeed = EditorGUILayout.FloatField("Скорость в run-клипе", runSpeed);
            if (GUILayout.Button("Проверить модель"))
            {
                var errors = ValidateModel();
                result = errors.Count == 0 ? "Структура модели подходит. Внешность, качество кожи и анимации нужно осмотреть в Unity." : string.Join("\n", errors);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Создать копию сцены с выбранным монстром", GUILayout.Height(36))) Install();
            if (!string.IsNullOrEmpty(result)) EditorGUILayout.HelpBox(result, MessageType.Info);
            EditorGUILayout.HelpBox("Слух, погоня, урон и сетевой корень остаются у существующего существа. Root motion и события импортированных клипов отключаются. Атака потребует отдельной привязки к серверному событию; здесь она не имитируется по таймеру.", MessageType.None);
        }
        private static AnimationClip Clip(string label, AnimationClip value) =>
            (AnimationClip)EditorGUILayout.ObjectField(label, value, typeof(AnimationClip), false);
        private List<string> ValidateModel()
        {
            var errors = new List<string>();
            if (model == null || !EditorUtility.IsPersistent(model)) { errors.Add("Выбери импортированный prefab/FBX из Project."); return errors; }
            var animators = model.GetComponentsInChildren<Animator>(true);
            if (model.GetComponentsInChildren<Transform>(true).Any(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) != 0))
                errors.Add("В импортированном prefab есть Missing Script. Очисти визуальный prefab перед подключением.");
            if (animators.Length != 1) errors.Add("Нужен один Animator на выбранной модели. Выбери отдельный prefab существа.");
            if (!model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.sharedMesh != null && r.bones.Length > 4))
                errors.Add("На модели не найден skinned mesh со скелетом. Статичный OBJ/текстура недостаточны.");
            if (idle == null || walk == null || run == null) errors.Add("Назначь обязательные idle, walk и run из комплекта модели.");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                    if (material == null || material.shader == null ||
                        !(material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) || material.GetTag("RenderPipeline", false) == "UniversalPipeline"))
                        errors.Add("Материал требует настройки под URP: " + renderer.name);
            foreach (var clip in new[] { idle, walk, run, listen, recover }.Where(c => c != null).Distinct())
            {
                if (clip.legacy) { errors.Add("Клип Legacy не подходит для Animator: " + clip.name); continue; }
                if (clip.humanMotion)
                {
                    if (animators.Length == 1 && (animators[0].avatar == null || !animators[0].avatar.isValid || !animators[0].avatar.isHuman))
                        errors.Add("Humanoid-клип требует совместимый Humanoid Avatar: " + clip.name);
                }
                else if (animators.Length == 1)
                {
                    foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                        if (binding.type == typeof(Transform) && !string.IsNullOrEmpty(binding.path) && animators[0].transform.Find(binding.path) == null)
                        { errors.Add("Клип не совпадает со скелетом: " + clip.name + " / " + binding.path); break; }
                }
            }
            if (float.IsNaN(walkSpeed) || float.IsInfinity(walkSpeed) || float.IsNaN(runSpeed) || float.IsInfinity(runSpeed) || walkSpeed <= 0 || runSpeed <= 0)
                errors.Add("Скорости клипов должны быть конечными положительными числами.");
            return errors.Distinct().ToList();
        }
        private void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            var errors = ValidateModel();
            if (errors.Count != 0) { result = string.Join("\n", errors); Debug.LogError(result); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var source = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(source.path)) throw new InvalidOperationException("Сначала сохрани и открой игровую сцену Campaign или визуальную ферму.");
            if (Creatures(source).Length != 1) throw new InvalidOperationException("В открытой сцене должно быть одно HearingCreature. Открой игровую сцену.");
            if (Creatures(source).Single().GetComponent<CreatureModelPresentation>() != null)
                throw new InvalidOperationException("В этой сцене уже есть модель монстра. Для новой пробы открой исходную сцену.");
            string output = "Assets/_Silence/Generated/CreatureArt/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            VisualAssetLibrary.Folder(output);
            string scenePath = output + "/Creature_Art.unity";
            if (!EditorSceneManager.SaveScene(source, scenePath, true)) throw new IOException("Cannot copy source scene.");
            Scene copy = default; bool saved = false;
            try
            {
                AssetDatabase.Refresh(); copy = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive); SceneManager.SetActiveScene(copy);
                var creature = Creatures(copy).Single();
                var previousRenderers = creature.GetComponentsInChildren<Renderer>(true);
                var imported = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.UnpackPrefabInstance(imported, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var wrapper = new GameObject("Creature authored model"); wrapper.transform.SetParent(creature.transform, false);
                imported.transform.SetParent(wrapper.transform, false); imported.transform.localPosition = Vector3.zero;
                imported.transform.localRotation = Quaternion.identity;
                // Keep only rendering and skeleton components from third-party art, never another AI/network/audio rig.
                foreach (var component in imported.GetComponentsInChildren<MonoBehaviour>(true)) if (component != null) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(component);
                foreach (var component in imported.GetComponentsInChildren<Animation>(true)) Object.DestroyImmediate(component);
                wrapper.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                FitModel(wrapper.transform, height);
                var animator = imported.GetComponentInChildren<Animator>(true); animator.enabled = true; animator.applyRootMotion = false;
                animator.runtimeAnimatorController = Controller(output); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in previousRenderers) renderer.enabled = false;
                foreach (var placeholder in creature.GetComponents<CreatureVisual>()) placeholder.enabled = false;
                creature.gameObject.AddComponent<CreatureModelPresentation>().Configure(animator, walkSpeed, runSpeed);
                EditorUtility.SetDirty(creature.gameObject); AssetDatabase.SaveAssets();
                if (!EditorSceneManager.SaveScene(copy, scenePath)) throw new IOException("Cannot save creature scene.");
                saved = true;
                Directory.CreateDirectory("Library/Silence");
                File.WriteAllText("Library/Silence/CreatureArt.txt", "Created " + scenePath + "\nSource model: " + AssetDatabase.GetAssetPath(model) +
                    "\nUnity: " + Application.unityVersion + "\nUTC: " + DateTime.UtcNow.ToString("O") +
                    "\nNo native gameplay, animation quality or multiplayer approval is implied.\n");
                result = "Создано: " + scenePath + ". Осмотри модель в Play и пересобери обе сетевые стороны из этой сцены.";
            }
            finally
            {
                if (copy.IsValid() && copy.isLoaded) EditorSceneManager.CloseScene(copy, true);
                if (source.IsValid() && source.isLoaded) SceneManager.SetActiveScene(source);
            }
            if (saved)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var scenes = EditorBuildSettings.scenes.ToList(); scenes.Add(new EditorBuildSettingsScene(scenePath, true)); EditorBuildSettings.scenes = scenes.ToArray();
                Selection.activeGameObject = Creatures(SceneManager.GetActiveScene()).Single().gameObject;
                SceneView.lastActiveSceneView?.FrameSelected(); Debug.Log(result);
            }
        }
        private static HearingCreature[] Creatures(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<HearingCreature>(true)).ToArray();
        private AnimatorController Controller(string output)
        {
            var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/Creature.controller");
            var machine = controller.layers[0].stateMachine;
            var definitions = new[] { ("Idle", idle, true), ("Walk", walk, true), ("Run", run, true),
                ("Listen", listen != null ? listen : idle, true), ("Recover", recover != null ? recover : idle, recover == null) };
            foreach (var definition in definitions)
            {
                var clip = Object.Instantiate(definition.Item2); clip.name = definition.Item1;
                AnimationUtility.SetAnimationEvents(clip, Array.Empty<AnimationEvent>());
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = definition.Item3;
                AnimationUtility.SetAnimationClipSettings(clip, settings); AssetDatabase.CreateAsset(clip, output + "/" + definition.Item1 + ".anim");
                var state = machine.AddState(definition.Item1); state.motion = clip;
                if (definition.Item1 == "Idle") machine.defaultState = state;
            }
            return controller;
        }
        private static void FitModel(Transform wrapper, float metres)
        {
            var renderers = wrapper.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            bool first = true; var local = new Bounds();
            foreach (var renderer in renderers)
            {
                var b = renderer.bounds;
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                {
                    var point = wrapper.parent.InverseTransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x, y, z)));
                    if (first) { local = new Bounds(point, Vector3.zero); first = false; } else local.Encapsulate(point);
                }
            }
            if (first || local.size.y < .01f) throw new InvalidOperationException("У модели нет корректных skinned bounds. Проверь импорт скелета.");
            float scale = metres / local.size.y; wrapper.localScale = Vector3.one * scale;
            wrapper.localPosition = new Vector3(-local.center.x, -local.min.y, -local.center.z) * scale;
        }
    }
}
