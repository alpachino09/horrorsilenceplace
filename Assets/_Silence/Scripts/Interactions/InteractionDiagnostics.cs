using System;
using System.Diagnostics;
using System.IO;
using Silence.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace Silence.Interactions
{
    // Diagnostic instrumentation only; calls and their arguments are omitted in uninstrumented players.
    public static class InteractionDiagnostics
    {
        public const string Revision = "0.3.0";
        public static string ReportPath => Application.isEditor
            ? Path.GetFullPath("Library/Silence/InteractionTrace.txt")
            : Path.Combine(Application.persistentDataPath, "InteractionTrace.txt");
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
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

        [Conditional("UNITY_EDITOR"), Conditional("UNITY_INCLUDE_INSTRUMENTATION")]
        public static void Trace(UnityEngine.Object context, string stage, string detail)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            string line = "SILENCE INTERACTION " + Revision + " [" + stage + "] " + detail;
            Debug.Log(line, context);
            if (fileUnavailable) return;
            try { File.AppendAllText(ReportPath, DateTime.UtcNow.ToString("O") + " " + line + "\n"); }
            catch (Exception) { fileUnavailable = true; }
#endif
        }

        [Conditional("UNITY_EDITOR"), Conditional("UNITY_INCLUDE_INSTRUMENTATION")]
        public static void ProbeKey(PlayerContext player, bool actionPressed)
        {
#if UNITY_EDITOR || UNITY_INCLUDE_INSTRUMENTATION
            bool physicalE = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            if (!physicalE && !actionPressed) return;
            Trace(player, "KEY", "physicalE=" + physicalE + " action=" + actionPressed +
                " inputEnabled=" + player.Input.enabled + " localOwner=" + player.IsLocalOwner +
                " gameplay=" + player.Input.GameplayActive + " timeScale=" + Time.timeScale);
#endif
        }
    }
}
