#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ToyCarArena.App
{
    /// <summary>
    /// The web template's own loading screen (WebGLTemplates/poki-template/loading). Poki's loader goes away as soon as
    /// the engine has loaded, but Boot, the Game scene and the SDK take seconds more, so the template keeps its screen up
    /// until the game says the first round is on screen.
    /// </summary>
    public static class LoadingScreen
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void JS_LoadingScreen_Hide();
#endif

        /// <summary>Knocks the car off the loading bar and fades the screen out; does nothing the second time.</summary>
        public static void Hide()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            JS_LoadingScreen_Hide();
#endif
        }
    }
}
