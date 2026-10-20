using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// Loads the Game scene additively under the game scope and hands out the services of its GameplayScope.
    /// Loading again replaces the loaded scene with a fresh one. While no arena is loaded, a stand-in camera renders the
    /// screen, so the curtain lowered over the reload stays on it.
    /// </summary>
    public sealed class LevelScene
    {
        private const string SceneName = "Game";

        /// <summary>
        /// The curtain's colour (<c>--navy</c> in <c>ui_theme.tss</c>), so the stand-in's screen matches it.
        /// </summary>
        private static readonly Color StandInColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);

        private readonly LifetimeScope parentScope;

        private Scene scene;
        private Camera standInCamera;
        private GameplayScope gameplayScope;

        public LevelScene(LifetimeScope parentScope)
        {
            this.parentScope = parentScope;
        }

        /// <summary>
        /// Whether the Game scene is loaded and its services can be resolved.
        /// </summary>
        public bool IsLoaded => gameplayScope != null;

        public async UniTask Load(CancellationToken token)
        {
            // Without a camera nothing renders, the overlay UI included: the screen would freeze on the old arena's
            // last frame, and the curtain over it would vanish until the new arena's camera starts.
            SetStandInCamera(true);
            await Unload();

            // The scene's GameplayScope picks this up as its parent while the scene awakes.
            using (LifetimeScope.EnqueueParent(parentScope)) {
                await SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive).ToUniTask(cancellationToken: token);
            }

            scene = SceneManager.GetSceneByName(SceneName);
            SceneManager.SetActiveScene(scene);
            gameplayScope = FindGameplayScope(scene);

            // Returns once the scene has run a frame, so everything in it has started.
            await UniTask.NextFrame(PlayerLoopTiming.PostLateUpdate, token);
            SetStandInCamera(false);
        }

        /// <summary>
        /// Resolves a gameplay service from the loaded scene.
        /// </summary>
        public T Resolve<T>()
        {
            if (gameplayScope == null) {
                throw new InvalidOperationException($"The {SceneName} scene is not loaded.");
            }

            return gameplayScope.Container.Resolve<T>();
        }

        private async UniTask Unload()
        {
            gameplayScope = null;

            if (scene.IsValid() && scene.isLoaded) {
                await SceneManager.UnloadSceneAsync(scene).ToUniTask();
            }
        }

        /// <summary>
        /// Turns on a camera that sees nothing and clears the screen to the curtain's colour, behind any other camera.
        /// Created on first use and kept with the app across scene loads; off while an arena is loaded, so it costs
        /// nothing in play.
        /// </summary>
        private void SetStandInCamera(bool isOn)
        {
            if (standInCamera == null) {
                if (!isOn) {
                    return;
                }

                var root = new GameObject("Stand-in Camera");
                UnityEngine.Object.DontDestroyOnLoad(root);
                standInCamera = root.AddComponent<Camera>();
                standInCamera.cullingMask = 0;
                standInCamera.clearFlags = CameraClearFlags.SolidColor;
                standInCamera.backgroundColor = StandInColor;
                standInCamera.depth = -100f;
            }

            standInCamera.enabled = isOn;
        }

        private static GameplayScope FindGameplayScope(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects()) {
                if (root.TryGetComponent<GameplayScope>(out var scope)) {
                    return scope;
                }
            }

            throw new InvalidOperationException($"Scene '{scene.name}' has no {nameof(GameplayScope)} at its root.");
        }
    }
}
