using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Top of the tabletop's cloth variant on the real Game scene: only that variant has it, it stands on the table from
    /// the start without hurting anybody, wanders after its delay, stays on the floor, and flings a car in its way with
    /// nobody credited.
    /// </summary>
    public sealed class TopTests : ArenaObjectFixture
    {
        [UnityTest]
        public IEnumerator OtherVariantsHaveNoTop()
        {
            yield return Load("tabletop", "board");
            BeginRound();
            var top = Container.Resolve<ArenaTop>();
            yield return Until(() => Round.Elapsed > Tuning.Top.Delay + 2f, 20f, "The round never ran.");
            Assert.That(top.IsWandering, Is.False);
            Assert.That(Hazards.All.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheTopStandsHarmlesslyThenWandersAndFlingsACar()
        {
            yield return Load("tabletop", "cloth");
            var top = Container.Resolve<ArenaTop>();
            // It is on the table already, before the round begins.
            yield return null;
            yield return null;
            Assert.That(Hazards.All.Count, Is.EqualTo(1));
            Assert.That(top.IsWandering, Is.False);
            var home = top.Position;
            Assert.That(Flat(home).magnitude, Is.LessThan(1f), "It starts on the middle of the table.");
            Assert.That(top.TryAvoid(home, Tuning.Bots.HazardMargin, Edge, 1f, out _), Is.False, "Standing still it threatens nothing.");

            // A car right under it is not hurt while it stands.
            var car = Round.Cars[1];
            ParkOthers(car);
            Stand(car, home + Vector3.forward * 1.0f);
            BeginRound();
            var hurtBefore = false;
            while (Round.Elapsed < Tuning.Top.Delay - 0.2f) {
                Stand(car, home + Vector3.forward * 1.0f);
                hurtBefore |= !float.IsNegativeInfinity(car.LastHazardTime);
                yield return null;
            }

            Assert.That(hurtBefore, Is.False, "The top must not hit anybody before it starts to wander.");

            yield return Until(() => top.IsWandering, 5f, "The top never started to wander.");
            var version = top.Version;
            // The car that stood at its feet is flung the moment it starts to wander.
            var before = top.Position;
            yield return Until(() => !float.IsNegativeInfinity(car.LastHazardTime), 6f, "The top never flung the car.");
            Assert.That(car.LastHitBy, Is.Null, "A top fling is nobody's hit.");
            var speed = Flat(car.Movement.Velocity).magnitude;
            Assert.That(speed, Is.GreaterThan(3f), $"The car should be flung away: v={car.Movement.Velocity} car={car.Position} top={top.Position} dt={Time.time - car.LastHazardTime}");
            Assert.That(Flat(top.Position - before).magnitude, Is.GreaterThanOrEqualTo(0f));
            Assert.That(top.Version, Is.GreaterThanOrEqualTo(version));
        }

        [UnityTest]
        public IEnumerator TheTopWandersFarAndStaysOnTheFloor()
        {
            yield return Load("tabletop", "cloth");
            var top = Container.Resolve<ArenaTop>();
            BeginRound();
            yield return Until(() => top.IsWandering, 10f, "The top never started to wander.");
            var start = top.Position;
            var farthest = 0f;
            var end = Time.time + 12f;
            while (Time.time < end) {
                ParkOthers(null);
                Assert.That(Edge.IsSolid(top.Position), Is.True, "The top left the floor.");
                farthest = Mathf.Max(farthest, Flat(top.Position - start).magnitude);
                yield return null;
            }

            Assert.That(farthest, Is.GreaterThan(5f), "The top should roam.");
            Assert.That(top.IsWandering, Is.True);
        }
    }
}
