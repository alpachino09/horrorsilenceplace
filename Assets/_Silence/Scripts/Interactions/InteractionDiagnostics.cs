using System;
using System.Diagnostics;
using System.IO;
using Silence.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Silence.Interactions
{
    // Development diagnostics only; calls and their arguments are omitted in release.
    public static class InteractionDiagnostics
    {
        public const string Revision = "0.2.4";
        public static string ReportPath => Application.isEditor
            ? Path.GetFullPath("Library/Silence/InteractionTrace.txt")
            : Path.Combine(Application.persistentDataPath, "InteractionTrace.txt");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool fileUnavailable;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            fileUnavailable = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, "SILENCE interaction diagnostics " + Revision +
                    " | Unity " + Application.unityVersion + " | UTC " + DateTime.UtcNow.ToString("O") + "\n");
            }
            catch (Exception) { fileUnavailable = true; }
        }
#endif

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(UnityEngine.Object context, string stage, string detail)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string line = "SILENCE INTERACTION " + Revision + " [" + stage + "] " + detail;
            Debug.Log(line, context);
            if (fileUnavailable) return;
            try { File.AppendAllText(ReportPath, DateTime.UtcNow.ToString("O") + " " + line + "\n"); }
            catch (Exception) { fileUnavailable = true; }
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void ProbeKey(PlayerContext player, bool actionPressed)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool physicalE = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (!physicalE && !actionPressed) return;
            Trace(player, "KEY", "physicalE=" + physicalE + " action=" + actionPressed +
                " inputEnabled=" + player.Input.enabled + " localOwner=" + player.IsLocalOwner +
                " gameplay=" + player.Input.GameplayActive + " timeScale=" + Time.timeScale);
#endif
        }
    }
}
