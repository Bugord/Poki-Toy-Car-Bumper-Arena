using UnityEditor;
using UnityEditor.SceneManagement;

namespace ToyCarArena.Editor
{
    internal static class SceneMenu
    {
        // # = shift
        [MenuItem("Scenes/Open Boot #1")]
        private static void OpenBoot() => Open("Assets/Scenes/Boot.unity");

        [MenuItem("Scenes/Open Game #2")]
        private static void OpenGame() => Open("Assets/Scenes/Game.unity");

        private static void Open(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            }
        }
    }
}
