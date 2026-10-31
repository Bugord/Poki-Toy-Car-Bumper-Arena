using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    /// Shared set-up of the tests of the interactive arena objects (jelly pads, the stirrer, the train, the jack-in-the-
    /// boxes): the real Game scene on one arena and variant, the bots held still, the cars placed.
    /// </summary>
    public abstract class ObjectsBFixture : SimulationSceneFixture
    {
        protected ArenaRound Round;
        protected IArenaEdge Edge;
        protected GameTuning Tuning;
        protected IObjectResolver Container;
        private readonly Dictionary<Car, Vector3> parked = new();

        /// <summary>Loads the arena, places the cars and, with <paramref name="begin"/>, starts the round's clock.</summary>
        protected IEnumerator Open(string map, string variant, bool begin = true, int round = 3)
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = round }, map, variant);
            yield return LoadGame();
            Container = GameScope().Container;
            Round = Container.Resolve<ArenaRound>();
            Edge = Container.Resolve<IArenaEdge>();
            Tuning = Container.Resolve<GameTuning>();
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = 7;
            // The bots stand still, so only the object moves the cars.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);

            Round.Place();
            if (begin) {
                Round.Begin();
            }

            parked.Clear();
            foreach (var car in Round.Cars) {
                parked[car] = car.Position;
            }

            // The object is laid out on the first tick after the cars stand.
            yield return null;
            yield return null;
        }

        /// <summary>Keeps every car but those named at its starting place, at rest, for one physics step.</summary>
        protected void Hold(params Car[] free)
        {
            foreach (var car in Round.Cars) {
                if (free.Contains(car)) {
                    continue;
                }

                car.Movement.Place(parked[car], car.transform.rotation);
                car.Movement.Drive(Vector3.zero, 0f);
            }
        }

        /// <summary>One physics step with every car but those named held still.</summary>
        protected IEnumerator Step(params Car[] free)
        {
            Hold(free);
            yield return new WaitForFixedUpdate();
        }

        protected IEnumerator Wait(float seconds, params Car[] free)
        {
            var until = Time.time + seconds;
            while (Time.time < until) {
                Hold(free);
                yield return new WaitForFixedUpdate();
            }
        }

        /// <summary>A heading and a start three and a half metres short of the pad such that the car's run and its landing are on the floor.</summary>
        protected bool FindApproach(Vector3 centre, out Vector3 toward, out Vector3 start)
        {
            for (var i = 0; i < 12; i++) {
                var angle = i * 30f * Mathf.Deg2Rad;
                toward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                start = centre - toward * 3.5f;
                if (Edge.DistanceToEdge(start) > 1.5f && Edge.DistanceToEdge(centre + toward * 8f) > 1.5f) {
                    return true;
                }
            }

            toward = Vector3.forward;
            start = centre;
            return false;
        }

        protected static Vector3 Flat(Vector3 point) => new(point.x, 0f, point.z);
    }
}
