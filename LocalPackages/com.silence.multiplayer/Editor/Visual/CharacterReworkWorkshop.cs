using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Silence.Player;
using Silence.Graphics.Players;
using Silence.Core;

namespace Silence.Multiplayer.Editor.Visual
{
    public sealed class CharacterReworkWorkshop : EditorWindow
    {
        private string message;
        [MenuItem("SILENCE/27 Character Rework and Audit")]
        public static void Open()=>GetWindow<CharacterReworkWorkshop>("SILENCE Character");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Сначала открой свой нынешний игровой мир с героиней. Установка создаст отдельную копию: непрозрачные материалы кожи/одежды, общая защита рук, проверка skin weights и bounds по настоящим клипам. Модель и мир не пересоздаются. Это техническая переработка; визуальная приёмка выполняется в Play.",MessageType.Info);
            if(GUILayout.Button("Создать копию мира: исправить и проверить персонажей"))
                try{string path=PlayerViewWorkshop.Install(true);if(path!=null)message="Открыта копия "+path+". Play → Создать сессию → Начать раунд. V — камера. Проверь руки и материалы.";}catch(Exception e){message=e.Message;Debug.LogException(e);}
            if(GUILayout.Button("Проверить ссылки камеры и персонажа"))
                try{message=ValidateCurrent()?"Ссылки и материалы проверены. Остаётся визуальная проверка в Play.":"Смотри Console.";}catch(Exception e){message=e.Message;Debug.LogException(e);}
            if(GUILayout.Button("Собрать текущий мир: Host и Client"))
                try{if(ValidateCurrent())NetworkFarmGenerator.BuildScene(SceneManager.GetActiveScene().path);}catch(Exception e){message=e.Message;Debug.LogException(e);}
            if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
        }
        public static bool ValidateCurrent()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Сначала останови Play.");
            var managers=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NetworkManager>(true)).ToArray();
            if(managers.Length!=1){Debug.Log("SILENCE: сначала открой сохранённый игровой мир с одним NetworkManager и создай его копию через 27.");return false;}
            var manager=managers[0];var playerPrefab=manager.NetworkConfig.PlayerPrefab;
            string mainAudit=playerPrefab!=null?Path.GetDirectoryName(AssetDatabase.GetAssetPath(playerPrefab))+"/character-audit.json":null;
            if(manager.NetworkConfig.ProtocolVersion<9 || mainAudit==null || !File.Exists(mainAudit))
            {Debug.Log("SILENCE: открыт исходный мир. Копия из 27 ещё не установлена; её камеру/материалы пока проверять рано. Сначала успешно заверши «Создать копию мира». Версию протокола вручную менять не нужно.");return false;}
            if(!PlayerViewWorkshop.ValidateCurrent())return false;
            var errors=new List<string>();
            if(manager.NetworkConfig.ProtocolVersion<9)errors.Add("Нужна копия мира из меню 27, protocol >= 9.");
            var players=manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Where(l=>l!=null).SelectMany(l=>l.PrefabList).Concat(manager.NetworkConfig.Prefabs.Prefabs)
                .Where(e=>e!=null).Select(e=>e.Prefab).Append(manager.NetworkConfig.PlayerPrefab).Where(p=>p!=null && p.GetComponent<PlayerContext>()!=null).Distinct();
            foreach(var player in players)
            {
                string reportPath=Path.GetDirectoryName(AssetDatabase.GetAssetPath(player))+"/character-audit.json";
                if(!File.Exists(reportPath) || !JsonUtility.FromJson<CharacterReworkPreparation.Report>(File.ReadAllText(reportPath)).nativePoseSampled)errors.Add("Нет измерения поз для "+player.name);
                foreach(var renderer in player.GetComponentsInChildren<Renderer>(true))
                {
                    if(renderer is SkinnedMeshRenderer skin)
                    {
                        if(skin.sharedMesh==null || skin.sharedMesh.vertexCount==0)errors.Add("Нет skin-геометрии: "+renderer.name);
                        var bounds=skin.localBounds;var center=bounds.center;var extents=bounds.extents;
                        if(!SkinBoundsModel.IsUsable(new RigVector(center.x,center.y,center.z),new RigVector(extents.x,extents.y,extents.z)))
                            errors.Add("Некорректные bounds: "+renderer.name+", center="+center+", extents="+extents);
                    }
                    foreach(var material in renderer.sharedMaterials)
                        if(CharacterSurfaceFixer.IsSolid(renderer,material) &&
                            (!material.HasProperty("_Surface") || material.GetFloat("_Surface")!=0 || material.GetFloat("_ZWrite")!=1 || material.renderQueue!=2000 || material.GetColor("_BaseColor").a!=1))
                            errors.Add("Непрозрачность не настроена: "+renderer.name+" / "+material.name);
                }
            }
            Directory.CreateDirectory("Library/Silence");string report="SILENCE Character Rework reference validation — Failures: "+errors.Count+"\n"+string.Join("\n",errors)+"\nReferences/settings only. Native Play, contacts, visual quality and multiplayer remain to be reviewed.\n";
            File.WriteAllText("Library/Silence/CharacterReworkValidation.txt",report);if(errors.Count==0)Debug.Log(report);else Debug.LogError(report);return errors.Count==0;
        }
    }
}
