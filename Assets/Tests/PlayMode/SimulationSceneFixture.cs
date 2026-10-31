using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bugord.Core;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>Restores editor state around simulations of the real Game scene.</summary>
    public abstract class SimulationSceneFixture
    {
        protected LifetimeScope parentScope;
        private Scene originalScene;
        private float originalCaptureDeltaTime;
        private float originalTimeScale;
        private int originalTargetFrameRate;
        private int originalVSyncCount;
        private bool originalRunInBackground;
        private UnityEngine.Random.State originalRandomState;

        [SetUp]
        public void SetUp()
        {
            this.originalScene = SceneManager.GetActiveScene();
            this.originalCaptureDeltaTime = Time.captureDeltaTime;
            this.originalTimeScale = Time.timeScale;
            this.originalTargetFrameRate = Application.targetFrameRate;
            this.originalVSyncCount = QualitySettings.vSyncCount;
            this.originalRunInBackground = Application.runInBackground;
            this.originalRandomState = UnityEngine.Random.state;

            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try {
                yield return UnloadGame();
            }
            finally {
                if (parentScope != null) {
                    UnityEngine.Object.DestroyImmediate(parentScope.gameObject);
                    parentScope = null;
                }

                Time.captureDeltaTime = originalCaptureDeltaTime;
                Time.timeScale = originalTimeScale;
                Application.targetFrameRate = originalTargetFrameRate;
                QualitySettings.vSyncCount = originalVSyncCount;
                Application.runInBackground = originalRunInBackground;
                UnityEngine.Random.state = originalRandomState;
            }
        }

        protected void CreateParent(SimulationInput input, SimulationProgress progress, string map = null, string variant = null)
        {
            var catalog = LoadCarCatalog();
            var arenas = LoadArenaCatalog();
            var audio = new SilentAudioService(LoadAudioLibrary());
            parentScope = LifetimeScope.Create(builder => {
                builder.AddBugordCore("simulation");
                builder.Register<IOnScreenControls, NoOnScreenControls>(Lifetime.Singleton);
                builder.RegisterInstance<ISessionProgress>(progress);
                builder.RegisterInstance(catalog, typeof(CarCatalog));
                builder.RegisterInstance(arenas, typeof(ArenaCatalog));
                // A simulation measures one arena and variant, the first of each unless SIM_MAP and SIM_VARIANT name
                // others, and never rotates. A test that names its map also names its variant or gets the first.
                builder.RegisterInstance(new FixedArenaRotation(map ?? SimMap, map == null ? SimVariant : variant),
                    typeof(FixedArenaRotation))
                    .As(typeof(IArenaRotation));
                builder.Register<IPlayerCar, FirstPlayerCar>(Lifetime.Singleton);
                builder.Register<SessionRoster>(Lifetime.Singleton).As<ISessionRoster>();
                builder.RegisterInstance<IAudioService>(audio);
                builder.RegisterInstance<IInputService>(input);
            }, "Simulation Scope");
        }

        protected IEnumerator LoadGame()
        {
            using (LifetimeScope.EnqueueParent(parentScope)) {
                var loading = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
                Assert.That(loading, Is.Not.Null, "Game scene did not start loading.");
                var started = Time.realtimeSinceStartupAsDouble;
                while (!loading.isDone) {
                    Assert.That(Time.realtimeSinceStartupAsDouble - started, Is.LessThan(60d),
                        "Game scene did not load within 60 real seconds.");
                    yield return null;
                }
            }

            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Game"));
            yield return null;
            // Simulations pin difficulty to the measured round. Only the cosmetic roster advances on completion;
            // input, BotDriver.Seed and the measured progress stay as configured by the scenario.
            var container = GameScope().Container;
            var tuning = container.Resolve<GameTuning>();
            var progress = container.Resolve<ISessionProgress>();
            var roster = container.Resolve<ISessionRoster>();
            container.Resolve<ArenaRound>().Finished += result => roster.NextRound(result.CarCount - 1,
                RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis, tuning.Players);
        }

        protected GameplayScope GameScope()
        {
            var scene = SceneManager.GetSceneByName("Game");
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "Game scene was not loaded.");
            var scope = scene.GetRootGameObjects().Select(root => root.GetComponent<GameplayScope>())
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(scope, Is.Not.Null, "Game scene has no GameplayScope.");
            return scope;
        }

        protected IEnumerator UnloadGame()
        {
            if (originalScene.IsValid() && originalScene.isLoaded) {
                SceneManager.SetActiveScene(originalScene);
            }

            var scene = SceneManager.GetSceneByName("Game");
            if (scene.IsValid() && scene.isLoaded) {
                yield return SceneManager.UnloadSceneAsync(scene);
            }

            Time.timeScale = 1f;
        }

        /// <summary>The arena a simulation plays on: the SIM_MAP environment variable, else the first (sandbox).</summary>
        protected static string SimMap
        {
            get {
                var id = Environment.GetEnvironmentVariable("SIM_MAP");
                return string.IsNullOrWhiteSpace(id) ? "sandbox" : id.Trim();
            }
        }

        /// <summary>The variant a simulation plays: the SIM_VARIANT environment variable, else null for the arena's first.</summary>
        protected static string SimVariant
        {
            get {
                var id = Environment.GetEnvironmentVariable("SIM_VARIANT");
                return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            }
        }

        protected static ArenaCatalog LoadArenaCatalog()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            if (catalog == null) {
                throw new MissingReferenceException("Simulation needs the game's ArenaCatalog asset.");
            }

            return catalog;
#else
            throw new NotSupportedException("Run the simulation as editor PlayMode tests via Tools/simulate.sh.");
#endif
        }

        protected static CarCatalog LoadCarCatalog()
        {
#if UNITY_EDITOR
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            if (catalog == null) {
                throw new MissingReferenceException("Simulation needs the game's CarCatalog asset.");
            }

            return catalog;
#else
            throw new NotSupportedException("Run the simulation as editor PlayMode tests via Tools/simulate.sh.");
#endif
        }

        protected static AudioLibrary LoadAudioLibrary()
        {
#if UNITY_EDITOR
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>("Assets/Data/Audio/data_audio_library.asset");
            if (library == null) {
                throw new MissingReferenceException("Simulation needs the game's AudioLibrary asset.");
            }

            return library;
#else
            throw new NotSupportedException("Run the simulation as editor PlayMode tests via Tools/simulate.sh.");
#endif
        }

    }
}
