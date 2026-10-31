using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The Ball arena's ball on the real Game scene: it comes after the delay, knocks a car standing in its lane out of
    /// it without crediting anybody, and sandbox has none.
    /// </summary>
    public sealed class BallArenaTests : SimulationSceneFixture
    {
        // Frames of 1/60 s (the fixture fixes the delta time).
        private const int MaxFrames = 60 * 40;

        [UnityTest]
        public IEnumerator BallComesAfterTheDelayAndKnocksACarOutOfItsLane()
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 3 }, "tabletop");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var lane = container.Resolve<BallLane>();
            var tuning = container.Resolve<GameTuning>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            botDriver.Seed = 7;
            // The bots stand still, so only the ball moves the car.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);

            round.Place();
            round.Begin();
            var beganAt = Time.time;
            var frames = 0;
            while (!lane.IsActive && frames++ < MaxFrames) {
                yield return null;
            }

            Assert.That(lane.IsActive, Is.True, "No ball was announced.");
            Assert.That(Time.time - beganAt, Is.GreaterThanOrEqualTo(tuning.Ball.Delay - 0.1f), "The ball came before its delay.");

            var car = round.Cars[1];
            var origin = lane.Origin;
            var normal = lane.Normal;
            car.Movement.Place(origin + Vector3.up * 0.05f, Quaternion.LookRotation(lane.Direction));

            frames = 0;
            while (lane.IsActive && frames++ < MaxFrames) {
                yield return null;
            }

            Assert.That(lane.IsActive, Is.False, "The ball never left the arena.");
            Assert.That(float.IsNegativeInfinity(car.LastHazardTime), Is.False, "The ball did not touch the car in its lane.");
            Assert.That(car.LastHitBy, Is.Null, "A ball hit must credit no car.");
            if (!car.IsOut) {
                var offset = Mathf.Abs(Vector3.Dot(car.Position - origin, normal));
                Assert.That(offset, Is.GreaterThan(1f), "The car was not knocked out of the lane.");
            }
        }

        [UnityTest]
        public IEnumerator SandboxHasNoBall()
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 3 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var lane = container.Resolve<BallLane>();
            var tuning = container.Resolve<GameTuning>();
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);

            round.Place();
            round.Begin();
            var seen = false;
            var frames = 0;
            var until = (int)((tuning.Ball.Delay + tuning.Ball.Warning + 6f) * 60f);
            while (frames++ < until && round.IsRunning) {
                seen |= lane.IsActive;
                yield return null;
            }

            Assert.That(seen, Is.False, "Sandbox must not roll a ball.");
            var ball = Object.FindFirstObjectByType<ArenaBall>(FindObjectsInactive.Include);
            Assert.That(ball == null || !ball.gameObject.activeSelf, Is.True, "The ball object must be off on sandbox.");
        }
    }
}
