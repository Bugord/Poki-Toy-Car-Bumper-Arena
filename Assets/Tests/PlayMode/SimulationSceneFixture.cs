using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bugord.Core;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
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

        /// <summary>The topper the player's car wears in the scene loaded next; none unless a test sets it.</summary>
        protected string PlayerTopper { get; set; } = "";
        private Scene originalScene;
        private float originalCaptureDeltaTime;
        private float originalTimeScale;
        private int originalTargetFrameRate;
        private int originalVSyncCount;
        private bool originalRunInBackground;
        private UnityEngine.Random.State originalRandomState;
        private GameTuning savedTuning;
        private GameTuning.GameMode savedMode;
        private float savedDuration;

        private static readonly FieldInfo ModeField =
            typeof(GameTuning).GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MatchField =
            typeof(GameTuning).GetField("match", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo DurationField =
            typeof(GameTuning.MatchSettings).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic);

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

            // A screen an earlier test left shown would keep the arena undrawn.
            ShownScreens.Clear();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 1f / 60f;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;
            Application.runInBackground = true;

            // The asset ships the long match, but the suites were written for the last car standing: play that unless a
            // test asks for the match with UseMode.
            UseMode(GameTuning.GameMode.LastStanding);
            // The suites measure plain rounds whatever round of the visit they play; a test asks for one with UseSpecial.
            UseSpecial(SpecialRound.None);
        }

        /// <summary>
        /// Plays <paramref name="kind"/> in the Game scenes loaded after this call, whatever round of the visit they are:
        /// in memory only, the tear-down puts the schedule back.
        /// </summary>
        protected void UseSpecial(SpecialRound kind) => LoadTuning().SpecialRounds.Force(kind);

        /// <summary>The mode the tuning asset ships, whatever the test plays.</summary>
        protected GameTuning.GameMode ShippedMode => savedMode;

        /// <summary>The seconds a match of the tuning asset lasts, whatever the test plays.</summary>
        protected float ShippedMatchSeconds => savedDuration;

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
                RestoreMode();
                RestoreBoost();
                LoadTuning().SpecialRounds.Force(null);
            }
        }

        /// <summary>
        /// Lets cars boost, or not, in the Game scenes loaded after this call, whatever the build ships
        /// (<see cref="GameTuning.BoostSettings.IsEnabled"/>): in memory only, put back by the tear-down, as
        /// <see cref="UseMode"/>.
        /// </summary>
        protected void UseBoost(bool isEnabled)
        {
            var boost = LoadTuning().Boost;
            if (savedBoost == null) {
                savedBoost = boost;
                savedBoostEnabled = boost.IsEnabled;
            }

            BoostEnabledField.SetValue(boost, isEnabled);
        }

        private void RestoreBoost()
        {
            if (savedBoost == null) {
                return;
            }

            BoostEnabledField.SetValue(savedBoost, savedBoostEnabled);
            savedBoost = null;
        }

        private static readonly System.Reflection.FieldInfo BoostEnabledField =
            typeof(GameTuning.BoostSettings).GetField("enabled",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        private GameTuning.BoostSettings savedBoost;
        private bool savedBoostEnabled;

        /// <summary>
        /// Plays the Game scenes loaded after this call in <paramref name="mode"/> (and, when given, matches of
        /// <paramref name="matchSeconds"/>): it changes the shared tuning asset in memory only, never saved, and the
        /// tear-down puts it back, so a failed test cannot leave the next one, or the editor, in the other mode.
        /// </summary>
        protected GameTuning UseMode(GameTuning.GameMode mode, float? matchSeconds = null)
        {
            var tuning = LoadTuning();
            var match = MatchField.GetValue(tuning);
            if (savedTuning == null) {
                this.savedTuning = tuning;
                this.savedMode = (GameTuning.GameMode)ModeField.GetValue(tuning);
                this.savedDuration = (float)DurationField.GetValue(match);
            }

            ModeField.SetValue(tuning, mode);
            if (matchSeconds.HasValue) {
                DurationField.SetValue(match, matchSeconds.Value);
            }

            return tuning;
        }

        private void RestoreMode()
        {
            if (savedTuning == null) {
                return;
            }

            ModeField.SetValue(savedTuning, savedMode);
            DurationField.SetValue(MatchField.GetValue(savedTuning), savedDuration);
            this.savedTuning = null;
        }

        /// <summary>The mode a simulation plays: SIM_MODE=match for long matches, else the last car standing.</summary>
        protected static bool SimMatch =>
            string.Equals(Environment.GetEnvironmentVariable("SIM_MODE")?.Trim(), "match", StringComparison.OrdinalIgnoreCase);

        protected static GameTuning LoadTuning()
        {
#if UNITY_EDITOR
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            if (tuning == null) {
                throw new MissingReferenceException("Simulation needs the game's GameTuning asset.");
            }

            return tuning;
#else
            throw new NotSupportedException("Run the simulation as editor PlayMode tests via Tools/simulate.sh.");
#endif
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
                builder.RegisterInstance<IPlayerTopper>(new FixedTopper(PlayerTopper));
                builder.Register<SessionRoster>(Lifetime.Singleton).As<ISessionRoster>()
                    .WithParameter(typeof(GameTuning), LoadTuning());
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

    internal sealed class FixedTopper : IPlayerTopper
    {
        public FixedTopper(string topper) => SelectedTopper = topper ?? "";

        public string SelectedTopper { get; }
    }
}
