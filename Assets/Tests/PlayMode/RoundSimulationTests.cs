using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bugord.Core;
using NUnit.Framework;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
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
    /// <summary>
    /// Measures the actual Game scene with novice input; outcomes are observations, never pass/fail thresholds.
    /// </summary>
    public sealed class RoundSimulationTests
    {
        private LifetimeScope parentScope;
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

        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateNoviceRounds()
        {
            var runs = ReadPositiveInt("SIM_ROUNDS", "40");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,3,5,7")
                .Split(',').Select(value => ParsePositiveInt("SIM_LEVELS", value.Trim())).Distinct().ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var input = new SimulationInput();
            var progress = new SimulationProgress();
            var audio = new SilentAudioService(LoadAudioLibrary());
            var report = new SimulationReport(runs, seed);
            var stopwatch = Stopwatch.StartNew();

            // A plain parent supplies only gameplay dependencies; no app states, HUD or standalone scene reloader.
            this.parentScope = LifetimeScope.Create(builder => {
                builder.AddBugordCore("simulation");
                builder.Register<IOnScreenControls, NoOnScreenControls>(Lifetime.Singleton);
                builder.RegisterInstance<ISessionProgress>(progress);
                builder.RegisterInstance<IAudioService>(audio);
                builder.RegisterInstance<IInputService>(input);
            }, "Simulation Scope");

            foreach (var level in levels) {
                progress.Round = level;
                progress.LosingStreak = 0;
                for (var run = 0; run < runs; run++) {
                    var roundSeed = unchecked(seed + run);
                    UnityEngine.Random.InitState(roundSeed);
                    input.SetMove(Vector2.zero);
                    using (LifetimeScope.EnqueueParent(parentScope)) {
                        var loading = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
                        Assert.That(loading, Is.Not.Null, "Game scene did not start loading.");
                        var loadStarted = Time.realtimeSinceStartupAsDouble;
                        while (!loading.isDone) {
                            Assert.That(Time.realtimeSinceStartupAsDouble - loadStarted, Is.LessThan(60d),
                                "Game scene did not load within 60 real seconds.");
                            yield return null;
                        }
                    }

                    var scene = SceneManager.GetSceneByName("Game");
                    Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "Game scene was not loaded.");
                    var scope = scene.GetRootGameObjects().Select(root => root.GetComponent<GameplayScope>())
                        .FirstOrDefault(candidate => candidate != null);
                    Assert.That(scope, Is.Not.Null, "Game scene has no GameplayScope.");
                    SceneManager.SetActiveScene(scene);

                    // As in LevelScene, let Start and the entry points initialize before placing the cars.
                    yield return null;
                    var container = scope.Container;
                    var round = container.Resolve<ArenaRound>();
                    // BotDriver is registered as an entry point's interfaces, without AsSelf in the game.
                    container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = roundSeed;
                    var novice = new NoviceDriver(roundSeed, round, container.Resolve<ArenaCamera>(), input);
                    var limit = container.Resolve<GameTuning>().Round.Duration + 10f;
                    RoundResult? result = null;
                    round.Finished += value => result = value;
                    round.Begin();
                    var started = Time.timeAsDouble;
                    var slowFrames = 0;
                    while (!result.HasValue) {
                        Assert.That(Time.timeAsDouble - started, Is.LessThanOrEqualTo((double)limit),
                            $"Round hung: visit round {level}, run {run + 1}, seed {roundSeed}.");
                        novice.Tick();
                        yield return null;

                        // HitStop normally releases on the captured unscaled clock. If that clock ever stalls,
                        // restore speed here, in the test: its 60-80 ms pause must not hold a CPU-speed simulation.
                        slowFrames = Time.timeScale < 1f ? slowFrames + 1 : 0;
                        if (slowFrames >= 5) {
                            Time.timeScale = 1f;
                            slowFrames = 0;
                            report.HitStopRecoveries++;
                        }
                    }

                    report.Add(level, result.Value);
                    yield return UnloadGame();
                    UnityEngine.Debug.Log($"Simulation: visit round {level}, {run + 1}/{runs}, seed {roundSeed}.");
                }
            }

            stopwatch.Stop();
            var markdown = report.ToMarkdown(stopwatch.Elapsed.TotalSeconds);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/simulation-report.md", markdown);
            UnityEngine.Debug.Log(markdown);
        }

        private IEnumerator UnloadGame()
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

        private static AudioLibrary LoadAudioLibrary()
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

        private static int ReadPositiveInt(string name, string fallback)
        {
            return ParsePositiveInt(name, Environment.GetEnvironmentVariable(name) ?? fallback);
        }

        private static int ParsePositiveInt(string name, string value)
        {
            if (!int.TryParse(value, out var number) || number < 1) {
                throw new ArgumentException($"{name} must contain positive integers; got '{value}'.");
            }

            return number;
        }
    }
}
