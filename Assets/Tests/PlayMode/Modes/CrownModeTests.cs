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
    /// The CROWN mode in the real Game scene: the crown lands on the floor a moment after GO and the first car to reach
    /// it takes it; a bump (or a push held on it) steals it, but not in the new holder's first moment and not straight
    /// back; every whole second held is a point to the holder; the round ends on time and the car with the most seconds
    /// wins. A fence keeps the cars on every arena and the crown lands on floor there; the bots race for the loose
    /// crown, run with it, chase its holder from more than one side, and leave the player alone in its first seconds.
    /// </summary>
    public sealed class CrownModeTests : ModeFixture
    {
        private float? originalDuration;

        [TearDown]
        public void RestoreDuration()
        {
            if (originalDuration.HasValue) {
                DurationField().SetValue(LoadTuning().Crown, originalDuration.Value);
                originalDuration = null;
            }
        }

        [UnityTest]
        public IEnumerator ItsRoundPlaysItsRules()
        {
            yield return OpenMode(SpecialRound.Crown);
            ExpectRules<CrownMode>(SpecialRound.Crown);
            Assert.That(Round.Mode.HasFence, Is.True);
            Assert.That(Round.Mode.Fall, Is.EqualTo(FallRule.PutBack));
            Assert.That(Round.IsTimed, Is.True);
            Assert.That(Round.Duration, Is.EqualTo(Tuning.Crown.Duration));
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent()
        {
            yield return ExpectSilent<CrownMode>();
            var crown = Container.Resolve<CrownMode>();
            yield return Wait(Tuning.Crown.FirstDelay + 0.5f);
            Assert.That(crown.Holder, Is.Null);
            Assert.That(crown.IsShown, Is.False, "The crown was built in a plain round.");
            Assert.That(GameObject.Find("Mode Crown"), Is.Null, "The crown is in a plain round's scene.");
        }

        [UnityTest]
        public IEnumerator TheCrownLandsAMomentAfterGoAndTheFirstCarToReachItTakesIt()
        {
            yield return OpenMode(SpecialRound.Crown);
            var crown = Container.Resolve<CrownMode>();
            Assert.That(crown.Phase, Is.EqualTo(CrownMode.CrownPhase.Dropping));
            Assert.That(crown.Holder, Is.Null);

            // Dropping, a car on the spot does not take it yet.
            var car = Round.Cars[2];
            Park(car, crown.Spot);
            yield return Step(car);
            Assert.That(crown.Holder, Is.Null, "A car took the crown before it landed.");

            yield return WaitFor(() => crown.Phase == CrownMode.CrownPhase.Loose, Tuning.Crown.FirstDelay + 0.5f, car);
            yield return WaitFor(() => crown.Holder != null, 0.5f, car);
            Assert.That(crown.Holder, Is.SameAs(car), "The car on the spot did not take the crown.");
            Assert.That(crown.Phase, Is.EqualTo(CrownMode.CrownPhase.Held));
            Assert.That(crown.Changes, Is.EqualTo(1));
            Assert.That(crown.TakesOf(car), Is.EqualTo(1));
            Assert.That(crown.Steals, Is.Zero);
            Assert.That(GameObject.Find("Mode Crown"), Is.Not.Null, "The crown is not in the scene.");
            Assert.That(Flat(crown.CrownPosition - car.Position).magnitude, Is.LessThan(0.5f),
                "The crown does not ride over its holder.");
            Assert.That(crown.CrownPosition.y, Is.GreaterThan(car.Position.y + 1f), "The crown is not over the roof.");
        }

        [UnityTest]
        public IEnumerator ABumpStealsTheCrownNotInItsFirstMomentAndNotStraightBack()
        {
            yield return OpenMode(SpecialRound.Crown);
            var crown = Container.Resolve<CrownMode>();
            var settings = Tuning.Crown;
            var first = Round.Cars[1];
            var thief = Round.Cars[2];
            var third = Round.Cars[3];
            yield return GiveTo(crown, first);
            yield return Wait(settings.StealCooldown + 0.1f);

            yield return Ram(thief, first, () => crown.Holder == first, 2f);
            Assert.That(crown.Holder, Is.SameAs(thief), "The bump did not steal the crown.");
            Assert.That(crown.Robbed, Is.SameAs(first));
            Assert.That(crown.Steals, Is.EqualTo(1));
            Assert.That(crown.IsStealBack(first), Is.True);
            Assert.That(crown.IsProtected, Is.True);

            // A third car straight away: the new holder's first moment is safe.
            // (It ends a little before that moment does: a push still on the holder then steals it, as it should.)
            var safeUntil = Time.time + settings.StealCooldown - 0.2f;
            var hit = false;
            third.Hit += _ => hit = true;
            yield return Ram(third, thief, () => Time.time < safeUntil, 1.5f);
            Assert.That(hit, Is.True, "The third car did not reach the holder.");
            Assert.That(crown.Holder, Is.SameAs(thief), "The crown went on in its holder's first moment.");

            // The robbed car straight back, once the holder can lose it to others: it stays.
            yield return WaitFor(() => !crown.IsProtected, settings.StealCooldown + 0.2f);
            Assert.That(crown.IsStealBack(first), Is.True, "The steal-back time is shorter than the first moment.");
            yield return Ram(first, thief, () => crown.IsStealBack(first) && crown.Holder == thief, 0.6f);
            Assert.That(crown.Holder, Is.SameAs(thief), "The robbed car stole the crown straight back.");

            // Later it may.
            yield return WaitFor(() => !crown.IsStealBack(first), settings.StealBackTime + 0.2f);
            yield return Ram(first, thief, () => crown.Holder == thief, 2f);
            Assert.That(crown.Holder, Is.SameAs(first), "The robbed car could never take the crown back.");
            Assert.That(crown.Steals, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator AHolderPinnedAtTheFenceIsStillRobbed()
        {
            yield return OpenMode(SpecialRound.Crown);
            var crown = Container.Resolve<CrownMode>();
            var holder = Round.Cars[1];
            var thief = Round.Cars[2];
            yield return GiveTo(crown, holder);
            yield return Wait(Tuning.Crown.StealCooldown + 0.1f);

            // The holder against the fence, the thief right behind it pushing it there, never at a run.
            Assert.That(FindNearVoid(1.6f, 3f, out var spot), Is.True, "No floor near the fence.");
            var outward = -Flat(Edge.Inward(spot)).normalized;
            var pinned = spot + outward * (Edge.DistanceToEdge(spot) - 1.4f);
            Park(holder, pinned, outward);
            Park(thief, pinned - outward * 2.75f, outward);
            var until = Time.time + 3f;
            while (crown.Holder == holder && Time.time < until) {
                thief.Movement.Drive(outward, 1f);
                yield return Step(holder, thief);
            }

            Assert.That(crown.Holder, Is.SameAs(thief), "A holder parked at the fence cannot be robbed.");
        }

        [UnityTest]
        public IEnumerator EverySecondHeldIsAPointToTheHolder()
        {
            yield return OpenMode(SpecialRound.Crown);
            var crown = Container.Resolve<CrownMode>();
            var holder = Round.Cars[1];
            yield return GiveTo(crown, holder);
            var heldAt = crown.HeldOf(holder);
            yield return Wait(3.2f);

            var index = Round.IndexOf(holder);
            Assert.That(crown.HeldOf(holder) - heldAt, Is.EqualTo(3.2f).Within(0.1f));
            Assert.That(Round.Score.PointsOf(index), Is.EqualTo(Mathf.FloorToInt(crown.HeldOf(holder))));
            Assert.That(Round.Score.PointsOf(index), Is.GreaterThanOrEqualTo(3));
            foreach (var car in Round.Cars) {
                if (car != holder) {
                    Assert.That(Round.Score.PointsOf(Round.IndexOf(car)), Is.Zero, "A car without the crown scored.");
                }
            }

            Assert.That(crown.HolderIndex, Is.EqualTo(index));
        }

        [UnityTest]
        public IEnumerator TheRoundEndsOnTimeAndTheCarWithTheMostSecondsWins()
        {
            originalDuration = Tuning0().Duration;
            // Long enough that the player, who steals it a few seconds in, holds it far longer than the bot did.
            DurationField().SetValue(Tuning0(), 30f);
            yield return OpenMode(SpecialRound.Crown);
            var crown = Container.Resolve<CrownMode>();
            RoundResult? result = null;
            Round.Finished += r => result = r;

            // A bot holds it for a few seconds, then the player steals it and holds it to the end, longer.
            var bot = Round.Cars[1];
            yield return GiveTo(crown, bot);
            yield return Wait(2.2f);
            yield return Wait(Tuning.Crown.StealCooldown);
            yield return Ram(Round.Player, bot, () => crown.Holder == bot, 2f);
            Assert.That(crown.Holder, Is.SameAs(Round.Player), "The player did not steal the crown.");

            var until = Time.time + 32f;
            while (!result.HasValue && Time.time < until) {
                yield return Step();
            }

            Assert.That(result.HasValue, Is.True, "The round did not end on time.");
            Assert.That(result.Value.IsTimed, Is.True);
            Assert.That(result.Value.Kind, Is.EqualTo(SpecialRound.Crown));
            Assert.That(Round.Elapsed, Is.EqualTo(30f).Within(0.2f));
            var playerHeld = crown.HeldOf(Round.Player);
            var botHeld = crown.HeldOf(bot);
            Assert.That(playerHeld, Is.GreaterThan(botHeld), "The test meant the player to hold it longest.");
            Assert.That(result.Value.Outcome, Is.EqualTo(RoundOutcome.Won));
            Assert.That(result.Value.Place, Is.EqualTo(1));
            Assert.That(result.Value.Points, Is.EqualTo(Mathf.FloorToInt(playerHeld)));
            Assert.That(result.Value.Standings[0].Index, Is.Zero);
            Assert.That(result.Value.Standings[1].Index, Is.EqualTo(Round.IndexOf(bot)));
            Assert.That(result.Value.Standings[1].Points, Is.EqualTo(Mathf.FloorToInt(botHeld)));
            Assert.That(result.Value.IsLoss, Is.False);

            // After the end, nothing more is counted.
            var points = Round.Score.PointsOf(0);
            yield return Wait(1.5f);
            Assert.That(Round.Score.PointsOf(0), Is.EqualTo(points), "Points were counted after the end.");
        }

        [UnityTest]
        public IEnumerator OnTheSandboxTheFenceKeepsACarOnAndTheCrownLandsOnFloor() => KeepsOn("sandbox", "rake", false);

        [UnityTest]
        public IEnumerator OnTheTabletopTheFenceKeepsACarOnAndTheCrownLandsOnFloor() => KeepsOn("tabletop", "board", false);

        [UnityTest]
        public IEnumerator OnTheCandyArenaTheFenceKeepsACarOnAndTheCrownLandsOnFloor() => KeepsOn("candy", "frosting", true);

        [UnityTest]
        public IEnumerator OnThePlayroomTheFenceKeepsACarOnAndTheCrownLandsOnFloor() => KeepsOn("playroom", "puzzle", true);

        [UnityTest]
        public IEnumerator TheBotsRaceForTheCrownRunWithItAndChaseItsHolderFromMoreThanOneSide()
        {
            UseSpecial(SpecialRound.Crown);
            yield return Open("sandbox", "rake", round: 13);
            var crown = Container.Resolve<CrownMode>();
            var botDriver = LetTheBotsDrive();

            // Loose: the bots drive at the spot, and one of them gets it.
            yield return WaitFor(() => crown.Phase == CrownMode.CrownPhase.Loose, Tuning.Crown.FirstDelay + 0.5f);
            var racing = 0;
            var until = Time.time + 1.5f;
            while (Time.time < until) {
                yield return new WaitForFixedUpdate();
                racing = Round.Cars.Count(car => car != Round.Player && car.Movement.DiagnosticDirection.sqrMagnitude > 0.01f
                    && Vector3.Dot(Flat(car.Movement.DiagnosticDirection).normalized,
                        Flat(crown.Spot - car.Position).normalized) > 0.8f);
                if (racing >= 2) {
                    break;
                }
            }

            Assert.That(racing, Is.GreaterThanOrEqualTo(2), "The bots do not race for the loose crown.");
            HoldPlayer();
            yield return WaitFor(() => crown.Holder != null, 8f, Round.Cars.Where(car => car != Round.Player).ToArray());
            var holder = crown.Holder;
            Assert.That(holder, Is.Not.Null, "No bot reached the crown.");
            Assert.That(holder, Is.Not.SameAs(Round.Player));

            // Held: the holder runs from its nearest chaser, the others close in, not all at one point.
            var ran = false;
            var chasing = 0;
            var spread = 0f;
            var plays = (IDictionary)typeof(CrownMode).GetField("botPlay", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(crown);
            until = Time.time + 4f;
            while (Time.time < until && !(ran && chasing >= 2 && spread > 2f)) {
                HoldPlayer();
                yield return new WaitForFixedUpdate();
                if (crown.Holder != holder) {
                    holder = crown.Holder;
                    if (holder == null || holder == Round.Player) {
                        break;
                    }

                    continue;
                }

                var nearest = Round.Cars.Where(car => car != holder && car != Round.Player)
                    .OrderBy(car => Flat(car.Position - holder.Position).sqrMagnitude).FirstOrDefault();
                if (nearest != null && Vector3.Dot(Flat(holder.Movement.DiagnosticDirection).normalized,
                        Flat(holder.Position - nearest.Position).normalized) > 0.3f) {
                    ran = true;
                }

                var aims = new List<Vector3>();
                var count = 0;
                foreach (var car in Round.Cars) {
                    if (car == holder || car == Round.Player || crown.IsStealBack(car)) {
                        continue;
                    }

                    if (Vector3.Dot(Flat(car.Movement.DiagnosticDirection).normalized,
                            Flat(holder.Position - car.Position).normalized) > 0.5f) {
                        count++;
                    }

                    var bot = Bots(botDriver).Single(b => b.Car == car);
                    if (plays.Contains(bot)) {
                        var play = plays[bot];
                        aims.Add((Vector3)play.GetType().GetField("LastAim").GetValue(play));
                    }
                }

                chasing = Mathf.Max(chasing, count);
                for (var i = 0; i < aims.Count; i++) {
                    for (var j = i + 1; j < aims.Count; j++) {
                        spread = Mathf.Max(spread, Vector3.Distance(aims[i], aims[j]));
                    }
                }
            }

            Assert.That(ran, Is.True, "The bot with the crown did not run from its chaser.");
            Assert.That(chasing, Is.GreaterThanOrEqualTo(2), "The bots do not chase the holder.");
            Assert.That(spread, Is.GreaterThan(2f), "The chasers all drive at one point.");
        }

        [UnityTest]
        public IEnumerator OnEasyTheBotsLeaveThePlayerWithTheCrownAloneInItsFirstSeconds()
        {
            UseSpecial(SpecialRound.Crown);
            yield return Open("sandbox", "rake", round: 1);
            var crown = Container.Resolve<CrownMode>();
            var difficulty = Container.Resolve<RoundDifficulty>();
            Assert.That(difficulty.PlayerGrace, Is.GreaterThan(4f));
            yield return GiveTo(crown, Round.Player);
            LetTheBotsDrive();

            var closest = float.PositiveInfinity;
            var until = Time.time + 3f;
            while (Time.time < until && Round.Elapsed < difficulty.PlayerGrace) {
                HoldPlayer();
                yield return new WaitForFixedUpdate();
                foreach (var car in Round.Cars) {
                    if (car != Round.Player) {
                        closest = Mathf.Min(closest, Flat(car.Position - Round.Player.Position).magnitude);
                    }
                }
            }

            Assert.That(crown.Holder, Is.SameAs(Round.Player), "A bot stole the crown in the player's first seconds.");
            Assert.That(closest, Is.GreaterThan(4f), "A bot came at the player in its first seconds with the crown.");
        }

        /// <summary>
        /// The fence stops a car rammed at the nearest void, and the crown's spot has its margin of floor round it.
        /// </summary>
        private IEnumerator KeepsOn(string map, string variant, bool hasVoids)
        {
            yield return OpenMode(SpecialRound.Crown, map, variant);
            var crown = Container.Resolve<CrownMode>();
            Assert.That(Edge.DistanceToEdge(crown.Spot, Tuning.Crown.SpotMargin), Is.GreaterThanOrEqualTo(0f),
                $"The crown's spot {crown.Spot} on {map} is not on the floor with its margin.");
            var fence = Container.Resolve<ArenaFence>();
            Assert.That(fence.Walls.Count, Is.GreaterThan(0), "No fence.");
            if (hasVoids) {
                var shape = Container.Resolve<ArenaMap>().Shape;
                Assert.That(fence.Walls.Count, Is.GreaterThan(shape.Outline.Count), "The holes are not fenced.");
            }

            Assert.That(FindNearVoid(2.5f, 4.5f, out var start), Is.True, "No floor near a void.");
            var toward = -Flat(Edge.Inward(start)).normalized;
            var car = Round.Player;
            car.Movement.Place(start + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * 16f);
            var lowest = car.Position.y;
            var until = Time.time + 2.5f;
            while (Time.time < until) {
                yield return Step(car);
                lowest = Mathf.Min(lowest, car.Position.y);
            }

            Assert.That(car.IsOut, Is.False);
            Assert.That(lowest, Is.GreaterThan(-0.5f), $"The car dropped to {lowest:0.0} m: it went past the fence.");
            Assert.That(Edge.DistanceToEdge(car.Position), Is.GreaterThan(-0.3f), "The car is over the void.");
        }

        // Lets the crown land and gives it to the car by driving it onto the spot; the car goes back to its start after.
        private IEnumerator GiveTo(CrownMode crown, Car car)
        {
            yield return WaitFor(() => crown.Phase == CrownMode.CrownPhase.Loose, Tuning.Crown.FirstDelay + 0.5f);
            Park(car, crown.Spot);
            yield return WaitFor(() => crown.Holder != null, 0.5f, car);
            Assert.That(crown.Holder, Is.SameAs(car), "The car did not take the crown.");
        }

        private IEnumerator WaitFor(System.Func<bool> isDone, float seconds, params Car[] free)
        {
            var until = Time.time + seconds;
            while (!isDone() && Time.time < until) {
                yield return Step(free);
            }
        }

        // The car rams the other from three metres off, for the seconds given or until keepGoing says no.
        private IEnumerator Ram(Car car, Car other, System.Func<bool> keepGoing, float seconds)
        {
            var toward = Flat(other.Position - car.Position).normalized;
            car.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * 8f);
            var until = Time.time + seconds;
            while (Time.time < until && keepGoing()) {
                car.Movement.Drive(toward, 1f);
                yield return Step(car, other);
            }
        }

        private static void Park(Car car, Vector3 point, Vector3 facing = default)
        {
            var heading = facing.sqrMagnitude > 0.01f ? facing : car.transform.forward;
            car.Movement.Place(Flat(point) + Vector3.up * 0.05f, Quaternion.LookRotation(Flat(heading)));
        }

        private BotDriver LetTheBotsDrive()
        {
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
            return botDriver;
        }

        private static IList<Bot> Bots(BotDriver botDriver) =>
            (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(botDriver);

        // The player stands still at its start, the bots drive.
        private void HoldPlayer() => Hold(Round.Cars.Where(car => car != Round.Player).ToArray());

        private static FieldInfo DurationField() =>
            typeof(GameTuning.CrownSettings).GetField("duration", BindingFlags.Instance | BindingFlags.NonPublic);

        private static GameTuning.CrownSettings Tuning0() => LoadTuning().Crown;

        /// <summary>
        /// A spot of floor between <paramref name="near"/> and <paramref name="far"/> metres from the nearest void, out from
        /// the middle, whose run to that void passes no other car: the first such along any of 24 headings.
        /// </summary>
        private bool FindNearVoid(float near, float far, out Vector3 spot)
        {
            for (var i = 0; i < 24; i++) {
                var angle = i * 15f * Mathf.Deg2Rad;
                var heading = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (var t = 0f; t < 40f; t += 0.25f) {
                    spot = heading * t;
                    var distance = Edge.DistanceToEdge(spot);
                    if (distance < near || distance > far) {
                        continue;
                    }

                    var end = spot - Flat(Edge.Inward(spot)).normalized * distance;
                    if (IsClear(spot, end)) {
                        return true;
                    }

                    break;
                }
            }

            spot = Vector3.zero;
            return false;
        }

        // No car but the player within three and a half metres of the run from a to b.
        private bool IsClear(Vector3 a, Vector3 b)
        {
            foreach (var car in Round.Cars) {
                if (car == Round.Player) {
                    continue;
                }

                var point = Flat(car.Position);
                var along = Mathf.Clamp01(Vector3.Dot(point - a, b - a) / Mathf.Max(0.01f, (b - a).sqrMagnitude));
                if (Vector3.Distance(point, Vector3.Lerp(a, b, along)) < 3.5f) {
                    return false;
                }
            }

            return true;
        }

        [Test]
        public void ItsEventBucketsTheSecondsAndTakesWithinTwoNumbers()
        {
            Assert.AreEqual("none", ToyCarArena.App.CrownEvents.HeldBucketOf(0.9f));
            Assert.AreEqual("1", ToyCarArena.App.CrownEvents.HeldBucketOf(9.9f));
            Assert.AreEqual("10", ToyCarArena.App.CrownEvents.HeldBucketOf(19.5f));
            Assert.AreEqual("20plus", ToyCarArena.App.CrownEvents.HeldBucketOf(20f));
            Assert.AreEqual("9", ToyCarArena.App.CrownEvents.TakesBucketOf(9));
            Assert.AreEqual("10plus", ToyCarArena.App.CrownEvents.TakesBucketOf(14));
            foreach (var held in new[] { 0f, 5f, 15f, 25f }) {
                foreach (var takes in new[] { 0, 3, 12 }) {
                    var what = $"crown-held-{ToyCarArena.App.CrownEvents.HeldBucketOf(held)}-takes-"
                        + ToyCarArena.App.CrownEvents.TakesBucketOf(takes);
                    Assert.LessOrEqual(Bugord.App.Platform.GameEvent.CountNumbers("special", what, "crown"),
                        Bugord.App.Platform.GameEvent.MaxNumbers, what);
                }
            }
        }
    }
}
