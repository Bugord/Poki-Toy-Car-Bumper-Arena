using System.Collections;
using System.Collections.Generic;
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

        /// <summary>Every variant of every arena in the game's own tuning, as (arena, variant).</summary>
        public static IEnumerable<object[]> AllVariants()
        {
#if UNITY_EDITOR
            var tuning = UnityEditor.AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            foreach (var map in tuning.Maps) {
                foreach (var variant in map.Variants) {
                    yield return new object[] { map.Id, variant.Id };
                }
            }
#else
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator SceneBuildsForEveryVariantAndOnlyItsOwnMechanicRuns(
            [ValueSource(nameof(AllVariants))] object[] pair)
        {
            var map = (string)pair[0];
            var variant = (string)pair[1];
            CreateParent(new SimulationInput(), new SimulationProgress(), map, variant);
            yield return LoadGame();
            var container = GameScope().Container;
            var arenaMap = container.Resolve<ArenaMap>();
            var tuning = container.Resolve<GameTuning>();
            var expected = GameTuning.ArenaMechanic.None;
            foreach (var entry in tuning.Maps) {
                if (entry.Id == map) {
                    expected = entry.FindVariant(variant).Mechanic;
                }
            }

            Assert.That(arenaMap.Id, Is.EqualTo(map));
            Assert.That(arenaMap.Variant, Is.EqualTo(variant));
            Assert.That(arenaMap.Mechanic, Is.EqualTo(expected));
            Assert.That(arenaMap.FloorMaterial, Is.Not.Null, $"{map}/{variant} has no floor material.");

            // The mechanics are laid out on the first tick after the cars stand.
            container.Resolve<ArenaRound>().Place();
            yield return null;
            yield return null;

            var ball = Object.FindFirstObjectByType<ArenaBall>(FindObjectsInactive.Include);
            Assert.That(ball != null && ball.gameObject.activeSelf, Is.EqualTo(expected == GameTuning.ArenaMechanic.Ball),
                $"Ball on {map}/{variant}.");
            Assert.That(container.Resolve<CarouselDisc>().Exists, Is.EqualTo(expected == GameTuning.ArenaMechanic.Carousel),
                $"Carousel on {map}/{variant}.");
            Assert.That(container.Resolve<CandyPatches>().Count > 0, Is.EqualTo(expected == GameTuning.ArenaMechanic.Candy),
                $"Candy patches on {map}/{variant}.");
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
