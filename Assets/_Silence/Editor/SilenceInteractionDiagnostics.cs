using System.IO;
using UnityEditor;
using UnityEngine;
using Silence.Interactions;

namespace Silence.Editor
{
    public static class SilenceInteractionDiagnostics
    {
        [MenuItem("SILENCE/08 Copy Interaction Diagnostics")]
        public static void Copy()
        {
            string path = InteractionDiagnostics.ReportPath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("SILENCE: interaction report is missing. Start Play, create a session, aim at a door and press E once.");
                return;
            }
            GUIUtility.systemCopyBuffer = File.ReadAllText(path);
            Debug.Log("SILENCE: interaction report copied. Paste it into the chat. File: " + path);
        }
    }
}
