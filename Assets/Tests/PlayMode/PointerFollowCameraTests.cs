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
    /// The mouse held still drives the player's car towards it and stops there, although the arena camera follows the
    /// car and so moves the ground under the cursor (<see cref="ArenaCamera"/>, <see cref="PlayerDriver"/>): the car
    /// must come to rest, not hunt round a cursor that does not move or run on to the rim.
    /// </summary>
    public sealed class PointerFollowCameraTests : SimulationSceneFixture
    {
        private readonly SimulationInput input = new();
        private readonly SimulationProgress progress = new() { Round = 7 };

        [UnityTest]
        public IEnumerator AStillMouseBringsTheCarToRest([Values(1f, -1f)] float side, [Values(120f, 260f)] float pixels)
        {
            input.Reset();
            Random.InitState(1);
            CreateParent(input, progress, "sandbox", "rake");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var arenaCamera = container.Resolve<ArenaCamera>();
            var arena = container.Resolve<GameTuning.ArenaSettings>();
            round.Place();
            for (var i = 1; i < round.Cars.Count; i++) {
                round.Cars[i].gameObject.SetActive(false);
            }

            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
            var player = round.Player;
            player.Movement.Place(Vector3.up * arena.SpawnHeight, Quaternion.LookRotation(Vector3.forward));
            // The camera settles over the car before the mouse goes down.
            for (var frame = 0; frame < 90; frame++) {
                yield return null;
            }

            round.Begin();
            var camera = arenaCamera.Camera;
            var carOnScreen = (Vector2)camera.WorldToScreenPoint(player.Position);
            var cursor = carOnScreen + new Vector2(side * pixels, 0f);
            input.SetPointer(new PointerDrag(1, cursor, cursor, true));

            var reversals = 0;
            var lastSign = 0;
            var end = Time.time + 4f;
            while (Time.time < end && !round.IsFinished) {
                yield return null;
                var along = Vector3.Dot(player.Movement.Velocity, Vector3.right * side);
                var sign = along > 0.5f ? 1 : along < -0.5f ? -1 : 0;
                if (sign != 0 && lastSign != 0 && sign != lastSign) {
                    reversals++;
                }

                if (sign != 0) {
                    lastSign = sign;
                }
            }

            input.SetPointer(null);
            var velocity = player.Movement.Velocity;
            var speed = new Vector2(velocity.x, velocity.z).magnitude;
            var gap = (Vector2)camera.WorldToScreenPoint(player.Position) - cursor;
            Debug.Log($"Pointer follow {side * pixels:+0;-0} px: speed {speed:0.00} m/s, reversals {reversals}, " +
                $"car at x {player.Position.x:0.0} m, {gap.magnitude:0} px from the cursor");
            Assert.That(player.IsOut, Is.False, "The car ran off the arena after a still mouse");
            Assert.That(speed, Is.LessThan(1f), "The car never came to rest under a still mouse");
            Assert.That(reversals, Is.LessThanOrEqualTo(1), "The car hunted round a still mouse");
        }
    }
}
