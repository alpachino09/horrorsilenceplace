using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Silence.Editor
{
    public static class SilenceSourceUpdater
    {
        [MenuItem("SILENCE/00 Update Sources From Folder")]
        public static void UpdateSources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before updating source files.");
            string source = EditorUtility.OpenFolderPanel("Select extracted GitHub folder containing Assets and LocalPackages", "", "");
            if (string.IsNullOrEmpty(source)) return;
            string project = Path.GetFullPath(".");
            source = Path.GetFullPath(source);
            if (string.Equals(source.TrimEnd(Path.DirectorySeparatorChar), project.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Select the extracted download, not the Unity project.");
            if (!File.Exists(Path.Combine(source, "LocalPackages/com.silence.multiplayer/package.json")) ||
                !File.Exists(Path.Combine(source, "Assets/_Silence/Scripts/Networking/IWorldAuthority.cs")))
                throw new InvalidOperationException("Wrong folder: choose the extracted repository root with Assets and LocalPackages inside.");

            string[] roots = { "Assets/_Silence/Scripts", "Assets/_Silence/Editor", "Assets/_Silence/Tests",
                "Assets/_Silence/Settings", "LocalPackages/com.silence.multiplayer" };
            var files = roots.SelectMany(root => Directory.GetFiles(Path.Combine(source, root), "*", SearchOption.AllDirectories))
                .Where(path => new[] { ".cs", ".asmdef", ".inputactions", ".json", ".md" }.Contains(Path.GetExtension(path)))
                .Select(path => path.Substring(source.TrimEnd(Path.DirectorySeparatorChar).Length + 1)).ToArray();
            string backup = Path.Combine(project, "Library/Silence/SourceBackups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff"));
            var changed = new List<string>();
            // All old files are backed up before the first mutation. Meta files are never copied/deleted.
            foreach (string relative in files)
            {
                string target = Path.Combine(project, relative);
                if (!File.Exists(target)) continue;
                string saved = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(saved));
                File.Copy(target, saved, false);
            }
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string relative in files)
                {
                    string target = Path.Combine(project, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    changed.Add(relative);
                    File.Copy(Path.Combine(source, relative), target, true);
                }
            }
            catch
            {
                foreach (string relative in changed)
                {
                    string saved = Path.Combine(backup, relative), target = Path.Combine(project, relative);
                    if (File.Exists(saved)) File.Copy(saved, target, true);
                    else if (File.Exists(target)) File.Delete(target);
                }
                throw;
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
            Debug.Log("SILENCE: updated " + files.Length + " source/package files. Scenes, Generated and existing .meta preserved. " +
                "Backup: " + backup + ". Wait for compilation, then SILENCE / 03 Install Multiplayer Package.");
        }
    }
}
