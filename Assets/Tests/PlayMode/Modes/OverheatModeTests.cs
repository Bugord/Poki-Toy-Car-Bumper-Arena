using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// The OVERHEAT mode in the real Game scene: a hit heats the car that took it, by how hard, and the one that hit a
    /// little; the hotter a car, the more a hit pushes it and the longer it is off balance; a fall cools it and it comes
    /// back cold; a knock-out is a point, a knock-out of a red-hot car by the player a meltdown; the round ends by time
    /// and places by points; the player takes a share of the heat on Easy; the bots want hot cars, never more than the
    /// cap after the player, and a hot bot keeps off the edge; a plain round and HOT POTATO know nothing of heat.
    /// </summary>
    public sealed class OverheatModeTests : ModeFixture
    {
        private const float ShortRound = 4f;

        private readonly List<(object target, FieldInfo field, object value)> saved = new();

        [TearDown]
        public void RestoreTuning()
        {
            for (var i = saved.Count - 1; i >= 0; i--) {
                var (target, field, value) = saved[i];
                field.SetValue(target, value);
            }

            saved.Clear();
        }

        [UnityTest]
        public IEnumerator ItsRoundPlaysItsRules()
        {
            yield return OpenMode(SpecialRound.Overheat);
            ExpectRules<OverheatMode>(SpecialRound.Overheat);
            Assert.That(Round.Mode.Fall, Is.EqualTo(FallRule.Respawn), "The cars do not come back.");
            Assert.That(Round.IsTimed, Is.True);
            Assert.That(Round.Mode.Teams, Is.Zero, "Every car for itself.");
            Assert.That(Round.Mode.PointsPerKnockout, Is.EqualTo(1));
            Assert.That(Round.Mode.KnockoutRewardCap, Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent() => ExpectSilent<OverheatMode>();

        [UnityTest]
        public IEnumerator APlainRoundAndHotPotatoSeeNoHeat([Values(SpecialRound.None, SpecialRound.HotPotato)] SpecialRound kind)
        {
            yield return OpenMode(kind);
            var mode = Container.Resolve<OverheatMode>();
            Assert.That(mode.IsActive, Is.False);
            var hitter = Round.Cars[1];
            var other = Round.Cars[2];
            yield return Ram(hitter, other, 10f);
            Assert.That(other.LastHitBy, Is.SameAs(hitter), "The test's hit did not land.");
            foreach (var car in Round.Cars) {
                Assert.That(mode.HeatOf(car), Is.Zero, "A round of another kind heats a car.");
                Assert.That(car.PushTakenScale, Is.EqualTo(1f));
                Assert.That(car.StaggerTakenScale, Is.EqualTo(1f));
                Assert.That(car.BumpSpeedScale, Is.EqualTo(1f));
            }

            Assert.That(mode.EndEvents, Is.Empty, "Another round sends the mode's events.");
            Assert.That(mode.TargetBonus(null, other, 0), Is.Zero);
        }

        [UnityTest]
        public IEnumerator AHitHeatsTheCarItHitsByItsStrength()
        {
            yield return OpenMode(SpecialRound.Overheat);
            var mode = Container.Resolve<OverheatMode>();
            Assert.That(Round.Cars.Count, Is.GreaterThanOrEqualTo(5), "Too few cars for the test.");

            yield return Ram(Round.Cars[1], Round.Cars[2], 6f);
            var soft = mode.HeatOf(2);
            var softHitter = mode.HeatOf(1);
            yield return Ram(Round.Cars[3], Round.Cars[4], 10f);
            var hard = mode.HeatOf(4);
            Debug.Log($"OVERHEAT heat: soft hit {soft:0.000} (hitter {softHitter:0.000}), hard hit {hard:0.000} " +
                $"(hitter {mode.HeatOf(3):0.000})");

            Assert.That(soft, Is.GreaterThan(0f), "A hit did not heat the car it hit.");
            Assert.That(hard, Is.GreaterThan(soft * 1.4f), "A hard hit heats no more than a soft one.");
            Assert.That(softHitter, Is.LessThan(soft), "The car that hit got as hot as the one it hit.");
            Assert.That(mode.HeatOf(3), Is.LessThan(hard));
            Assert.That(mode.HeatOf(0), Is.Zero, "The player was not in it.");
            // The first hits feel like plain ones: a hard hit leaves the push nearly as it was.
            Assert.That(Round.Cars[4].PushTakenScale, Is.LessThan(1.15f), "One hit already changes the push a lot.");
        }

        [UnityTest]
        public IEnumerator PushAndStaggerGrowWithHeat()
        {
            yield return OpenMode(SpecialRound.Overheat);
            var mode = Container.Resolve<OverheatMode>();
            var settings = mode.Settings;
            var cold = Round.Cars[2];
            var hot = Round.Cars[4];
            mode.SetHeat(hot, 1f);
            Assert.That(hot.PushTakenScale, Is.EqualTo(settings.PushAtMax).Within(0.001f));
            Assert.That(hot.StaggerTakenScale, Is.EqualTo(settings.StaggerAtMax).Within(0.001f));
            Assert.That(hot.BumpSpeedScale, Is.EqualTo(settings.SpeedCapAtMax).Within(0.001f));
            Assert.That(cold.PushTakenScale, Is.EqualTo(1f));
            Assert.That(mode.IsRedHot(4), Is.True);

            var (coldSpeed, coldStagger) = default((float, float));
            yield return Hit(Round.Cars[1], cold, 8f, result => (coldSpeed, coldStagger) = result);
            var (hotSpeed, hotStagger) = default((float, float));
            yield return Hit(Round.Cars[3], hot, 8f, result => (hotSpeed, hotStagger) = result);
            Debug.Log($"OVERHEAT push at 8 m/s: cold {coldSpeed:0.0} m/s and {coldStagger:0.00} s off balance, " +
                $"red-hot {hotSpeed:0.0} m/s and {hotStagger:0.00} s");

            Assert.That(hotSpeed, Is.GreaterThan(coldSpeed * 1.5f), "A hot car is pushed no farther than a cold one.");
            Assert.That(hotStagger, Is.GreaterThan(coldStagger * 1.5f), "A hot car is off balance no longer.");
        }

        [UnityTest]
        public IEnumerator AFallAndARespawnResetHeat()
        {
            yield return OpenMode(SpecialRound.Overheat);
            var mode = Container.Resolve<OverheatMode>();
            var car = Round.Cars[1];
            mode.SetHeat(car, 0.9f);
            car.Movement.Place(new Vector3(0f, Tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(car);
            yield return Step(car);
            Assert.That(car.IsOut, Is.True);
            Assert.That(mode.HeatOf(car), Is.Zero, "A fall did not cool the car.");
            Assert.That(car.PushTakenScale, Is.EqualTo(1f));
            Assert.That(mode.Falls, Is.EqualTo(1));
            Assert.That(mode.AverageFallHeat, Is.EqualTo(0.9f).Within(0.001f), "The heat it fell with.");

            var timeout = Time.time + Tuning.Match.RespawnDelay + 2f;
            while (car.IsOut && Time.time < timeout) {
                yield return Step(car);
            }

            Assert.That(car.IsOut, Is.False, "The car did not come back.");
            Assert.That(car.IsInvulnerable, Is.True, "It came back unsafe.");
            Assert.That(mode.HeatOf(car), Is.Zero, "It came back hot.");
            Assert.That(car.StaggerTakenScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator AKnockOutIsAPointAndOfARedHotCarAMeltdown()
        {
            yield return OpenMode(SpecialRound.Overheat);
            var mode = Container.Resolve<OverheatMode>();
            var meltdowns = new List<int>();
            mode.Meltdown += meltdowns.Add;

            Assert.That(Round.KnockOut(Round.Cars[1], Round.Player), Is.True);
            Assert.That(Round.Score.PointsOf(0), Is.EqualTo(1), "A knock-out gave no point.");
            Assert.That(meltdowns, Is.Empty, "A cold car's knock-out was a meltdown.");

            mode.SetHeat(Round.Cars[2], 0.95f);
            Assert.That(Round.KnockOut(Round.Cars[2], Round.Player), Is.True);
            Assert.That(Round.Score.PointsOf(0), Is.EqualTo(2));
            Assert.That(meltdowns, Is.EqualTo(new[] { 2 }), "Knocking a red-hot car off was no meltdown.");
            Assert.That(mode.PlayerHotKnockouts, Is.EqualTo(1));

            // A bot's knock-out of a hot car is a point for the bot, no meltdown.
            mode.SetHeat(Round.Cars[3], 0.95f);
            Assert.That(Round.KnockOut(Round.Cars[3], Round.Cars[4]), Is.True);
            Assert.That(Round.Score.PointsOf(4), Is.EqualTo(1));
            Assert.That(meltdowns.Count, Is.EqualTo(1));

            // The player falls warm: its own events say one hot knock-out and a warm fall, each with at most one number.
            mode.SetHeat(Round.Player, 0.5f);
            Assert.That(Round.KnockOut(Round.Player, Round.Cars[4]), Is.True);
            var events = mode.EndEvents.ToList();
            Assert.That(events, Is.EqualTo(new[] {
                ("overheat-hotko-1", "overheat-knockouts"), ("overheat-fall-warm", "overheat-falls"),
            }));
            Assert.That(Round.IsFinished, Is.False, "A timed round goes on after the player falls.");
        }

        [UnityTest]
        public IEnumerator TheRoundEndsByTimeAndPlacesByPoints([Values("win", "second", "last")] string end)
        {
            SetField(LoadTuning().Overheat, "duration", ShortRound);
            yield return OpenMode(SpecialRound.Overheat);
            RoundResult? result = null;
            Round.Finished += r => result = r;
            // Every bot out for a moment ends nothing: the round is timed.
            Round.KnockOut(Round.Cars[1], Round.Player);
            for (var index = 2; index < Round.Cars.Count; index++) {
                Round.KnockOut(Round.Cars[index], null);
            }

            if (end == "second") {
                Assert.That(Round.AddPoints(Round.Cars[2], 3), Is.True);
            }
            else if (end == "last") {
                for (var index = 1; index < Round.Cars.Count; index++) {
                    Assert.That(Round.AddPoints(Round.Cars[index], 3), Is.True);
                }
            }

            yield return Wait(ShortRound - 1.5f);
            Assert.That(result.HasValue, Is.False, "The round ended before its time.");
            var timeout = Time.time + 3f;
            while (!result.HasValue && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(result.HasValue, Is.True, "The round did not end when its time ran out.");
            var r = result.Value;
            Assert.That(r.IsTimed, Is.True);
            Assert.That(r.Kind, Is.EqualTo(SpecialRound.Overheat));
            Assert.That(r.Points, Is.EqualTo(1));
            Assert.That(r.Place, Is.EqualTo(end == "win" ? 1 : end == "second" ? 2 : Round.Cars.Count));
            Assert.That(r.Outcome, Is.EqualTo(end == "win" ? RoundOutcome.Won : RoundOutcome.Survived));
            Assert.That(r.IsLoss, Is.EqualTo(end == "last"), "A loss is a place below the top half.");
        }

        [Test]
        public void TheRoundPaysAtMostFiveKnockOutsAndALowPlaceIsALoss()
        {
            var tuning = LoadTuning();
            var rules = tuning.RulesOf(SpecialRound.Overheat);
            var many = new RoundResult(RoundOutcome.Won, 1, 7, 60f, 9, RoundResult.Nobody, FallCause.None, false, false,
                isTimed: true, kind: SpecialRound.Overheat, knockoutRewardCap: rules.KnockoutRewardCap);
            Assert.That(many.RewardKnockouts, Is.EqualTo(5));
            Assert.That(many.Knockouts, Is.EqualTo(9), "The missions and the analytics see every knock-out.");
            // A loss is a place below the top half: below third of seven, below second of four.
            Assert.That(rules.TopPlacesOf(7), Is.EqualTo(3));
            Assert.That(rules.TopPlacesOf(4), Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ThePlayerTakesAShareOfTheHeatOnEasy([Values(1, 13)] int round)
        {
            UseSpecial(SpecialRound.Overheat);
            yield return Open("sandbox", "rake", round: round);
            var mode = Container.Resolve<OverheatMode>();
            var difficulty = Container.Resolve<RoundDifficulty>();
            yield return Ram(Round.Cars[1], Round.Player, 9f);
            yield return Ram(Round.Cars[2], Round.Cars[3], 9f);
            var player = mode.HeatOf(0);
            var bot = mode.HeatOf(3);
            var share = mode.Settings.PlayerShareAt(difficulty.Level);
            Debug.Log($"OVERHEAT round {round} (level {difficulty.Level:0.00}): the player {player:0.000} against a bot " +
                $"{bot:0.000}, share {share:0.00}");
            Assert.That(bot, Is.GreaterThan(0f), "The test's hit did not land.");
            Assert.That(player / bot, Is.EqualTo(share).Within(0.2f), "The player's share of the heat.");
            if (round == 1) {
                Assert.That(player, Is.LessThan(bot * 0.75f), "On Easy the player heats as fast as a bot.");
            }
            else {
                Assert.That(player, Is.LessThan(bot), "On Normal the player heats as fast as a bot.");
            }
        }

        [Test]
        public void TheBonusWantsHotCarsLessWithEveryHunterAndCapsThePlayersHunters()
        {
            var settings = LoadTuning().Overheat;
            Assert.That(settings.EffectOf(0f), Is.Zero);
            Assert.That(settings.EffectOf(1f), Is.EqualTo(1f));
            Assert.That(settings.EffectOf(0.3f), Is.LessThan(0.1f), "The first hits already count a lot.");
            Assert.That(settings.EffectOf(settings.Hot), Is.GreaterThan(0.4f), "A red-hot car is not much hotter.");
            Assert.That(settings.MaxPlayerHunters, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheBotsPreferAHotTargetAndNeverHuntThePlayerPastTheCap()
        {
            UseSpecial(SpecialRound.Overheat);
            yield return Open("sandbox", "rake", round: 13);
            var mode = Container.Resolve<OverheatMode>();
            var hot = Round.Cars[2];
            var cold = Round.Cars[3];
            mode.SetHeat(hot, 1f);
            Assert.That(mode.TargetBonus(null, hot, 0), Is.GreaterThan(mode.TargetBonus(null, hot, 2)));
            Assert.That(mode.TargetBonus(null, cold, 0), Is.Zero, "A cold car is worth more.");
            Assert.That(mode.TargetBonus(null, Round.Player, mode.Settings.MaxPlayerHunters), Is.LessThan(-100f),
                "The player can be hunted past the cap.");

            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
            yield return null;
            var bots = (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
            Assert.That(bots.Count, Is.GreaterThanOrEqualTo(4), "The bots did not start.");
            var picks = new Dictionary<Car, int>();
            var most = 0;
            var until = Time.time + 10f;
            while (Time.time < until) {
                // The hot car and the player stay red-hot, whatever happens to them.
                if (!hot.IsOut) {
                    mode.SetHeat(hot, 1f);
                }

                if (!Round.Player.IsOut) {
                    mode.SetHeat(Round.Player, 1f);
                }

                yield return new WaitForFixedUpdate();
                var onPlayer = 0;
                // Picks count only while the hot car and the others can be picked: out or safe, nobody wants it.
                var isOpen = hot.IsTargetable;
                foreach (var bot in bots) {
                    if (bot.Target == null || bot.Car == hot || !isOpen || !bot.Target.IsTargetable) {
                        if (bot.Target == Round.Player) {
                            onPlayer++;
                        }

                        continue;
                    }

                    picks[bot.Target] = picks.TryGetValue(bot.Target, out var n) ? n + 1 : 1;
                    if (bot.Target == Round.Player) {
                        onPlayer++;
                    }
                }

                most = Mathf.Max(most, onPlayer);
            }

            picks.TryGetValue(hot, out var hotPicks);
            var others = Round.Cars.Where(car => car != hot && car != Round.Player).Select(car =>
                picks.TryGetValue(car, out var n) ? n : 0).ToList();
            var average = others.Count > 0 ? (float)others.Sum() / others.Count : 0f;
            Debug.Log($"OVERHEAT targets over 10 s: the hot car {hotPicks}, other bots {average:0} on average, " +
                $"most after the hot player at once {most}");
            Assert.That(hotPicks, Is.GreaterThan(average * 1.5f), "The bots do not prefer the hot car.");
            Assert.That(most, Is.LessThanOrEqualTo(mode.Settings.MaxPlayerHunters), "Too many bots after the player.");
        }

        [UnityTest]
        public IEnumerator AHotBotKeepsOffTheEdge()
        {
            yield return OpenMode(SpecialRound.Overheat);
            var mode = Container.Resolve<OverheatMode>();
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // The fixture stills the bots before they are made; let the driver make them.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
            yield return null;
            var bots = (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
            Assert.That(bots.Count, Is.GreaterThan(0), "The bots did not start.");
            var bot = bots.First();
            var car = bot.Car;
            // Two metres in from the edge: inside a hot bot's margin, outside a cold one's.
            var rim = Edge.ClampToFloor(new Vector3(30f, 0f, 0f));
            var spot = rim - Flat(rim).normalized * (Tuning.Bots.EdgeMargin + 1f);
            car.Movement.Place(spot + Vector3.up * 0.3f, Quaternion.identity);
            yield return Step(car);
            Assert.That(mode.SteerBot(bot, Edge, 0.02f), Is.False, "A cold bot is steered by the mode.");
            mode.SetHeat(car, 1f);
            Assert.That(mode.SteerBot(bot, Edge, 0.02f), Is.True, "A hot bot near the edge does not drive back in.");
            car.Movement.Place(new Vector3(0f, 0.3f, 0f), Quaternion.identity);
            yield return Step(car);
            Assert.That(mode.SteerBot(bot, Edge, 0.02f), Is.False, "A hot bot in the middle is steered by the mode.");
        }

        [UnityTest]
        public IEnumerator ThePeakLeavesRoomToComeBack([Values("sandbox", "candy", "playroom")] string map)
        {
            var cycles = LoadTuning().FloorCycles;
            SetField(cycles, "open", 1f);
            SetField(cycles, "shrink", 4f);
            SetField(cycles, "peak", 3f);
            SetField(cycles, "returnTime", 2f);
            SetField(cycles, "returnWarning", 0.8f);
            UseSpecial(SpecialRound.Overheat);
            yield return Open(map, null, round: 13);
            var crumble = Container.Resolve<ArenaCrumble>();
            var floor = Container.Resolve<ArenaFloor>();
            Assert.That(crumble.IsCycling, Is.True, $"{map}: the floor does not go in cycles.");
            var timeout = Time.time + 10f;
            while (crumble.Phase != FloorPhase.Peak && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(crumble.Phase, Is.EqualTo(FloorPhase.Peak), $"{map}: no peak.");
            var fallen = Enumerable.Range(0, floor.WaveCount).Count(w => floor.StateOf(w) != WaveState.Standing);
            var spots = new List<Vector3>();
            for (var x = -25f; x <= 25f; x += 0.5f) {
                for (var z = -25f; z <= 25f; z += 0.5f) {
                    var spot = new Vector3(x, 0f, z);
                    if (floor.DistanceToEdge(spot) >= 2f && spots.All(other => (other - spot).magnitude >= 3.2f)) {
                        spots.Add(spot);
                    }
                }
            }

            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(Round.Cars.Count), $"{map}: no room for every car.");
            for (var i = 0; i < Round.Cars.Count; i++) {
                Round.Cars[i].Movement.Place(spots[i] + Vector3.up * 0.3f, Quaternion.identity);
            }

            yield return null;
            var back = RespawnSpot.Find(floor, Tuning.Match, Round.Cars, null);
            var nearest = Round.Cars.Min(car => (Flat(car.Position) - back).magnitude);
            Debug.Log($"OVERHEAT {map}: {Round.Cars.Count} cars, {fallen} of {floor.WaveCount} waves fall, comes back " +
                $"{floor.DistanceToEdge(back):0.0} m from the edge, {nearest:0.0} m from a car");
            Assert.That(floor.DistanceToEdge(back), Is.GreaterThanOrEqualTo(Tuning.Match.SpawnMargin - 0.01f),
                $"{map}: no safe floor to come back on.");
            Assert.That(nearest, Is.GreaterThanOrEqualTo(3f), $"{map}: it comes back on top of a car.");
        }

        // The car rams the other from three metres off at a set speed, until the hit lands or half a second goes.
        private IEnumerator Ram(Car car, Car other, float speed)
        {
            var toward = Flat(other.Position - car.Position).normalized;
            car.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * speed);
            var until = Time.time + 0.5f;
            while (Time.time < until && other.LastHitBy != car && !car.IsOut) {
                yield return Step(car, other);
            }

            // A few steps more, so the hit is told and both settle.
            for (var i = 0; i < 3; i++) {
                yield return Step(car, other);
            }
        }

        // A ram of the car into the other, and how fast the other leaves (most over the next steps, on the floor) and
        // for how long it is off balance.
        private IEnumerator Hit(Car car, Car other, float speed, System.Action<(float, float)> done)
        {
            // In the middle of the floor, so the push has room.
            other.Movement.Place(new Vector3(0f, 0.05f, 0f), Quaternion.identity);
            for (var i = 0; i < 3; i++) {
                yield return Step(other);
            }

            var toward = Vector3.forward;
            car.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * speed);
            var until = Time.time + 0.5f;
            while (Time.time < until && other.LastHitBy != car) {
                yield return Step(car, other);
            }

            var staggerEnd = (float)typeof(CarMovement)
                .GetField("staggerEndTime", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(other.Movement);
            var stagger = staggerEnd - Time.time;
            var most = 0f;
            for (var i = 0; i < 6; i++) {
                yield return Step(car, other);
                most = Mathf.Max(most, Flat(other.Movement.Velocity).magnitude);
            }

            done((most, stagger));
        }

        private void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            saved.Add((target, field, field.GetValue(target)));
            field.SetValue(target, value);
        }
    }
}
