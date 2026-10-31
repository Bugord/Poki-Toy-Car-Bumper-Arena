using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Jack-in-the-boxes of the Playroom arena's blocks variant: the spots lie whole and clear of the starts, a spot
    /// rattles before the box springs up, the box tosses the cars inside its circle and nobody outside, a bot is told to
    /// leave the circle, nothing opens in the first seconds, and the other variants have none.
    /// </summary>
    public sealed class JackTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator PuzzleHasNoJackBoxes()
        {
            yield return Open("playroom", "puzzle");
            Assert.That(Container.Resolve<JackBoxes>().Exists, Is.False);
        }

        [UnityTest]
        public IEnumerator SpotsLieWholeAndClearOfTheStarts()
        {
            yield return Open("playroom", "blocks");
            var jack = Container.Resolve<JackBoxes>();
            Assert.That(jack.Exists, Is.True);
            Assert.That(jack.SpotCount, Is.EqualTo(Tuning.Jack.SpotCount));
            for (var i = 0; i < jack.SpotCount; i++) {
                var centre = jack.CentreOf(i);
                Assert.That(Edge.DistanceToEdge(centre), Is.GreaterThanOrEqualTo(1.5f), $"Spot {i} is not whole on the floor.");
                foreach (var car in Round.Cars) {
                    Assert.That((Flat(car.Position) - centre).magnitude, Is.GreaterThan(Tuning.Jack.HitRadius + 0.8f),
                        $"Spot {i} is within reach of a car's start.");
                }
            }
        }

        [UnityTest]
        public IEnumerator NothingOpensBeforeTheDelayAndASpotRattlesBeforeTheBoxSpringsUp()
        {
            yield return Open("playroom", "blocks");
            var jack = Container.Resolve<JackBoxes>();
            var settings = Tuning.Jack;
            var warnedAt = -1f;
            var poppedAt = -1f;
            var timeout = Time.time + 40f;
            while (poppedAt < 0f) {
                Assert.That(Time.time, Is.LessThan(timeout), "No box ever sprang up.");
                yield return Step();
                for (var i = 0; i < jack.SpotCount; i++) {
                    var state = jack.StateOf(i);
                    if (warnedAt < 0f && state == JackBoxes.SpotState.Warning) {
                        warnedAt = jack.Clock;
                    }

                    if (poppedAt < 0f && state == JackBoxes.SpotState.Open) {
                        poppedAt = jack.Clock;
                    }
                }
            }

            Assert.That(warnedAt, Is.GreaterThanOrEqualTo(settings.Delay - 0.05f), "A spot was announced before the delay.");
            Assert.That(poppedAt - warnedAt, Is.GreaterThanOrEqualTo(settings.Warning - 0.1f), "The box sprang up unannounced.");
            Assert.That(poppedAt, Is.GreaterThanOrEqualTo(5f), "A box sprang up in the first five seconds.");
        }

        [UnityTest]
        public IEnumerator TheBoxTossesACarInsideItsCircleAndLeavesOneOutside()
        {
            yield return Open("playroom", "blocks", begin: true);
            var jack = Container.Resolve<JackBoxes>();
            var settings = Tuning.Jack;
            // Stops the schedule from announcing others for the length of the test.
            var inside = Round.Cars[1];
            var outside = Round.Cars[2];
            var centre = jack.CentreOf(0);
            var near = centre + new Vector3(1f, 0f, 0f);
            var beyond = centre + new Vector3(-(settings.HitRadius + 1f), 0f, 0f);
            inside.Movement.Place(new Vector3(near.x, 0.05f, near.z), Quaternion.identity);
            outside.Movement.Place(new Vector3(beyond.x, 0.05f, beyond.z), Quaternion.identity);
            jack.Announce(0);

            var insideBefore = inside.LastHazardTime;
            var outsideBefore = outside.LastHazardTime;
            var peak = 0f;
            var until = Time.time + settings.Warning + 1.5f;
            while (Time.time < until) {
                Hold(inside, outside);
                if (jack.StateOf(0) == JackBoxes.SpotState.Warning) {
                    inside.Movement.Place(new Vector3(near.x, 0.05f, near.z), Quaternion.identity);
                    outside.Movement.Place(new Vector3(beyond.x, 0.05f, beyond.z), Quaternion.identity);
                }

                yield return new WaitForFixedUpdate();
                peak = Mathf.Max(peak, inside.Position.y);
            }

            Assert.That(inside.LastHazardTime, Is.Not.EqualTo(insideBefore), "The box did not toss the car beside it.");
            Assert.That(inside.LastHitBy, Is.Null, "A box toss must credit no car.");
            Assert.That(peak, Is.GreaterThan(0.4f), "The car was not thrown up.");
            Assert.That(outside.LastHazardTime, Is.EqualTo(outsideBefore), "The box tossed a car outside its circle.");
            Assert.That(jack.Tosses, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ABotInsideAnAnnouncedCircleIsToldToLeaveItAndOneOutsideIsNot()
        {
            yield return Open("playroom", "blocks");
            var jack = Container.Resolve<JackBoxes>();
            var settings = Tuning.Jack;
            var centre = jack.CentreOf(1);
            var before = jack.Version;
            var inside = centre + new Vector3(0.8f, 0f, 0f);
            Assert.That(jack.TryAvoid(inside, 1.2f, Edge, 1f, out _), Is.False, "A closed spot is no threat.");

            jack.Announce(1);
            Assert.That(jack.Version, Is.GreaterThan(before));
            Assert.That(jack.TryAvoid(inside, 1.2f, Edge, 1f, out var heading), Is.True);
            Assert.That(Vector3.Dot(heading.normalized, (inside - centre).normalized), Is.GreaterThan(0f), "The bot should go away from the box.");
            Assert.That(Edge.DistanceToEdge(inside + heading.normalized * 3f, 1f), Is.GreaterThanOrEqualTo(0f));

            var far = centre + new Vector3(settings.HitRadius + 3f, 0f, 0f);
            Assert.That(jack.TryAvoid(far, 1.2f, Edge, 1f, out _), Is.False);
        }
    }
}
