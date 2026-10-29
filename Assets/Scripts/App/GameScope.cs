using Bugord.App;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Integrations.VContainer;
using UniState;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// The app scope in the Boot scene. The kit's <see cref="AppLifetimeScope"/> brings storage, logging, the platform
    /// (Poki with <c>POKI_ENABLED</c>, a local stand-in otherwise), UIKit and the state machine; this adds the game's
    /// screens, states and services. Rounds go round from <see cref="RoundState"/> to <see cref="ResultState"/> and back.
    /// </summary>
    public sealed class GameScope : AppLifetimeScope
    {
        [Tooltip("The game's sounds and music")]
        [SerializeField]
        private AudioLibrary audioLibrary;

        [Header("Progress")]
        [Tooltip("The same tuning asset used by GameplayScope; progress survives the arena scene")]
        [SerializeField]
        private GameTuning tuning;

        [Tooltip("Garage identities and the level at which each car unlocks")]
        [SerializeField]
        private CarCatalog carCatalog;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            // Checked here, as GameplayScope checks its own: in the WebGL player a missing reference fails much later,
            // with an empty type name.
            if (audioLibrary == null) {
                throw new MissingReferenceException($"{nameof(GameScope)} in {gameObject.scene.name} has no " +
                    $"{nameof(audioLibrary)}.");
            }

            if (tuning == null) {
                throw new MissingReferenceException($"{nameof(GameScope)} in {gameObject.scene.name} has no " +
                    $"{nameof(tuning)}.");
            }

            if (carCatalog == null) {
                throw new MissingReferenceException($"{nameof(GameScope)} in {gameObject.scene.name} has no " +
                    $"{nameof(carCatalog)}.");
            }

            builder.RegisterInstance(tuning, typeof(GameTuning));
            builder.RegisterInstance(carCatalog, typeof(CarCatalog));

            builder.Register<LevelScene>(Lifetime.Singleton);
            // Here, so the music goes on across the Game scene reloaded every round and the screens can click. The
            // non-generic overload for a ScriptableObject, as GameplayScope explains.
            builder.RegisterInstance(audioLibrary, typeof(AudioLibrary));
            builder.RegisterEntryPoint<AudioService>().As<IAudioService>();
            builder.Register<ScreenCurtain>(Lifetime.Singleton);
            // Here and not in the arena's scope: the HUD is an app screen; the arena's input finds it in this parent.
            builder.Register<HudControls>(Lifetime.Singleton).AsSelf().As<IOnScreenControls>();
            // Here, so they outlive the Game scene reloaded every round: the bots read the progress as the scene is
            // built.
            builder.Register<SessionProgress>(Lifetime.Singleton).AsSelf().As<ISessionProgress>();
            builder.Register<SessionRoster>(Lifetime.Singleton).As<ISessionRoster>();
            builder.Register<PersonalBests>(Lifetime.Singleton);
            builder.Register<PlayerProgress>(Lifetime.Singleton).AsSelf().As<IPlayerCar>();
#if BUGORD_SUPERCONSOLE
            if (Debug.isDebugBuild) {
                builder.RegisterEntryPoint<ProgressDevConsole>();
            }
#endif

            builder.RegisterScreen<CurtainViewModel, CurtainView>();
            builder.RegisterScreen<HudViewModel, HudView>();
            builder.RegisterScreen<RoundOverViewModel, RoundOverView>();

            builder.RegisterState<RoundState>();
            builder.RegisterState<ResultState>();
            builder.RegisterAppEntryPoint<RoundState>();
        }
    }
}
