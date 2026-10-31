using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Jelly pads of the Candy arena's chocolate variant: they lie whole and clear of the starts, they toss a car
    /// that drives onto one and set it down on the floor, and the other variants have none.
    /// </summary>
    public sealed class JellyTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator PadsLieWholeAndClearOfTheStarts()
        {
            yield return Open("candy", "chocolate");
            var jelly = Container.Resolve<JellyPads>();
            Assert.That(jelly.Exists, Is.True);
            Assert.That(jelly.Count, Is.EqualTo(Tuning.Jelly.PadCount));
            for (var i = 0; i < jelly.Count; i++) {
                var centre = jelly.CentreOf(i);
                Assert.That(Edge.DistanceToEdge(centre), Is.GreaterThanOrEqualTo(Tuning.Jelly.PadRadius + 0.5f),
                    $"Pad {i} is not whole on the floor.");
                foreach (var car in Round.Cars) {
                    Assert.That((Flat(car.Position) - centre).magnitude, Is.GreaterThan(Tuning.Jelly.SpawnClearance - 0.01f),
                        $"Pad {i} lies on a car's start.");
                }
            }
        }

        [UnityTest]
        public IEnumerator FrostingHasNoPads()
        {
            yield return Open("candy", "frosting");
            Assert.That(Container.Resolve<JellyPads>().Exists, Is.False);
        }

        [UnityTest]
        public IEnumerator ACarDrivingOntoAPadIsTossedAndLandsOnTheFloor()
        {
            yield return Open("candy", "chocolate");
            var jelly = Container.Resolve<JellyPads>();
            var car = Round.Cars[1];
            var centre = jelly.CentreOf(0);
            // Heading for the pad from the middle's side, three metres out, so the throw lands in the ring.
            Assert.That(FindApproach(centre, out var toward, out var start), Is.True, "No clear run at the pad.");
            car.Movement.Place(new Vector3(start.x, 0.05f, start.z), Quaternion.LookRotation(toward));

            var tossedAt = Vector3.zero;
            var peak = 0f;
            var tossed = false;
            var timeout = Time.time + 8f;
            while (Time.time < timeout && !car.IsOut) {
                Hold(car);
                car.Movement.Drive(toward, 1f);
                yield return new WaitForFixedUpdate();
                if (!tossed && jelly.Tosses > 0) {
                    tossed = true;
                    tossedAt = Flat(car.Position);
                }

                if (tossed) {
                    peak = Mathf.Max(peak, car.Position.y);
                    if (car.Position.y < 0.2f && car.Movement.IsGrounded && Time.time > timeout - 7f && peak > 0.5f) {
                        break;
                    }
                }
            }

            Assert.That(tossed, Is.True, "The pad never tossed the car.");
            Assert.That(peak, Is.GreaterThan(0.6f), "The car was not thrown up.");
            Assert.That(car.IsOut, Is.False, "The toss threw the car off the floor.");
            Assert.That(Edge.IsSolid(car.Position), Is.True, "The car landed over a gap.");
            Assert.That((Flat(car.Position) - tossedAt).magnitude, Is.GreaterThan(3f), "The toss did not carry it forward.");
            Assert.That(float.IsNegativeInfinity(car.LastHazardTime), Is.True, "A jelly pad is no hazard.");
        }

        [UnityTest]
        public IEnumerator ATossBesideTheEdgeIsShortenedToKeepTheCarOnTheFloor()
        {
            yield return Open("candy", "chocolate");
            var jelly = Container.Resolve<JellyPads>();
            var car = Round.Cars[1];
            var centre = jelly.CentreOf(0);
            // Aimed from the pad straight at the nearest edge: the throw must be cut short to land on the floor.
            var outward = -Edge.Inward(centre);
            outward.y = 0f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : centre.normalized;
            car.Movement.Place(new Vector3(centre.x, 0.05f, centre.z), Quaternion.LookRotation(outward));
            for (var i = 0; i < 4; i++) {
                Hold(car);
                yield return new WaitForFixedUpdate();
            }

            // Whatever the pad did while the car settled, one clean throw from a moving start.
            var timeout = Time.time + 10f;
            var before = jelly.Tosses;
            while (Time.time < timeout && jelly.Tosses == before && !car.IsOut) {
                Hold(car);
                car.Movement.Place(new Vector3(centre.x - outward.x * 2f, 0.05f, centre.z - outward.z * 2f),
                    Quaternion.LookRotation(outward));
                car.Movement.Push(outward * 8f);
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }

            yield return Wait(2.5f, car);
            Assert.That(car.IsOut, Is.False, "The pad threw the car off the floor.");
        }
    }
}
