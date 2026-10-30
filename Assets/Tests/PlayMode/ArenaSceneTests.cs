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
    /// one the floor, camera and settings readers are built for.
    /// </summary>
    public sealed class ArenaSceneTests : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator SceneIsBuiltForTheArenaOfTheRotation([Values("sandbox", "tabletop", "playroom")] string map)
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
            Assert.That(container.Resolve<ArenaFloor>().Radius, Is.EqualTo(expected.Radius));
        }
    }
}
