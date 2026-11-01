using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// What the tests of the arena objects (crab, mounds, top, pencil) share: the real Game scene on one variant of an arena
    /// with the bots standing still and the hazards' seed fixed, so only the object moves the cars.
    /// </summary>
    public abstract class ArenaObjectFixture : SimulationSceneFixture
    {
        private BotDriver botDriver;

        protected ArenaRound Round { get; private set; }
        protected ArenaHazards Hazards { get; private set; }
        protected GameTuning Tuning { get; private set; }
        protected IArenaEdge Edge { get; private set; }
        protected IObjectResolver Container { get; private set; }

        /// <summary>
        /// Loads the scene on <paramref name="map"/> and <paramref name="variant"/> and places the cars; the round of the
        /// visit (<paramref name="visitRound"/>) sets the bots' number.
        /// </summary>
        protected IEnumerator Load(string map, string variant, int seed = 7, int visitRound = 3)
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = visitRound }, map, variant);
            yield return LoadGame();
            Container = GameScope().Container;
            Round = Container.Resolve<ArenaRound>();
            Hazards = Container.Resolve<ArenaHazards>();
            Tuning = Container.Resolve<GameTuning>();
            Edge = Container.Resolve<IArenaEdge>();
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = seed;
            Round.Place();
            // The objects are built on the first tick after the cars stand.
            yield return null;
            yield return null;
            this.botDriver = botDriver;
        }

        /// <summary>
        /// Begins the round with the bots standing still: the driver never creates them once it believes it already drives.
        /// Done here, right before <see cref="ArenaRound.Begin"/>, because a tick of the driver before the round runs takes
        /// the belief back.
        /// </summary>
        protected void BeginRound()
        {
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            Round.Begin();
        }

        /// <summary>Waits, in game time, until <paramref name="condition"/> holds; fails after <paramref name="seconds"/>.</summary>
        protected static IEnumerator Until(Func<bool> condition, float seconds, string failure)
        {
            var end = Time.time + seconds;
            while (!condition()) {
                Assert.That(Time.time, Is.LessThan(end), failure);
                yield return null;
            }
        }

        /// <summary>Parks every car but <paramref name="except"/> far out on a ring, at rest.</summary>
        protected void ParkOthers(Car except)
        {
            var index = 0;
            foreach (var car in Round.Cars) {
                if (car == except) {
                    continue;
                }

                var angle = (200f + index++ * 25f) * Mathf.Deg2Rad;
                var spot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 6f + Vector3.up * 0.05f;
                car.Movement.Place(spot, Quaternion.LookRotation(-spot));
                car.Movement.Drive(Vector3.zero, 0f);
            }
        }

        /// <summary>Stands <paramref name="car"/> at rest on the floor under <paramref name="spot"/>.</summary>
        protected static void Stand(Car car, Vector3 spot)
        {
            car.Movement.Place(new Vector3(spot.x, 0.05f, spot.z), Quaternion.identity);
            car.Movement.Drive(Vector3.zero, 0f);
        }

        protected static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
