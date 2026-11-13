using System.Collections;
using System.Collections.Generic;
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
    /// One bot's item at the player at a time (<see cref="PlayerItemTurn"/>): of two bots ready to bomb the player, one
    /// throws and the other waits out the gap before it may; a third, aimed at another car well away from the player, is
    /// not held back meanwhile.
    /// </summary>
    public sealed class PlayerItemTurnTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator TwoBotsReadyOnThePlayerTakeTurns()
        {
            yield return Open("sandbox", "rake");
            var pickups = Container.Resolve<ArenaPickups>();
            var turn = Container.Resolve<PlayerItemTurn>();
            var gap = Tuning.Pickups.Bots.PlayerItemGap;
            Assert.That(gap, Is.GreaterThan(Tuning.Pickups.Bomb.FlightTime + Tuning.Pickups.Bomb.Fuse),
                "the next item waits past the last bomb's blast");
            var throwDistance = Tuning.Pickups.Bomb.ThrowDistance;
            var player = Round.Player;
            var first = Round.Cars[1];
            var second = Round.Cars[2];
            var other = Round.Cars[3];
            var target = Round.Cars[4];
            // The player in the middle with both bots a throw away facing it; the third bot a throw from another car on
            // the far side, its blast well clear of the player.
            var spots = new Dictionary<Car, (Vector3 at, Quaternion facing)> {
                [player] = (new Vector3(0f, 0.2f, 0f), Quaternion.identity),
                [first] = (new Vector3(0f, 0.2f, -throwDistance), Quaternion.LookRotation(Vector3.forward)),
                [second] = (new Vector3(-throwDistance, 0.2f, 0f), Quaternion.LookRotation(Vector3.right)),
                [other] = (new Vector3(10f, 0.2f, -throwDistance + 2f), Quaternion.LookRotation(Vector3.forward)),
                [target] = (new Vector3(10f, 0.2f, 2f), Quaternion.LookRotation(Vector3.back)),
            };
            for (var i = 5; i < Round.Cars.Count; i++) {
                spots[Round.Cars[i]] = (new Vector3(-12f + i, 0.2f, -12f), Quaternion.identity);
            }

            void Stand()
            {
                foreach (var spot in spots) {
                    spot.Key.Movement.Place(spot.Value.at, spot.Value.facing);
                    spot.Key.Movement.Drive(Vector3.zero, 0f);
                }
            }

            for (var frame = 0; frame < 20; frame++) {
                Stand();
                yield return new WaitForFixedUpdate();
            }

            var bots = new Dictionary<Car, Bot>();
            foreach (var car in new[] { first, second, other }) {
                Assert.That(pickups.Give(car, ItemKind.Bomb), Is.True);
                bots[car] = new Bot(car, GameTuning.BotPersonality.Bully, Tuning, Container.Resolve<RoundDifficulty>(),
                    player, 11, Container.Resolve<BallLane>(), Container.Resolve<ArenaHazards>());
            }

            Assert.That(bots[first].Threatens(player) && bots[second].Threatens(player), Is.True,
                "both bombs would land on the player");
            Assert.That(bots[other].Threatens(player), Is.False, "the third bomb lands well away from the player");

            var used = new Dictionary<Car, float>();
            var taken = 0;
            turn.Taken += (_, _, _) => taken++;
            var until = Round.Elapsed + gap + 2f;
            while (Round.Elapsed < until && used.Count < 3) {
                Stand();
                foreach (var pair in bots) {
                    if (!used.ContainsKey(pair.Key) && turn.TryUse(pair.Value, Edge, Time.fixedDeltaTime, null)) {
                        used[pair.Key] = Round.Elapsed;
                    }
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.That(used.Count, Is.EqualTo(3), "every bot threw its bomb in the end");
            var early = Mathf.Min(used[first], used[second]);
            var late = Mathf.Max(used[first], used[second]);
            Assert.That(late - early, Is.GreaterThanOrEqualTo(gap - 0.05f), "the second bomb at the player waits its turn");
            Assert.That(used[other], Is.LessThan(early + gap), "a bomb at another car does not wait for the player's turn");
            Assert.That(taken, Is.EqualTo(2), "only the two at the player took the turn");
        }
    }
}
