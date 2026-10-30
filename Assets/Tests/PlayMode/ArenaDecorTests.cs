using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Loads the real Game scene on each arena and checks that its catalog entry's props are built around the floor, as
    /// pure scenery: no colliders and no shadows.
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
            Assert.That(root.GetComponentsInChildren<Collider>().Length, Is.Zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>()) {
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            }
        }
    }
}
