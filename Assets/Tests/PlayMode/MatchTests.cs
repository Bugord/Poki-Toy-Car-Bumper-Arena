using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The long match on the real Game scene: it ends only on time, a fallen car comes back onto the floor (even once
    /// every wave has fallen), safe for a moment, and knock-outs rank the cars. Each test switches the shared tuning to
    /// the match in memory and the fixture switches it back.
    /// </summary>
    public sealed class MatchTests : SimulationSceneFixture
    {
        // Frames of 1/60 s (the fixture fixes the delta time).
        private const int FramesPerSecond = 60;

        private static readonly PropertyInfo LastHitByProperty = typeof(Car).GetProperty(nameof(Car.LastHitBy));
        private static readonly PropertyInfo LastHitTimeProperty = typeof(Car).GetProperty(nameof(Car.LastHitTime));

        [Test]
        public void TheGameTuningAssetPlaysTheLongMatch()
        {
            Assert.That(ShippedMode, Is.EqualTo(GameTuning.GameMode.Match),
                "The mode is chosen per build; since v0.6.0 the asset ships the long match.");
            Assert.That(ShippedMatchSeconds, Is.EqualTo(60f), "A match of the asset lasts a minute.");
        }

        [Test]
        public void KnockoutsRankTheMatchThenFewerFallsThenWhoScoredFirst()
        {
            var score = new MatchScore(4);
            score.RecordFall(1, 2, 1f);
            score.RecordFall(3, 0, 2f);
            score.RecordFall(0, RoundResult.Nobody, 3f);
            Assert.That(score.KnockoutsOf(0), Is.EqualTo(1));
            Assert.That(score.FallsOf(0), Is.EqualTo(1));
            Assert.That(score.KnockedOffOf(0), Is.EqualTo(0), "falling off alone is nobody's knock-out");
            // 0 and 2 have one knock-out each; 0 fell once, 2 never.
            Assert.That(score.Leader, Is.EqualTo(2));
            Assert.That(score.PlaceOf(0), Is.EqualTo(2));

            score.RecordFall(2, 1, 4f);
            // Now both have one knock-out and one fall, and 2 scored first.
            Assert.That(score.PlaceOf(2), Is.EqualTo(1));
            Assert.That(score.PlaceOf(0), Is.EqualTo(2));
            Assert.That(score.PlaceOf(1), Is.EqualTo(3), "a knock-out beats having fallen less");
            Assert.That(score.PlaceOf(3), Is.EqualTo(4));
            var standings = score.Snapshot();
            Assert.That(standings.Select(standing => standing.Index), Is.EqualTo(new[] { 2, 0, 1, 3 }));
            Assert.That(standings.Select(standing => standing.Place), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        }

        [Test]
        public void MatchEventsKeepWithinPokisNumberLimitAndSendNoRoundEvents()
        {
            var platform = new RecordingPlatform();
            var colors = Enumerable.Range(0, 7).ToArray();
            var result = new RoundResult(RoundOutcome.Survived, 7, 7, 150f, 15, RoundResult.Nobody, FallCause.None, true,
                false, null, colors, true, 12, 11, true);
            platform.MeasureMatchStart(12);
            // A later match skips the first match's controls, which read the player's driver.
            platform.MeasureRoundEnd(12, result, 150f, null);

            Assert.That(platform.Events, Is.Not.Empty);
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers),
                    $"{category} / {what} / {action}");
                Assert.That(category, Is.Not.EqualTo("round"), $"{category} / {what} / {action}");
            }

            var names = platform.Events.Select(e => $"{e.Item1} / {e.Item2} / {e.Item3}").ToList();
            Assert.That(names, Does.Contain("match / 10plus / start"));
            Assert.That(names, Does.Contain("match / 10plus / complete"));
            Assert.That(names, Does.Contain("match / place-7-r4plus / place"));
            Assert.That(names, Does.Contain("match / knockouts-10plus-r4plus / knockouts"));
            Assert.That(names, Does.Contain("match / falls-10plus-r4plus / falls"));
            Assert.That(names, Does.Contain("match / pushed-10plus-r4plus / pushed"));
            Assert.That(names, Does.Contain("match / revenge-missed-r4plus / revenge"));
            Assert.That(names.Any(name => name.EndsWith("/ outcome") || name.EndsWith("/ boosts")), Is.False);
        }

        [UnityTest]
        public IEnumerator AMatchEndsOnlyOnTimeAndTheFallenPlayerComesBackSafe()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 12f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var edge = container.Resolve<IArenaEdge>();
            FreezeBots(container);
            RoundResult? result = null;
            round.Finished += value => result = value;
            round.Place();
            round.Begin();
            Assert.That(round.IsMatch, Is.True);

            var player = round.Player;
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "The player off the floor never fell.");
            Assert.That(round.IsFinished, Is.False, "A match must go on after the player falls.");
            Assert.That(round.RespawnTimeLeft(player), Is.GreaterThan(0f));

            yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "The player never came back.");
            Assert.That(player.IsInvulnerable, Is.True, "A car that comes back is safe at first.");
            Assert.That(player.IsTargetable, Is.False, "Bots must not pick a safe car.");
            Assert.That(round.SafeShareLeft(player), Is.GreaterThan(0f));
            yield return Until(() => player.Movement.IsGrounded, tuning.Match.MaxDropTime + 1f,
                "The player dropped back never landed.");
            Assert.That(edge.DistanceToEdge(player.Position), Is.GreaterThanOrEqualTo(0f), "It came back off the floor.");
            yield return Until(() => !player.IsInvulnerable,
                tuning.Match.MaxDropTime + tuning.Match.Invulnerability + 2f, "The player's safe time never ended.");

            yield return Until(() => result.HasValue, tuning.Match.Duration + 2f, "The match never ended.");
            Assert.That(round.Elapsed, Is.GreaterThanOrEqualTo(tuning.Match.Duration - 0.05f), "It ended before its time.");
            var value = result.Value;
            Assert.That(value.IsMatch, Is.True);
            Assert.That(value.Falls, Is.EqualTo(1));
            Assert.That(value.Knockouts, Is.EqualTo(0));
            Assert.That(value.KnockedOff, Is.EqualTo(0));
            Assert.That(value.KnockedOutBy, Is.EqualTo(RoundResult.Nobody));
            Assert.That(value.Outcome, Is.Not.EqualTo(RoundOutcome.Out), "A match has no knock-out end.");
        }

        [UnityTest]
        public IEnumerator KnockoutsScoreTheMatchAndTheRivalLivesWithinIt()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 20f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            FreezeBots(container);
            RoundResult? result = null;
            round.Finished += value => result = value;
            round.Place();
            round.Begin();
            var player = round.Player;
            var first = round.Cars[1];
            var second = round.Cars[2];
            Assert.That(round.Nemesis, Is.Null, "A match starts without a rival.");

            // The player knocks the first bot off.
            yield return KnockOff(first, player);
            Assert.That(round.PlayerKnockouts, Is.EqualTo(1));
            Assert.That(round.Score.KnockoutsOf(0), Is.EqualTo(1));
            Assert.That(round.Score.Leader, Is.EqualTo(0));

            // The second bot knocks the player off and becomes the rival.
            yield return KnockOff(player, second);
            Assert.That(round.Nemesis, Is.SameAs(second));
            Assert.That(round.HadNemesis, Is.True);
            Assert.That(round.IsFinished, Is.False);
            yield return Until(() => !player.IsOut && !first.IsOut, tuning.Match.RespawnDelay + 1f,
                "The fallen cars never came back.");

            // The player takes revenge.
            yield return KnockOff(second, player);
            Assert.That(round.RevengeTaken, Is.True);
            Assert.That(round.Nemesis, Is.Null);

            yield return Until(() => result.HasValue, tuning.Match.Duration + 2f, "The match never ended.");
            var value = result.Value;
            Assert.That(value.Place, Is.EqualTo(1));
            Assert.That(value.Outcome, Is.EqualTo(RoundOutcome.Won));
            Assert.That(value.Knockouts, Is.EqualTo(2));
            Assert.That(value.Falls, Is.EqualTo(1));
            Assert.That(value.KnockedOff, Is.EqualTo(1));
            Assert.That(value.HadNemesis && value.RevengeTaken, Is.True);
            Assert.That(value.IsLoss, Is.False);
            Assert.That(value.Standings[0].Index, Is.EqualTo(0));
            Assert.That(value.Standings[0].Knockouts, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ASafeCarIsNeitherPushedNorHitByHazards()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 30f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            FreezeBots(container);
            round.Place();
            round.Begin();
            var player = round.Player;
            var bot = round.Cars[1];

            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "The player off the floor never fell.");
            yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "The player never came back.");
            yield return Until(() => player.Movement.IsGrounded, tuning.Match.MaxDropTime + 1f, "It never landed.");
            Assert.That(player.IsInvulnerable, Is.True);
            var mine = player.GetComponent<Collider>();
            var theirs = bot.GetComponent<Collider>();
            Assert.That(Physics.GetIgnoreCollision(mine, theirs), Is.True, "A safe car passes through the others.");

            // A bot rams it from three metres.
            var toPlayer = Vector3.ProjectOnPlane(player.Position - Vector3.zero, Vector3.up);
            var from = player.Position - (toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward) * 3f;
            var heading = (player.Position - from).normalized;
            heading.y = 0f;
            bot.Movement.Place(new Vector3(from.x, 0.2f, from.z), Quaternion.LookRotation(heading));
            bot.Movement.Push(heading * 10f);
            for (var frame = 0; frame < FramesPerSecond / 2; frame++) {
                yield return null;
            }

            Assert.That(player.LastHitBy, Is.Null, "A safe car must not be pushed.");
            Assert.That(bot.LastHitBy, Is.Null, "A safe car must not push.");

            var velocity = player.Movement.Velocity;
            new HazardBlow(tuning, container.Resolve<RoundDifficulty>(), null, null)
                .Apply(player, Vector3.right, 9f, 3f, 0.5f, 1f);
            Assert.That(float.IsNegativeInfinity(player.LastHazardTime), Is.True, "A hazard must pass a safe car by.");
            Assert.That((player.Movement.Velocity - velocity).magnitude, Is.LessThan(0.01f));

            // Once clear, its safe time ends and it collides again with a car in play (the rammer, dropped off the
            // floor to clear it, may be back and safe itself by then).
            bot.Movement.Place(new Vector3(60f, 0.5f, 0f), Quaternion.identity);
            var other = round.Cars[2].GetComponent<Collider>();
            yield return Until(() => !player.IsInvulnerable,
                tuning.Match.Invulnerability + 2f, "The safe time never ended.");
            Assert.That(Physics.GetIgnoreCollision(mine, other), Is.False, "It must collide again after its safe time.");
        }

        [UnityTest]
        public IEnumerator ACarComingBackAfterEveryWaveFellLandsOnTheFloor([Values("sandbox", "candy", "playroom")] string map)
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 60f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, map);
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var floor = container.Resolve<ArenaFloor>();
            var edge = container.Resolve<IArenaEdge>();
            FreezeBots(container);
            round.Place();
            round.Begin();
            for (var wave = 0; wave < floor.WaveCount; wave++) {
                floor.Warn(wave);
                floor.Drop(wave);
            }

            // The bots on the fallen rim fall with it and come back too.
            var player = round.Player;
            player.Movement.Place(new Vector3(60f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "The player off the floor never fell.");
            yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "The player never came back.");
            yield return Until(() => player.Movement.IsGrounded, tuning.Match.MaxDropTime + 1f,
                "The player dropped back never landed.");
            Assert.That(edge.DistanceToEdge(player.Position), Is.GreaterThanOrEqualTo(0f));

            for (var frame = 0; frame < FramesPerSecond * 2; frame++) {
                yield return null;
            }

            foreach (var car in round.Cars) {
                Assert.That(car.IsOut, Is.False, $"Car {round.IndexOf(car)} fell again after coming back.");
                Assert.That(car.Position.y, Is.GreaterThan(-0.5f), $"Car {round.IndexOf(car)} is not on the floor.");
                Assert.That(edge.DistanceToEdge(car.Position), Is.GreaterThanOrEqualTo(0f),
                    $"Car {round.IndexOf(car)} came back off the floor.");
            }
        }

        [UnityTest]
        public IEnumerator BotsDoNotTargetACarThatIsSafe()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 40f);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = 3;
            round.Place();
            round.Begin();
            var player = round.Player;
            player.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            yield return Until(() => player.IsOut, 5f, "The player off the floor never fell.");

            var bots = (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
            var safeFrames = 0;
            for (var frame = 0; frame < FramesPerSecond * 20; frame++) {
                yield return null;
                foreach (var bot in bots) {
                    if (bot.Car.IsOut || bot.Target == null || bot.Target.IsOut) {
                        continue;
                    }

                    Assert.That(bot.Target.IsInvulnerable, Is.False,
                        $"Bot {round.IndexOf(bot.Car)} chases car {round.IndexOf(bot.Target)} while it is safe.");
                }

                safeFrames += round.Cars.Count(car => !car.IsOut && car.IsInvulnerable);
            }

            Assert.That(safeFrames, Is.GreaterThan(0), "No car came back to check against.");
        }

        private static void FreezeBots(IObjectResolver container)
        {
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // Marked as driving before the round begins, it never makes its bots: the cars stand still.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
        }

        /// <summary>
        /// Drops <paramref name="car"/> off the floor as if <paramref name="by"/> had just pushed it, and waits for the
        /// round to count the fall.
        /// </summary>
        private IEnumerator KnockOff(Car car, Car by)
        {
            car.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            LastHitByProperty.SetValue(car, by);
            LastHitTimeProperty.SetValue(car, Time.time);
            yield return Until(() => car.IsOut, 5f, "The pushed car never fell.");
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds, string message)
        {
            var frames = Mathf.CeilToInt(seconds * FramesPerSecond);
            for (var frame = 0; frame < frames && !condition(); frame++) {
                yield return null;
            }

            Assert.That(condition(), Is.True, message);
        }

        internal sealed class RecordingPlatform : IGamePlatform
        {
            public readonly List<(string, string, string)> Events = new();

            public bool IsInitialized => true;
            public bool IsAdBlocked => false;
            public string Language => "en";

            public UniTask Initialize(CancellationToken token) => UniTask.CompletedTask;

            public void GameplayStart()
            {
            }

            public void GameplayStop()
            {
            }

            public UniTask CommercialBreak(CancellationToken token) => UniTask.CompletedTask;

            public UniTask<bool> RewardedBreak(CancellationToken token) => UniTask.FromResult(false);

            public void Measure(string category, string what, string action)
            {
                Events.Add((category, what, action));
            }
        }
    }
}
