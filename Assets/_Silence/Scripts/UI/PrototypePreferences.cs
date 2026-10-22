using UnityEngine;

namespace Silence.UI
{
    public static class PrototypePreferences
    {
        public static bool English
        {
            get => PlayerPrefs.GetInt("Silence.Prototype.English", 0) == 1;
            set => PlayerPrefs.SetInt("Silence.Prototype.English", value ? 1 : 0);
        }
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat("Silence.Prototype.MouseSensitivity", 0.1f), 0.02f, 0.4f);
            set => PlayerPrefs.SetFloat("Silence.Prototype.MouseSensitivity", Mathf.Clamp(value, 0.02f, 0.4f));
        }
    }
}
