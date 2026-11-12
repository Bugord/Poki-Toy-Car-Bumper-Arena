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
    /// The CROWN mode's balance, measured on the real Game scene with a novice who knows the mode: it drives at the
    /// loose crown, at its holder when a bot has it, and away from the nearest car when it has it, between slow, wrong
    /// looks. Observations, not thresholds: wins, places, the player's and the best bot's seconds with the crown and how
    /// often it changes hands, per round of the visit. <c>SIM_ROUNDS</c> (20), <c>SIM_LEVELS</c> (1,7), <c>SIM_SEED</c>
    /// (1) and <c>SIM_MAP</c> as the other simulation; the report goes to <c>Logs/crown-simulation-&lt;map&gt;.md</c>.
    /// </summary>
    [Explicit("A balance measurement, minutes long")]
    public sealed class CrownSimulationTests : SimulationSceneFixture
    {
        [UnityTest]
        [Timeout(int.MaxValue)]
        public IEnumerator SimulateCrownRounds()
        {
            var runs = int.Parse(Environment.GetEnvironmentVariable("SIM_ROUNDS") ?? "20");
            var levels = (Environment.GetEnvironmentVariable("SIM_LEVELS") ?? "1,7").Split(',')
                .Select(value => int.Parse(value.Trim())).ToArray();
            var seed = int.Parse(Environment.GetEnvironmentVariable("SIM_SEED") ?? "1");
            var input = new SimulationInput();
            var progress = new SimulationProgress();
            UseSpecial(SpecialRound.Crown);
            CreateParent(input, progress);

            var report = new StringBuilder();
            report.AppendLine($"# CROWN: novice simulation on {SimMap}");
            report.AppendLine();
            report.AppendLine($"{runs} rounds per round of the visit, seed {seed}. Seconds are with the crown.");
            report.AppendLine();
            report.AppendLine("| Round | Cars | Wins | Avg place | Loss (bottom half) | Player s | Best bot s | Player share | Changes | Steals | Player takes |");
            report.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");
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
                    var crown = container.Resolve<CrownMode>();
                    Assert.That(crown.IsActive, Is.True);
                    var novice = new CrownNovice(roundSeed, round, crown, container.Resolve<ArenaCamera>(), input);
                    RoundResult? result = null;
                    round.Finished += value => result = value;
                    round.Begin();
                    var limit = Time.timeAsDouble + crown.Settings.Duration + 10f;
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

                    var best = round.Cars.Where(car => car != round.Player).Max(car => crown.HeldOf(car));
                    rows.Add(new Row {
                        Cars = round.Cars.Count,
                        Won = result.Value.Outcome == RoundOutcome.Won,
                        Place = result.Value.Place,
                        IsLoss = result.Value.IsLoss,
                        Player = crown.HeldOf(round.Player),
                        BestBot = best,
                        Changes = crown.Changes,
                        Steals = crown.Steals,
                        PlayerTakes = crown.TakesOf(round.Player),
                        Total = round.Cars.Sum(car => crown.HeldOf(car)),
                    });
                    yield return UnloadGame();
                    Debug.Log($"Crown simulation: round {level}, {run + 1}/{runs}, seed {roundSeed}: place " +
                        $"{result.Value.Place}, player {rows[^1].Player:0.0} s, best bot {best:0.0} s.");
                }

                report.AppendLine($"| {level} | {rows.Average(r => r.Cars):0.#} | {rows.Count(r => r.Won)}/{rows.Count} " +
                    $"({100f * rows.Count(r => r.Won) / rows.Count:0}%) | {rows.Average(r => r.Place):0.00} | " +
                    $"{100f * rows.Count(r => r.IsLoss) / rows.Count:0}% | {rows.Average(r => r.Player):0.0} | " +
                    $"{rows.Average(r => r.BestBot):0.0} | {100f * rows.Sum(r => r.Player) / Mathf.Max(1f, rows.Sum(r => r.Total)):0}% | " +
                    $"{rows.Average(r => r.Changes):0.0} | {rows.Average(r => r.Steals):0.0} | {rows.Average(r => r.PlayerTakes):0.0} |");
            }

            Directory.CreateDirectory("Logs");
            var text = report.ToString();
            File.WriteAllText($"Logs/crown-simulation-{SimMap}.md", text);
            Debug.Log(text);
        }

        private struct Row
        {
            public int Cars;
            public bool Won;
            public int Place;
            public bool IsLoss;
            public float Player;
            public float BestBot;
            public int Changes;
            public int Steals;
            public int PlayerTakes;
            public float Total;
        }

        /// <summary>
        /// A novice who knows the crown: slow, imperfect looks (as <see cref="NoviceDriver"/>), the loose crown or its
        /// holder as the target, and with the crown, away from the nearest car, inward near the fence.
        /// </summary>
        private sealed class CrownNovice
        {
            private const float ReactionMin = 0.45f;
            private const float ReactionMax = 0.8f;
            private const float HeadingError = 40f;
            private const float IdleChance = 0.15f;
            private const float FenceFear = 4f;
            private const float Threat = 9f;

            private readonly System.Random random;
            private readonly ArenaRound round;
            private readonly CrownMode crown;
            private readonly ArenaCamera arenaCamera;
            private readonly SimulationInput input;
            private float nextLook;

            public CrownNovice(int seed, ArenaRound round, CrownMode crown, ArenaCamera arenaCamera, SimulationInput input)
            {
                random = new System.Random(seed);
                this.round = round;
                this.crown = crown;
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
                Vector3 heading;
                if (crown.Holder == player) {
                    Car nearest = null;
                    var distance = Threat;
                    foreach (var car in round.Cars) {
                        var d = Flat(car.Position - player.Position).magnitude;
                        if (car != player && car.IsTargetable && d < distance) {
                            nearest = car;
                            distance = d;
                        }
                    }

                    heading = nearest != null ? (position - Flat(nearest.Position)).normalized : Flat(player.transform.forward);
                    if (round.Edge.DistanceToEdge(position) < FenceFear) {
                        heading = (heading + Flat(round.Edge.Inward(position)).normalized * 1.2f).normalized;
                    }
                }
                else {
                    var target = crown.Holder != null ? Flat(crown.Holder.Position) : Flat(crown.Spot);
                    heading = (target - position).normalized;
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
