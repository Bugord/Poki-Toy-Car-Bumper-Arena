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
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The game's boost for the player (<see cref="AutoBoost"/>) on the real Game scene, one bot left beside the player
    /// and its decisions off: a ram on easy when the player drives at a car ahead, none on normal, none while the player
    /// only stands, and a rescue back inward when a car shoves the player out near the edge.
    /// </summary>
    public sealed class AutoBoostTests : SimulationSceneFixture
    {
        private readonly SimulationInput input = new();
        private readonly SimulationProgress progress = new() { Round = 1 };

        private ArenaRound round;
        private PlayerDriver driver;
        private ArenaCamera arenaCamera;
        private GameTuning.ArenaSettings arena;
        private readonly List<AutoBoostKind> fired = new();

        [UnityTest]
        public IEnumerator EasyRamsACarThePlayerDrivesAt()
        {
            yield return Setup(1);
            yield return DriveAtStillCar(Vector3.forward, 2f);

            Assert.That(fired, Does.Contain(AutoBoostKind.Ram), "No ram fired on easy.");
            Assert.That(driver.AutoBoosts, Is.GreaterThanOrEqualTo(1));
            Assert.That(driver.Boosts, Is.Zero, "The game's boost counts as the player's own.");
            Assert.That(driver.FirstBoostInput, Is.Null, "The game's boost counts as the player's first boost.");
        }

        [UnityTest]
        public IEnumerator NormalLeavesTheBoostToThePlayer()
        {
            yield return Setup(7);
            yield return DriveAtStillCar(Vector3.forward, 2f);

            Assert.That(fired, Is.Empty);
            Assert.That(driver.AutoBoosts, Is.Zero);
        }

        [UnityTest]
        public IEnumerator NoRamWhileThePlayerOnlyStands()
        {
            yield return Setup(1);
            Place(round.Player, Vector3.zero, Vector3.forward);
            Place(round.Cars[1], Vector3.forward * 3.5f, Vector3.forward);
            yield return Begin();
            yield return Run(1.5f, () => { });

            Assert.That(fired, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EasyRescuesThePlayerShovedToTheEdge()
        {
            yield return Setup(1);
            var outward = Vector3.right;
            var start = arena.Radius - 3f;
            Place(round.Player, outward * start, Quaternion.LookRotation(Vector3.forward) * Vector3.forward);
            Place(round.Cars[1], outward * (start - 4f), outward);
            yield return Begin();
            round.Cars[1].Movement.Drive(outward, 1f);
            yield return Run(3f, () => round.Cars[1].Movement.Drive(outward, 1f));

            Assert.That(fired, Does.Contain(AutoBoostKind.Rescue), "No rescue fired on easy.");
            Assert.That(round.Player.IsOut, Is.False, "The rescued player still fell.");
        }

        private IEnumerator Setup(int visitRound)
        {
            UseBoost(true);
            progress.Round = visitRound;
            progress.LosingStreak = 0;
            input.Reset();
            fired.Clear();
            Random.InitState(1);
            CreateParent(input, progress, "sandbox", "rake");
            yield return LoadGame();
            var container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            arena = container.Resolve<GameTuning.ArenaSettings>();
            arenaCamera = container.Resolve<ArenaCamera>();
            driver = container.Resolve<IEnumerable<ITickable>>().OfType<PlayerDriver>().Single();
            driver.AutoBoosted += kind => fired.Add(kind);
            var difficulty = container.Resolve<RoundDifficulty>();
            Assert.That(difficulty.AutoBoost, Is.EqualTo(visitRound == 1 ? 1f : 0f), "Unexpected auto boost.");
            round.Place();
            for (var i = 2; i < round.Cars.Count; i++) {
                round.Cars[i].gameObject.SetActive(false);
            }

            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            var field = typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "BotDriver setup changed; update the test's AI isolation.");
            field.SetValue(botDriver, true);
        }

        // The player from the middle drives at a car standing ahead along way, from start metres away.
        private IEnumerator DriveAtStillCar(Vector3 way, float seconds)
        {
            Place(round.Player, -way * 4f, way);
            Place(round.Cars[1], way * 4f, way);
            yield return Begin();
            yield return Run(seconds, () => Steer(way));
        }

        private IEnumerator Begin()
        {
            for (var frame = 0; frame < 10; frame++) {
                yield return null;
            }

            round.Begin();
        }

        private IEnumerator Run(float seconds, System.Action each)
        {
            var end = Time.time + seconds;
            while (Time.time < end && !round.IsFinished) {
                each();
                yield return null;
            }

            input.SetMove(Vector2.zero);
        }

        private void Steer(Vector3 way)
        {
            var forward = Vector3.ProjectOnPlane(arenaCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(arenaCamera.transform.right, Vector3.up).normalized;
            input.SetMove(new Vector2(Vector3.Dot(way, right), Vector3.Dot(way, forward)));
        }

        private void Place(Car car, Vector3 position, Vector3 facing) =>
            car.Movement.Place(position + Vector3.up * arena.SpawnHeight, Quaternion.LookRotation(facing));
    }
}
