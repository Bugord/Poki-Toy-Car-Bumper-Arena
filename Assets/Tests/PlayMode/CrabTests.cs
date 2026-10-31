using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Crab of the sandbox's molds variant on the real Game scene: only that variant has it, its footprints show before
    /// it walks, it shoves a car in its path sideways and credits nobody, and the bots see its path as a hazard.
    /// </summary>
    public sealed class CrabTests : ArenaObjectFixture
    {
        [UnityTest]
        public IEnumerator OtherVariantsHaveNoCrab()
        {
            yield return Load("sandbox", "rake");
            BeginRound();
            yield return Until(() => Round.Elapsed > Tuning.Crab.Delay + 2f, 20f, "The round never ran.");
            var crab = Container.Resolve<ArenaCrab>();
            Assert.That(crab.IsAnnounced, Is.False);
            Assert.That(Hazards.All.Count, Is.EqualTo(0), "A variant without the crab must register no hazard.");
        }

        [UnityTest]
        public IEnumerator TheCrabShowsFootprintsBeforeItWalksAndShovesACarSideways()
        {
            yield return Load("sandbox", "molds");
            var crab = Container.Resolve<ArenaCrab>();
            BeginRound();
            Assert.That(Hazards.All.Count, Is.EqualTo(1));

            yield return Until(() => crab.IsAnnounced, 20f, "The crab never appeared.");
            Assert.That(crab.IsWalking, Is.False, "It must stand announced before it walks.");
            Assert.That(Round.Elapsed, Is.LessThan(Tuning.Crab.Delay), "It appears ahead of its first walk.");
            Assert.That(crab.VisibleFootprints, Is.GreaterThan(3), "The path ahead should show footprints.");
            var version = crab.Version;

            // A car stands on its path, three and a half metres on.
            var car = Round.Cars[1];
            ParkOthers(car);
            var path = crab.Direction;
            var across = new Vector3(-path.z, 0f, path.x);
            var spot = crab.Position + path * 3.5f;
            Stand(car, spot);
            Assert.That(crab.TryAvoid(spot, Tuning.Bots.HazardMargin, Edge, 1f, out var heading), Is.True,
                "A car in the crab's path should be told to leave it.");
            Assert.That(Mathf.Abs(Vector3.Dot(heading, across)), Is.GreaterThan(0.9f), "It leaves across the path.");
            Assert.That(crab.TryAvoid(crab.Position - path * 6f, Tuning.Bots.HazardMargin, Edge, 1f, out _), Is.False,
                "A car the crab has left behind is out of its way.");

            yield return Until(() => crab.IsWalking, 10f, "The crab never set out.");
            Assert.That(crab.Version, Is.EqualTo(version), "The threat was announced when the crab appeared, not as it set out.");
            Assert.That(Round.Elapsed, Is.GreaterThanOrEqualTo(Tuning.Crab.Delay - 0.1f));

            yield return Until(() => !float.IsNegativeInfinity(car.LastHazardTime), 10f, "The crab never touched the car.");
            Assert.That(car.LastHitBy, Is.Null, "A crab shove is nobody's hit.");
            var settled = Time.time + 0.4f;
            yield return Until(() => Time.time > settled, 2f, "Time stood still.");
            var offset = Mathf.Abs(Vector3.Dot(Flat(car.Position) - spot, across));
            Assert.That(offset, Is.GreaterThan(0.8f), "The car should have been shoved out of the crab's path.");
        }

        [UnityTest]
        public IEnumerator TheCrabKeepsOnTheFloorAndWalksOnToNewChords()
        {
            yield return Load("sandbox", "molds");
            var crab = Container.Resolve<ArenaCrab>();
            BeginRound();
            var legs = 0;
            var version = 0;
            var end = Time.time + 40f;
            while (Time.time < end && Round.Elapsed < Tuning.Crab.Delay + 36f) {
                ParkOthers(null);
                if (crab.Version != version) {
                    version = crab.Version;
                    legs++;
                }

                if (crab.IsAnnounced) {
                    Assert.That(Edge.IsSolid(crab.Position), Is.True, "The crab walked off the floor.");
                }

                yield return null;
            }

            Assert.That(legs, Is.GreaterThanOrEqualTo(3), "The crab should have set out on several chords.");
            Assert.That(crab.HasFallen, Is.False);
        }
    }
}
