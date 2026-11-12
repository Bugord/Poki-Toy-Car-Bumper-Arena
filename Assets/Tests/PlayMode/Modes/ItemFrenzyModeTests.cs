using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bugord.App.Platform;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The ITEM FRENZY mode in the real Game scene: boxes from the first round of a visit, one waiting in front of every
    /// car as the round starts, coming two and a half times as often and up to seven at once against a plain round's three,
    /// a quicker roll; the floor still crumbles; the bots go for the boxes and use what they get, and spare the player for
    /// the round's first seconds; a plain round leaves the mode silent and its boxes as they were.
    /// </summary>
    public sealed class ItemFrenzyModeTests : ModeFixture
    {
        [UnityTest]
        public IEnumerator ItsRoundPlaysItsRules()
        {
            yield return OpenMode(SpecialRound.ItemFrenzy);
            var mode = ExpectRules<ItemFrenzyMode>(SpecialRound.ItemFrenzy);
            var rules = Tuning.RulesOf(SpecialRound.ItemFrenzy);
            Assert.That(rules.Pickups, Is.EqualTo(PickupRule.Always));
            Assert.That(rules.PickupPace, Is.EqualTo(mode.Settings.PickupPace));
            Assert.That(rules.PickupMax, Is.EqualTo(mode.Settings.PickupMax));
            Assert.That(rules.PickupRollTime, Is.LessThan(Tuning.Pickups.RollTime), "the roll is quicker than usual");
            Assert.That(rules.Floor, Is.EqualTo(FloorRule.Crumble));
            Assert.That(rules.Fall, Is.EqualTo(FallRule.Out));
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent() => ExpectSilent<ItemFrenzyMode>();

        [UnityTest]
        public IEnumerator TheFirstRoundOfAVisitHasABoxWaitingInFrontOfEveryCar()
        {
            UseSpecial(SpecialRound.ItemFrenzy);
            yield return Open("sandbox", "rake", begin: false, round: 1);
            var pickups = Container.Resolve<ArenaPickups>();
            var mode = Container.Resolve<ItemFrenzyMode>();
            Assert.That(pickups.IsEnabled, Is.True, "the mode has boxes in the visit's first round");
            Assert.That(Round.HasBegun, Is.False);
            var expected = Mathf.Min(Round.Cars.Count, mode.Settings.PickupMax);
            Assert.That(pickups.Live.Count, Is.EqualTo(expected), "a box for every car before the round begins");

            // Each car's box is nearer to it than to any other car, and the player's comes first.
            foreach (var car in Round.Cars) {
                var nearest = pickups.Live.OrderBy(box => Distance(box.Position, car.Position)).First();
                var mine = Distance(nearest.Position, car.Position);
                Assert.That(mine, Is.LessThanOrEqualTo(mode.Settings.OpeningDistance + 0.01f));
                foreach (var other in Round.Cars) {
                    if (other != car) {
                        Assert.That(Distance(nearest.Position, other.Position), Is.GreaterThan(mine + 1f),
                            "a car's opening box lies nearer another car");
                    }
                }
            }

            Assert.That(Distance(mode.Opening[0], Round.Player.Position), Is.LessThanOrEqualTo(mode.Settings.OpeningDistance + 0.01f));
        }

        [UnityTest]
        public IEnumerator BoxesComeFasterAndMoreAtOnceThanInAPlainRound()
        {
            var plain = new List<float>();
            yield return CountBoxes(SpecialRound.None, 14f, plain);
            var plainMost = mostLive;
            var frenzy = new List<float>();
            yield return CountBoxes(SpecialRound.ItemFrenzy, 14f, frenzy);
            var frenzyMost = mostLive;

            var plainGap = MeanGap(plain);
            var frenzyGap = MeanGap(frenzy);
            Debug.Log($"Boxes in 14 s with nobody taking them: plain {plain.Count} (first at {First(plain):0.0} s, " +
                $"a gap of {plainGap:0.00} s, {plainMost} at most), frenzy {frenzy.Count} (first at {First(frenzy):0.0} s, " +
                $"a gap of {frenzyGap:0.00} s, {frenzyMost} at most)");
            Assert.That(plainMost, Is.LessThanOrEqualTo(Tuning.Pickups.MaxCount), "a plain round keeps its usual number");
            Assert.That(plainGap, Is.GreaterThanOrEqualTo(Tuning.Pickups.Interval - Tuning.Pickups.Jitter - 0.05f),
                "a plain round keeps its usual pace");
            Assert.That(First(frenzy), Is.LessThan(0.1f), "the frenzy's boxes are there from the start");
            Assert.That(frenzyMost, Is.GreaterThan(plainMost * 2));
            Assert.That(frenzy.Count, Is.GreaterThan(plain.Count * 2));
        }

        [UnityTest]
        public IEnumerator ABrokenBoxLandsQuicklyAndTheItemIsUsedAndCounted()
        {
            UseSpecial(SpecialRound.ItemFrenzy);
            yield return Open("sandbox", "rake");
            var pickups = Container.Resolve<ArenaPickups>();
            var mode = Container.Resolve<ItemFrenzyMode>();
            var player = Round.Player;
            var box = mode.Opening[0];
            player.Movement.Place(new Vector3(box.x, 0.3f, box.z), player.transform.rotation);
            var rolled = -1f;
            player.ItemRolled += _ => rolled = Time.time;
            var timeout = Time.time + 2f;
            while (!player.HasItem && Time.time < timeout) {
                yield return Step(player);
            }

            Assert.That(player.HasItem, Is.True, "the player's car did not break the box under it");
            Assert.That(player.IsItemRolling, Is.True, "the slot spins first");
            while (!player.IsItemReady && Time.time < timeout + 1f) {
                yield return Step(player);
            }

            var roll = Time.time - rolled;
            Debug.Log($"The frenzy's roll took {roll:0.00} s (a plain round's {Tuning.Pickups.RollTime:0.00} s)");
            Assert.That(roll, Is.LessThan(Tuning.Pickups.RollTime - 0.2f));
            Assert.That(pickups.Use(player), Is.True);
            Assert.That(mode.PlayerItemsUsed, Is.EqualTo(1));
            Assert.That(mode.PlayerFirstUse, Is.GreaterThanOrEqualTo(0f));
            Assert.That(player.HasItem, Is.False, "the slot is free at once");
        }

        [UnityTest]
        public IEnumerator TheFloorStillCrumbles()
        {
            UseSpecial(SpecialRound.ItemFrenzy);
            yield return Open("sandbox", "rake");
            var crumble = Container.Resolve<ArenaCrumble>();
            var warned = false;
            crumble.WaveWarning += _ => warned = true;
            var timeout = Time.time + Container.Resolve<GameTuning.ArenaSettings>().CrumbleDelay + 2f;
            while (!warned && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(warned, Is.True, "no wave of the floor began to crumble");
        }

        [UnityTest]
        public IEnumerator TheBotsTakeBoxesAndUseItemsMoreThanInAPlainRound()
        {
            // The player drives as a novice, so the round lasts as a real one does; counted per second of the round.
            var plain = new PlayStats();
            var frenzy = new PlayStats();
            for (var seed = 1; seed <= 2; seed++) {
                yield return Play(SpecialRound.None, 3, seed, 20f, plain, novice: true);
                yield return Play(SpecialRound.ItemFrenzy, 3, seed, 20f, frenzy, novice: true);
            }

            var plainUses = plain.BotUses / plain.Seconds * 10f;
            var frenzyBoxes = frenzy.BotBoxes / frenzy.Seconds * 10f;
            var frenzyUses = frenzy.BotUses / frenzy.Seconds * 10f;
            Debug.Log($"Round 3, two seeds, the bots: plain {plain.BotBoxes} boxes and {plain.BotUses} items in " +
                $"{plain.Seconds:0} s ({plainUses:0.0} items in 10 s); frenzy {frenzy.BotBoxes} boxes and {frenzy.BotUses} " +
                $"items in {frenzy.Seconds:0} s ({frenzyBoxes:0.0} boxes, {frenzyUses:0.0} items in 10 s), kinds " +
                string.Join(", ", frenzy.Kinds));
            Assert.That(frenzyBoxes, Is.GreaterThanOrEqualTo(4f), "the bots go for the boxes");
            Assert.That(frenzyUses, Is.GreaterThanOrEqualTo(2.5f), "the bots use what they get");
            Assert.That(frenzyUses, Is.GreaterThan(plainUses * 2f));
            Assert.That(frenzy.Kinds.Count, Is.GreaterThanOrEqualTo(3), "more than one or two kinds of item go off");
        }

        [UnityTest]
        public IEnumerator TheBotsSpareThePlayerTheirItemsForTheFirstSeconds([Values(1, 7)] int visitRound)
        {
            var hits = 0;
            var grace = 0f;
            var bots = 0;
            for (var seed = 1; seed <= 5; seed++) {
                var stats = new PlayStats();
                yield return Play(SpecialRound.ItemFrenzy, visitRound, seed, 0f, stats, novice: true);
                grace = stats.Grace;
                hits += stats.GraceHitsOnPlayer;
                bots += stats.BotUsesInGrace;
            }

            Debug.Log($"Frenzy round {visitRound}: grace {grace:0.0} s, bots used {bots} items in it over 5 seeds, " +
                $"{hits} of them hit the player");
            Assert.That(grace, Is.GreaterThan(1f));
            Assert.That(hits, Is.Zero, "a bot's item hit the player in the grace");
        }

        [Test]
        public void TheRoundEndSaysHowManyItemsThePlayerUsed()
        {
            var platform = new MatchTests.RecordingPlatform();
            var result = new RoundResult(RoundOutcome.Won, 1, 7, 40f, 2, RoundResult.Nobody, FallCause.None, false, false,
                kind: SpecialRound.ItemFrenzy);
            foreach (var used in new[] { 0, 2, 5, 14 }) {
                platform.MeasureSpecialEnd(SpecialRound.ItemFrenzy, result, false, used);
            }

            // A round without boxes, or a special round of the rotation, says nothing of items.
            platform.MeasureSpecialEnd(SpecialRound.HotPotato, result, false);
            platform.MeasureSpecialEnd(SpecialRound.LowGravity, result, false, 3);
            var names = platform.Events.Select(e => $"{e.Item1} / {e.Item2} / {e.Item3}").ToList();
            foreach (var bucket in new[] { "none", "few", "some", "many" }) {
                Assert.That(names, Does.Contain($"special / item-frenzy-items-{bucket} / items"));
            }

            Assert.That(names.Count(name => name.EndsWith("/ items")), Is.EqualTo(4));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        // --- Helpers ---

        private int mostLive;

        private IEnumerator CountBoxes(SpecialRound kind, float seconds, List<float> times)
        {
            DropParent();
            UseSpecial(kind);
            yield return Open("sandbox", "rake", begin: false);
            var pickups = Container.Resolve<ArenaPickups>();
            pickups.Spawned += _ => times.Add(Round.Elapsed);
            // The opening boxes are laid on the tick after the cars are placed, before the clock runs.
            yield return null;
            var opening = pickups.Live.Count;
            for (var i = 0; i < opening; i++) {
                times.Add(0f);
            }

            Round.Begin();
            mostLive = pickups.Live.Count;
            var end = Time.time + seconds;
            while (Time.time < end) {
                yield return Step();
                mostLive = Mathf.Max(mostLive, pickups.Live.Count);
            }

            yield return UnloadGame();
        }

        private sealed class PlayStats
        {
            public int BotBoxes;
            public int BotUses;
            public int BotUsesInGrace;
            public int GraceHitsOnPlayer;
            public float Grace;
            public float Seconds;
            public readonly HashSet<ItemKind> Kinds = new();
        }

        /// <summary>
        /// A round of <paramref name="kind"/> with the bots driving for <paramref name="seconds"/> (0 for the grace only);
        /// the player stands still, or drives as a novice with <paramref name="novice"/>.
        /// </summary>
        private IEnumerator Play(SpecialRound kind, int visitRound, int seed, float seconds, PlayStats stats,
            bool novice = false)
        {
            DropParent();
            UseSpecial(kind);
            var input = new SimulationInput();
            CreateParent(input, new SimulationProgress { Round = visitRound }, "sandbox", "rake");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var pickups = container.Resolve<ArenaPickups>();
            var difficulty = container.Resolve<RoundDifficulty>();
            container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single().Seed = seed;
            var driver = novice ? new NoviceDriver(seed, round, container.Resolve<ArenaCamera>(), input) : null;
            stats.Grace = difficulty.PlayerGrace;
            pickups.Collected += (car, _, _) => {
                if (car != round.Player) {
                    stats.BotBoxes++;
                }
            };
            round.Begin();
            foreach (var car in round.Cars.Where(car => car != round.Player)) {
                car.ItemUsed += used => {
                    stats.BotUses++;
                    stats.Kinds.Add(used);
                    if (round.Elapsed < difficulty.PlayerGrace) {
                        stats.BotUsesInGrace++;
                    }
                };
                car.ItemHit += (hitKind, point) => {
                    if (round.Elapsed < difficulty.PlayerGrace && Distance(point, round.Player.Position) < 1.5f) {
                        stats.GraceHitsOnPlayer++;
                        Debug.Log($"A bot's {hitKind} hit the player at {round.Elapsed:0.00} s (seed {seed}).");
                    }
                };
            }

            var end = Time.time + (seconds > 0f ? seconds : difficulty.PlayerGrace);
            while (Time.time < end && !round.IsFinished) {
                driver?.Tick();
                yield return null;
            }

            stats.Seconds += round.Elapsed;
            yield return UnloadGame();
        }

        /// <summary>Frees the scope of the Game scene before, so a test can load the scene again under another.</summary>
        private void DropParent()
        {
            if (parentScope != null) {
                Object.DestroyImmediate(parentScope.gameObject);
                parentScope = null;
            }
        }

        private static float Distance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

        private static float First(List<float> times) => times.Count > 0 ? times.Min() : float.NaN;

        private static float MeanGap(List<float> times)
        {
            var regular = times.Where(time => time > 0f).OrderBy(time => time).ToList();
            if (regular.Count < 2) {
                return float.NaN;
            }

            return (regular[regular.Count - 1] - regular[0]) / (regular.Count - 1);
        }
    }
}
