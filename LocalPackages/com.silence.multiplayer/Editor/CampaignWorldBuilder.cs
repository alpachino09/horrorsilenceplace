using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using Unity.Netcode;
using Silence.Config;
using Silence.Audio;
using Silence.Core;
using Silence.Creatures;
using Silence.Gameplay;
using Silence.Interactions;
using Silence.Inventory;
using Silence.SoundSimulation;
using Object = UnityEngine.Object;

namespace Silence.Multiplayer.Editor
{
    // Original procedural blockout and draft narrative. It does not import third-party artwork.
    internal sealed class CampaignWorldBuilder
    {
        private const string Output = CampaignSceneGenerator.Output;
        private readonly GameplayDirector game;
        private readonly Dictionary<string, ItemDefinition> items = new Dictionary<string, ItemDefinition>();
        private readonly Material wood, concrete, metal, grass, dirt, glass, water, paper, skin, cloth;
        public CampaignWorldBuilder(GameplayDirector director)
        {
            game = director;
            wood = Material("WeatheredWood", new Color(.27f, .21f, .15f));
            concrete = Material("WeatheredConcrete", new Color(.4f, .4f, .37f));
            metal = Material("OxidizedMetal", new Color(.27f, .31f, .3f));
            grass = Material("DryGrass", new Color(.2f, .24f, .15f));
            dirt = Material("Mud", new Color(.27f, .23f, .18f));
            glass = Material("GlassDebris", new Color(.35f, .5f, .5f));
            water = Material("LakeWater", new Color(.08f, .19f, .2f));
            paper = Material("Paper", new Color(.75f, .69f, .5f));
            skin = Material("CreatureSkin", new Color(.27f, .29f, .26f));
            cloth = Material("Cloth", new Color(.2f, .23f, .24f));
            Item("battery", "Батарейка", "Battery"); Item("document", "Документ", "Document");
            Item("medicine", "Лекарство", "Medicine"); Item("rescue_kit", "Спасательный набор", "Rescue kit");
            Item("backpack", "Рюкзак +2 места", "Backpack +2 slots");
        }
        public ChapterDefinition CreateDefinition(int index)
        {
            string[] ru = { "Ферма", "Город", "Тоннели", "Завод", "Озеро", "Радиостанция", "Лаборатория" };
            string[] en = { "Farm", "Town", "Tunnels", "Factory", "Lake", "Radio Station", "Laboratory" };
            // Content below is an editable proposal, not an alteration of the owner's original GDD.
            string[] notesRu = {
                "Дежурный журнал. После заката скот замолкает раньше птиц. Вчера стекло у сарая привело его прямо к двери. Оно не смотрело на свет. Когда я перестал двигаться, оно потеряло меня.",
                "Записка в аптеке. Мы оставили маршрут для тех, кто ещё идёт с севера. У тоннеля есть люди. Если перекрыть проход, их уже никто не услышит. Патруль реагирует на шаги по стеклу.",
                "Журнал ремонтника. Вентиляция скрывает тихие шаги, но металлические настилы выдают бег. По тоннелю двигали герметичные контейнеры из северной лаборатории. Один пришёл пустым.",
                "Сменная ведомость. Завод больше не выпускает продукцию. Генераторы оставлены для водозабора и радиомачты. При эвакуации нам приказали не включать внешнюю связь.",
                "Полевой отчёт. Изо льда извлекли живой образец. После оттаивания он искал источник колебаний. На берегу остались люди, которых не взяли в последнюю группу. Их сигналы идут к старой радиостанции.",
                "Копия радиограммы. Карантин касается не территории, а всего вывозимого материала. У лаборатории есть исходные записи. Уничтожение записей избавит от преследования, но лишит остальных предупреждения.",
                "Последний протокол. Зрение образца не обнаружено. Слух и вибрационная чувствительность превышают человеческие. Выраженное акустическое маскирование мешает локализации. Данные нужно передать до эвакуации. Не перевозить образцы."
            };
            string[] notesEn = {
                "Duty log. At dusk the cattle fall silent before the birds. Broken glass drew it to the barn door. It never followed the light. When I stopped moving, it lost me.",
                "Pharmacy note. We left a route for survivors coming from the north. People are waiting at the tunnel. Closing the passage will abandon them. The patrol hears footsteps on broken glass.",
                "Maintenance log. Ventilation masks quiet steps, but running on the metal walkways carries far. Sealed containers came down from the northern laboratory. One arrived empty.",
                "Shift record. The factory no longer manufactures anything. The generators support the pumps and radio mast. During evacuation we were ordered not to use outside communications.",
                "Field report. A living specimen was extracted from the ice. After thawing it sought vibration sources. Survivors were left on the shore. Their calls reach the old radio station.",
                "Radio message copy. The quarantine applies to transported material. The laboratory holds the original research. Destroying it may hide our trail, but it will leave others without a warning.",
                "Final protocol. No functional vision detected. Hearing and vibration sensitivity exceed human limits. Acoustic masking disrupts localization. Transmit the research before evacuation. Do not transport specimens."
            };
            string[] radioRu = {
                "Если кто-нибудь слышит: уходите с фермы через северные ворота. Следуйте в город. Не бегите по дороге.",
                "Северный путь закрыт. Спуститесь в служебный тоннель. Оставьте проход открытым для людей за вами.",
                "Тоннель выходит к заводу. Слушайте вентиляцию и обходите металл. Там есть запасной маршрут к озеру.",
                "На озере сохранилась береговая тропа. Радиомачта на противоположной стороне, идите по суше.",
                "У радиостанции есть передатчик. Дождитесь ответа перед тем, как покинуть берег.",
                "В лаборатории остались данные о первом образце. Сохраните их, если хотите предупредить остальных.",
                "Канал эвакуации открыт. Передайте предупреждение и выходите через шлюз. Не берите образцы с собой."
            };
            string[] radioEn = {
                "If anyone hears this: leave the farm through the north gate. Head into town. Keep your footsteps quiet.",
                "The northern road is closed. Use the service tunnel. Leave a route for the people behind you.",
                "The tunnel leads to the factory. Listen for ventilation; avoid running on metal. The lake route starts there.",
                "A shore path remains at the lake. The radio mast is beyond it. Travel over land.",
                "There is a transmitter at the radio station. Wait for a reply before leaving the shore.",
                "The laboratory still holds the original specimen records. Preserve them to warn the others.",
                "The evacuation channel is open. Send the warning and leave through the airlock. Do not carry specimens."
            };
            var chapter = ScriptableObject.CreateInstance<ChapterDefinition>();
            chapter.id = (ChapterId)index; chapter.titleRu = ru[index]; chapter.titleEn = en[index];
            chapter.noteRu = notesRu[index]; chapter.noteEn = notesEn[index];
            chapter.radioRu = radioRu[index]; chapter.radioEn = radioEn[index];
            chapter.center = new Vector3(index * 400, 0, 0);
            float spawnZ = index == 0 ? -11 : -22;
            chapter.spawnPoints = new Vector3[4];
            for (int i = 0; i < 4; i++) chapter.spawnPoints[i] = chapter.center + new Vector3(-2.25f + i * 1.5f, .12f, spawnZ);
            chapter.creatureSpawn = chapter.center + new Vector3(14, .1f, 14);
            chapter.patrolPoints = new[] { chapter.center + new Vector3(12, 0, 12), chapter.center + new Vector3(0, 0, 15),
                chapter.center + new Vector3(-15, 0, 12), chapter.center + new Vector3(0, 0, -3) };
            chapter.ambientMasking = index == 3 ? .2f : index == 4 ? .16f : .025f;
            chapter.fogColor = index == 2 || index == 6 ? new Color(.07f, .1f, .12f) : new Color(.16f, .19f, .2f);
            chapter.hasVote = index == 1 || index == 4 || index == 5;
            chapter.voteRu = index == 5 ? "Сохранить и передать исследовательские записи?" : "Оставить безопасный проход для выживших?";
            chapter.voteEn = index == 5 ? "Preserve and transmit the research records?" : "Leave a safe passage for other survivors?";
            AssetDatabase.CreateAsset(chapter, Output + "/Chapters/" + chapter.id + ".asset"); return chapter;
        }
        public void DressFarm(Transform root)
        {
            foreach (var render in root.GetComponentsInChildren<Renderer>())
            {
                string name = render.gameObject.name;
                if (name.Contains("Fence") || name.Contains("Door") || name.Contains("Barn") || name.Contains("Stair")) render.sharedMaterial = wood;
                if (name == "Ground") { render.sharedMaterial = grass; Surface(render.gameObject, SurfaceKind.Grass); }
                else if (name == "Path") { render.sharedMaterial = dirt; Surface(render.gameObject, SurfaceKind.Dirt); }
                else if (name == "Floor" || name == "HomeLoft") Surface(render.gameObject, SurfaceKind.Wood);
            }
            // Trees and roof ridges add silhouette; all meshes and textures are generated locally.
            TreeLine(root);
            Roof(root, new Vector3(-10, 4.5f, 0), 9, 9); Roof(root, new Vector3(9, 4.9f, 6), 9, 11);
            Surface(Box("Glass debris", root, new Vector3(-2, .07f, 5), new Vector3(2, .08f, 3), glass), SurfaceKind.Glass);
            var barrier = root.Find("Barn/Door"); if (barrier != null) barrier.gameObject.AddComponent<BreakableBarrier>();
        }
        public Transform CreateMap(ChapterDefinition chapter)
        {
            Transform root = new GameObject(chapter.id + "_Environment").transform; root.position = chapter.center;
            SurfaceKind ground = chapter.id == ChapterId.Lake ? SurfaceKind.Dirt : SurfaceKind.Concrete;
            Surface(Box("Ground", root, new Vector3(0, -.25f, 0), new Vector3(58, .5f, 58), ground == SurfaceKind.Dirt ? dirt : concrete), ground);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Boundary", root, new Vector3(side * 28, 2, 0), new Vector3(.4f, 4, 56), concrete);
                Box("Boundary", root, new Vector3(0, 2, side * 28), new Vector3(56, 4, .4f), concrete);
            }
            switch (chapter.id)
            {
                case ChapterId.Town:
                    for (int i = 0; i < 3; i++)
                    { Building(root, new Vector3(-15, 0, -9 + i * 14), 9, 10, 4.8f); Building(root, new Vector3(15, 0, -9 + i * 14), 9, 10, 4.8f); }
                    Surface(Box("Broken storefront glass", root, new Vector3(0, .06f, 3), new Vector3(6, .06f, 5), glass), SurfaceKind.Glass);
                    for (int i = 0; i < 4; i++) Lamp(root, new Vector3(-5, 3, -15 + i * 10));
                    break;
                case ChapterId.Tunnels:
                    Box("Tunnel ceiling", root, new Vector3(0, 4.5f, 0), new Vector3(54, .4f, 54), concrete);
                    for (int i = 0; i < 4; i++)
                    {
                        float z = -14 + i * 10;
                        Box("Service partition", root, new Vector3(i % 2 == 0 ? -12 : 12, 2, z), new Vector3(23, 4, .5f), concrete);
                        Surface(Box("Grating", root, new Vector3(0, .05f, z + 3), new Vector3(10, .05f, 3), metal), SurfaceKind.Metal);
                        Lamp(root, new Vector3(0, 3.6f, z));
                    }
                    Mask(root, new Vector3(-8, 1, 0), new Vector3(14, 4, 45), .5f);
                    break;
                case ChapterId.Factory:
                    Box("Industrial roof", root, new Vector3(0, 7, 0), new Vector3(54, .5f, 54), metal);
                    for (int x = -18; x <= 18; x += 12)
                        for (int z = -14; z <= 14; z += 14)
                        {
                            Box("Support", root, new Vector3(x, 3.5f, z), new Vector3(.5f, 7, .5f), metal);
                            if (x != -6 && x != 6) Box("Machine", root, new Vector3(x + 2, 1, z + 3), new Vector3(3, 2, 4), metal);
                        }
                    Surface(Box("Metal aisle", root, new Vector3(0, .06f, 0), new Vector3(4, .06f, 42), metal), SurfaceKind.Metal);
                    Mask(root, new Vector3(-15, 2, 0), new Vector3(18, 6, 44), .6f);
                    Lamp(root, new Vector3(0, 5, 0)); break;
                case ChapterId.Lake:
                    Box("Water surface", root, new Vector3(17, .025f, 0), new Vector3(20, .05f, 47), water);
                    Surface(Box("Shallow wet shore", root, new Vector3(7, .045f, 0), new Vector3(2, .05f, 45), water), SurfaceKind.Water);
                    Surface(Box("Wooden pier", root, new Vector3(17, .22f, 5), new Vector3(18, .4f, 3), wood), SurfaceKind.Wood);
                    Box("Beached boat", root, new Vector3(20, .4f, 9), new Vector3(2, .8f, 4), wood);
                    Building(root, new Vector3(-15, 0, 7), 9, 9, 3.4f); TreeLine(root); break;
                case ChapterId.RadioStation:
                    Building(root, new Vector3(-14, 0, 5), 12, 14, 4);
                    for (int i = 0; i < 8; i++)
                    {
                        Box("Mast crossbar", root, new Vector3(15, 2 + i * 3, 10), new Vector3(4, .15f, .15f), metal);
                    }
                    foreach (float x in new[] { 13f, 17f }) Box("Mast upright", root, new Vector3(x, 12, 10), new Vector3(.2f, 24, .2f), metal);
                    TreeLine(root); break;
                case ChapterId.Laboratory:
                    Box("Lab ceiling", root, new Vector3(0, 4.5f, 0), new Vector3(54, .4f, 54), concrete);
                    for (int i = 0; i < 4; i++)
                    {
                        float z = -12 + i * 10;
                        foreach (float x in new[] { -16f, 16f })
                        {
                            Box("Cold chamber", root, new Vector3(x, 1.2f, z), new Vector3(4, 2.4f, 3), glass);
                            Box("Specimen silhouette", root, new Vector3(x, 1.3f, z), new Vector3(.5f, 1.8f, .5f), skin);
                        }
                        Lamp(root, new Vector3(0, 3.8f, z));
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float z = -5 + i * 10;
                        Box("Partition left", root, new Vector3(-15, 2, z), new Vector3(24, 4, .25f), concrete);
                        Box("Partition right", root, new Vector3(15, 2, z), new Vector3(24, 4, .25f), concrete);
                        Door(root, new Vector3(-3, 0, z), 3, true); // A second 3 m lane stays open, avoiding an AI dead end.
                    }
                    break;
            }
            // Distinct positions and routes are editable; the objective loop is intentionally shared.
            Pickup(root, "battery", new Vector3(-3, .5f, -12));
            Pickup(root, "document", new Vector3(3, 1.05f, 17));
            Box("Document desk", root, new Vector3(3, .45f, 17), new Vector3(1.5f, .9f, .8f), wood);
            Box("Radio desk", root, new Vector3(-3, .45f, 9), new Vector3(1.5f, .9f, .8f), metal);
            Box("Radio", root, new Vector3(-3, 1.1f, 9), new Vector3(.7f, .4f, .4f), metal).AddComponent<MissionRadio>();
            Box("Chapter exit", root, new Vector3(0, 1, 25), new Vector3(2, 2, .25f), metal).AddComponent<ChapterExit>();
            return root;
        }
        public void AddSupplies(Transform root, bool farm)
        {
            Pickup(root, "battery", new Vector3(3, .5f, -8));
            Pickup(root, "medicine", new Vector3(-3, .5f, -6));
            if (farm || root.name.StartsWith("Factory", StringComparison.Ordinal)) Pickup(root, "rescue_kit", new Vector3(3, .5f, -5));
            if (farm) Pickup(root, "backpack", new Vector3(-4, .5f, -10));
            for (int i = 0; i < 5; i++)
            {
                var prop = Box(i % 2 == 0 ? "Metal can" : "Wood box", root, new Vector3(-5 + i * 2, i % 2 == 0 ? .18f : .28f, -3),
                    Vector3.one * (i % 2 == 0 ? .3f : .5f), i % 2 == 0 ? metal : wood);
                var body = prop.AddComponent<Rigidbody>(); body.mass = i % 2 == 0 ? .5f : 3;
                body.isKinematic = true; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                prop.AddComponent<CarryableProp>(); Surface(prop, i % 2 == 0 ? SurfaceKind.Metal : SurfaceKind.Wood);
            }
        }
        public HearingCreature CreateCreature()
        {
            var root = new GameObject("Blind creature"); root.transform.position = new Vector3(14, .1f, 14);
            root.AddComponent<NetworkObject>();
            var agent = root.AddComponent<NavMeshAgent>(); agent.enabled = false;
            agent.agentTypeID = NavMesh.GetSettingsByIndex(0).agentTypeID; agent.radius = .4f; agent.height = 2;
            agent.acceleration = 16; agent.angularSpeed = 420; agent.stoppingDistance = .65f;
            var capsule = root.AddComponent<CapsuleCollider>(); capsule.radius = .32f; capsule.height = 2; capsule.center = Vector3.up;
            var creature = root.AddComponent<HearingCreature>();
            Sphere("Hunched torso", root.transform, new Vector3(0, 1.2f, 0), new Vector3(.8f, .9f, .5f), skin);
            Sphere("Faceless head", root.transform, new Vector3(0, 1.85f, .3f), new Vector3(.38f, .52f, .4f), skin);
            var bones = new List<Transform>();
            foreach (float side in new[] { -1f, 1f })
            {
                var arm = new GameObject("Long arm").transform; arm.SetParent(root.transform, false);
                arm.localPosition = new Vector3(side * .48f, 1.5f, .05f); arm.localRotation = Quaternion.Euler(0, 0, side * 12);
                Sphere("Arm", arm, new Vector3(0, -.6f, .1f), new Vector3(.19f, 1.3f, .2f), skin); bones.Add(arm);
                var leg = new GameObject("Leg").transform; leg.SetParent(root.transform, false); leg.localPosition = new Vector3(side * .24f, .8f, 0);
                Sphere("Leg", leg, new Vector3(0, -.35f, -.1f), new Vector3(.22f, .95f, .23f), skin); bones.Add(leg);
            }
            root.AddComponent<CreatureVisual>().Configure(bones.ToArray()); root.AddComponent<CreatureAudio>(); root.AddComponent<GameplayCreatureNetwork>(); return creature;
        }
        private void Building(Transform parent, Vector3 position, float width, float depth, float height)
        {
            var root = new GameObject("Abandoned building").transform; root.SetParent(parent, false); root.localPosition = position;
            Box("Floor", root, new Vector3(0, .04f, 0), new Vector3(width, .08f, depth), concrete);
            Box("Back", root, new Vector3(0, height / 2, depth / 2), new Vector3(width, height, .25f), concrete);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Side", root, new Vector3(side * width / 2, height / 2, 0), new Vector3(.25f, height, depth), concrete);
                Box("Front", root, new Vector3(side * (width + 2) / 4, height / 2, -depth / 2), new Vector3((width - 2) / 2, height, .25f), concrete);
            }
            Box("Lintel", root, new Vector3(0, (height + 2.4f) / 2, -depth / 2), new Vector3(2, height - 2.4f, .25f), concrete);
            Door(root, new Vector3(-1, 0, -depth / 2), 2, true); Roof(root, Vector3.up * height, width + .8f, depth + .8f);
        }
        private void Door(Transform root, Vector3 hinge, float width, bool breakable)
        {
            var pivot = new GameObject("Manual door"); pivot.transform.SetParent(root, false); pivot.transform.localPosition = hinge;
            Box("DoorLeaf", pivot.transform, new Vector3(width / 2, 1.12f, 0), new Vector3(width, 2.24f, .12f), wood);
            pivot.AddComponent<ManualDoor>().Configure(game);
            if (breakable) pivot.AddComponent<BreakableBarrier>(); CampaignSceneGenerator.AddDoorObstacle(pivot);
        }
        private void Roof(Transform root, Vector3 position, float width, float depth)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var roof = Box("Pitched roof", root, position + new Vector3(side * width / 4, .6f, 0), new Vector3(width * .54f, .18f, depth), metal);
                roof.transform.localRotation = Quaternion.Euler(0, 0, -side * 18);
            }
        }
        private void TreeLine(Transform root)
        {
            for (int i = 0; i < 18; i++)
            {
                float angle = i * Mathf.PI * 2 / 18, distance = 24 + i % 3;
                var tree = new GameObject("Pine").transform; tree.SetParent(root, false); tree.localPosition = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
                Box("Trunk", tree, new Vector3(0, 2.5f, 0), new Vector3(.28f, 5, .28f), wood);
                for (int j = 0; j < 3; j++) Sphere("Needles", tree, new Vector3(0, 3.5f + j, 0), new Vector3(3 - j * .65f, 2, 3 - j * .65f), grass);
            }
        }
        private static void Mask(Transform root, Vector3 position, Vector3 size, float strength)
        {
            var zone = new GameObject("Ventilation masking"); zone.transform.SetParent(root, false); zone.transform.localPosition = position;
            var masking = zone.AddComponent<AcousticMaskingZone>(); masking.size = size; masking.strength = strength; zone.AddComponent<MaskingAmbientSound>();
        }
        private void Lamp(Transform root, Vector3 position)
        {
            var go = new GameObject("Emergency lamp"); go.transform.SetParent(root, false); go.transform.localPosition = position;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(.9f, .66f, .4f);
            light.range = 9; light.intensity = 3; light.shadows = LightShadows.None;
        }
        private void Pickup(Transform root, string id, Vector3 position)
        {
            var go = Box(id, root, position, id == "document" ? new Vector3(.3f, .035f, .4f) : new Vector3(.25f, .25f, .25f), id == "document" ? paper : id == "backpack" ? cloth : metal);
            go.AddComponent<ItemPickup>().Configure(items[id]);
        }
        private void Item(string id, string ru, string en)
        {
            var value = ScriptableObject.CreateInstance<ItemDefinition>(); value.id = id; value.labelRu = ru; value.labelEn = en;
            AssetDatabase.CreateAsset(value, Output + "/" + id + ".asset"); items[id] = value;
        }
        private static void Surface(GameObject go, SurfaceKind kind)
        {
            var material = go.GetComponent<SurfaceMaterial>() ?? go.AddComponent<SurfaceMaterial>(); material.kind = kind;
            material.soundObstruction = kind == SurfaceKind.Wood ? .25f : kind == SurfaceKind.Glass ? .1f : .45f;
        }
        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        private static void Sphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(go.GetComponent<Collider>());
        }
        private static Material Material(string name, Color color)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, true) { name = name + " grain", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[64 * 64]; var random = new System.Random(name.Length * 731);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float grain = (float)random.NextDouble() * .15f + .85f;
                float stain = .85f + .15f * Mathf.PerlinNoise(x * .12f, y * .12f);
                if (name.Contains("Wood")) stain *= .85f + .15f * Mathf.Sin(x * 2 + Mathf.Sin(y * .3f));
                pixels[y * 64 + x] = color * grain * stain;
            }
            texture.SetPixels(pixels); texture.Apply(); AssetDatabase.CreateAsset(texture, Output + "/Materials/" + name + "Texture.asset");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, enableInstancing = true };
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", name.Contains("Metal") || name.Contains("Water") ? .4f : .1f);
            AssetDatabase.CreateAsset(material, Output + "/Materials/" + name + ".mat"); return material;
        }
    }
}
