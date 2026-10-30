using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Candy arena's patches in the real Game scene: they lie clear of the spawn circle, caramel slows a car and
    /// icing makes it slide, and the arenas without the mechanic have no patches at all.
    /// </summary>
    public sealed class CandyPatchesTests : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator SandboxHasNoPatches()
        {
            yield return Load("sandbox");
            Assert.That(Patches().Count, Is.EqualTo(0));
            Assert.That(Surface().Sample(Vector3.zero).Speed, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator PatchesLieClearOfTheSpawnCircleAndReachPastTheSolidDisc()
        {
            yield return Load("candy");
            var patches = Patches();
            var arena = GameScope().Container.Resolve<GameTuning.ArenaSettings>();
            var spawn = arena.Radius * arena.SpawnRadius;
            Assert.That(patches.Count, Is.EqualTo(6));
            Assert.That(Enumerable.Range(0, 6).Count(patches.IsCaramel), Is.EqualTo(3), "Three caramel, three icing.");
            for (var i = 0; i < patches.Count; i++) {
                var distance = patches.CenterOf(i).magnitude;
                var radius = patches.RadiusOf(i);
                // A car is about 2.6 m long, and starts nose to the middle on the circle.
                var clear = distance + radius < spawn - 1.3f || distance - radius > spawn + 1.3f;
                Assert.That(clear, Is.True, $"Patch {i} lies over the spawn circle.");
                Assert.That(distance + radius, Is.GreaterThan(arena.MinRadius), $"Patch {i} lies inside the final disc.");
                Assert.That(distance + radius, Is.LessThanOrEqualTo(arena.Radius), $"Patch {i} lies off the floor.");
            }
        }

        [UnityTest]
        public IEnumerator CaramelSlowsACarAndIcingLetsItSlide()
        {
            yield return Load("candy");
            var patches = Patches();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var car = round.Cars.First(candidate => candidate != round.Player);
            var tuning = container.Resolve<GameTuning>();

            var caramel = Enumerable.Range(0, patches.Count).First(patches.IsCaramel);
            var icing = Enumerable.Range(0, patches.Count).First(index => !patches.IsCaramel(index));
            var plain = PlainSpot(patches, container.Resolve<IArenaSurface>());

            // Top speed: drive east for a moment from the middle of a caramel patch and of plain floor.
            var plainTop = 0f;
            yield return Run(car, At(plain), Vector3.right, 0.7f, speed => plainTop = Mathf.Max(plainTop, speed));
            var caramelTop = 0f;
            // Only while still over the patch: a car that drives off it speeds up again.
            yield return Run(car, At(patches.CenterOf(caramel)), Vector3.right, 0.7f,
                speed => caramelTop = car.Movement.Surface.Speed < 0.99f ? Mathf.Max(caramelTop, speed) : caramelTop);
            Assert.That(caramelTop, Is.LessThan(plainTop * 0.75f), "A car on caramel should top out slower.");
            Assert.That(caramelTop, Is.GreaterThan(1f), "The car should have run on the patch.");

            // Grip: shoved sideways with the throttle off, a car keeps more of its slide on icing.
            var plainSlide = 0f;
            yield return Slide(car, At(plain), value => plainSlide = value);
            var icingSlide = 0f;
            yield return Slide(car, At(patches.CenterOf(icing)), value => icingSlide = value);
            Assert.That(icingSlide, Is.GreaterThan(plainSlide * 2f), "A car on icing should keep sliding.");
            Assert.That(tuning.Candy.IcingGrip, Is.LessThan(1f));
        }

        private IEnumerator Load(string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            GameScope().Container.Resolve<ArenaRound>().Place();
            // The patches are laid out on the first tick after the cars stand.
            yield return null;
            yield return null;
        }

        private CandyPatches Patches() => GameScope().Container.Resolve<CandyPatches>();

        private IArenaSurface Surface() => GameScope().Container.Resolve<IArenaSurface>();

        private static Vector3 At(Vector2 spot) => new(spot.x, 0.05f, spot.y);

        /// <summary>A spot with plain floor for five metres east and two to each side, away from the idle cars.</summary>
        private static Vector2 PlainSpot(CandyPatches patches, IArenaSurface surface)
        {
            for (var x = -6f; x <= 2f; x += 0.5f) {
                for (var z = -6f; z <= 6f; z += 0.5f) {
                    var clear = true;
                    for (var step = -2f; step <= 5f && clear; step += 0.5f) {
                        for (var side = -2f; side <= 2f && clear; side += 2f) {
                            clear = surface.Sample(new Vector3(x + step, 0f, z + side)).Speed == 1f
                                && surface.Sample(new Vector3(x + step, 0f, z + side)).Grip == 1f;
                        }
                    }

                    if (clear && new Vector2(x + 5f, z).magnitude < 7f) {
                        return new Vector2(x, z);
                    }
                }
            }

            Assert.Fail("No plain floor found to compare with.");
            return Vector2.zero;
        }

        private static IEnumerator Run(Car car, Vector3 start, Vector3 heading, float seconds,
            System.Action<float> speed)
        {
            car.Movement.Place(start, Quaternion.LookRotation(heading));
            yield return new WaitForFixedUpdate();
            var end = Time.time + seconds;
            while (Time.time < end) {
                car.Movement.Drive(heading, 1f);
                yield return new WaitForFixedUpdate();
                speed(car.Movement.Velocity.magnitude);
            }

            car.Movement.Drive(Vector3.zero, 0f);
        }

        private static IEnumerator Slide(Car car, Vector3 start, System.Action<float> sideSpeed)
        {
            car.Movement.Place(start, Quaternion.LookRotation(Vector3.right));
            car.Movement.Drive(Vector3.zero, 0f);
            yield return new WaitForFixedUpdate();
            car.Movement.Push(new Vector3(0f, 0f, 6f));
            var end = Time.time + 0.2f;
            while (Time.time < end) {
                yield return new WaitForFixedUpdate();
            }

            sideSpeed(Mathf.Abs(car.Movement.Velocity.z));
        }
    }
}
