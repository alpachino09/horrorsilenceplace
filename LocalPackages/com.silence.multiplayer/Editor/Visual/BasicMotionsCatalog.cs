using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace Silence.Multiplayer.Editor.Visual
{
    public static class BasicMotionsCatalog
    {
        public const string Root = "Assets/PolyOne/Basic Motions/Animation/";
        public const string PackageSha256 = "20b52be48a73cd5a288d6c405b17d27e0a0541fdfa777a1fa36db7d3c51e5b7d";
        public static readonly string[] Names = { "Angry", "Climbing Down Wall", "Dying", "Happy", "Idle", "Jumping Down", "Jumping Up", "Pointing Forward",
            "Run", "Run Backward", "Run Fast", "Run Left", "Run Right", "Run Turn 180", "Run Up Stairs", "Sad", "Tripping", "Turn Left", "Turn Right", "Walk", "Walk Sad", "Walking Turn 180", "Yelling" };
        public static Dictionary<string, AnimationClip> Load()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (string name in Names)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + name + ".anim");
                if (clip == null) throw new InvalidOperationException("Сначала импортируй присланный Basic Motions пакет. Не найден: " + Root + name + ".anim");
                if (clip.legacy || !clip.isHumanMotion || clip.length <= .05f || AnimationUtility.GetCurveBindings(clip).Length < 20)
                    throw new InvalidOperationException("Не подходит Humanoid-клип: " + name);
                result.Add(name, clip);
            }
            return result;
        }
        public static void ImportSuppliedPackage()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Сначала останови Play.");
            string path = EditorUtility.OpenFilePanel("Выбери animation_pack_basic_motions.unitypackage", "", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != PackageSha256)
                    throw new InvalidOperationException("Это другой пакет. Нужен исходный animation_pack_basic_motions.unitypackage, присланный в чат (23 клипа). Ничего не импортировано.");
            // This exact archive was inspected: no scripts; the vendor's demo is never opened.
            // It is the original user-provided licensed archive, not a redistributed copy.
            if (Names.All(name => File.Exists(Root + name + ".anim")))
            { Load(); Debug.Log("SILENCE: все 23 Humanoid-клипа уже импортированы. Можно выполнять шаг 2."); return; }
            if (Directory.Exists("Assets/PolyOne"))
            { ImportPackage(path, true); Debug.Log("SILENCE: найдены существующие ресурсы PolyOne. В стандартном окне импорта выбери недостающие файлы; затем проверь все 23 клипа."); return; }
            ImportPackage(path, false);
        }
        private static void ImportPackage(string path, bool interactive)
        {
#if UNITY_6000_6_OR_NEWER
            UnityEditor.AssetPackage.Package.Import(path, interactive);
#else
            AssetDatabase.ImportPackage(path, interactive);
#endif
        }
        public static AnimationClip[] FallbackClips(Animator source)
        {
            var current = source.runtimeAnimatorController != null ? source.runtimeAnimatorController.animationClips : Array.Empty<AnimationClip>();
            string avatarPath = AssetDatabase.GetAssetPath(source.avatar);
            var imported = string.IsNullOrEmpty(avatarPath) ? Array.Empty<AnimationClip>() : AssetDatabase.LoadAllAssetsAtPath(avatarPath).OfType<AnimationClip>().ToArray();
            return imported.Concat(current).Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal)).Distinct().ToArray();
        }
    }
}
