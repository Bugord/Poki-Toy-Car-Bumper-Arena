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
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Measures ITEM FRENZY against the plain round with the novice model (<see cref="NoviceDriver"/>), who also uses an
    /// item a moment after it lands (a novice does not wait for a good one): the same seeds in both, at the visit rounds
    /// FRENZY_LEVELS (1,7), FRENZY_ROUNDS (30) each, on the sandbox's first variant; FRENZY_BEFORE (0) is the rounds the
    /// player finished in earlier visits (20 plays a returning player's warm-up, the round the mode is entered from the
    /// menu). Writes <c>Logs/item-frenzy-balance.md</c>: wins, places, round length, items used and how the player went out. Numbers
    /// to read, no thresholds.
    /// </summary>
    [Explicit("A balance simulation, minutes long")]
    public sealed class ItemFrenzyBalance : ModeFixture
    {
        // Seconds a novice takes to use an item once it lands.
        private const float UseMin = 0.4f;
        private const float UseMax = 2.2f;
        // An item hit on the player counts as what sent them off within this many seconds of the fall.
        private const float ItemCredit = 3f;

        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator CompareTheFrenzyWithAPlainRound()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("FRENZY_ROUNDS") ?? "30");
            var levels = (Environment.GetEnvironmentVariable("FRENZY_LEVELS") ?? "1,7").Split(',')
                .Select(value => int.Parse(value.Trim())).ToArray();
            var before = int.Parse(Environment.GetEnvironmentVariable("FRENZY_BEFORE") ?? "0");
            var rows = new List<Row>();
            foreach (var level in levels) {
                foreach (var kind in new[] { SpecialRound.None, SpecialRound.ItemFrenzy }) {
                    var row = new Row { Level = level, Kind = kind };
                    rows.Add(row);
                    for (var run = 0; run < runs; run++) {
                        yield return PlayOne(kind, level, before, 1 + run, row);
                        Debug.Log($"Frenzy balance: round {level}, {kind}, {run + 1}/{runs}.");
                    }
                }
            }

            var markdown = Report(rows, runs, before);
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/item-frenzy-balance.md", markdown);
            Debug.Log(markdown);
        }

        private IEnumerator PlayOne(SpecialRound kind, int level, int before, int seed, Row row)
        {
            if (parentScope != null) {
                UnityEngine.Object.DestroyImmediate(parentScope.gameObject);
                parentScope = null;
            }

            UseSpecial(kind);
            UnityEngine.Random.InitState(seed);
            var input = new SimulationInput();
            CreateParent(input, new SimulationProgress { Round = level, RoundsBefore = before }, "sandbox", "rake");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var pickups = container.Resolve<ArenaPickups>();
            container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = seed;
            var novice = new NoviceDriver(seed, round, container.Resolve<ArenaCamera>(), input);
            var random = new System.Random(seed * 31 + 7);
            var limit = container.Resolve<GameTuning>().PlayDuration + 10f;

            RoundResult? result = null;
            round.Finished += value => result = value;
            var lastItemHit = float.NegativeInfinity;
            round.Begin();
            foreach (var car in round.Cars) {
                if (car == round.Player) {
                    car.ItemUsed += _ => row.PlayerItems++;
                    continue;
                }

                car.ItemUsed += _ => row.BotItems++;
                car.ItemHit += (_, point) => {
                    var flat = point - round.Player.Position;
                    flat.y = 0f;
                    if (flat.magnitude < 1.5f) {
                        lastItemHit = round.Elapsed;
                    }
                };
            }

            pickups.Collected += (car, _, _) => {
                if (car == round.Player) {
                    row.PlayerBoxes++;
                }
            };

            var useAt = -1f;
            var started = Time.time;
            while (!result.HasValue) {
                Assert.That(Time.time - started, Is.LessThan(limit), "The round hung.");
                novice.Tick();
                var player = round.Player;
                if (round.IsRunning && player.IsItemReady) {
                    if (useAt < 0f) {
                        useAt = round.Elapsed + (float)(UseMin + random.NextDouble() * (UseMax - UseMin));
                    }
                    else if (round.Elapsed >= useAt) {
                        input.PressItem();
                        useAt = -1f;
                    }
                }
                else {
                    useAt = -1f;
                }

                yield return null;
                if (Time.timeScale < 1f) {
                    Time.timeScale = 1f;
                }
            }

            var value = result.Value;
            row.Rounds++;
            row.Seconds += value.Seconds;
            row.Places += value.Place;
            row.Knockouts += value.Knockouts;
            switch (value.Outcome) {
                case RoundOutcome.Won:
                    row.Wins++;
                    break;
                case RoundOutcome.Survived:
                    row.Survived++;
                    break;
                default:
                    if (value.Seconds - lastItemHit <= ItemCredit) {
                        row.OutByItem++;
                    }
                    else if (value.KnockedOutBy != RoundResult.Nobody) {
                        row.OutPushed++;
                    }
                    else {
                        row.OutFell++;
                    }

                    if (value.Seconds < 10f) {
                        row.OutEarly++;
                    }

                    break;
            }

            input.SetMove(Vector2.zero);
            yield return UnloadGame();
        }

        private static string Report(List<Row> rows, int runs, int before)
        {
            var text = new StringBuilder();
            text.AppendLine("# ITEM FRENZY balance");
            text.AppendLine();
            text.AppendLine($"Novice model with item use, {runs} rounds per row, seeds 1..{runs} in both kinds, sandbox / rake, " +
                $"{before} rounds played before the visit.");
            text.AppendLine();
            text.AppendLine("| visit round | kind | wins | survived | avg place | avg seconds | player KO | " +
                "player boxes | player items | bot items | out: pushed | out: item | out: fell | out < 10 s |");
            text.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var row in rows) {
                var n = Math.Max(1, row.Rounds);
                text.AppendLine($"| {row.Level} | {(row.Kind == SpecialRound.None ? "plain" : "frenzy")} | " +
                    $"{100f * row.Wins / n:0}% | {100f * row.Survived / n:0}% | {(float)row.Places / n:0.00} | " +
                    $"{row.Seconds / n:0.0} | {(float)row.Knockouts / n:0.00} | {(float)row.PlayerBoxes / n:0.0} | " +
                    $"{(float)row.PlayerItems / n:0.0} | {(float)row.BotItems / n:0.0} | {row.OutPushed} | " +
                    $"{row.OutByItem} | {row.OutFell} | {row.OutEarly} |");
            }

            text.AppendLine();
            text.AppendLine("\"out: item\" is an item hit on the player in the last 3 s before the fall (oil is not seen); " +
                "\"pushed\" a knock-out by a car otherwise; \"fell\" off with nobody's credit.");
            return text.ToString();
        }

        private sealed class Row
        {
            public int Level;
            public SpecialRound Kind;
            public int Rounds;
            public int Wins;
            public int Survived;
            public int Places;
            public float Seconds;
            public int Knockouts;
            public int PlayerBoxes;
            public int PlayerItems;
            public int BotItems;
            public int OutPushed;
            public int OutByItem;
            public int OutFell;
            public int OutEarly;
        }
    }
}
