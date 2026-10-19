using Bugord.Core;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The arena's scope in the Game scene. Registers everything gameplay needs, so the scene also plays on its own when
    /// opened directly; loaded from Boot it becomes a child of the game scope, whose app states drive the round.
    /// </summary>
    public sealed class GameplayScope : LifetimeScope
    {
        [Tooltip("Every physics and gameplay number of the prototype, in one asset")]
        [SerializeField]
        private GameTuning tuning;

        [Tooltip("The camera over the arena, which steering turns screen points into the arena with")]
        [SerializeField]
        private ArenaCamera arenaCamera;

        [Tooltip("The car every driver gets, the player's and the bots', placed by the round")]
        [SerializeField]
        private Car carPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // Checked here: in the WebGL player a missing reference does not throw where it is used, a null instance's
            // GetType() reads garbage and VContainer fails later with an empty type name.
            Require(tuning, nameof(tuning));
            Require(arenaCamera, nameof(arenaCamera));
            Require(carPrefab, nameof(carPrefab));

            // No parent means no Boot, so no app scope to bring the kit's storage and logging, nor app states to play
            // the round.
            var isStandalone = Parent == null;
            if (isStandalone) {
                builder.AddBugordCore();
            }

            // The non-generic overload on purpose: in the release WebGL player (IL2CPP, faster-smaller code generation)
            // RegisterInstance<GameTuning>(tuning) hands VContainer a broken instance, and the build fails with
            // "VContainerException:  is not assignable from ToyCarArena.Gameplay.GameTuning". Development builds are fine.
            builder.RegisterInstance(tuning, typeof(GameTuning));

            // Nothing is drawn over the game yet; the HUD with the boost button will take its own presses.
            builder.Register<IOnScreenControls, NoOnScreenControls>(Lifetime.Singleton);
            builder.RegisterEntryPoint<InputService>().As<IInputService>();

            builder.RegisterComponent(arenaCamera);

            // The cars go under this scope's object: with no parent given they would land in DontDestroyOnLoad, where
            // the injector builds them, and outlive the scene.
            builder.RegisterEntryPoint<ArenaRound>().AsSelf().WithParameter(carPrefab).WithParameter(transform);
            builder.RegisterEntryPoint<PlayerDriver>();
            if (isStandalone) {
                builder.RegisterEntryPoint<StandaloneRound>();
            }

            builder.RegisterBuildCallback(InjectScene);
        }

        private void Require(Object reference, string field)
        {
            if (reference == null) {
                throw new MissingReferenceException($"{nameof(GameplayScope)} in {gameObject.scene.name} has no {field}.");
            }
        }

        /// <summary>
        /// Calls the Construct methods of everything placed in the scene, so a new component needs no registration.
        /// Runs before any Awake: LifetimeScope executes first. <see cref="SceneInjector"/> says why not VContainer's own
        /// InjectGameObject.
        /// </summary>
        private void InjectScene(IObjectResolver resolver)
        {
            foreach (var root in gameObject.scene.GetRootGameObjects()) {
                if (root != gameObject) {
                    resolver.InjectConstructs(root);
                }
            }
        }
    }
}
