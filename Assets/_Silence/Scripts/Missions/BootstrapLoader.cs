using UnityEngine;
using UnityEngine.SceneManagement;

namespace Silence.Missions
{
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField] private string farmScenePath;
        public void Configure(string path) => farmScenePath = path;
        private void Start()
        {
            if (string.IsNullOrEmpty(farmScenePath) || !Application.CanStreamedLevelBeLoaded(farmScenePath))
            {
                Debug.LogError("SILENCE: generate scenes and register them in Build Settings.", this);
                return;
            }
            SceneManager.LoadSceneAsync(farmScenePath, LoadSceneMode.Single);
        }
    }
}
