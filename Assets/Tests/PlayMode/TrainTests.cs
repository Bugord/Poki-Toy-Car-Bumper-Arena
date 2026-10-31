using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Train of the Playroom arena's road variant: its rail keeps clear of the pits and the rim, it lights the rail
    /// and whistles before it sets off, a car on the rail is thrown aside by nobody in particular, a bot is told to leave
    /// the rail, the train derails when its floor falls away, and the other variants have no train.
    /// </summary>
    public sealed class TrainTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator PuzzleHasNoTrain()
        {
            yield return Open("playroom", "puzzle");
            Assert.That(Container.Resolve<ToyTrain>().Exists, Is.False);
        }

        [UnityTest]
        public IEnumerator TheRailKeepsClearOfThePitsAndTheRim()
        {
            yield return Open("playroom", "road");
            var train = Container.Resolve<ToyTrain>();
            Assert.That(train.Exists, Is.True);
            Assert.That(train.RailClearance, Is.GreaterThanOrEqualTo(1.2f), "The rail runs too near a pit or the rim.");
            Assert.That(train.TrainLength, Is.GreaterThan(5f));
        }

        [UnityTest]
        public IEnumerator ItLightsTheRailBeforeItSetsOffAndNothingHitsBeforeFiveSeconds()
        {
            yield return Open("playroom", "road");
            var train = Container.Resolve<ToyTrain>();
            var settings = Tuning.Train;
            var warnedStill = false;
            var timeout = Time.time + 30f;
            while (train.Clock < settings.Delay + 1.5f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
                if (train.Clock < settings.Delay) {
                    Assert.That(train.Speed, Is.EqualTo(0f), "The train moved before its delay.");
                    warnedStill |= train.IsWarning && train.Clock >= settings.Delay - settings.Warning;
                    Assert.That(train.Hits, Is.EqualTo(0));
                }
            }

            Assert.That(warnedStill, Is.True, "The rail should light before the train moves.");
            Assert.That(train.IsMoving, Is.True);
        }

        [UnityTest]
        public IEnumerator ACarOnTheRailIsThrownAsideAndNobodyIsCredited()
        {
            yield return Open("playroom", "road");
            var train = Container.Resolve<ToyTrain>();
            var settings = Tuning.Train;
            var car = Round.Cars[1];
            var timeout = Time.time + 30f;
            while (train.Clock < settings.Delay + 1.2f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
            }

            // On the rail, a few metres in front of the nose.
            var spot = train.RailPosition(train.Head + train.Direction * 6f);
            var hitBefore = car.LastHazardTime;
            car.Movement.Place(new Vector3(spot.x, 0.05f, spot.z), Quaternion.identity);
            var until = Time.time + 5f;
            while (Time.time < until && car.LastHazardTime == hitBefore) {
                Hold(car);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(car.LastHazardTime, Is.Not.EqualTo(hitBefore), "The train never hit the car.");
            Assert.That(car.LastHitBy, Is.Null, "A train hit must credit no car.");
            Assert.That(train.Hits, Is.GreaterThan(0));
            Assert.That(car.Movement.Velocity.magnitude, Is.GreaterThan(3f), "The car should be thrown aside.");
        }

        [UnityTest]
        public IEnumerator ABotOnTheRailInTheTrainsWayIsToldToLeaveItAndOneBeyondIsNot()
        {
            yield return Open("playroom", "road");
            var train = Container.Resolve<ToyTrain>();
            var settings = Tuning.Train;
            var timeout = Time.time + 30f;
            while (train.Clock < settings.Delay + 1.2f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
            }

            var inWay = train.RailPosition(train.Head + train.Direction * 5f);
            Assert.That(train.TryAvoid(inWay, 1.2f, Edge, 1f, out var heading), Is.True);
            Assert.That(Edge.DistanceToEdge(inWay + heading.normalized * 3f, 1f), Is.GreaterThanOrEqualTo(0f));

            // Half a lap away, and off to one side of the rail, nothing is in the train's way.
            var far = train.RailPosition(train.Head + train.Direction * train.RailLength * 0.5f);
            Assert.That(train.TryAvoid(far, 1.2f, Edge, 1f, out _), Is.False);
            Assert.That(train.Version, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator TheTrainDerailsWhenItsFloorFallsAwayAndANewOneIsPutOnTheRailThatIsLeft()
        {
            yield return Open("playroom", "road");
            var train = Container.Resolve<ToyTrain>();
            var timeout = Time.time + 120f;
            while (!train.IsDerailed && !Round.IsFinished) {
                Assert.That(Time.time, Is.LessThan(timeout), "The train never derailed.");
                yield return Step();
            }

            Assert.That(train.IsDerailed, Is.True, "The train should come off the rail once its floor has fallen.");
            Assert.That(train.Speed, Is.EqualTo(0f));
            Debug.Log($"TRAINDERAIL at clock {train.Clock:0.0} s");

            // A few seconds on, a new train stands on the rail that is left, lit, then runs.
            var derailedAt = train.Clock;
            while (train.IsDerailed && train.Clock < derailedAt + 30f && !Round.IsFinished) {
                yield return Step();
            }

            Assert.That(train.Respawns, Is.GreaterThanOrEqualTo(1), "No new train was put on the rail that was left.");
            Assert.That(train.IsDerailed, Is.False);
            Debug.Log($"TRAINRESPAWN at clock {train.Clock:0.0} s");
        }
    }
}
