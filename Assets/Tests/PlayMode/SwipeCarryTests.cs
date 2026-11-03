using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Input;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// A swipe keeps the player's car driving its way after the finger lifts (<see cref="PlayerDriver"/>) on the real
    /// Game scene, the player alone on the arena: it carries the car several metres and stops it, a new touch takes
    /// over at once, one during the kick-off drives from GO, and one off the edge on easy does not throw the car off.
    /// </summary>
    public sealed class SwipeCarryTests : SimulationSceneFixture
    {
        // Up on the screen: away from the camera.
        private static readonly Vector2 Up = new(0f, 200f);

        private readonly SimulationInput input = new();
        private readonly SimulationProgress progress = new() { Round = 7 };

        private ArenaRound round;
        private PlayerDriver driver;
        private ArenaCamera arenaCamera;
        private GameTuning tuning;
        private GameTuning.ArenaSettings arena;

        [UnityTest]
        public IEnumerator ASwipeCarriesTheCarItsWayAndStops()
        {
            yield return Setup(7);
            Place(round.Player, Vector3.zero, Away);
            Place(round.Cars[1], -Away * 9f, Away);
            yield return Begin();
            input.Swipe(Up);
            var start = Flat(round.Player.Position);
            var controls = tuning.Controls;
            yield return Wait(controls.SwipeCarry + controls.SwipeFade + 0.6f);

            var moved = Flat(round.Player.Position) - start;
            Assert.That(Vector3.Dot(moved, Away), Is.GreaterThan(4f), "The swipe did not carry the car away from the camera");
            Assert.That(Vector3.Angle(moved, Away), Is.LessThan(10f), "The swipe drove the car off its way");
            Assert.That(Flat(round.Player.Movement.Velocity).magnitude, Is.LessThan(1f), "The car kept driving after the swipe");
            Assert.That(driver.Swipes, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ANewTouchTakesOverTheSwipe()
        {
            yield return Setup(7);
            Place(round.Player, Vector3.zero, Away);
            Place(round.Cars[1], -Away * 9f, Away);
            yield return Begin();
            input.Swipe(Up);
            yield return Wait(0.3f);
            // A finger held where it touched: the joystick in its dead zone, so the car brakes.
            var point = new Vector2(Screen.width * 0.5f, Screen.height * 0.3f);
            input.SetPointer(new PointerDrag(1, point, point, false));
            yield return Wait(0.9f);
            var speed = Flat(round.Player.Movement.Velocity).magnitude;
            input.SetPointer(null);

            Assert.That(speed, Is.LessThan(1f), "The swipe kept driving under a new touch");
        }

        [UnityTest]
        public IEnumerator ASwipeDuringTheKickOffDrivesFromGo()
        {
            yield return Setup(7);
            Place(round.Player, Vector3.zero, Away);
            Place(round.Cars[1], -Away * 9f, Away);
            yield return Wait(0.2f);
            input.Swipe(Up);
            var start = Flat(round.Player.Position);
            yield return Wait(0.5f);
            Assert.That((Flat(round.Player.Position) - start).magnitude, Is.LessThan(0.3f), "The car moved before GO");

            round.Begin();
            yield return Wait(0.6f);
            Assert.That(Vector3.Dot(Flat(round.Player.Position) - start, Away), Is.GreaterThan(1.5f),
                "The kick-off's swipe did not drive from GO");
        }

        [UnityTest]
        public IEnumerator EasyKeepsASwipeOffTheEdgeOnTheArena()
        {
            yield return Setup(1);
            var right = Flat(arenaCamera.transform.right).normalized;
            Place(round.Player, right * (arena.Radius - 3f), right);
            Place(round.Cars[1], -right * 9f, right);
            yield return Begin();
            input.Swipe(new Vector2(200f, 0f));
            yield return Wait(2f);

            Assert.That(round.Player.IsOut, Is.False, "A swipe off the edge threw the car off on easy");
        }

        private Vector3 Away => Flat(arenaCamera.transform.forward).normalized;

        private IEnumerator Setup(int visitRound)
        {
            progress.Round = visitRound;
            progress.LosingStreak = 0;
            input.Reset();
            Random.InitState(1);
            CreateParent(input, progress, "sandbox", "rake");
            yield return LoadGame();
            var container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            arena = container.Resolve<GameTuning.ArenaSettings>();
            arenaCamera = container.Resolve<ArenaCamera>();
            tuning = container.Resolve<GameTuning>();
            driver = container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single();
            round.Place();
            // One bot stays, standing behind the camera's side of the player, so the round has another car in it.
            for (var i = 2; i < round.Cars.Count; i++) {
                round.Cars[i].gameObject.SetActive(false);
            }

            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            var field = typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "BotDriver setup changed; update the test's AI isolation.");
            field.SetValue(botDriver, true);
        }

        private IEnumerator Begin()
        {
            for (var frame = 0; frame < 10; frame++) {
                yield return null;
            }

            round.Begin();
        }

        private IEnumerator Wait(float seconds)
        {
            var end = Time.time + seconds;
            while (Time.time < end && !round.IsFinished) {
                yield return null;
            }
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        private void Place(Car car, Vector3 position, Vector3 facing) =>
            car.Movement.Place(position + Vector3.up * arena.SpawnHeight, Quaternion.LookRotation(facing));
    }
}
