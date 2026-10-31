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
    /// The Carousel arena's turntable in the real Game scene: a car idling on it is carried round the way it turns, a
    /// car beyond its rim stays put, it reverses on schedule, and the arenas without the mechanic have no turntable.
    /// </summary>
    public sealed class CarouselTests : SimulationSceneFixture
    {
        [UnityTest]
        public IEnumerator SandboxHasNoTurntable()
        {
            yield return Load("sandbox");
            var disc = Disc();
            Assert.That(disc.Exists, Is.False);
            Assert.That(Platform().Sample(new Vector3(3f, 0f, 0f)).IsMoving, Is.False);
        }

        [UnityTest]
        public IEnumerator ACarOnTheDiscIsCarriedAndACarBeyondItIsNot()
        {
            yield return Load("playroom");
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var tuning = container.Resolve<GameTuning>();
            var disc = Disc();
            Assert.That(disc.Exists, Is.True);
            round.Begin();

            // The player's car is the one under test: with no input it idles, where a bot would drive off.
            var car = round.Player;
            var parked = round.Cars.Where(candidate => candidate != car).ToList();
            var onSpot = new Vector3(5f, 0.05f, 0f);
            var beyondSpot = new Vector3(0f, 0.05f, -11f);

            // Wait, with everything else held in place, until the turntable has got up to its full speed.
            var timeout = Time.time + 30f;
            while (disc.Clock < tuning.Carousel.StartDelay + tuning.Carousel.EaseTime + 0.5f) {
                Assert.That(Time.time, Is.LessThan(timeout), "The turntable never got up to speed.");
                Hold(parked);
                car.Movement.Place(onSpot, Quaternion.LookRotation(Vector3.forward));
                yield return new WaitForFixedUpdate();
            }

            // From here the car is left alone to settle in the turntable's frame.
            car.Movement.Place(onSpot, Quaternion.LookRotation(Vector3.forward));
            yield return Settle(parked, 0.6f);

            var omega = disc.AngularVelocity;
            Assert.That(Mathf.Abs(omega), Is.GreaterThan(tuning.Carousel.AngularSpeed * Mathf.Deg2Rad * 0.9f));
            var position = car.Position;
            var expected = new Vector3(position.z * omega, 0f, -position.x * omega);
            var velocity = car.Movement.Velocity;
            velocity.y = 0f;
            Assert.That(Vector3.Dot(velocity, expected.normalized), Is.GreaterThan(expected.magnitude * 0.8f),
                $"The car should move with the turntable: {velocity} against {expected}.");
            Assert.That(velocity.magnitude, Is.LessThan(expected.magnitude * 1.25f));
            var yaw = car.GetComponent<Rigidbody>().angularVelocity.y;
            Assert.That(yaw * omega, Is.GreaterThan(0f), "The car should turn with the turntable.");
            Assert.That(Mathf.Abs(yaw), Is.GreaterThan(Mathf.Abs(omega) * 0.7f));

            // The same car beyond the rim, with the turntable still turning, stays where it is.
            car.Movement.Place(beyondSpot, Quaternion.identity);
            yield return Settle(parked, 0.6f);
            var outside = car.Movement.Velocity;
            outside.y = 0f;
            Assert.That(disc.AngularVelocity, Is.Not.EqualTo(0f));
            Assert.That(outside.magnitude, Is.LessThan(0.3f), "A car off the turntable should not be carried.");
        }

        private static IEnumerator Settle(System.Collections.Generic.List<Car> parked, float seconds)
        {
            var until = Time.time + seconds;
            while (Time.time < until) {
                Hold(parked);
                yield return new WaitForFixedUpdate();
            }
        }

        [UnityTest]
        public IEnumerator TheTurntableReversesAndWarnsFirst()
        {
            yield return Load("playroom");
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var tuning = container.Resolve<GameTuning>().Carousel;
            var disc = Disc();
            round.Begin();

            var parked = round.Cars.ToList();
            var timeout = Time.time + 60f;
            while (disc.Clock < tuning.StartDelay + tuning.EaseTime + 1f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                Hold(parked);
                yield return new WaitForFixedUpdate();
            }

            var first = Mathf.Sign(disc.AngularVelocity);
            var warned = false;
            var stopped = false;
            var reversed = false;
            while (disc.Clock < tuning.StartDelay + tuning.ReverseInterval + tuning.EaseTime + 1f) {
                Assert.That(Time.time, Is.LessThan(timeout));
                Hold(parked);
                warned |= disc.IsWarning;
                stopped |= Mathf.Abs(disc.AngularVelocity) < 0.2f * tuning.AngularSpeed * Mathf.Deg2Rad;
                reversed |= Mathf.Sign(disc.AngularVelocity) == -first && Mathf.Abs(disc.AngularVelocity) > 0.01f;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(warned, Is.True, "The arrows should flash before the reversal.");
            Assert.That(stopped, Is.True, "The turntable should ease through a stop.");
            Assert.That(reversed, Is.True, "The turntable should turn the other way.");
            Assert.That(Mathf.Sign(disc.AngularVelocity), Is.EqualTo(-first));
        }

        private IEnumerator Load(string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress(), map);
            yield return LoadGame();
            GameScope().Container.Resolve<ArenaRound>().Place();
            // The turntable is laid out on the first tick after the cars stand.
            yield return null;
            yield return null;
        }

        private CarouselDisc Disc() => GameScope().Container.Resolve<CarouselDisc>();

        private IArenaPlatform Platform() => GameScope().Container.Resolve<IArenaPlatform>();

        /// <summary>Stands the cars on a ring well beyond the turntable, at rest, so nothing else touches the test.</summary>
        private static void Hold(System.Collections.Generic.List<Car> cars)
        {
            for (var i = 0; i < cars.Count; i++) {
                var angle = (60f + i * 40f) * Mathf.Deg2Rad;
                var spot = new Vector3(Mathf.Cos(angle) * 14f, 0.05f, Mathf.Sin(angle) * 14f);
                cars[i].Movement.Place(spot, Quaternion.LookRotation(-spot));
                cars[i].Movement.Drive(Vector3.zero, 0f);
            }
        }
    }
}
