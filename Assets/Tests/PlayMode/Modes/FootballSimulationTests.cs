using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Measures FOOTBALL with the novice model on the real Game scene, at the player's rounds 1 and 7: how often the
    /// player's team wins and draws, the goals per match, the player's own goals and how often the player touches the
    /// ball. The novice drives at the ball (and now and then lines up behind it), not at the cars. Observations, never
    /// pass/fail. Explicit: it plays dozens of 90-second matches. <c>SIM_ROUNDS</c> (matches per round, 20),
    /// <c>SIM_LEVELS</c> (1,7) and <c>SIM_SEED</c> (1) set the runs; the report is <c>Logs/football-sim.md</c>.
    /// </summary>
    [Explicit("Balance measurement: dozens of 90-second matches")]
    public sealed class FootballSimulationTests : SimulationSceneFixture
    {
        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateFootball()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("SIM_ROUNDS") ?? "20");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,7").Split(',').Select(int.Parse).ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var tuning = LoadTuning();
            UseSpecial(SpecialRound.Football);
            var rows = new List<Row>();
            var input = new SimulationInput();
            var progress = new SimulationProgress();
            CreateParent(input, progress, "sandbox", null);
            foreach (var level in levels) {
                progress.Round = level;
                for (var run = 0; run < runs; run++) {
                    var roundSeed = unchecked(seed + run * 7919 + level * 104729);
                    UnityEngine.Random.InitState(roundSeed);
                    input.SetMove(Vector2.zero);
                    yield return LoadGame();
                    var container = GameScope().Container;
                    var round = container.Resolve<ArenaRound>();
                    var mode = container.Resolve<FootballMode>();
                    container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = roundSeed;
                    var novice = new NoviceDriver(roundSeed, round, container.Resolve<ArenaCamera>(), input) {
                        Ball = () => mode.Pitch != null ? mode.BallPosition : (Vector3?)null,
                        Goal = mode.TargetOf(RoundTeams.Blue),
                    };
                    RoundResult? result = null;
                    round.Finished += value => result = value;
                    var row = new Row { Level = level };
                    var firstGoal = -1f;
                    mode.Scored += (team, author) => {
                        if (firstGoal < 0f) {
                            firstGoal = round.Elapsed;
                        }

                        row.OwnGoals += author == null ? 1 : 0;
                        row.AllyGoals += author != null && author != round.Player && team == RoundTeams.Blue ? 1 : 0;
                    };
                    round.Begin();
                    var started = Time.timeAsDouble;
                    var lastTouch = -1f;
                    while (!result.HasValue) {
                        Assert.That(Time.timeAsDouble - started, Is.LessThan(tuning.Football.Duration + 30d));
                        novice.Tick();
                        if (mode.Ball != null && mode.Ball.LastTouch == round.Player &&
                            mode.Ball.LastTouchTime > lastTouch + 0.5f) {
                            lastTouch = mode.Ball.LastTouchTime;
                            row.PlayerTouches++;
                        }

                        yield return null;
                        if (Time.timeScale < 1f) {
                            Time.timeScale = 1f;
                        }
                    }

                    row.Result = result.Value;
                    row.Goals = mode.Goals;
                    row.PlayerGoals = mode.PlayerGoals;
                    row.FirstGoal = firstGoal;
                    rows.Add(row);
                    yield return UnloadGame();
                    Debug.Log($"FOOTBALL sim: round {level} run {run + 1}/{runs}: " +
                        $"{result.Value.TeamPointsOf(0)}:{result.Value.TeamPointsOf(1)}, player goals {row.PlayerGoals}, " +
                        $"touches {row.PlayerTouches}");
                }
            }

            UnityEngine.Object.DestroyImmediate(parentScope.gameObject);
            parentScope = null;
            var report = Report(rows, tuning, runs, seed);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/football-sim.md", report);
            Debug.Log(report);
        }

        private static string Report(List<Row> rows, GameTuning tuning, int runs, int seed)
        {
            var text = new StringBuilder();
            text.AppendLine("# FOOTBALL simulation");
            text.AppendLine();
            text.AppendLine($"{tuning.Football.Duration:0} s matches, enemy Easy pace {tuning.Teams.EasyEnemyPace:0.00}, " +
                $"ally pace {tuning.Football.AllyPace:0.00}, {runs} matches per round, seed {seed}, novice model chasing " +
                $"the ball ({DateTime.Now:yyyy-MM-dd HH:mm}).");
            text.AppendLine();
            text.AppendLine("| Player round | Matches | Blue wins | Draws | Losses | Blue : Red | Goals / match | " +
                "No-goal matches | Player goals | Ally goals | Own goals | Player touches | First goal, s |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var group in rows.GroupBy(row => row.Level).OrderBy(g => g.Key)) {
                var list = group.ToList();
                var count = list.Count;
                var wins = list.Count(row => row.Result.Outcome == RoundOutcome.Won);
                var draws = list.Count(row => row.Result.IsDraw);
                var scored = list.Where(row => row.FirstGoal >= 0f).ToList();
                text.AppendLine($"| {group.Key} | {count} | {wins} ({100f * wins / count:0}%) | {draws} | " +
                    $"{count - wins - draws} | {list.Average(row => row.Result.TeamPointsOf(0)):0.0} : " +
                    $"{list.Average(row => row.Result.TeamPointsOf(1)):0.0} | {list.Average(row => row.Goals):0.0} " +
                    $"(min {list.Min(row => row.Goals)}, max {list.Max(row => row.Goals)}) | " +
                    $"{list.Count(row => row.Goals == 0)} | {list.Average(row => row.PlayerGoals):0.00} | " +
                    $"{list.Average(row => row.AllyGoals):0.0} | {list.Average(row => row.OwnGoals):0.0} | " +
                    $"{list.Average(row => row.PlayerTouches):0.0} | " +
                    $"{(scored.Count > 0 ? scored.Average(row => row.FirstGoal).ToString("0") : "-")} |");
            }

            return text.ToString();
        }

        private sealed class Row
        {
            public int Level;
            public RoundResult Result;
            public int Goals;
            public int PlayerGoals;
            public int AllyGoals;
            public int OwnGoals;
            public int PlayerTouches;
            public float FirstGoal;
        }
    }
}
