using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Measures the actual Game scene with novice input; outcomes are observations, never pass/fail thresholds.
    /// </summary>
    public sealed class RoundSimulationTests : SimulationSceneFixture
    {
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
            var report = new SimulationReport(runs, seed, SimMap);
            var stopwatch = Stopwatch.StartNew();

            CreateParent(input, progress);

            foreach (var level in levels) {
                progress.Round = level;
                progress.LosingStreak = 0;
                for (var run = 0; run < runs; run++) {
                    var roundSeed = unchecked(seed + run);
                    UnityEngine.Random.InitState(roundSeed);
                    input.SetMove(Vector2.zero);
                    yield return LoadGame();
                    var scope = GameScope();
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
