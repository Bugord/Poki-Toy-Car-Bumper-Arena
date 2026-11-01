using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
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
    /// The kick-off's 3, 2, 1 runs while the cars stand placed and the round has not begun: the player's car must not
    /// move or boost then, whatever is held or pressed, and steering held through it must drive the car from GO.
    /// </summary>
    public sealed class KickoffTests : SimulationSceneFixture
    {
        // About the kick-off's length at 30 frames a second.
        private const int KickoffFrames = 64;

        [UnityTest]
        public IEnumerator TheCarStandsUntilTheRoundBeginsAndThenDrivesAsHeld()
        {
            UseMode(GameTuning.GameMode.Match);
            var input = new SimulationInput();
            CreateParent(input, new SimulationProgress { Round = 3 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            var driver = container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single();
            round.Place();
            for (var frame = 0; frame < 20; frame++) {
                yield return new WaitForFixedUpdate();
            }

            var player = round.Player;
            var start = player.Position;
            input.SetMove(Vector2.up);
            input.PressBoost();
            input.PressBoost(BoostInputMethod.Finger);
            input.PressItem();
            for (var frame = 0; frame < KickoffFrames; frame++) {
                yield return null;
            }

            var flat = player.Position - start;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.LessThan(0.05f), "The car moved before the round began");
            Assert.That(driver.Boosts, Is.Zero, "A boost fired before the round began");
            Assert.That(player.Movement.Velocity.magnitude, Is.LessThan(0.2f));
            Assert.That(round.HasBegun, Is.False);
            Assert.That(round.Elapsed, Is.Zero, "The clock ran before the round began");

            round.Begin();
            for (var frame = 0; frame < 30; frame++) {
                yield return null;
            }

            flat = player.Position - start;
            flat.y = 0f;
            Assert.That(flat.magnitude, Is.GreaterThan(0.5f), "Steering held through the kick-off did not drive at GO");
            input.Reset();
        }
    }
}
