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
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The GOLD RUSH mode's balance, measured on the real Game scene with a novice who knows the mode: it drives to the
    /// nearest coin it sees, now and then at a nearby rich bot, between slow, wrong looks (as <see cref="NoviceDriver"/>).
    /// Observations, not thresholds: wins, places, the player's and the best bot's coins, coins taken, lost and shaken out,
    /// falls, per round of the visit. <c>SIM_ROUNDS</c> (20), <c>SIM_LEVELS</c> (1,7), <c>SIM_SEED</c> (1) and
    /// <c>SIM_MAP</c> as the other simulations; the report goes to <c>Logs/gold-rush-sim.md</c> (and
    /// <c>Logs/gold-rush-sim-&lt;map&gt;.md</c> off the sandbox).
    /// </summary>
    [Explicit("A balance measurement, minutes long")]
    public sealed class GoldRushSimulationTests : SimulationSceneFixture
    {
        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateGoldRushRounds()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("SIM_ROUNDS") ?? "20");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,7").Split(',')
                .Select(value => int.Parse(value.Trim())).ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var input = new SimulationInput();
            var progress = new SimulationProgress();
            UseSpecial(SpecialRound.GoldRush);
            CreateParent(input, progress);

            var report = new StringBuilder();
            report.AppendLine($"# GOLD RUSH: novice simulation on {SimMap}");
            report.AppendLine();
            report.AppendLine($"{runs} rounds per round of the visit, seed {seed}. Coins are the ones carried at the end.");
            report.AppendLine();
            report.AppendLine("| Round | Cars | Wins | Top 3 | Avg place | Loss (bottom half) | Player coins | Best bot coins | Player took | Player lost | Player shook | Player falls | Coins dropped | Coins taken | Shaken out | Fall losses | Falls |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var level in levels) {
                progress.Round = level;
                progress.LosingStreak = 0;
                var rows = new List<Row>();
                for (var run = 0; run < runs; run++) {
                    var roundSeed = unchecked(seed + run);
                    UnityEngine.Random.InitState(roundSeed);
                    input.SetMove(Vector2.zero);
                    yield return LoadGame();
                    var container = GameScope().Container;
                    var round = container.Resolve<ArenaRound>();
                    container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = roundSeed;
                    var mode = container.Resolve<GoldRushMode>();
                    Assert.That(mode.IsActive, Is.True);
                    var novice = new GoldNovice(roundSeed, round, mode, container.Resolve<ArenaCamera>(), input);
                    RoundResult? result = null;
                    round.Finished += value => result = value;
                    var falls = 0;
                    var playerFalls = 0;
                    round.Eliminated += (index, _) => {
                        falls++;
                        playerFalls += index == 0 ? 1 : 0;
                    };
                    round.Begin();
                    var limit = Time.timeAsDouble + mode.Settings.Duration + 10f;
                    var slowFrames = 0;
                    while (!result.HasValue) {
                        Assert.That(Time.timeAsDouble, Is.LessThan(limit), $"Round hung: round {level}, seed {roundSeed}.");
                        novice.Tick();
                        yield return null;
                        slowFrames = Time.timeScale < 1f ? slowFrames + 1 : 0;
                        if (slowFrames >= 5) {
                            Time.timeScale = 1f;
                            slowFrames = 0;
                        }
                    }

                    var best = Enumerable.Range(1, round.Cars.Count - 1).Max(i => round.Score.PointsOf(i));
                    rows.Add(new Row {
                        Cars = round.Cars.Count,
                        Won = result.Value.Outcome == RoundOutcome.Won,
                        Place = result.Value.Place,
                        IsLoss = result.Value.IsLoss,
                        Player = mode.PlayerCoins,
                        BestBot = best,
                        PlayerTook = mode.PlayerTaken,
                        PlayerLost = mode.PlayerLost,
                        PlayerShook = mode.PlayerShook,
                        PlayerFalls = playerFalls,
                        Dropped = mode.Dropped,
                        Taken = mode.Taken,
                        Shaken = mode.Shaken,
                        FallLost = mode.FallLost,
                        Falls = falls,
                    });
                    yield return UnloadGame();
                    Debug.Log($"Gold rush simulation: round {level}, {run + 1}/{runs}, seed {roundSeed}: place " +
                        $"{result.Value.Place}, player {rows[^1].Player} coins, best bot {best}.");
                }

                report.AppendLine($"| {level} | {rows.Average(r => r.Cars):0.#} | {rows.Count(r => r.Won)}/{rows.Count} " +
                    $"({100f * rows.Count(r => r.Won) / rows.Count:0}%) | {100f * rows.Count(r => r.Place <= 3) / rows.Count:0}% | " +
                    $"{rows.Average(r => r.Place):0.00} | {100f * rows.Count(r => r.IsLoss) / rows.Count:0}% | " +
                    $"{rows.Average(r => r.Player):0.0} | {rows.Average(r => r.BestBot):0.0} | {rows.Average(r => r.PlayerTook):0.0} | " +
                    $"{rows.Average(r => r.PlayerLost):0.0} | {rows.Average(r => r.PlayerShook):0.0} | " +
                    $"{rows.Average(r => r.PlayerFalls):0.0} | {rows.Average(r => r.Dropped):0.0} | {rows.Average(r => r.Taken):0.0} | " +
                    $"{rows.Average(r => r.Shaken):0.0} | {rows.Average(r => r.FallLost):0.0} | {rows.Average(r => r.Falls):0.0} |");
            }

            Directory.CreateDirectory("Logs");
            var text = report.ToString();
            File.WriteAllText(SimMap == "sandbox" ? "Logs/gold-rush-sim.md" : $"Logs/gold-rush-sim-{SimMap}.md", text);
            Debug.Log(text);
        }

        private struct Row
        {
            public int Cars;
            public bool Won;
            public int Place;
            public bool IsLoss;
            public int Player;
            public int BestBot;
            public int PlayerTook;
            public int PlayerLost;
            public int PlayerShook;
            public int PlayerFalls;
            public int Dropped;
            public int Taken;
            public int Shaken;
            public int FallLost;
            public int Falls;
        }

        /// <summary>
        /// A novice who knows the coins: slow, imperfect looks, the nearest coin as the target, now and then a nearby bot
        /// carrying more coins than it, inward when near the edge.
        /// </summary>
        private sealed class GoldNovice
        {
            private const float ReactionMin = 0.45f;
            private const float ReactionMax = 0.8f;
            private const float HeadingError = 35f;
            private const float IdleChance = 0.15f;
            private const float RamChance = 0.25f;
            private const float RamRange = 8f;
            private const float EdgeFear = 3f;

            private readonly System.Random random;
            private readonly ArenaRound round;
            private readonly GoldRushMode mode;
            private readonly ArenaCamera arenaCamera;
            private readonly SimulationInput input;
            private float nextLook;

            public GoldNovice(int seed, ArenaRound round, GoldRushMode mode, ArenaCamera arenaCamera, SimulationInput input)
            {
                random = new System.Random(seed);
                this.round = round;
                this.mode = mode;
                this.arenaCamera = arenaCamera;
                this.input = input;
            }

            public void Tick()
            {
                if (!round.IsRunning || round.Elapsed < nextLook) {
                    return;
                }

                nextLook = round.Elapsed + Between(ReactionMin, ReactionMax);
                if (random.NextDouble() < IdleChance) {
                    input.SetMove(Vector2.zero);
                    return;
                }

                var player = round.Player;
                var position = Flat(player.Position);
                var target = position + Flat(player.transform.forward) * 4f;
                var found = false;
                if (random.NextDouble() < RamChance) {
                    Car rich = null;
                    var most = mode.CoinsOf(player);
                    foreach (var car in round.Cars) {
                        if (car != player && car.IsTargetable && mode.CoinsOf(car) > most
                            && Flat(car.Position - player.Position).magnitude < RamRange) {
                            rich = car;
                            most = mode.CoinsOf(car);
                        }
                    }

                    if (rich != null) {
                        target = Flat(rich.Position);
                        found = true;
                    }
                }

                if (!found) {
                    var nearest = float.PositiveInfinity;
                    foreach (var coin in mode.Coins) {
                        if (!coin.IsLive) {
                            continue;
                        }

                        var distance = Flat(coin.Position - player.Position).magnitude;
                        if (distance < nearest) {
                            nearest = distance;
                            target = Flat(coin.Position);
                        }
                    }
                }

                var heading = (target - position).normalized;
                if (round.Edge.DistanceToEdge(position) < EdgeFear) {
                    heading = (heading + Flat(round.Edge.Inward(position)).normalized).normalized;
                }

                heading = Quaternion.Euler(0f, Between(-HeadingError, HeadingError), 0f) * heading;
                var local = Quaternion.Inverse(Quaternion.Euler(0f, arenaCamera.transform.eulerAngles.y, 0f)) * heading;
                input.SetMove(new Vector2(local.x, local.z));
            }

            private float Between(float min, float max) => min + (max - min) * (float)random.NextDouble();

            private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);
        }
    }
}
