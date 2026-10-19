using Bugord.Core;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Input;
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

        protected override void Configure(IContainerBuilder builder)
        {
            // Checked here: in the WebGL player a missing reference does not throw where it is used, a null instance's
            // GetType() reads garbage and VContainer fails later with an empty type name.
            Require(tuning, nameof(tuning));
            Require(arenaCamera, nameof(arenaCamera));

            // No parent means no Boot, so no app scope to bring the kit's storage and logging.
            if (Parent == null) {
                builder.AddBugordCore();
            }

            builder.RegisterInstance(tuning);

            // Nothing is drawn over the game yet; the HUD with the boost button will take its own presses.
            builder.Register<IOnScreenControls, NoOnScreenControls>(Lifetime.Singleton);
            builder.RegisterEntryPoint<InputService>().As<IInputService>();

            builder.RegisterComponent(arenaCamera);

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
