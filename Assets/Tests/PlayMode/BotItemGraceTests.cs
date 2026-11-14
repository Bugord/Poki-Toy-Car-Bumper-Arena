using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The grace that spares the player as the bots' target in a round's first seconds (<see cref="RoundDifficulty.PlayerGrace"/>)
    /// spares them the bots' items too: a bot holding a bomb, a shockwave, a bowling ball or a glove finds no moment to use it
    /// while the spared car is in its reach, and finds it as before once nobody is spared or the car is out of reach.
    /// </summary>
    public sealed class BotItemGraceTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator ABotHoldsItsItemWhileTheSparedCarIsInItsReach(
            [Values(ItemKind.Bomb, ItemKind.Shockwave, ItemKind.Bowling, ItemKind.Glove)] ItemKind kind)
        {
            yield return Open("sandbox", "rake");
            var pickups = Container.Resolve<ArenaPickups>();
            Assert.That(pickups.IsEnabled, Is.True, "a third round of the visit has boxes");
            var holder = Round.Cars[1];
            var player = Round.Player;
            // The bot faces the player across the middle, the player just where each item reaches.
            var distance = kind switch {
                ItemKind.Bomb => Tuning.Pickups.Bomb.ThrowDistance,
                ItemKind.Glove => Tuning.Pickups.Glove.Reach - 1f,
                _ => 5f,
            };
            var from = new Vector3(0f, 0.2f, -distance * 0.5f);
            holder.Movement.Place(from, Quaternion.LookRotation(Vector3.forward));
            player.Movement.Place(from + Vector3.forward * distance, Quaternion.LookRotation(Vector3.back));
            // A second car beside the player, so a shockwave has the two it waits for; the others far off.
            Round.Cars[2].Movement.Place(from + Vector3.forward * distance + Vector3.right * 2.5f,
                Quaternion.LookRotation(Vector3.back));
            for (var i = 3; i < Round.Cars.Count; i++) {
                Round.Cars[i].Movement.Place(new Vector3(-12f + i, 0.2f, -12f), Quaternion.identity);
            }

            for (var frame = 0; frame < 20; frame++) {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(pickups.Give(holder, kind), Is.True);
            var bot = new Bot(holder, GameTuning.BotPersonality.Bully, Tuning, Container.Resolve<RoundDifficulty>(), player,
                11, Container.Resolve<BallLane>(), Container.Resolve<ArenaHazards>());
            bot.SetTarget(player);

            var dt = 1f / 30f;
            for (var step = 0; step < 60; step++) {
                Assert.That(bot.WantsToUseItem(Edge, Round.Cars, dt, player), Is.False,
                    $"the bot used its {kind} on the spared car");
            }

            var used = false;
            for (var step = 0; step < 60 && !used; step++) {
                used = bot.WantsToUseItem(Edge, Round.Cars, dt);
            }

            Assert.That(used, Is.True, $"with nobody spared the bot finds its moment for the {kind}");
        }

        /// <summary>
        /// A bowling ball takes a moment to roll to the spared car, so a car beside the line but able to drive onto it by
        /// then (at its top speed, whatever it does now) is in its reach; a car farther out, which the ball would pass
        /// before it got there, is not, and the bot bowls at another car on the line.
        /// </summary>
        [UnityTest]
        public IEnumerator ABotHoldsItsBowlingBallWhileTheSparedCarDrivesOntoItsLine()
        {
            yield return Open("sandbox", "rake");
            var pickups = Container.Resolve<ArenaPickups>();
            var holder = Round.Cars[1];
            // A bot's car stands for the spared one here: the player's own driver would hold it still.
            var player = Round.Cars[3];
            var from = new Vector3(0f, 0.2f, -6f);
            holder.Movement.Place(from, Quaternion.LookRotation(Vector3.forward));
            // The car the bot bowls at, on its line; the spared car beside the line, out of its static reach.
            Round.Cars[2].Movement.Place(from + Vector3.forward * 12f, Quaternion.LookRotation(Vector3.back));
            for (var i = 4; i < Round.Cars.Count; i++) {
                Round.Cars[i].Movement.Place(new Vector3(-12f + i, 0.2f, -12f), Quaternion.identity);
            }

            var beside = from + Vector3.forward * 8f + Vector3.right * 5f;
            player.Movement.Place(beside, Quaternion.LookRotation(Vector3.left));
            for (var frame = 0; frame < 20; frame++) {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(pickups.Give(holder, ItemKind.Bowling), Is.True);
            var bot = new Bot(holder, GameTuning.BotPersonality.Bully, Tuning, Container.Resolve<RoundDifficulty>(), player,
                11, Container.Resolve<BallLane>(), Container.Resolve<ArenaHazards>());
            bot.SetTarget(Round.Cars[2]);

            // The spared car drives towards the line for a moment, still beside it.
            for (var frame = 0; frame < 10; frame++) {
                player.Movement.Drive(Vector3.left, 1f);
                yield return new WaitForFixedUpdate();
            }

            var flat = player.Movement.Velocity;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.GreaterThan(3f), "the spared car is on the move");
            Assert.That(Mathf.Abs(player.Position.x - from.x), Is.GreaterThan(Tuning.Pickups.Bowling.Radius +
                Tuning.Pickups.Bowling.HitReach + 1.5f), "the spared car is still beside the ball's static reach");
            var dt = 1f / 30f;
            for (var step = 0; step < 60; step++) {
                Assert.That(bot.WantsToUseItem(Edge, Round.Cars, dt, player), Is.False,
                    "the bot bowled at a line the spared car is driving onto");
            }

            // Far out beside the ball's start, the spared car could not reach the line before the ball rolls past.
            player.Movement.Drive(Vector3.zero, 0f);
            player.Movement.Place(from + Vector3.forward * 2f + Vector3.right * 9f, Quaternion.LookRotation(Vector3.left));
            for (var frame = 0; frame < 20; frame++) {
                yield return new WaitForFixedUpdate();
            }

            var used = false;
            for (var step = 0; step < 60 && !used; step++) {
                used = bot.WantsToUseItem(Edge, Round.Cars, dt, player);
            }

            Assert.That(used, Is.True, "the bot bowls past a spared car too far out to reach its line");
        }
    }
}
