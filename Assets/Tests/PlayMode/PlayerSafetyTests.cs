using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// A new player's safe first rounds on the real Game scene: a fall off the floor is caught and the car carried back
    /// onto safe floor, nobody's knock-out; the player's hits grow over the round; the round after them is played for
    /// real.
    /// </summary>
    public sealed class PlayerSafetyTests : SimulationSceneFixture
    {
        private ArenaRound round;
        private PlayerSafety safety;

        [UnityTest]
        public IEnumerator AFallInASafeRoundIsCaughtAndUndone([Values("sandbox", "candy")] string map)
        {
            UseSafety(2);
            yield return Load(2, map);
            var player = round.Player;
            DropOffTheEdge(player);

            var wasCarried = false;
            for (var frame = 0; frame < 300 && (!wasCarried || player.Movement.IsCarried || !player.Movement.IsGrounded);
                 frame++) {
                if (player.Movement.IsCarried && !wasCarried) {
                    wasCarried = true;
                    Assert.That(player.LastHitBy, Is.Null, "the caught fall still credits a knock-out");
                }

                yield return null;
            }

            Assert.That(wasCarried, Is.True, "the fall was not caught");
            Assert.That(player.IsOut, Is.False);
            Assert.That(round.IsFinished, Is.False);
            Assert.That(safety.Rescues, Is.EqualTo(1));
            Assert.That(player.Movement.IsGrounded, Is.True, "the car did not land");
            Assert.That(round.Edge.DistanceToEdge(Flat(player.Position), 0f), Is.GreaterThan(0f), "landed off the floor");
        }

        [UnityTest]
        public IEnumerator ThePlayersHitsGrowOverASafeRound()
        {
            UseSafety(2);
            yield return Load(1, "sandbox");
            yield return null;
            var start = round.Player.PowerBoost;
            for (var frame = 0; frame < 60; frame++) {
                yield return null;
            }

            Assert.That(start, Is.EqualTo(1f).Within(0.05f));
            Assert.That(round.Player.PowerBoost, Is.GreaterThan(start));
        }

        [UnityTest]
        public IEnumerator TheRoundAfterTheSafeOnesIsPlayedForReal()
        {
            UseSafety(2);
            yield return Load(3, "sandbox");
            var player = round.Player;
            DropOffTheEdge(player);
            for (var frame = 0; frame < 300 && !player.IsOut; frame++) {
                yield return null;
            }

            Assert.That(player.IsOut, Is.True);
            Assert.That(safety.Rescues, Is.EqualTo(0));
            Assert.That(player.PowerBoost, Is.EqualTo(1f));
        }

        private IEnumerator Load(int visitRound, string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = visitRound }, map);
            yield return LoadGame();
            var container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            safety = container.Resolve<PlayerSafety>();
            round.Begin();
            yield return null;
        }

        /// <summary>The player's car just past the rim, over nothing.</summary>
        private void DropOffTheEdge(Gameplay.Cars.Car player)
        {
            var out_ = new Vector3(round.Edge.FullExtent + 3f, 0.3f, 0f);
            player.Movement.Place(out_, player.transform.rotation);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
