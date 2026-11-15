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
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Measures OVERHEAT with the novice model on the real Game scene, at the player's rounds 1 and 7: how often the
    /// player wins, the average place, the player's knock-outs (and of red-hot cars) and falls, the falls per round and the
    /// heat cars fall with. Observations, never pass/fail. Explicit: it plays dozens of minute-long rounds.
    /// <c>SIM_ROUNDS</c> (rounds per arena and round, 20), <c>SIM_LEVELS</c> (1,7), <c>SIM_SEED</c> (1), <c>SIM_MAPS</c>
    /// (sandbox,playroom), <c>SIM_OVERHEAT_FLOOR</c> (Still or Cycles, the mode's own by default) and
    /// <c>SIM_HEAT_PUSH</c> (the push at full heat) set the runs; the report is <c>Logs/overheat-sim.md</c>.
    /// </summary>
    [Explicit("Balance measurement: dozens of minute-long rounds")]
    public sealed class OverheatSimulationTests : SimulationSceneFixture
    {
        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateOverheat()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("SIM_ROUNDS") ?? "20");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,7").Split(',').Select(int.Parse).ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var maps = (Environment.GetEnvironmentVariable("SIM_MAPS") ?? "sandbox,playroom").Split(',');
            var tuning = LoadTuning();
            var saved = new List<(FieldInfo field, object value)>();
            Override(tuning, saved, "floor", Environment.GetEnvironmentVariable("SIM_OVERHEAT_FLOOR"),
                text => Enum.Parse(typeof(FloorRule), text));
            Override(tuning, saved, "pushAtMax", Environment.GetEnvironmentVariable("SIM_HEAT_PUSH"),
                text => float.Parse(text, CultureInfo.InvariantCulture));

            UseSpecial(SpecialRound.Overheat);
            var rows = new List<Row>();
            var report = "";
            try {
                foreach (var map in maps) {
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
                            var mode = container.Resolve<OverheatMode>();
                            container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = roundSeed;
                            var novice = new NoviceDriver(roundSeed, round, container.Resolve<ArenaCamera>(), input);
                            RoundResult? result = null;
                            round.Finished += value => result = value;
                            var selfFalls = 0;
                            var hottest = 0f;
                            round.Eliminated += (_, by) => selfFalls += by == RoundResult.Nobody ? 1 : 0;
                            round.Begin();
                            var started = Time.timeAsDouble;
                            while (!result.HasValue) {
                                Assert.That(Time.timeAsDouble - started, Is.LessThan(tuning.Overheat.Duration + 20d));
                                novice.Tick();
                                yield return null;
                                if (Time.timeScale < 1f) {
                                    Time.timeScale = 1f;
                                }

                                hottest = Mathf.Max(hottest, mode.HeatOf(0));
                            }

                            rows.Add(new Row {
                                Map = map, Level = level, Result = result.Value, Cars = round.Cars.Count,
                                Falls = mode.Falls, FallHeat = mode.AverageFallHeat, SelfFalls = selfFalls,
                                PlayerFalls = mode.PlayerFalls, PlayerFallHeat = mode.AveragePlayerFallHeat,
                                HotKnockouts = mode.PlayerHotKnockouts, PlayerHottest = hottest,
                            });
                            yield return UnloadGame();
                            Debug.Log($"OVERHEAT sim: {map} round {level} run {run + 1}/{runs}: place " +
                                $"{result.Value.Place}/{round.Cars.Count}, KOs {result.Value.Knockouts}, falls " +
                                $"{mode.Falls}, heat at falls {mode.AverageFallHeat:0.00}");
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(parentScope.gameObject);
                    parentScope = null;
                }

                report = Report(rows, tuning, runs, seed);
            }
            finally {
                foreach (var (field, value) in saved) {
                    field.SetValue(tuning.Overheat, value);
                }
            }

            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/overheat-sim.md", report);
            Debug.Log(report);
        }

        private static void Override(GameTuning tuning, List<(FieldInfo, object)> saved, string name, string text,
            Func<string, object> parse)
        {
            if (string.IsNullOrEmpty(text)) {
                return;
            }

            var field = typeof(GameTuning.OverheatSettings).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            saved.Add((field, field.GetValue(tuning.Overheat)));
            field.SetValue(tuning.Overheat, parse(text));
        }

        private static string Report(List<Row> rows, GameTuning tuning, int runs, int seed)
        {
            var settings = tuning.Overheat;
            var text = new StringBuilder();
            text.AppendLine("# OVERHEAT simulation");
            text.AppendLine();
            text.AppendLine($"Floor {settings.Floor}, push at full heat ×{settings.PushAtMax:0.0}, stagger ×" +
                $"{settings.StaggerAtMax:0.0}, speed cap ×{settings.SpeedCapAtMax:0.0}, heat per full hit " +
                $"{settings.HeatPerHit:0.00}, {runs} rounds per arena and round, seed {seed}, novice model " +
                $"({DateTime.Now:yyyy-MM-dd HH:mm}).");
            text.AppendLine();
            text.AppendLine("| Player round | Arena | Rounds | Cars | Wins | Avg place | Losses | Player KOs | " +
                "Red-hot KOs | Player falls | Player heat at falls | Player hottest | Falls / round | Own falls | " +
                "Heat at falls |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
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
            var losses = rows.Count(row => row.Result.IsLoss);
            var falling = rows.Where(row => row.PlayerFalls > 0).ToList();
            var playerHeat = falling.Count > 0
                ? falling.Sum(row => row.PlayerFallHeat * row.PlayerFalls) / falling.Sum(row => row.PlayerFalls)
                : 0f;
            var allFalls = rows.Sum(row => row.Falls);
            var heat = allFalls > 0 ? rows.Sum(row => row.FallHeat * row.Falls) / allFalls : 0f;
            text.AppendLine($"| {level} | {map} | {count} | {rows.Average(row => row.Cars):0.0} | " +
                $"{wins} ({100f * wins / count:0}%) | {rows.Average(row => row.Result.Place):0.00} | {losses} | " +
                $"{rows.Average(row => row.Result.Knockouts):0.0} | {rows.Average(row => row.HotKnockouts):0.0} | " +
                $"{rows.Average(row => row.PlayerFalls):0.0} | {100f * playerHeat:0}% | " +
                $"{100f * rows.Average(row => row.PlayerHottest):0}% | {rows.Average(row => row.Falls):0.0} " +
                $"(min {rows.Min(row => row.Falls)}) | {rows.Average(row => row.SelfFalls):0.0} | {100f * heat:0}% |");
        }

        private sealed class Row
        {
            public string Map;
            public int Level;
            public int Cars;
            public RoundResult Result;
            public int Falls;
            public float FallHeat;
            public int SelfFalls;
            public int PlayerFalls;
            public float PlayerFallHeat;
            public int HotKnockouts;
            public float PlayerHottest;
        }
    }
}
