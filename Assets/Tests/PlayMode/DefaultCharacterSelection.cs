using ArknightsFrontline.Arena;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArknightsFrontline.Tests.PlayMode
{
    internal static class DefaultCharacterSelection
    {
        internal static void LoadScene(string name, LoadSceneMode mode = LoadSceneMode.Single)
        {
            SceneManager.sceneLoaded += Select;
            SceneManager.LoadScene(name, mode);
        }
        internal static AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode = LoadSceneMode.Single)
        {
            SceneManager.sceneLoaded += Select;
            return SceneManager.LoadSceneAsync(name, mode);
        }
        internal static void Select(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "PrototypeArena") return;
            SceneManager.sceneLoaded -= Select;
            var selection = Object.FindFirstObjectByType<PlayerCharacterSelection>();
            if (selection) selection.ConfirmSelection(false);
        }
    }
}
