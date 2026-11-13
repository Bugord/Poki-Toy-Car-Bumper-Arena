using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    /// Measures TEAM PUSH with the novice model on the real Game scene, on every arena, at visit rounds 1 and 7: how often
    /// the player's team wins, the score, the falls per round and the player's knock-outs. Observations, never pass/fail.
    /// Explicit: it plays dozens of minute-long rounds. <c>SIM_ROUNDS</c> (rounds per arena and round, 5),
    /// <c>SIM_LEVELS</c> (1,7), <c>SIM_SEED</c> (1), <c>SIM_TEAM_FLOOR</c> (Still or Cycles, the mode's own by default)
    /// and <c>SIM_ENEMY_PACE</c> (the other team's Easy pace) set the runs; the report is <c>Logs/team-push-sim.md</c>.
    /// </summary>
    [Explicit("Balance measurement: dozens of minute-long rounds")]
    public sealed class TeamPushSimulationTests : SimulationSceneFixture
    {
        private static readonly string[] Maps = { "sandbox", "tabletop", "candy", "playroom" };

        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateTeamPush()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("SIM_ROUNDS") ?? "5");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,7").Split(',').Select(int.Parse).ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var tuning = LoadTuning();
            var floorField = typeof(GameTuning.TeamPushSettings).GetField("floor",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var paceField = typeof(GameTuning.TeamSettings).GetField("easyEnemyPace",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var savedFloor = floorField.GetValue(tuning.TeamPush);
            var savedPace = paceField.GetValue(tuning.Teams);
            var floor = Environment.GetEnvironmentVariable("SIM_TEAM_FLOOR");
            if (!string.IsNullOrEmpty(floor)) {
                floorField.SetValue(tuning.TeamPush, Enum.Parse(typeof(FloorRule), floor));
            }

            var pace = Environment.GetEnvironmentVariable("SIM_ENEMY_PACE");
            if (!string.IsNullOrEmpty(pace)) {
                paceField.SetValue(tuning.Teams, float.Parse(pace, CultureInfo.InvariantCulture));
            }

            UseSpecial(SpecialRound.TeamPush);
            var rows = new List<Row>();
            var report = "";
            try {
                foreach (var map in Maps) {
                    var input = new SimulationInput();
                    var progress = new SimulationProgress();
                    CreateParent(input, progress, map, null);
                    foreach (var level in levels) {
                        progress.Round = level;
                        for (var run = 0; run < runs; run++) {
                            var roundSeed = unchecked(seed + run * 7919 + level * 104729);
                            UnityEngine.Random.InitState(roundSeed);
                            input.SetMove(Vector2.zero);
                            yield return LoadGame();
                            var container = GameScope().Container;
                            var round = container.Resolve<ArenaRound>();
                            container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = roundSeed;
                            var novice = new NoviceDriver(roundSeed, round, container.Resolve<ArenaCamera>(), input);
                            RoundResult? result = null;
                            round.Finished += value => result = value;
                            var selfFalls = 0;
                            round.Eliminated += (_, by) => selfFalls += by == RoundResult.Nobody ? 1 : 0;
                            round.Begin();
                            var started = Time.timeAsDouble;
                            while (!result.HasValue) {
                                Assert.That(Time.timeAsDouble - started, Is.LessThan(tuning.TeamPush.Duration + 20d));
                                novice.Tick();
                                yield return null;
                                if (Time.timeScale < 1f) {
                                    Time.timeScale = 1f;
                                }
                            }

                            var score = round.Score;
                            var falls = 0;
                            for (var index = 0; index < score.Count; index++) {
                                falls += score.FallsOf(index);
                            }

                            rows.Add(new Row {
                                Map = map, Level = level, Result = result.Value, Falls = falls, SelfFalls = selfFalls,
                                PlayerFalls = score.FallsOf(0),
                            });
                            yield return UnloadGame();
                            Debug.Log($"TEAM PUSH sim: {map} round {level} run {run + 1}/{runs}: " +
                                $"{result.Value.TeamPointsOf(0)}:{result.Value.TeamPointsOf(1)}, falls {falls}");
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(parentScope.gameObject);
                    parentScope = null;
                }

                report = Report(rows, tuning, runs, seed);
            }
            finally {
                floorField.SetValue(tuning.TeamPush, savedFloor);
                paceField.SetValue(tuning.Teams, savedPace);
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/team-push-sim.md", report);
            Debug.Log(report);
        }

        private static string Report(List<Row> rows, GameTuning tuning, int runs, int seed)
        {
            var text = new StringBuilder();
            text.AppendLine("# TEAM PUSH simulation");
            text.AppendLine();
            text.AppendLine($"Floor {tuning.TeamPush.Floor}, enemy Easy pace {tuning.Teams.EasyEnemyPace:0.00}, " +
                $"{runs} rounds per arena and round, seed {seed}, novice model ({DateTime.Now:yyyy-MM-dd HH:mm}).");
            text.AppendLine();
            text.AppendLine("| Visit round | Arena | Rounds | Blue wins | Draws | Losses | Blue : Red | Falls / round | " +
                "Own falls | Player KOs | Player falls |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var group in rows.GroupBy(row => (row.Level, row.Map)).OrderBy(g => g.Key.Level)) {
                Line(text, group.Key.Level, group.Key.Map, group.ToList());
            }

            foreach (var group in rows.GroupBy(row => row.Level).OrderBy(g => g.Key)) {
                Line(text, group.Key, "**all**", group.ToList());
            }

            return text.ToString();
        }

        private static void Line(StringBuilder text, int level, string map, List<Row> rows)
        {
            var count = rows.Count;
            var wins = rows.Count(row => row.Result.Outcome == RoundOutcome.Won);
            var draws = rows.Count(row => row.Result.IsDraw);
            var losses = count - wins - draws;
            text.AppendLine($"| {level} | {map} | {count} | {wins} ({100f * wins / count:0}%) | {draws} | {losses} | " +
                $"{rows.Average(row => row.Result.TeamPointsOf(0)):0.0} : " +
                $"{rows.Average(row => row.Result.TeamPointsOf(1)):0.0} | " +
                $"{rows.Average(row => row.Falls):0.0} (min {rows.Min(row => row.Falls)}) | " +
                $"{rows.Average(row => row.SelfFalls):0.0} | {rows.Average(row => row.Result.Knockouts):0.0} | " +
                $"{rows.Average(row => row.PlayerFalls):0.0} |");
        }

        private sealed class Row
        {
            public string Map;
            public int Level;
            public RoundResult Result;
            public int Falls;
            public int SelfFalls;
            public int PlayerFalls;
        }
    }
}
