using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bugord.App.Platform;
using NUnit.Framework;
using ToyCarArena.Gameplay;
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
    /// The GOLD RUSH mode in the real Game scene: its round plays its rules and a plain one builds nothing; coins drop on
    /// safe floor at the start and in bursts; driving over a coin takes it for a point, the player's car pulling near
    /// ones in; a hard bump shakes a share of a car's coins out, a soft one none, and the car cannot take them straight
    /// back; a fall loses a bigger share, less for the player on Easy; the bots drive to the coins and take them, and
    /// the usual picks favour a rich car but never past the cap after the player; the round ends by time with the most
    /// coins winning, the reward caps its knock-outs, and its events carry at most one number.
    /// </summary>
    public sealed class GoldRushModeTests : ModeFixture
    {
        private const float ShortRound = 6f;

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
            yield return OpenMode(SpecialRound.GoldRush);
            var mode = ExpectRules<GoldRushMode>(SpecialRound.GoldRush);
            var rules = Tuning.RulesOf(SpecialRound.GoldRush);
            Assert.That(rules.Fall, Is.EqualTo(FallRule.Respawn));
            Assert.That(rules.Floor, Is.EqualTo(FloorRule.Still));
            Assert.That(rules.IsTimed, Is.True);
            Assert.That(rules.Duration, Is.EqualTo(Tuning.GoldRush.Duration));
            Assert.That(rules.KnockoutRewardCap, Is.EqualTo(Tuning.GoldRush.KnockoutRewardCap));
            Assert.That(mode.Coins.Count, Is.GreaterThan(Tuning.GoldRush.MaxCoins), "The pool has no room for spills.");
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent()
        {
            yield return ExpectSilent<GoldRushMode>();
            var mode = Container.Resolve<GoldRushMode>();
            yield return Wait(1.5f);
            Assert.That(mode.Coins, Is.Empty, "A plain round built coins.");
            Assert.That(mode.EndEvents, Is.Empty);
            Assert.That(mode.TargetBonus(null, Round.Cars[1], 0), Is.Zero);
            Assert.That(GameObject.Find("Gold Rush"), Is.Null, "A plain round built the coins' looks.");
        }

        [UnityTest]
        public IEnumerator CoinsDropOnSafeFloorAtTheStartAndInBursts([Values("sandbox", "candy", "playroom")] string map)
        {
            UseSpecial(SpecialRound.GoldRush);
            yield return Open(map, null);
            var mode = Container.Resolve<GoldRushMode>();
            var settings = mode.Settings;
            yield return Wait(settings.FirstDelay + 0.7f);
            var opening = mode.LiveCount;
            Debug.Log($"GOLD RUSH {map}: {opening} coins at the start for {Round.Cars.Count} cars");
            Assert.That(opening, Is.GreaterThanOrEqualTo(Round.Cars.Count), "Not a coin in front of every car.");
            ExpectOnSafeFloor(mode, map);

            var dropped = mode.Dropped;
            yield return Wait(settings.BurstInterval + settings.BurstJitter + 1f);
            Assert.That(mode.Dropped, Is.GreaterThan(dropped), $"{map}: no burst came.");
            ExpectOnSafeFloor(mode, map);
            Assert.That(mode.Coins.Count(c => (c.IsLive || c.IsFlying) && !c.IsSpilled),
                Is.LessThanOrEqualTo(settings.MaxCoins), "More burst coins than the cap.");
        }

        [UnityTest]
        public IEnumerator DrivingOverACoinTakesIt()
        {
            yield return OpenMode(SpecialRound.GoldRush);
            var mode = Container.Resolve<GoldRushMode>();
            var taken = new List<int>();
            mode.CoinTaken += (index, _) => taken.Add(index);
            var bot = Round.Cars[2];
            var coin = mode.PlaceCoin(Flat(bot.Position) + bot.transform.forward * 0.5f);
            Assert.That(coin, Is.Not.Null);
            yield return Step();
            yield return Step();
            Assert.That(coin.IsLive, Is.False, "A car on a coin did not take it.");
            Assert.That(Round.Score.PointsOf(2), Is.EqualTo(1), "A coin taken is no point.");
            Assert.That(mode.CoinsOf(bot), Is.EqualTo(1));
            Assert.That(taken, Is.EqualTo(new[] { 2 }));

            // The player's car pulls a coin in from farther than a bot takes one.
            var player = Round.Player;
            var near = Flat(player.Position) + player.transform.right * (mode.Settings.PlayerMagnet - 0.4f);
            var pulled = mode.PlaceCoin(near);
            var timeout = Time.time + 1.5f;
            while (pulled.IsLive && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(pulled.IsLive, Is.False, "The player's car did not pull a near coin in.");
            Assert.That(mode.PlayerTaken, Is.EqualTo(1));
            Assert.That(Round.Score.PointsOf(0), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AHardBumpShakesCoinsOutAndASoftOneNone()
        {
            yield return OpenMode(SpecialRound.GoldRush);
            var mode = Container.Resolve<GoldRushMode>();
            // No burst lands in the test: the coins counted are the shaken ones.
            SetField(mode.Settings, "burstInterval", 100f);
            var lost = new List<(int index, int amount, int by)>();
            mode.CoinsLost += (index, amount, by) => lost.Add((index, amount, by));

            var soft = Round.Cars[4];
            mode.GiveCoins(soft, 10);
            yield return Ram(Round.Cars[3], soft, 2.5f);
            Assert.That(mode.CoinsOf(soft), Is.EqualTo(10), "A soft bump shook coins out.");

            var victim = Round.Cars[2];
            mode.GiveCoins(victim, 10);
            yield return Ram(Round.Cars[1], victim, 10f);
            var left = mode.CoinsOf(victim);
            var thrown = mode.Coins.Where(c => c.IsSpilled && (c.IsLive || c.IsFlying)).ToList();
            Debug.Log($"GOLD RUSH: a 10 m/s bump took {10 - left} of 10 coins, {thrown.Count} thrown");
            Assert.That(left, Is.LessThan(10), "A hard bump shook nothing out.");
            Assert.That(left, Is.GreaterThan(0), "A hard bump took every coin.");
            Assert.That(mode.Shaken, Is.EqualTo(10 - left));
            // Still in the air a moment after the bump: nobody has taken one yet.
            Assert.That(thrown.Count + left, Is.EqualTo(10), "The coins thrown are not the coins lost.");
            Assert.That(lost.Last(), Is.EqualTo((2, 10 - left, 1)));

            // The car that lost them cannot take them straight back: driven right onto one, it leaves it there.
            var back = thrown.First();
            yield return Wait(mode.Settings.SpillTime + 0.05f);
            Assert.That(back.IsLive, Is.True, "A thrown coin did not land.");
            victim.Movement.Place(back.Position + Vector3.up * 0.05f, Quaternion.identity);
            yield return Step(victim);
            yield return Step(victim);
            Assert.That(back.IsLive, Is.True, "The car took its own coin straight back.");

            // Another bump in the cooldown shakes nothing more (counted as shaken out: the car may take a dropped coin).
            var shaken = mode.Shaken;
            yield return Ram(Round.Cars[1], victim, 10f);
            Assert.That(mode.Shaken, Is.EqualTo(shaken), "A second bump at once shook more out.");
        }

        [UnityTest]
        public IEnumerator AFallLosesABiggerShareAndThePlayerLessOnEasy()
        {
            UseSpecial(SpecialRound.GoldRush);
            yield return Open("sandbox", "rake", round: 1);
            var mode = Container.Resolve<GoldRushMode>();
            var bot = Round.Cars[1];
            mode.GiveCoins(bot, 10);
            mode.GiveCoins(Round.Player, 10);
            yield return Fall(bot);
            yield return Fall(Round.Player);
            var botLost = 10 - mode.CoinsOf(bot);
            var playerLost = 10 - mode.CoinsOf(Round.Player);
            Debug.Log($"GOLD RUSH on Easy: a bot's fall lost {botLost} of 10 coins, the player's {playerLost}");
            Assert.That(botLost, Is.EqualTo(Mathf.RoundToInt(10 * mode.Settings.FallShare)), "A fall's share.");
            Assert.That(playerLost, Is.GreaterThan(0), "The player's fall lost nothing.");
            Assert.That(playerLost, Is.LessThan(botLost), "On Easy the player loses as much as a bot.");
            Assert.That(mode.FallLost, Is.EqualTo(botLost + playerLost));
            Assert.That(mode.PlayerLost, Is.EqualTo(playerLost));
            var spilled = mode.Coins.Where(c => c.IsSpilled && (c.IsLive || c.IsFlying)).ToList();
            Assert.That(spilled.Count, Is.EqualTo(botLost + playerLost), "The coins lost were not spilled.");
            yield return Wait(mode.Settings.SpillTime + 0.1f);
            ExpectOnSafeFloor(mode, "sandbox");
        }

        [UnityTest]
        public IEnumerator TheBotsDriveToTheCoinsAndTakeThem()
        {
            UseSpecial(SpecialRound.GoldRush);
            yield return Open("sandbox", "rake", round: 7);
            var mode = Container.Resolve<GoldRushMode>();
            var bots = StartBots();
            yield return null;
            Assert.That(bots.Count, Is.GreaterThanOrEqualTo(3), "The bots did not start.");
            // Bumped off by a hunter is the mode (it has a knock-out); driven off going for a coin is not.
            var falls = 0;
            var drivenOff = 0;
            Round.Eliminated += (index, by) => {
                falls += index > 0 ? 1 : 0;
                drivenOff += index > 0 && by == RoundResult.Nobody ? 1 : 0;
            };
            var until = Time.time + 12f;
            while (Time.time < until) {
                yield return Step(Round.Cars.Where(car => !car.IsPlayer).ToArray());
            }

            var botCoins = Enumerable.Range(1, Round.Cars.Count - 1).Sum(i => Round.Score.PointsOf(i));
            Debug.Log($"GOLD RUSH over 12 s: the bots took {mode.Taken} coins (carry {botCoins}), {mode.Dropped} dropped, " +
                $"{mode.Shaken} shaken out, {falls} bot falls, {drivenOff} with no knock-out");
            Assert.That(mode.Taken, Is.GreaterThanOrEqualTo(6), "The bots hardly took any coins.");
            Assert.That(drivenOff, Is.LessThanOrEqualTo(2), "The bots drive off the floor going for coins.");
        }

        [UnityTest]
        public IEnumerator TheBotsWantARichCarButNeverHuntThePlayerPastTheCap()
        {
            UseSpecial(SpecialRound.GoldRush);
            yield return Open("sandbox", "rake", round: 13);
            var mode = Container.Resolve<GoldRushMode>();
            var rich = Round.Cars[3];
            var poor = Round.Cars[4];
            mode.GiveCoins(rich, 12);
            Assert.That(mode.TargetBonus(null, rich, 0), Is.GreaterThan(mode.TargetBonus(null, poor, 0)));
            Assert.That(mode.TargetBonus(null, poor, 0), Is.Zero, "A car with no coins is worth more.");
            Assert.That(mode.TargetBonus(null, rich, 0), Is.LessThanOrEqualTo(mode.Settings.TargetCap));
            Assert.That(mode.TargetBonus(null, Round.Player, mode.Settings.MaxPlayerHunters), Is.LessThan(-100f),
                "The player can be hunted past the cap.");

            // A rich player is chased, but by no more bots than the cap.
            mode.GiveCoins(Round.Player, 20);
            var bots = StartBots();
            yield return null;
            var most = 0;
            var until = Time.time + 8f;
            while (Time.time < until) {
                yield return Step(Round.Cars.Where(car => !car.IsPlayer).ToArray());
                most = Mathf.Max(most, bots.Count(bot => bot.Target == Round.Player && !bot.Car.IsOut));
            }

            Debug.Log($"GOLD RUSH: most bots after the rich player at once {most}");
            Assert.That(most, Is.LessThanOrEqualTo(mode.Settings.MaxPlayerHunters), "Too many bots after the player.");
        }

        [UnityTest]
        public IEnumerator TheRoundEndsByTimeAndTheMostCoinsWin([Values("win", "second", "last")] string end)
        {
            SetField(LoadTuning().GoldRush, "duration", ShortRound);
            yield return OpenMode(SpecialRound.GoldRush);
            var mode = Container.Resolve<GoldRushMode>();
            RoundResult? result = null;
            Round.Finished += r => result = r;
            // Every car out for a moment ends nothing: the round is timed. Out before the coins, so its fall loses none.
            Round.KnockOut(Round.Cars[1], Round.Player);
            mode.GiveCoins(Round.Player, 20);
            if (end == "second") {
                mode.GiveCoins(Round.Cars[2], 40);
            }
            else if (end == "last") {
                for (var index = 1; index < Round.Cars.Count; index++) {
                    mode.GiveCoins(Round.Cars[index], 40);
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
            Assert.That(r.Kind, Is.EqualTo(SpecialRound.GoldRush));
            Assert.That(r.Points, Is.EqualTo(mode.PlayerCoins), "The result's points are not the player's coins.");
            Assert.That(r.Place, Is.EqualTo(end == "win" ? 1 : end == "second" ? 2 : Round.Cars.Count));
            Assert.That(r.Outcome, Is.EqualTo(end == "win" ? RoundOutcome.Won : RoundOutcome.Survived));
            Assert.That(r.IsLoss, Is.EqualTo(end == "last"), "A loss is a place below the top half.");
            Assert.That(r.KnockoutRewardCap, Is.EqualTo(mode.Settings.KnockoutRewardCap));
            Assert.That(r.BonusKnockouts, Is.Zero, "Coins are paid as knock-outs: every round would pay the most.");

            // The coins stay where they are on the result: no more drop, none taken.
            var taken = mode.Taken;
            yield return Wait(1f);
            Assert.That(mode.Taken, Is.EqualTo(taken), "Coins were taken after the round.");
        }

        [UnityTest]
        public IEnumerator ItsEventsCarryOneNumberEach()
        {
            yield return OpenMode(SpecialRound.GoldRush);
            var mode = Container.Resolve<GoldRushMode>();
            Assert.That(mode.EndEvents.ToList(), Is.EqualTo(new[] {
                ("gold-rush-coins-none", "gold-rush-coins"), ("gold-rush-lost-none", "gold-rush-lost"),
                ("gold-rush-shook-none", "gold-rush-shook"),
            }));

            mode.GiveCoins(Round.Player, 23);
            var events = mode.EndEvents.ToList();
            Assert.That(events[0], Is.EqualTo(("gold-rush-coins-20", "gold-rush-coins")));
            mode.GiveCoins(Round.Player, 20);
            Assert.That(mode.EndEvents.First(), Is.EqualTo(("gold-rush-coins-30plus", "gold-rush-coins")));
            foreach (var (id, action) in mode.EndEvents) {
                Assert.That(GameEvent.CountNumbers("special", id, action), Is.LessThanOrEqualTo(1), $"{id} / {action}");
                Assert.That(GameEvent.CountNumbers("special", id, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        private void ExpectOnSafeFloor(GoldRushMode mode, string map)
        {
            foreach (var coin in mode.Coins.Where(c => c.IsLive || c.IsFlying)) {
                Assert.That(Edge.DistanceToEdge(coin.Position, mode.Settings.EdgeMargin - 0.01f),
                    Is.GreaterThanOrEqualTo(0f), $"{map}: a coin at {coin.Position} is off the safe floor.");
            }
        }

        private IList<Bot> StartBots()
        {
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // The fixture stills the bots before they are made; let the driver make them.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
            return (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
        }

        private IEnumerator Fall(Car car)
        {
            car.Movement.Place(new Vector3(car.Position.x, Tuning.Round.FallHeight - 1f, car.Position.z),
                Quaternion.identity);
            var timeout = Time.time + 1f;
            while (!car.IsOut && Time.time < timeout) {
                yield return Step(car);
            }

            Assert.That(car.IsOut, Is.True, "The car below the fall height is not out.");
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

            for (var i = 0; i < 3; i++) {
                yield return Step(car, other);
            }
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
