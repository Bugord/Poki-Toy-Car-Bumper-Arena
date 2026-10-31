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
    /// Loads the real Game scene on each arena and checks that its catalog entry's world is built as pure scenery: the
    /// ground far below, the pedestal narrower than the floor, and the props standing on the ground, with no colliders, no
    /// shadows and nothing on the arena's layer; and that the fog and the removal depth of a falling car fit the world.
    /// </summary>
    public sealed class ArenaDecorTests : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator PropsOfTheArenaAreBuiltAsScenery([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            var scope = GameScope();
            var expected = scope.Container.Resolve<ArenaMap>().Theme.Decor.Count;

            var root = scope.transform.Find("Decor");
            Assert.That(root, Is.Not.Null, $"{map} has no Decor root");
            Assert.That(root.childCount, Is.EqualTo(expected));
            Assert.That(expected, Is.GreaterThan(0));
            AssertScenery(root);
        }

        [UnityTest]
        public IEnumerator WorldBelowTheArenaIsBuiltAsScenery([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            var scope = GameScope();
            var arenaMap = scope.Container.Resolve<ArenaMap>();
            var theme = arenaMap.Theme;

            var root = scope.transform.Find("World");
            Assert.That(root, Is.Not.Null, $"{map} has no World root");
            Assert.That(root.Find("Ground"), Is.Not.Null, $"{map} has no ground");
            Assert.That(root.Find("Ground").position.y, Is.LessThan(-theme.GroundDepth + 0.01f));
            Assert.That(theme.Pedestal.Count, Is.GreaterThan(0));
            Assert.That(root.childCount, Is.EqualTo(1 + theme.Pedestal.Count + theme.GroundPieces.Count));
            AssertScenery(root);

            // The pedestal stays under the floor, and what meets the floor is narrower than it, so the rim and the drop show.
            foreach (var piece in theme.Pedestal) {
                var top = piece.BaseHeight + (piece.Shape == ArenaCatalog.PieceShape.Model ? 0f : piece.Size.y);
                Assert.That(top, Is.LessThan(0f), $"{map}: a pedestal piece pokes through the floor");
                if (top > -1f) {
                    var half = piece.Shape == ArenaCatalog.PieceShape.Box
                        ? Mathf.Sqrt(piece.Size.x * piece.Size.x + piece.Size.z * piece.Size.z) * 0.5f
                        : piece.Size.x * 0.5f;
                    Assert.That(half, Is.LessThan(arenaMap.Settings.Radius), $"{map}: the top of the pedestal is as wide as the floor");
                }
            }

            // A car that falls out of the round is hidden by the ground or the fog long before it is put away.
            var tuning = scope.Container.Resolve<GameTuning>();
            Assert.That(-tuning.Round.RemoveHeight, Is.GreaterThan(theme.GroundDepth));
            Assert.That(theme.FogEnd, Is.GreaterThan(theme.FogStart));

            yield return null;
            yield return null;
            Assert.That(RenderSettings.fog, Is.True);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.Linear));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(theme.Background));
            Assert.That(RenderSettings.fogEndDistance, Is.GreaterThan(RenderSettings.fogStartDistance));
        }

        private static void AssertScenery(Transform root)
        {
            Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
            var arenaLayer = LayerMask.NameToLayer("Arena");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) {
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(renderer.gameObject.layer, Is.Not.EqualTo(arenaLayer));
            }
        }
    }
}
