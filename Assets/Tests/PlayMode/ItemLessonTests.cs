using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The item lesson on the real Game scene: until the player has used an item, a round with boxes starts with one
    /// waiting in front of their car, towards the middle, on safe floor, already as the cars are placed; a player who has
    /// used one, and a round without boxes, get none; the player's first use ends the lesson for good.
    /// </summary>
    public sealed class ItemLessonTests : SimulationSceneFixture
    {
        private IObjectResolver container;
        private ArenaRound round;
        private ArenaPickups pickups;
        private ItemLesson lesson;

        [UnityTest]
        public IEnumerator ANewPlayerFindsABoxInFrontOfTheirCar([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            HasUsedItem = false;
            yield return Load(2, map);

            Assert.That(lesson.Box.HasValue, Is.True, "no lesson box");
            var box = lesson.Box.Value;
            Assert.That(pickups.Live.Any(live => (live.Position - box).sqrMagnitude < 0.01f), Is.True,
                "the box is not on the floor");
            var player = round.Player;
            var toBox = box - Flat(player.Position);
            var tuning = container.Resolve<GameTuning>();
            Assert.That(toBox.magnitude, Is.InRange(tuning.Pickups.PickRadius, tuning.Pickups.LessonDistance + 0.01f));
            Assert.That(Vector3.Dot(toBox.normalized, Flat(player.transform.forward).normalized), Is.GreaterThan(0.95f),
                "the box is not in front of the car");
            Assert.That(round.Edge.DistanceToEdge(box, 0f), Is.GreaterThan(0f), "the box is off the safe floor");
            Assert.That(round.HasBegun, Is.False, "laid before the round begins, for the countdown to show it");
        }

        [UnityTest]
        public IEnumerator APlayerWhoUsedAnItemGetsNoLessonBox()
        {
            HasUsedItem = true;
            yield return Load(2, "sandbox");

            Assert.That(lesson.Box.HasValue, Is.False);
            Assert.That(pickups.Live, Is.Empty);
        }

        [UnityTest]
        public IEnumerator ARoundWithoutBoxesLaysNone()
        {
            HasUsedItem = false;
            yield return Load(1, "sandbox");

            Assert.That(pickups.IsEnabled, Is.False);
            Assert.That(lesson.Box.HasValue, Is.False);
        }

        [UnityTest]
        public IEnumerator OnlyThePlayerBreaksTheLessonBox()
        {
            HasUsedItem = false;
            yield return Load(2, "sandbox");
            var box = lesson.Box.Value;
            var bot = round.Cars[1];
            bot.Movement.Place(box + Vector3.up * 0.3f, bot.transform.rotation);
            round.Begin();
            for (var frame = 0; frame < 30; frame++) {
                yield return null;
            }

            Assert.That(bot.HasItem, Is.False, "a bot broke the player's box");
            Assert.That(pickups.Live.Any(live => (live.Position - box).sqrMagnitude < 0.01f), Is.True,
                "the box is gone");

            bot.Movement.Place(box + new Vector3(8f, 0.3f, 0f), bot.transform.rotation);
            round.Player.Movement.Place(box + Vector3.up * 0.3f, round.Player.transform.rotation);
            for (var frame = 0; frame < 30 && !round.Player.HasItem; frame++) {
                yield return null;
            }

            Assert.That(round.Player.HasItem, Is.True, "the player did not break their box");
        }

        [UnityTest]
        public IEnumerator ThePlayersFirstUseEndsTheLesson()
        {
            HasUsedItem = false;
            yield return Load(2, "sandbox");
            var memory = (ItemLessonMemory)container.Resolve<IItemLesson>();
            round.Begin();
            yield return null;

            Assert.That(pickups.Give(round.Cars[1], ItemKind.Balloon), Is.True);
            Assert.That(pickups.Use(round.Cars[1]), Is.True);
            Assert.That(memory.HasUsedItem, Is.False, "a bot's item teaches the player nothing");

            Assert.That(pickups.Give(round.Player, ItemKind.Balloon), Is.True);
            Assert.That(pickups.Use(round.Player), Is.True);
            Assert.That(memory.HasUsedItem, Is.True);
        }

        private IEnumerator Load(int visitRound, string map)
        {
            CreateParent(new SimulationInput(), new SimulationProgress { Round = visitRound }, map);
            yield return LoadGame();
            container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            pickups = container.Resolve<ArenaPickups>();
            lesson = container.Resolve<ItemLesson>();
            round.Place();
            yield return null;
            yield return null;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
