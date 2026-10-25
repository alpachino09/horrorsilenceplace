using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Core;
using Silence.Player;
using Silence.Graphics.Players;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class ImportedAssetsWorkshop : EditorWindow
    {
        public const string Version="0.6.0";
        [SerializeField] private DefaultAsset textureFolder,motionFolder;
        [SerializeField] private bool textures=true,motions=true,details;
        [SerializeField] private float tileMetres=4;
        [SerializeField] private ImportedTextureSet[] ground=new ImportedTextureSet[4];
        [SerializeField] private AnimationClip[] clips=new AnimationClip[14];
        private ImportedTextureSet[] sets=Array.Empty<ImportedTextureSet>();
        private AnimationClip[] available=Array.Empty<AnimationClip>();
        private string message;
        private Vector2 scroll;
        private static readonly string[] GroundLabels={"Лесная почва","Трава","Земля / грязь","Гравий / камни"};
        private static readonly string[] MotionLabels={"Покой","Ходьба","Бег","Спринт","Ходьба назад","Шаг влево","Шаг вправо","Бег назад","Бег влево","Бег вправо","Покой в приседе","Ходьба в приседе","Ранение: покой","Ранение: ходьба"};
        [MenuItem("SILENCE/28 Imported Textures and Human Motions")]
        public static void Open()=>GetWindow<ImportedAssetsWorkshop>("SILENCE Assets");
        private void OnEnable()
        {
            if(textureFolder==null)textureFolder=FolderAsset(ImportedAssetCatalog.FindFolder("ObjectiveEnvironment_Assets"," realistic terrain textures "));
            if(motionFolder==null)motionFolder=FolderAsset(ImportedAssetCatalog.FindFolder("Kevin Iglesias"," human basic motions "));
            try{Scan(false);}catch(Exception e){message=e.Message;}
        }
        private static DefaultAsset FolderAsset(string path)=>string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
        private void Scan(bool assign)
        {
            sets=textureFolder!=null?ImportedAssetCatalog.Textures(AssetDatabase.GetAssetPath(textureFolder)):Array.Empty<ImportedTextureSet>();
            available=motionFolder!=null?ImportedAssetCatalog.Motions(AssetDatabase.GetAssetPath(motionFolder)):Array.Empty<AnimationClip>();
            if(ground==null || ground.Length!=4)ground=new ImportedTextureSet[4];if(clips==null || clips.Length!=MotionLabels.Length)Array.Resize(ref clips,MotionLabels.Length);
            if(assign)
            {
                for(int i=0;i<4;i++)ground[i]=sets.FirstOrDefault(s=>ImportedAssetModel.Ground(s.name+" "+Path.GetDirectoryName(AssetDatabase.GetAssetPath(s.color)))==(ImportedGround)i);
                for(int i=0;i<clips.Length;i++)clips[i]=ImportedAssetCatalog.Suggest(available,(ImportedMotion)i);
                // Shift reaches the existing sprint ring. If this free pack has
                // one run recording, use that real recording at measured cadence
                // there too, instead of leaving Shift on the previous vendor.
                if(clips[(int)ImportedMotion.Sprint]==null)clips[(int)ImportedMotion.Sprint]=clips[(int)ImportedMotion.Run];
            }
            message="Найдено наборов текстур: "+sets.Length+", Humanoid-клипов: "+available.Length+". Проверь назначения ниже.";
        }
        private void Run(Action action){try{if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Сначала останови Play.");action();}catch(Exception e){message=e.Message;Debug.LogException(e);}finally{EditorUtility.ClearProgressBar();}}
        private void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("Текстуры и движения — "+Version,EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Проект",Path.GetFullPath("."),EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Открытый мир",SceneManager.GetActiveScene().path,EditorStyles.wordWrappedLabel);
            EditorGUILayout.HelpBox("Использует уже импортированные ассеты из твоего проекта. Назначь текстуры и Humanoid-клипы, затем создай одну копию текущего игрового мира. Рельеф, покраска Terrain, здания, двери и предметы сохраняются. Персонаж и его управление остаются прежними.",MessageType.Info);
            textureFolder=(DefaultAsset)EditorGUILayout.ObjectField("Папка текстур",textureFolder,typeof(DefaultAsset),false);
            motionFolder=(DefaultAsset)EditorGUILayout.ObjectField("Папка анимаций",motionFolder,typeof(DefaultAsset),false);
            if(GUILayout.Button("Найти импортированные ассеты и предложить назначения"))Run(()=>Scan(true));
            textures=EditorGUILayout.Toggle("Подключить текстуры земли",textures);
            if(textures)
            {
                tileMetres=EditorGUILayout.Slider("Повтор текстур, метров",tileMetres,.5f,12);
                var labels=new[]{"Оставить текущую текстуру"}.Concat(sets.Select(s=>s.name)).ToArray();
                for(int i=0;i<4;i++)
                {
                    int selected=ground[i]==null?0:Array.FindIndex(sets,s=>s.color==ground[i].color && s.name==ground[i].name)+1;
                    int next=EditorGUILayout.Popup(GroundLabels[i],Mathf.Max(0,selected),labels);
                    if(next!=selected)ground[i]=next==0?null:sets[next-1];
                }
            }
            motions=EditorGUILayout.Toggle("Подключить Human Basic Motions",motions);
            if(motions)
            {
                EditorGUILayout.HelpBox("Выбери Покой, Ходьбу и Бег. Для Shift при отсутствии отдельного спринта предлагается выбранный клип бега с измеренным темпом. Остальные необязательные пустые поля сохраняют прежние движения. При равных вариантах выбери клип вручную; модель Kevin Iglesias в мир не добавляется.",MessageType.Info);
                DrawMotion(0);DrawMotion(1);DrawMotion(2);
            }
            details=EditorGUILayout.Foldout(details,"Дополнительные движения и карты PBR");
            if(details)
            {
                if(motions)for(int i=3;i<clips.Length;i++)DrawMotion(i);
                if(textures)for(int i=0;i<4;i++)
                {
                    var set=ground[i];if(set==null)continue;
                    EditorGUILayout.LabelField(GroundLabels[i],EditorStyles.boldLabel);
                    Texture2D Field(string label,Texture2D value)=>(Texture2D)EditorGUILayout.ObjectField(label,value,typeof(Texture2D),false);
                    set.color=Field("Цвет",set.color);set.normal=Field("Normal",set.normal);set.roughness=Field("Roughness",set.roughness);set.smoothness=Field("Smoothness",set.smoothness);
                    set.occlusion=Field("Occlusion",set.occlusion);set.height=Field("Height",set.height);set.metallic=Field("Metallic",set.metallic);
                }
            }
            if(GUILayout.Button("Создать копию текущего мира с выбранными ассетами"))Run(()=>{string path=Install(ground,clips,textures,motions,tileMetres);if(path!=null)message="Открыт новый мир: "+path+". Проверь ссылки ниже, затем Play. V — внешний вид персонажа.";});
            if(GUILayout.Button("Проверить подключённые текстуры и движения"))Run(()=>{message=ValidateCurrent()?"Ссылки проверены. Осталось проверить внешний вид и движения в Play.":"Смотри Console.";});
            if(GUILayout.Button("Собрать текущий мир: Host и Client"))Run(()=>{if(ValidateCurrent())NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path);});
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
            EditorGUILayout.EndScrollView();
        }
        private void DrawMotion(int index)
        {
            var options=new[]{index<3?"Назначь клип":"Оставить текущее движение"}.Concat(available.Select(c=>c.name+" — "+AssetDatabase.GetAssetPath(c))).ToArray();
            int current=Array.IndexOf(available,clips[index])+1;int next=EditorGUILayout.Popup(MotionLabels[index],Mathf.Max(0,current),options);
            if(next!=current)clips[index]=next==0?null:available[next-1];
        }
        [Serializable] public sealed class Installation
        {
            public string version=Version,sourceScene,createdScene;
            public bool textures,motions,nativePlayTested;
            public ImportedGroundInstaller.Report ground;
            public string[] players;
            public ushort protocol;
        }
        private static NetworkManager Manager(Scene scene)
        {
            var managers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if(managers.Length!=1)throw new InvalidOperationException("Открой свой сохранённый игровой мир с одним NetworkManager. Сцена просмотра персонажа не подходит.");return managers[0];
        }
        private static NetworkPrefab[] Entries(NetworkManager manager)=>manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l=>l!=null).SelectMany(l=>l.PrefabList).Concat(manager.NetworkConfig.Prefabs.Prefabs)
            .Where(e=>e!=null && e.Prefab!=null).GroupBy(e=>(e.Override,e.Prefab,e.SourcePrefabToOverride,e.SourceHashToOverride,e.OverridingTargetPrefab)).Select(g=>g.First()).ToArray();
        public static string Install(ImportedTextureSet[] ground,AnimationClip[] clips,bool textures,bool motions,float tileMetres)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Сначала останови Play.");
            if(!textures && !motions)throw new InvalidOperationException("Выбери текстуры или движения.");
            if(motions)ImportedMotionInstaller.RequireChoices(clips);
            if(textures && (ground==null || ground.Length!=4 || !ground.Any(s=>s!=null && s.color!=null)))throw new InvalidOperationException("Выбери хотя бы один набор текстур земли.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return null;
            var source=SceneManager.GetActiveScene();if(string.IsNullOrEmpty(source.path) || source.isDirty)throw new InvalidOperationException("Сохрани текущий игровой мир.");
            var sourceManager=Manager(source);var entries=Entries(sourceManager);var original=sourceManager.NetworkConfig.PlayerPrefab;
            var players=entries.Select(e=>e.Prefab).Append(original).Where(p=>p!=null && p.GetComponent<PlayerContext>()!=null).Distinct().ToArray();
            if(motions)
            {
                if(original==null || !players.Contains(original))throw new InvalidOperationException("В мире не настроен сетевой игрок.");
                foreach(var player in players)
                    if(player.GetComponent<PlayerAvatarPresentation>()?.AnimationProfile==null || player.GetComponent<NetworkPlayer>()==null || player.GetComponentsInChildren<NetworkObject>(true).Length!=1 || entries.Any(e=>e.Prefab==player && e.Override!=NetworkPrefabOverride.None))
                        throw new InvalidOperationException("Нужен уже анимированный игрок из текущего мира: "+player.name+". Сначала заверши подключение персонажа/анимаций.");
            }
            string folder="Assets/_Silence/Generated/ImportedAssets/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");VisualAssetLibrary.Folder(folder);
            string target=folder+"/"+Path.GetFileNameWithoutExtension(source.path)+"_Assets.unity";
            Scene copy=default;bool saved=false;var record=new Installation{sourceScene=source.path,createdScene=target,textures=textures,motions=motions,protocol=sourceManager.NetworkConfig.ProtocolVersion};
            try
            {
                var replacements=new Dictionary<GameObject,GameObject>();
                if(motions)for(int i=0;i<players.Length;i++)
                {
                    string output=folder+"/Player"+i;VisualAssetLibrary.Folder(output);string path=output+"/Player.prefab";
                    if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(players[i]),path))throw new IOException("Не скопирован player prefab.");
                    var contents=PrefabUtility.LoadPrefabContents(path);
                    try{ImportedMotionInstaller.Apply(contents,clips,output);if(PrefabUtility.SaveAsPrefabAsset(contents,path)==null)throw new IOException("Не сохранён player prefab.");}
                    finally{PrefabUtility.UnloadPrefabContents(contents);}
                    replacements.Add(players[i],AssetDatabase.LoadAssetAtPath<GameObject>(path));
                }
                if(!EditorSceneManager.SaveScene(source,target,true))throw new IOException("Не создана копия текущего мира.");
                AssetDatabase.Refresh();copy=EditorSceneManager.OpenScene(target,OpenSceneMode.Additive);SceneManager.SetActiveScene(copy);var manager=Manager(copy);
                if(motions)
                {
                    var config=JsonUtility.FromJson<NetworkConfig>(JsonUtility.ToJson(manager.NetworkConfig));var registry=ScriptableObject.CreateInstance<NetworkPrefabsList>();AssetDatabase.CreateAsset(registry,folder+"/NetworkPrefabs.asset");
                    foreach(var entry in entries)if(!replacements.ContainsKey(entry.Prefab))registry.Add(entry);
                    foreach(var prefab in replacements.Values)registry.Add(new NetworkPrefab{Prefab=prefab});
                    config.PlayerPrefab=replacements[original];config.ForceSamePrefabs=true;config.ProtocolVersion=checked((ushort)Math.Max(9,sourceManager.NetworkConfig.ProtocolVersion+1));
                    config.Prefabs=new NetworkPrefabs{NetworkPrefabsLists=new List<NetworkPrefabsList>{registry}};manager.NetworkConfig=config;EditorUtility.SetDirty(manager);EditorUtility.SetDirty(registry);record.protocol=config.ProtocolVersion;
                }
                if(textures)record.ground=ImportedGroundInstaller.Apply(copy,ground,tileMetres,folder+"/Ground");
                record.players=replacements.Values.Select(AssetDatabase.GetAssetPath).ToArray();AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(copy,target))throw new IOException("Не сохранён мир с импортированными ассетами.");
                File.WriteAllText(folder+"/installation.json",JsonUtility.ToJson(record,true));saved=true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();if(copy.IsValid() && copy.isLoaded)EditorSceneManager.CloseScene(copy,true);
                if(source.IsValid() && source.isLoaded)SceneManager.SetActiveScene(source);if(!saved)AssetDatabase.DeleteAsset(folder);
            }
            AssetDatabase.Refresh();EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(target,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=target)).ToArray();
            EditorSceneManager.OpenScene(target,OpenSceneMode.Single);Debug.Log("SILENCE Imported Assets "+Version+": открыт "+target+". Исходный мир сохранён. Play и кооператив ещё требуют проверки.");return target;
        }
        public static bool ValidateCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Сначала останови Play.");
            var scene=SceneManager.GetActiveScene();string folder=Path.GetDirectoryName(scene.path)?.Replace('\\','/'),path=folder+"/installation.json";
            if(string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets/_Silence/Generated/ImportedAssets/",StringComparison.Ordinal) || !File.Exists(path))throw new InvalidOperationException("Открой созданный через 28 мир ..._Assets.");
            var record=JsonUtility.FromJson<Installation>(File.ReadAllText(path));var errors=new List<string>();var manager=Manager(scene);
            if(record==null)throw new InvalidOperationException("Не прочитан отчёт установки ассетов.");
            if(record.motions)
            {
                if(!PlayerViewWorkshop.ValidateCurrent())errors.Add("Не пройдена проверка игрока/камеры.");
                if(record.players==null || record.players.Length==0 || !record.players.Contains(AssetDatabase.GetAssetPath(manager.NetworkConfig.PlayerPrefab)))errors.Add("В мире выбран другой player prefab, чем при установке.");
                foreach(var player in record.players ?? Array.Empty<string>())
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(player);var profile=prefab!=null?prefab.GetComponent<PlayerAvatarPresentation>()?.AnimationProfile:null;
                    if(profile==null || profile.controller==null){errors.Add("Нет нового профиля: "+player);continue;}
                    foreach(string name in new[]{"Imported_Idle","Imported_Walk","Imported_Run"})if(!profile.controller.animationClips.Any(c=>c!=null && c.name==name))errors.Add("Не подключён "+name+": "+player);
                }
            }
            if(record.textures && (record.ground==null || record.ground.terrainLayers+record.ground.meshSlots==0))errors.Add("Нет записанных назначений текстур.");
            else if(record.textures)
            {
                var colors=new HashSet<string>(record.ground.colorPaths);string output=record.ground.generatedFolder+"/";
                int references=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>(true)).Where(t=>t.terrainData!=null && AssetDatabase.GetAssetPath(t.terrainData).StartsWith(output,StringComparison.Ordinal))
                    .Sum(t=>t.terrainData.terrainLayers.Count(l=>l!=null && l.diffuseTexture!=null && colors.Contains(AssetDatabase.GetAssetPath(l.diffuseTexture))));
                references+=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r=>r.sharedMaterials)
                    .Count(m=>m!=null && AssetDatabase.GetAssetPath(m).StartsWith(output,StringComparison.Ordinal) && m.HasProperty("_BaseMap") && colors.Contains(AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))));
                if(references==0)errors.Add("В открытом мире отсутствуют ссылки на назначенные текстуры.");
            }
            if(manager.NetworkConfig.ProtocolVersion!=record.protocol)errors.Add("Изменён протокол после установки.");
            string report="SILENCE Imported Assets "+Version+" reference validation — Failures: "+errors.Count+"\nScene: "+scene.path+"\n"+string.Join("\n",errors)+"\nReference checks only; native rendering, animation quality and multiplayer still require Play tests.\n";
            Directory.CreateDirectory("Library/Silence");File.WriteAllText("Library/Silence/ImportedAssetsValidation.txt",report);if(errors.Count==0)Debug.Log(report);else Debug.LogError(report);return errors.Count==0;
        }
    }
}
