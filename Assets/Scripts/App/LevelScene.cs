using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// Loads the Game scene additively under the game scope and hands out the services of its GameplayScope.
    /// Loading again replaces the loaded scene with a fresh one.
    /// </summary>
    public sealed class LevelScene
    {
        private const string SceneName = "Game";

        private readonly LifetimeScope parentScope;

        private Scene scene;
        private GameplayScope gameplayScope;

        public LevelScene(LifetimeScope parentScope)
        {
            this.parentScope = parentScope;
        }

        public async UniTask Load(CancellationToken token)
        {
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
