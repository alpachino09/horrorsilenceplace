using UnityEngine;

namespace Silence
{
    public static class SceneQuery
    {
        public static T[] All<T>() where T : Object
        {
#if UNITY_6000_6_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
#else
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
        }
    }
}
