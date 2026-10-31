using Bugord.Core;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
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

        [Tooltip("The arena's floor, whose edge crumbles away ring by ring over a round")]
        [SerializeField]
        private ArenaFloor arenaFloor;

        [Tooltip("The car every driver gets, the player's and the bots', placed by the round")]
        [SerializeField]
        private Car carPrefab;

        [Tooltip("Car catalog for Game opened without Boot; with a parent the parent's catalog is used")]
        [SerializeField]
        private CarCatalog carCatalog;

        [Tooltip("Arena catalog for Game opened without Boot; with a parent the parent's catalog is used")]
        [SerializeField]
        private ArenaCatalog arenaCatalog;

        [Tooltip("Materials of the Candy arena's caramel and icing patches; unused on the other arenas")]
        [SerializeField]
        private CandyDecor candyDecor;

        [Tooltip("Materials of the Carousel arena's turntable; unused on the other arenas")]
        [SerializeField]
        private CarouselDecor carouselDecor;

        [Tooltip("Sounds and music for the scene played on its own; from Boot the app's audio plays")]
        [SerializeField]
        private AudioLibrary audioLibrary;

        protected override void Configure(IContainerBuilder builder)
        {
            // Checked here: in the WebGL player a missing reference does not throw where it is used, a null instance's
            // GetType() reads garbage and VContainer fails later with an empty type name.
            Require(tuning, nameof(tuning));
            Require(arenaCamera, nameof(arenaCamera));
            Require(arenaFloor, nameof(arenaFloor));
            Require(carPrefab, nameof(carPrefab));
            Require(candyDecor, nameof(candyDecor));
            if (!candyDecor.IsComplete) {
                throw new MissingReferenceException($"{nameof(CandyDecor)} in {gameObject.scene.name} lacks a material.");
            }
            Require(carouselDecor, nameof(carouselDecor));
            if (!carouselDecor.IsComplete) {
                throw new MissingReferenceException($"{nameof(CarouselDecor)} in {gameObject.scene.name} lacks a material.");
            }
            Require(audioLibrary, nameof(audioLibrary));

            // No parent means no Boot, so no app scope to bring the kit's storage and logging, nor app states to play
            // the round.
            var isStandalone = Parent == null;
            if (isStandalone) {
                builder.AddBugordCore();
                Require(carCatalog, nameof(carCatalog));
                builder.RegisterInstance(carCatalog, typeof(CarCatalog));
                builder.Register<IPlayerCar, FirstPlayerCar>(Lifetime.Singleton);
                // On its own the scene plays the first arena round after round: there is no level to open the others.
                Require(arenaCatalog, nameof(arenaCatalog));
                builder.RegisterInstance(arenaCatalog, typeof(ArenaCatalog));
                builder.RegisterInstance(new FixedArenaRotation(tuning.Maps[0].Id), typeof(FixedArenaRotation))
                    .As(typeof(IArenaRotation));
            }

            // The non-generic overload on purpose: in the release WebGL player (IL2CPP, faster-smaller code generation)
            // RegisterInstance<GameTuning>(tuning) hands VContainer a broken instance, and the build fails with
            // "VContainerException:  is not assignable from ToyCarArena.Gameplay.GameTuning". Development builds are fine.
            builder.RegisterInstance(tuning, typeof(GameTuning));

            // From Boot the HUD's buttons come from the app scope (the HUD is an app screen); on its own the scene has no
            // HUD, so nothing is drawn over the game.
            if (isStandalone) {
                builder.Register<IOnScreenControls, NoOnScreenControls>(Lifetime.Singleton);
            }

            // From Boot the audio is the app's, so the music goes on across the rounds' reloads; on its own the scene
            // brings its own, which starts over with each round.
            if (isStandalone) {
                builder.RegisterInstance(audioLibrary, typeof(AudioLibrary));
                builder.RegisterEntryPoint<AudioService>().As<IAudioService>();
            }

            // A simulation brings its own input from the parent, so it still drives through PlayerDriver.
            if (!builder.Exists(typeof(IInputService), includeInterfaceTypes: true, findParentScopes: true)) {
                builder.RegisterEntryPoint<InputService>().As<IInputService>();
            }

            // The arena of this scene, from the rotation of the app scope (or the fixed one of a scene on its own, or of a
            // simulation): chosen once, as the scene is built. Each reader takes what it needs from it.
            builder.Register<ArenaMap>(Lifetime.Singleton);
            builder.Register(resolver => resolver.Resolve<ArenaMap>().Settings, Lifetime.Singleton);

            builder.RegisterComponent(arenaCamera);
            // The floor is also the arena's edge: everything that asks how far the edge is, or which way, asks the interface.
            builder.RegisterComponent(arenaFloor).AsSelf().As<IArenaEdge>();
            builder.RegisterComponent(candyDecor);
            builder.RegisterComponent(carouselDecor);

            // What the floor does to a car: the Candy arena's patches, neutral on the others. Cars ask for it as they are
            // built, so it is registered on every arena.
            builder.RegisterEntryPoint<CandyPatches>().AsSelf().As<IArenaSurface>();

            // The part of the floor that moves and carries the cars: the Carousel arena's turntable, still on the others.
            builder.RegisterEntryPoint<CarouselDisc>().AsSelf().As<IArenaPlatform>();

            // Read from the session's progress once, as the scene is built: the round's cars, the bots and the player's
            // driver share it, and the session moves on before the next scene is.
            builder.Register<RoundDifficulty>(Lifetime.Singleton);

            // The cars go under this scope's object: with no parent given they would land in DontDestroyOnLoad, where
            // the injector builds them, and outlive the scene.
            builder.RegisterEntryPoint<ArenaRound>().AsSelf().WithParameter(carPrefab).WithParameter(transform);
            builder.RegisterEntryPoint<PlayerDriver>().AsSelf();
            builder.Register<BallLane>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BotDriver>();
            builder.RegisterEntryPoint<ArenaCrumble>().AsSelf();
            builder.RegisterEntryPoint<ArenaDecor>().WithParameter(transform);
            if (isStandalone) {
                builder.RegisterEntryPoint<StandaloneRound>();
                // From Boot the session's progress lives in the app scope; on its own the scene reloads itself every
                // round, so StandaloneRound keeps it across the reloads.
                builder.RegisterInstance(StandaloneRound.Session, typeof(SessionProgress))
                    .As(typeof(ISessionProgress));
                builder.RegisterInstance(StandaloneRound.Roster(carCatalog), typeof(ISessionRoster));
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
