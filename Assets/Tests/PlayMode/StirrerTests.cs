using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Stirrer of the Candy arena's waffle variant: it flashes before it first sweeps, hits a car in its way along
    /// the sweep and credits nobody, stops and flashes before each reversal, tells the bots where it is going, and
    /// the other variants have none.
    /// </summary>
    public sealed class StirrerTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator FrostingHasNoStirrer()
        {
            yield return Open("candy", "frosting");
            Assert.That(Container.Resolve<CandyStirrer>().Exists, Is.False);
        }

        [UnityTest]
        public IEnumerator ItFlashesBeforeItSweepsAndStopsAndFlashesBeforeEachReversal()
        {
            yield return Open("candy", "waffle");
            var stirrer = Container.Resolve<CandyStirrer>();
            var settings = Tuning.Stirrer;
            Assert.That(stirrer.Exists, Is.True);

            var warnedBeforeStart = false;
            var firstMoveAt = -1f;
            var firstSign = 0f;
            var stoppedAndFlashed = false;
            var reversed = false;
            var timeout = Time.time + 40f;
            while (stirrer.Clock < settings.Delay + settings.ReverseInterval + settings.EaseTime + 1.2f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
                if (stirrer.Clock < settings.Delay) {
                    warnedBeforeStart |= stirrer.IsWarning && stirrer.Clock > settings.Delay - 1.05f;
                    Assert.That(Mathf.Abs(stirrer.AngularVelocity), Is.EqualTo(0f), "The arm moved before its delay.");
                }
                else if (firstMoveAt < 0f && Mathf.Abs(stirrer.AngularVelocity) > 0.01f) {
                    firstMoveAt = stirrer.Clock;
                    firstSign = Mathf.Sign(stirrer.AngularVelocity);
                }

                stoppedAndFlashed |= stirrer.IsWarning && stirrer.Clock > settings.Delay && Mathf.Abs(stirrer.AngularVelocity) < 0.01f;
                reversed |= firstSign != 0f && Mathf.Sign(stirrer.AngularVelocity) == -firstSign
                    && Mathf.Abs(stirrer.AngularVelocity) > 0.05f;
            }

            Assert.That(warnedBeforeStart, Is.True, "The arm should flash for the second before it sets off.");
            Assert.That(firstMoveAt, Is.GreaterThanOrEqualTo(settings.Delay));
            Assert.That(stoppedAndFlashed, Is.True, "The arm should stand still and flash before reversing.");
            Assert.That(reversed, Is.True, "The arm should come back the other way.");
        }

        [UnityTest]
        public IEnumerator ACarInItsWayIsPushedAlongTheSweepAndNobodyIsCredited()
        {
            yield return Open("candy", "waffle");
            var stirrer = Container.Resolve<CandyStirrer>();
            var settings = Tuning.Stirrer;
            var car = Round.Cars[1];
            var timeout = Time.time + 30f;
            while (stirrer.Clock < settings.Delay + settings.EaseTime + 0.5f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
            }

            // Where the arm will be in a moment, half way along it, in front of its sweep.
            var direction = Mathf.Sign(stirrer.AngularVelocity);
            var ahead = stirrer.Angle + direction * 0.25f;
            var spot = new Vector3(Mathf.Sin(ahead), 0f, Mathf.Cos(ahead)) * 9f;
            Assert.That(Edge.DistanceToEdge(spot), Is.GreaterThan(1f), "The test spot is off the floor.");

            var hitBefore = car.LastHazardTime;
            car.Movement.Place(new Vector3(spot.x, 0.05f, spot.z), Quaternion.identity);
            var until = Time.time + 6f;
            while (Time.time < until && car.LastHazardTime == hitBefore) {
                Hold(car);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(car.LastHazardTime, Is.Not.EqualTo(hitBefore), "The arm never hit the car.");
            Assert.That(car.LastHitBy, Is.Null, "An arm hit must credit no car.");
            Assert.That(stirrer.Hits, Is.GreaterThan(0));
            var tangent = direction * new Vector3(car.Position.z, 0f, -car.Position.x).normalized;
            Assert.That(Vector3.Dot(car.Movement.Velocity, tangent), Is.GreaterThan(1f), "The car should be sent along the sweep.");
        }

        [UnityTest]
        public IEnumerator ABotAheadOfTheArmIsToldToRunAlongAndOneBehindIsLeftAlone()
        {
            yield return Open("candy", "waffle");
            var stirrer = Container.Resolve<CandyStirrer>();
            var settings = Tuning.Stirrer;
            var timeout = Time.time + 30f;
            while (stirrer.Clock < settings.Delay + 1f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                yield return Step();
            }

            var direction = stirrer.Direction;
            var near = stirrer.Angle + direction * 0.3f;
            var inWay = new Vector3(Mathf.Sin(near), 0f, Mathf.Cos(near)) * 9f;
            Assert.That(stirrer.TryAvoid(inWay, 1.2f, Edge, 1f, out var heading), Is.True);
            var tangent = direction * new Vector3(inWay.z, 0f, -inWay.x).normalized;
            Assert.That(Vector3.Dot(heading.normalized, tangent), Is.GreaterThan(0.5f), "The bot should run along the sweep.");
            Assert.That(Edge.DistanceToEdge(inWay + heading.normalized * 3f, 1f), Is.GreaterThanOrEqualTo(0f));

            var behind = stirrer.Angle - direction * 1.5f;
            var clear = new Vector3(Mathf.Sin(behind), 0f, Mathf.Cos(behind)) * 9f;
            Assert.That(stirrer.TryAvoid(clear, 1.2f, Edge, 1f, out _), Is.False);
            Assert.That(stirrer.Version, Is.GreaterThan(0));
        }
    }
}
