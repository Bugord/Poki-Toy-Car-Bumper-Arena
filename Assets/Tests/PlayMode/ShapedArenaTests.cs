using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The tiled floors in the real Game scene: the one collider has the pits and the gaps of fallen tiles in it, a car
    /// over a tile that falls drops with it and one on the lip of a pit stays up, and the cars start on the floor.
    /// </summary>
    public sealed class ShapedArenaTests : SimulationSceneFixture
    {
        private IEnumerator Load(string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            GameScope().Container.Resolve<ArenaRound>().Place();
            yield return null;
        }

        private static bool HitsFloor(Vector3 above, out RaycastHit hit)
        {
            var mask = 1 << LayerMask.NameToLayer("Arena");
            return Physics.Raycast(above + Vector3.up * 3f, Vector3.down, out hit, 8f, mask);
        }

        [UnityTest]
        public IEnumerator SandboxKeepsItsRoundFloor()
        {
            yield return Load("sandbox");
            var floor = GameScope().Container.Resolve<ArenaFloor>();
            Assert.That(GameScope().Container.Resolve<ArenaMap>().Shape, Is.Null);
            Assert.That(floor.TryGetSpawn(0, 4, out _), Is.False);
            Assert.That(floor.Outline(true), Is.Null);
            Assert.That(floor.Wave(0).Spots, Is.Null, "a ring has no spots, only a band");
        }

        [UnityTest]
        public IEnumerator ThePitsOfThePlayroomAreHolesInTheCollider()
        {
            yield return Load("playroom");
            var floor = GameScope().Container.Resolve<ArenaFloor>();
            Assert.That(HitsFloor(new Vector3(10f, 0f, 0f), out _), Is.False, "the right pit");
            Assert.That(HitsFloor(new Vector3(-10f, 0f, 2f), out _), Is.False, "the left pit");
            Assert.That(HitsFloor(new Vector3(0f, 0f, -12f), out var hit), Is.True);
            Assert.That(hit.collider.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Arena")));
            Assert.That(hit.point.y, Is.EqualTo(0f).Within(0.01f), "the driving surface is at height 0");
            Assert.That(HitsFloor(new Vector3(10f, 0f, 6f), out _), Is.True, "beyond the end of the pit");
            Assert.That(floor.WaveCount, Is.GreaterThan(5));
        }

        [UnityTest]
        public IEnumerator ADonutHasAHoleInTheColliderAndAFallenTileLeavesAGap()
        {
            yield return Load("candy");
            var floor = GameScope().Container.Resolve<ArenaFloor>();
            Assert.That(HitsFloor(Vector3.zero, out _), Is.False, "the hole");
            Assert.That(HitsFloor(new Vector3(6f, 0f, 0f), out _), Is.True);
            var spot = floor.Wave(0).Spots[0];
            var over = new Vector3(spot.x, 0f, spot.y);
            Assert.That(HitsFloor(over, out _), Is.True);
            floor.Warn(0);
            Assert.That(HitsFloor(over, out _), Is.True, "a shaking tile still holds a car up");
            floor.Drop(0);
            Assert.That(HitsFloor(over, out _), Is.False, "a fallen tile is gone");
            Assert.That(HitsFloor(new Vector3(6f, 0f, 0f), out _), Is.True, "the core stays");
        }

        [UnityTest]
        public IEnumerator ACarOverATileThatFallsDrops()
        {
            yield return Load("tabletop");
            var container = GameScope().Container;
            var floor = container.Resolve<ArenaFloor>();
            var round = container.Resolve<ArenaRound>();
            var car = round.Cars.First(candidate => candidate != round.Player);
            // On the left strip, wave 1.
            var spot = floor.Wave(1).Spots[0];
            var start = new Vector3(spot.x, 0.3f, spot.y);
            Assert.That(floor.Contains(1, start), Is.True);
            car.Movement.Place(start, Quaternion.identity);
            yield return new WaitForSeconds(0.5f);
            Assert.That(car.Position.y, Is.GreaterThan(-0.2f), "it stands on the strip");

            floor.Warn(1);
            floor.Drop(1);
            yield return new WaitForSeconds(1.2f);
            Assert.That(car.Position.y, Is.LessThan(-3f), "the car falls with the strip");
        }

        [UnityTest]
        public IEnumerator ACarOnTheLipOfAPitStaysUp()
        {
            yield return Load("playroom");
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var car = round.Cars.First(candidate => candidate != round.Player);
            // Its middle 0.4 m inside the pit's near edge, its nose over the pit.
            car.Movement.Place(new Vector3(8.1f, 0.3f, 0f), Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(1.5f);
            Assert.That(car.Position.y, Is.GreaterThan(-0.2f), "the lip holds it");
            Assert.That(car.Position.x, Is.LessThan(9f));

            // Past the lip it drops into the pit.
            car.Movement.Place(new Vector3(10f, 0.3f, 0f), Quaternion.LookRotation(Vector3.right));
            yield return new WaitForSeconds(1.2f);
            Assert.That(car.Position.y, Is.LessThan(-3f), "over the pit it falls");
        }

        [UnityTest]
        public IEnumerator TheCarsStartOnTheFloor([Values("tabletop", "candy", "playroom")] string map)
        {
            yield return Load(map);
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var floor = container.Resolve<ArenaFloor>();
            Assert.That(round.Cars.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var car in round.Cars) {
                var position = car.Position;
                position.y = 0f;
                Assert.That(floor.DistanceToEdge(position), Is.GreaterThanOrEqualTo(2f), $"{map}: {car.name} starts at the edge");
            }

            yield return new WaitForSeconds(0.6f);
            foreach (var car in round.Cars) {
                Assert.That(car.Position.y, Is.GreaterThan(-0.2f), $"{map}: {car.name} fell at the start");
            }
        }

        [UnityTest]
        public IEnumerator TheTurntableIsClearOfThePitsAtTheStart()
        {
            yield return Load("playroom");
            var container = GameScope().Container;
            var floor = container.Resolve<ArenaFloor>();
            var tuning = container.Resolve<GameTuning>();
            Assert.That(floor.DistanceToEdge(Vector3.zero), Is.GreaterThan(tuning.Carousel.Radius + 0.5f));
            yield return null;
            yield return null;
            var disc = container.Resolve<CarouselDisc>();
            Assert.That(disc.Radius, Is.EqualTo(tuning.Carousel.Radius).Within(0.01f), "the disc starts whole");
        }
    }
}
