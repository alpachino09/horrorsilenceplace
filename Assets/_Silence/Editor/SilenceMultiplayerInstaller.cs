using System;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Silence.Editor
{
    public static class SilenceMultiplayerInstaller
    {
        private static AddRequest pending;

        [MenuItem("SILENCE/03 Install Multiplayer Package")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || pending != null) return;
            string path = Path.GetFullPath("LocalPackages/com.silence.multiplayer");
            if (!File.Exists(Path.Combine(path, "package.json")))
                throw new InvalidOperationException("Copy LocalPackages beside Assets in your Unity project, then retry.");
            pending = Client.Add("file:" + path.Replace('\\', '/'));
            EditorApplication.update += Poll;
            Debug.Log("SILENCE: installing local multiplayer package and official NGO/Transport dependencies. Wait for Unity compilation.");
        }

        private static void Poll()
        {
            if (pending == null || !pending.IsCompleted) return;
            EditorApplication.update -= Poll;
            if (pending.Status == StatusCode.Success)
                Debug.Log("SILENCE: multiplayer package installed. After compilation use SILENCE / 04 Generate Network Farm.");
            else Debug.LogError("SILENCE: package installation failed: " + pending.Error?.message);
            pending = null;
        }
    }
}
