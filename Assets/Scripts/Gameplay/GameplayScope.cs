using Bugord.Core;
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

        protected override void Configure(IContainerBuilder builder)
        {
            // No parent means no Boot, so no app scope to bring the kit's storage and logging.
            if (Parent == null) {
                builder.AddBugordCore();
            }

            // Checked here: in the WebGL player a null instance does not throw, its GetType() reads garbage and VContainer
            // fails later with an empty type name.
            if (tuning == null) {
                throw new MissingReferenceException($"{nameof(GameplayScope)} in {gameObject.scene.name} has no {nameof(GameTuning)}.");
            }

            builder.RegisterInstance(tuning);

            builder.RegisterBuildCallback(InjectScene);
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
