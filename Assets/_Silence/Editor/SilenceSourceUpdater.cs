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
                "Assets/_Silence/Settings", "Assets/_Silence/Graphics", "Assets/_Silence/Art/VisualRebuild", "Assets/Creatures/SilencePredator", "Assets/Characters/SilencePlayer", "Assets/Characters/HoodedGirl", "Assets/Props/TacticalFlashlight", "LocalPackages/com.silence.multiplayer" };
            var files = roots.Where(root => Directory.Exists(Path.Combine(source, root))).SelectMany(root => Directory.GetFiles(Path.Combine(source, root), "*", SearchOption.AllDirectories))
                .Where(path => new[] { ".cs", ".asmdef", ".inputactions", ".json", ".md", ".fbx", ".png", ".shader", ".hlsl", ".bytes" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
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
            bool vesper = File.Exists(Path.Combine(source, "Assets/Creatures/SilencePredator/Documentation/game-animation.json"));
            bool player = File.Exists(Path.Combine(source, "Assets/_Silence/Graphics/Runtime/Players/PlayerAvatarDefinition.cs"));
            bool hoodedGirl = File.Exists(Path.Combine(source, "Assets/Characters/HoodedGirl/Models/HoodedGirl_LOD0.fbx"));
            bool humanMotion = File.Exists(Path.Combine(source, "LocalPackages/com.silence.multiplayer/Editor/Visual/PlayerAnimationWorkshop.cs"));
            bool playerView = File.Exists(Path.Combine(source, "LocalPackages/com.silence.multiplayer/Editor/Visual/PlayerViewWorkshop.cs"));
            Debug.Log("SILENCE: updated " + files.Length + " source/art/package files. Scenes, Generated and existing .meta preserved. " +
                "Backup: " + backup + (playerView ? ". Wait for compilation. For the existing player world: SILENCE / 26 Player Camera and Body, then create a COPY of the current world. V switches first/third person. Your environment is preserved; do not regenerate Campaign. For a first character installation use 21, then 25 for Basic Motions. Rebuild BOTH Host and Client after creating the new player scene." :
                humanMotion ? ". Wait for compilation, open your latest GAME scene with the girl (not preview 23), then SILENCE / 25 Human Animation Workshop. Import the original Basic Motions unitypackage and install into a COPY of the open world. The separate review lab is optional; button Return opens the game world again. Rebuild BOTH Host and Client." :
                hoodedGirl ? ". Wait for compilation, then SILENCE / 22 Prepare Hooded Girl Assets. Menu 23 opens an isolated preview. To install, open your latest improved GAME scene and use menu 21; that tool copies the open scene. Tactical flashlight workshop: menu 24, requires the actual flashlight model and calibrated parts." :
                player ? ". Player avatar tools: SILENCE / 21 Player Avatar Workshop. Installation requires a finished art model and controller. Keep your latest improved scene open; the tool copies that scene." :
                vesper ? ". Wait for compilation, open the latest game scene with your improved environment, then run SILENCE / 18 Install Vesper in Game Scene. The installer copies the OPEN scene; Campaign may be the old prototype." : ". Wait for compilation, then SILENCE / 03 Install Multiplayer Package."));
        }
    }
}
