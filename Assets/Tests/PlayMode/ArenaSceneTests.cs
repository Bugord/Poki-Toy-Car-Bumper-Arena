using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Loads the real Game scene under a parent whose rotation is fixed, and checks that the arena of the rotation is the
    /// one the floor, camera and settings readers are built for. The rings of the round floor are checked here; the tiled
    /// floors of the other arenas are in <see cref="ShapedArenaTests"/>.
    /// </summary>
    public sealed class ArenaSceneTests : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator SceneIsBuiltForTheArenaOfTheRotation([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            var container = GameScope().Container;
            var tuning = container.Resolve<GameTuning>();
            var expected = tuning.Maps[0].Arena;
            foreach (var entry in tuning.Maps) {
                if (entry.Id == map) {
                    expected = entry.Arena;
                }
            }

            Assert.That(container.Resolve<ArenaMap>().Id, Is.EqualTo(map));
            Assert.That(container.Resolve<GameTuning.ArenaSettings>(), Is.SameAs(expected));
            var arenaMap = container.Resolve<ArenaMap>();
            var extent = arenaMap.Shape == null ? expected.Radius : arenaMap.Shape.FullExtent;
            Assert.That(container.Resolve<ArenaFloor>().SafeExtent, Is.EqualTo(extent).Within(0.01f));
            Assert.That(arenaMap.Shape == null, Is.EqualTo(expected.Shape == GameTuning.ArenaShapeKind.Circle));
        }

        [UnityTest]
        public IEnumerator FloorMovesTheEdgeAsWavesShakeAndFall([Values("sandbox")] string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            var container = GameScope().Container;
            var floor = container.Resolve<ArenaFloor>();
            var edge = container.Resolve<IArenaEdge>();
            Assert.That(edge, Is.SameAs(floor));

            var full = floor.FullExtent;
            Assert.That(floor.WaveCount, Is.GreaterThan(1));
            Assert.That(floor.SafeExtent, Is.EqualTo(full));
            Assert.That(floor.SolidExtent, Is.EqualTo(full));

            var changes = 0;
            floor.Changed += () => changes++;
            var outer = floor.Wave(0);
            Assert.That(outer.Outer, Is.EqualTo(full));
            var onOuterRing = new Vector3((outer.Inner + outer.Outer) * 0.5f, 0.4f, 0f);
            Assert.That(floor.Contains(0, onOuterRing), Is.True);
            Assert.That(floor.Contains(1, onOuterRing), Is.False);

            floor.Warn(0);
            Assert.That(floor.SafeExtent, Is.EqualTo(outer.Inner), "a shaking wave is no longer safe floor");
            Assert.That(floor.SolidExtent, Is.EqualTo(full), "but it is still solid");
            Assert.That(changes, Is.EqualTo(1));

            floor.Drop(0);
            Assert.That(floor.SolidExtent, Is.EqualTo(outer.Inner), "a fallen wave is gone");
            Assert.That(floor.IsSolid(onOuterRing), Is.False);
            Assert.That(changes, Is.EqualTo(2));
        }
    }
}
