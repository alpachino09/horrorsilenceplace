using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Silence.Config;
using Silence.Input;
using Silence.Interactions;
using Silence.Inventory;
using Silence.Missions;
using Silence.Networking;
using Silence.Player;
using Silence.SoundSimulation;
using Silence.UI;
using Object = UnityEngine.Object;
using PlayerSettings = Silence.Config.PlayerSettings;

namespace Silence.Editor
{
    public static class SilenceSceneGenerator
    {
        public const string Output = "Assets/_Silence/Generated/Sprint01";
        public const string FarmPath = Output + "/Farm_Prototype.unity";
        public const string BootstrapPath = Output + "/Bootstrap.unity";
        public const string InputPath = "Assets/_Silence/Settings/SilenceControls.inputactions";
        private const string Marker = Output + "/silence-generated.txt";

        [MenuItem("SILENCE/01 Generate Farm Prototype")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before generating scenes.");
            if (!Application.unityVersion.StartsWith("6000.", StringComparison.Ordinal))
                throw new InvalidOperationException("This package targets Unity 6 (6000.x).");
            if (GraphicsSettings.currentRenderPipeline == null ||
                GraphicsSettings.currentRenderPipeline.GetType().Name != "UniversalRenderPipelineAsset")
                throw new InvalidOperationException("Create/open a Universal 3D project with URP enabled.");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            if (actions == null) throw new InvalidOperationException("Input Actions asset was not imported: " + InputPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");

            if (AssetDatabase.IsValidFolder(Output))
            {
                if (File.Exists(Marker) && File.Exists(FarmPath) && File.Exists(BootstrapPath))
                {
                    RegisterScenes();
                    Debug.Log("SILENCE: existing prototype preserved. Nothing regenerated.");
                    return;
                }
                throw new InvalidOperationException("Output folder already exists or generation was interrupted. " +
                    "Preserve it by renaming it in the Project window, then generate again: " + Output);
            }

            EnsureFolder(Output);
            Material grass = Material("Grass", new Color(0.18f, 0.23f, 0.13f), shader);
            Material wood = Material("Wood", new Color(0.3f, 0.2f, 0.12f), shader);
            Material wall = Material("Plaster", new Color(0.48f, 0.45f, 0.35f), shader);
            Material metal = Material("Metal", new Color(0.24f, 0.29f, 0.3f), shader);
            Material dirt = Material("Dirt", new Color(0.28f, 0.24f, 0.17f), shader);
            Material itemMaterial = Material("Items", new Color(0.8f, 0.57f, 0.18f), shader);
            var config = ScriptableObject.CreateInstance<PlayerSettings>();
            AssetDatabase.CreateAsset(config, Output + "/PlayerSettings.asset");
            ItemDefinition battery = Item("battery", "Батарейка", "Battery");
            ItemDefinition medicine = Item("medicine", "Медикаменты", "Medicine");
            ItemDefinition document = Item("document", "Документ", "Document");

            Scene previous = SceneManager.GetActiveScene();
            Scene farm = default;
            Scene bootstrap = default;
            try
            {
                farm = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(farm);
                GameObject world = new GameObject("Farm_Graybox");
                Transform root = world.transform;
                Box("Ground", root, new Vector3(0, -0.25f, 0), new Vector3(44, 0.5f, 44), grass);
                Box("Path", root, new Vector3(0, 0.01f, 2), new Vector3(3, 0.02f, 36), dirt);
                BuildFence(root, wood);
                BuildBuilding(root, "Home", new Vector3(-10, 0, 0), 8, 8, 4.4f, 1.6f, wall, wood);
                BuildBuilding(root, "Barn", new Vector3(9, 0, 6), 8, 10, 4.8f, 2.8f, wood, metal);
                for (int i = 0; i < 10; i++)
                {
                    float h = (i + 1) * 0.18f;
                    Box("Stair_" + i, root, new Vector3(-8, h / 2, -2 + i * 0.4f),
                        new Vector3(1.8f, h, 0.4f), wood);
                }
                Box("HomeLoft", root, new Vector3(-10, 1.7f, 2.8f), new Vector3(7.5f, 0.2f, 2), wood);
                Box("LowCanopy", root, new Vector3(5, 1.3f, -7), new Vector3(3, 0.2f, 3), wood);
                foreach (float x in new[] { 3.6f, 6.4f })
                    foreach (float z in new[] { -8.4f, -5.6f })
                        Box("CanopyPost", root, new Vector3(x, 0.6f, z), new Vector3(0.15f, 1.2f, 0.15f), wood);
                Box("HomeTable", root, new Vector3(-11, 0.5f, -1), new Vector3(2, 1, 1), wood);
                Pickup(root, "Battery_Home", new Vector3(-11.4f, 1.15f, -1), battery, itemMaterial);
                Pickup(root, "Document_Home", new Vector3(-10.5f, 1.15f, -1), document, itemMaterial);
                Box("BarnTable", root, new Vector3(9, 0.5f, 6), new Vector3(2.4f, 1, 1), wood);
                Pickup(root, "Medicine_Barn", new Vector3(8.4f, 1.15f, 6), medicine, itemMaterial);
                Pickup(root, "Battery_Barn", new Vector3(9.5f, 1.15f, 6), battery, itemMaterial);
                GameObject radio = Box("Radio_Prototype", root, new Vector3(-12.5f, 0.8f, 1),
                    new Vector3(0.7f, 0.5f, 0.4f), metal);
                Light indicator = CreateLight("RadioIndicator", radio.transform,
                    new Vector3(0, 0.35f, 0), LightType.Point, Color.green, 0.8f, 2f);
                indicator.enabled = false;
                radio.AddComponent<PrototypeRadio>().Configure(indicator);
                GameObject exit = Box("ExitMarker", root, new Vector3(0, 1f, 20),
                    new Vector3(1.2f, 2, 0.2f), metal);
                exit.AddComponent<PrototypeExit>();
                SetupLighting(root);

                GameObject services = new GameObject("OfflineWorld");
                var noise = services.AddComponent<NoiseSystem>();
                var authority = services.AddComponent<OfflineWorldAuthority>();
                authority.Configure(noise);
                PlayerContext player = BuildPlayer(config, actions, wood,
                    new[] { battery, medicine, document });
                services.AddComponent<FarmBootstrap>().Configure(authority, player);
                SaveScene(farm, FarmPath);
                EditorSceneManager.CloseScene(farm, true);
                farm = default;

                bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(bootstrap);
                new GameObject("BootstrapLoader").AddComponent<BootstrapLoader>().Configure(FarmPath);
                SaveScene(bootstrap, BootstrapPath);
                EditorSceneManager.CloseScene(bootstrap, true);
                bootstrap = default;
                RegisterScenes();
                File.WriteAllText(Marker, "Generated by SILENCE Sprint 01. Existing assets are never overwritten.\n");
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                Debug.Log("SILENCE: scenes created. Open " + BootstrapPath + ", then press Play.");
            }
            finally
            {
                if (farm.IsValid() && farm.isLoaded) EditorSceneManager.CloseScene(farm, true);
                if (bootstrap.IsValid() && bootstrap.isLoaded) EditorSceneManager.CloseScene(bootstrap, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        private static PlayerContext BuildPlayer(PlayerSettings settings, InputActionAsset actions,
            Material bodyMaterial, ItemDefinition[] definitions)
        {
            var go = new GameObject("Player");
            go.transform.position = new Vector3(0, 0.12f, -11);
            var controller = go.AddComponent<CharacterController>();
            controller.height = settings.standingHeight;
            controller.radius = 0.3f;
            controller.center = Vector3.up * 0.9f;
            controller.stepOffset = 0.28f;
            controller.skinWidth = 0.03f;
            controller.minMoveDistance = 0f;
            var input = go.AddComponent<PlayerInputReader>();
            input.enabled = false;
            input.Configure(actions);
            var eye = new GameObject("PlayerCamera");
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = Vector3.up * 1.64f;
            eye.tag = "MainCamera";
            Camera camera = eye.AddComponent<Camera>();
            camera.fieldOfView = 75;
            camera.nearClipPlane = 0.05f;
            eye.AddComponent<AudioListener>();
            Light lamp = CreateLight("Flashlight", eye.transform, new Vector3(0.2f, -0.1f, 0.2f),
                LightType.Spot, new Color(1, 0.94f, 0.8f), 4f, 18f);
            lamp.spotAngle = 65f;
            var context = go.AddComponent<PlayerContext>();
            context.Configure(settings, input, camera);
            go.AddComponent<FirstPersonMotor>();
            go.AddComponent<PlayerLook>();
            go.AddComponent<PlayerFlashlight>().Configure(lamp);
            go.AddComponent<InteractionScanner>();
            go.AddComponent<PrototypeHud>().Configure(definitions);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body_Graybox";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.up * 0.9f;
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
            body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            eye.SetActive(false); // Only a registered local owner enables camera/audio/input.
            if (PrefabUtility.SaveAsPrefabAssetAndConnect(go, Output + "/Player.prefab", InteractionMode.AutomatedAction) == null)
                throw new IOException("Cannot save Player.prefab.");
            return context;
        }

        private static void BuildBuilding(Transform parent, string name, Vector3 center,
            float width, float depth, float height, float doorway, Material wall, Material roof)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = center;
            Box("Floor", root, new Vector3(0, 0.025f, 0), new Vector3(width, 0.05f, depth), wall);
            Box("Back", root, new Vector3(0, height / 2, depth / 2), new Vector3(width, height, 0.2f), wall);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Side", root, new Vector3(side * width / 2, height / 2, 0), new Vector3(0.2f, height, depth), wall);
                float segment = (width - doorway) / 2;
                Box("Front", root, new Vector3(side * (doorway / 2 + segment / 2), height / 2, -depth / 2),
                    new Vector3(segment, height, 0.2f), wall);
            }
            float doorHeight = doorway > 2 ? 3f : 2.3f;
            Box("Lintel", root, new Vector3(0, (doorHeight + height) / 2, -depth / 2),
                new Vector3(doorway, height - doorHeight, 0.2f), wall);
            Box("Roof", root, new Vector3(0, height + 0.1f, 0), new Vector3(width + 0.5f, 0.2f, depth + 0.5f), roof);
            Door(root, "Door", new Vector3(-doorway / 2, 0, -depth / 2), doorway, doorHeight - 0.08f, roof);
            CreateLight("InteriorLight", root, new Vector3(0, 2.6f, 0), LightType.Point,
                new Color(1, 0.65f, 0.35f), 5f, 11f);
        }

        private static void BuildFence(Transform parent, Material wood)
        {
            for (int i = -20; i <= 20; i += 2)
            {
                Box("FencePost", parent, new Vector3(i, 0.9f, -18), new Vector3(0.18f, 1.8f, 0.18f), wood);
                if (Mathf.Abs(i) >= 2)
                    Box("FencePost", parent, new Vector3(i, 0.9f, 18), new Vector3(0.18f, 1.8f, 0.18f), wood);
            }
            for (int z = -16; z <= 16; z += 2)
                foreach (float x in new[] { -20f, 20f })
                    Box("FencePost", parent, new Vector3(x, 0.9f, z), new Vector3(0.18f, 1.8f, 0.18f), wood);
            foreach (float y in new[] { 0.65f, 1.35f })
            {
                Box("FenceRail", parent, new Vector3(0, y, -18), new Vector3(40, 0.16f, 0.14f), wood);
                foreach (float x in new[] { -20f, 20f })
                    Box("FenceRail", parent, new Vector3(x, y, 0), new Vector3(0.14f, 0.16f, 36), wood);
                foreach (float x in new[] { -10.8f, 10.8f })
                    Box("FenceRail", parent, new Vector3(x, y, 18), new Vector3(18.4f, 0.16f, 0.14f), wood);
            }
            Door(parent, "ExitGate", new Vector3(-1.6f, 0, 18), 3.2f, 1.7f, wood);
        }

        private static void Door(Transform parent, string name, Vector3 hinge, float width, float height, Material material)
        {
            Transform pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hinge;
            Box("DoorLeaf", pivot, new Vector3(width / 2, height / 2, 0), new Vector3(width, height, 0.12f), material);
            pivot.gameObject.AddComponent<PrototypeDoor>();
        }
        private static void Pickup(Transform parent, string name, Vector3 position, ItemDefinition item, Material material)
        {
            Box(name, parent, position, new Vector3(0.3f, 0.25f, 0.25f), material)
                .AddComponent<ItemPickup>().Configure(item);
        }
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
        private static Light CreateLight(string name, Transform parent, Vector3 position, LightType type,
            Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Light light = go.AddComponent<Light>();
            light.type = type; light.color = color; light.intensity = intensity; light.range = range;
            return light;
        }
        private static void SetupLighting(Transform root)
        {
            Light sun = CreateLight("DuskSun", root, Vector3.zero, LightType.Directional,
                new Color(0.7f, 0.76f, 1), 0.7f, 0);
            sun.transform.rotation = Quaternion.Euler(35, -25, 0);
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.23f, 0.25f, 0.3f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.15f, 0.18f, 0.22f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.012f;
        }
        private static Material Material(string name, Color color, Shader shader)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, Output + "/" + name + ".mat");
            return material;
        }
        private static ItemDefinition Item(string id, string ru, string en)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.id = id; item.labelRu = ru; item.labelEn = en;
            AssetDatabase.CreateAsset(item, Output + "/" + id + ".asset");
            return item;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static void SaveScene(Scene scene, string path)
        {
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Cannot save scene: " + path);
        }
        private static void RegisterScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (string path in new[] { BootstrapPath, FarmPath })
            {
                int i = scenes.FindIndex(x => x.path == path);
                if (i < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[i] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
