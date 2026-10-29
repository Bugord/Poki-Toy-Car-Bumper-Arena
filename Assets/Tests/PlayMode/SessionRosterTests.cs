using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class SessionRosterTests
    {
        private GameTuning tuning;
        private CarCatalog catalog;
        private SessionRoster roster;
        private Random.State randomState;

        [SetUp]
        public void SetUp()
        {
            randomState = Random.state;
            Random.InitState(731);
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            catalog = ScriptableObject.CreateInstance<CarCatalog>();
            JsonUtility.FromJsonOverwrite("{\"cars\":[{\"id\":\"racer\"},{\"id\":\"truck\",\"unlockLevel\":99}]}", catalog);
            roster = new SessionRoster(catalog);
            Settings(1f, 2);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tuning);
            Object.DestroyImmediate(catalog);
            Random.state = randomState;
        }

        [Test]
        public void FirstDeckNeverRepeatsANameAcrossDepartures()
        {
            var seen = new HashSet<string>(roster.ForRound(6).Select(player => player.Name));
            Assert.That(seen.Count, Is.EqualTo(6));
            for (var round = 0; round < 33; round++) {
                roster.NextRound(6, 6, RoundResult.Nobody, tuning.Players);
                for (var index = 1; index <= 6; index++) {
                    if (roster.Joined(index)) {
                        Assert.That(seen.Add(roster.NameOf(index)), Is.True, "A name repeated before all 72 were used.");
                    }
                }
            }
            Assert.That(seen.Count, Is.EqualTo(72));
        }

        [Test]
        public void RefilledDeckNeverDuplicatesSomeoneStillSeated()
        {
            roster.ForRound(6);
            for (var round = 0; round < 200; round++) {
                var rival = roster.NameOf(6);
                roster.NextRound(6, 6, 6, tuning.Players);
                Assert.That(roster.ForRound(6).Select(player => player.Name).Distinct().Count(), Is.EqualTo(6));
                Assert.That(roster.NameOf(6), Is.EqualTo(rival));
            }
        }

        [Test]
        public void NemesisKeepsNameAndCarEvenWhileItsSlotIsAbsent()
        {
            var rival = roster.ForRound(6)[5];
            roster.NextRound(6, 3, 6, tuning.Players);
            for (var round = 0; round < 10; round++) {
                roster.NextRound(3, 3, 6, tuning.Players);
            }
            roster.NextRound(3, 6, 6, tuning.Players);
            Assert.That(roster.ForRound(6)[5], Is.SameAs(rival));
            Assert.That(roster.Joined(6), Is.False);
            Assert.That(roster.LeftNameOf(6), Is.Null);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void MaxLeavesCapsOnlyDeparturesNotNewlyAddedSlots(int maximum)
        {
            Settings(1f, maximum);
            var before = roster.ForRound(3);
            roster.NextRound(3, 6, 2, tuning.Players);
            var departures = Enumerable.Range(1, 6).Count(index => roster.LeftNameOf(index) != null);
            Assert.That(departures, Is.EqualTo(maximum));
            Assert.That(roster.NameOf(2), Is.EqualTo(before[1].Name));
            for (var index = 4; index <= 6; index++) {
                Assert.That(roster.Joined(index), Is.True);
                Assert.That(roster.LeftNameOf(index), Is.Null);
            }
        }

        [Test]
        public void NoLeaveChanceKeepsPlayedSeatsAndClearsLastTransitions()
        {
            roster.ForRound(6);
            roster.NextRound(6, 6, RoundResult.Nobody, tuning.Players);
            var before = roster.ForRound(6);
            Settings(0f, 2);
            roster.NextRound(6, 6, RoundResult.Nobody, tuning.Players);
            Assert.That(roster.ForRound(6), Is.EqualTo(before));
            foreach (var index in Enumerable.Range(1, 6)) {
                Assert.That(roster.Joined(index), Is.False);
                Assert.That(roster.LeftNameOf(index), Is.Null);
            }
        }

        [Test]
        public void SnapshotKeepsPlayedOccupantsAfterTheyLeave()
        {
            var played = roster.ForRound(6);
            var names = played.Select(player => player.Name).ToArray();
            roster.NextRound(6, 6, RoundResult.Nobody, tuning.Players);
            Assert.That(played.Select(player => player.Name), Is.EqualTo(names));
            for (var index = 1; index <= 6; index++) {
                if (roster.Joined(index)) {
                    Assert.That(roster.LeftNameOf(index), Is.EqualTo(played[index - 1].Name));
                    Assert.That(roster.NameOf(index), Is.Not.EqualTo(played[index - 1].Name));
                }
            }
        }

        [Test]
        public void ReadingAssignedSeatsDoesNotConsumeRandomNumbers()
        {
            roster.ForRound(6);
            var before = Random.state;
            roster.ForRound(6);
            for (var index = 1; index <= 6; index++) {
                roster.NameOf(index);
                roster.CarOf(index);
                roster.Joined(index);
                roster.LeftNameOf(index);
            }
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [Test]
        public void ShrinkingMarksRemovedSeatsLeftEvenWithoutRandomDepartures()
        {
            Settings(0f, 0);
            var before = roster.ForRound(6);
            roster.NextRound(6, 3, 6, tuning.Players);
            for (var index = 1; index <= 6; index++) {
                Assert.That(roster.LeftNameOf(index), Is.EqualTo(index > 3 ? before[index - 1].Name : null));
            }
            Assert.That(roster.ForRound(6)[5], Is.SameAs(before[5]), "An absent revenge target only leaves the lobby.");
            roster.NextRound(3, 6, 6, tuning.Players);
            Assert.That(roster.NameOf(6), Is.EqualTo(before[5].Name));
        }

        [Test]
        public void LobbyCountMatchesRoundDifficultyThroughRampAndLosingStreaks()
        {
            var progress = new SimulationProgress();
            for (var round = 1; round <= 15; round++) {
                for (var streak = 0; streak <= 8; streak++) {
                    progress.Round = round;
                    progress.LosingStreak = streak;
                    Assert.That(RoundDifficulty.BotCountFor(tuning, progress),
                        Is.EqualTo(new RoundDifficulty(tuning, progress).BotCount));
                }
            }
        }

        private void Settings(float chance, int maximum) => JsonUtility.FromJsonOverwrite(
            "{\"players\":{\"leaveChance\":" + chance.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"maxLeaves\":" + maximum + "}}", tuning);
    }
}
