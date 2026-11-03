using Bugord.App.Localization;
using Bugord.App;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
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

        [Tooltip("Arena names and looks; the arenas themselves and their levels are in the tuning")]
        [SerializeField]
        private ArenaCatalog arenaCatalog;

        [Header("Rendering")]
        [Tooltip("On a touch device the 3D scene renders with its shorter side at most this many pixels (render scale " +
                 "is lowered to fit); phones have the weakest GPUs and the most pixels. 0 = no limit")]
        [SerializeField]
        private float touchMaxShortSide = 900f;

        [Tooltip("The same for a desktop: the shorter side of the 3D scene at most this many pixels. 0 = no limit")]
        [SerializeField]
        private float desktopMaxShortSide = 1080f;

        [Header("Localization")]
        [Tooltip("The string table: a key, a translator's note and a column per language")]
        [SerializeField]
        private TextAsset strings;

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

            if (arenaCatalog == null) {
                throw new MissingReferenceException($"{nameof(GameScope)} in {gameObject.scene.name} has no " +
                    $"{nameof(arenaCatalog)}.");
            }

            if (strings == null) {
                throw new MissingReferenceException($"{nameof(GameScope)} in {gameObject.scene.name} has no " +
                    $"{nameof(strings)}.");
            }

            // Loaded now, so the screens built before the boot tasks read English; the saved or platform language is
            // chosen once the platform is up.
            builder.AddLocalization(strings);
            builder.RegisterInstance(tuning, typeof(GameTuning));
            builder.RegisterInstance(carCatalog, typeof(CarCatalog));
            builder.RegisterInstance(arenaCatalog, typeof(ArenaCatalog));

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
            // The arena of the next round is chosen before its scene is built, so it lives here too.
            builder.Register<ArenaRotation>(Lifetime.Singleton).As<IArenaRotation>();
            builder.Register<PersonalBests>(Lifetime.Singleton);
            builder.Register<AdBreaks>(Lifetime.Singleton);
            // The 3D scene's render scale follows the screen; the UI is drawn on top at full resolution.
            builder.RegisterEntryPoint<RenderScaler>().AsSelf()
                .WithParameter("touchMaxShortSide", touchMaxShortSide)
                .WithParameter("desktopMaxShortSide", desktopMaxShortSide);
            builder.Register<PlayerProgress>(Lifetime.Singleton).AsSelf().As<IPlayerCar>();
            // Coins, toppers and missions are saved and outlive the arena scene, as XP does.
            builder.Register<PlayerWallet>(Lifetime.Singleton).AsSelf().As<IPlayerTopper>();
            builder.Register<MissionTracker>(Lifetime.Singleton);
#if BUGORD_SUPERCONSOLE
            if (Debug.isDebugBuild) {
                builder.RegisterEntryPoint<ProgressDevConsole>();
            }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterEntryPoint<PerfDevTools>();
#endif

            builder.RegisterScreen<CurtainViewModel, CurtainView>();
            builder.RegisterScreen<HudViewModel, HudView>();
            builder.RegisterScreen<RoundOverViewModel, RoundOverView>();
            builder.RegisterScreen<GarageViewModel, GarageView>();
            builder.RegisterScreen<SettingsViewModel, SettingsView>();

            builder.RegisterState<RoundState>();
            builder.RegisterState<ResultState>();
            builder.RegisterAppEntryPoint<RoundState>();
        }
    }
}
