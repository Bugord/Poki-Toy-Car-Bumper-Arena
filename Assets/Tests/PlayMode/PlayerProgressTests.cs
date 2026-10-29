using System.Collections.Generic;
using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Protects the saved reward and level boundaries, especially when one round unlocks more than one car.
    /// </summary>
    public sealed class PlayerProgressTests
    {
        private GameTuning tuning;
        private CarCatalog catalog;
        private MemoryStorage storage;
        private PlayerProgress progress;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            catalog = ScriptableObject.CreateInstance<CarCatalog>();
            JsonUtility.FromJsonOverwrite("{\"cars\":[" +
                "{\"id\":\"racer\",\"displayName\":\"RACER\",\"unlockLevel\":1}," +
                "{\"id\":\"speedster\",\"displayName\":\"SPEEDSTER\",\"unlockLevel\":2}," +
                "{\"id\":\"vintage\",\"displayName\":\"VINTAGE\",\"unlockLevel\":3}]}", catalog);
            storage = new MemoryStorage();
            progress = new PlayerProgress(storage, tuning, catalog);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(tuning);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void ThirdOfFourWithOneKnockoutEarnsTwentyFive()
        {
            var gain = progress.Record(Result(RoundOutcome.Out, 3, 4, 1));
            Assert.That(gain.RoundXp, Is.EqualTo(10));
            Assert.That(gain.PlaceXp, Is.EqualTo(5));
            Assert.That(gain.WinXp, Is.Zero);
            Assert.That(gain.KnockoutXp, Is.EqualTo(10));
            Assert.That(gain.Total, Is.EqualTo(25));
            Assert.That(gain.TotalBefore, Is.Zero);
            Assert.That(gain.TotalAfter, Is.EqualTo(25));
            Assert.That(gain.Before.Level, Is.EqualTo(1));
            Assert.That(gain.Before.Xp, Is.Zero);
            Assert.That(gain.Before.Cost, Is.EqualTo(40));
            Assert.That(gain.After.Level, Is.EqualTo(1));
            Assert.That(gain.After.Xp, Is.EqualTo(25));
            Assert.That(gain.UnlockedCarIds, Is.Empty);
        }

        [Test]
        public void WinOfFourWithTwoKnockoutsEarnsSixtyAndPersists()
        {
            var gain = progress.Record(Result(RoundOutcome.Won, 1, 4, 2));
            Assert.That(gain.RoundXp, Is.EqualTo(10));
            Assert.That(gain.PlaceXp, Is.EqualTo(15));
            Assert.That(gain.WinXp, Is.EqualTo(15));
            Assert.That(gain.KnockoutXp, Is.EqualTo(20));
            Assert.That(gain.Total, Is.EqualTo(60));
            Assert.That(gain.After.Level, Is.EqualTo(2));
            Assert.That(gain.After.Xp, Is.EqualTo(20));
            Assert.That(gain.After.Cost, Is.EqualTo(60));
            Assert.That(gain.UnlockedCarIds, Is.EqualTo(new[] { "speedster" }));

            var reloaded = new PlayerProgress(storage, tuning, catalog);
            Assert.That(reloaded.TotalXp, Is.EqualTo(60));
            Assert.That(reloaded.Level, Is.EqualTo(2));
            Assert.That(reloaded.IsUnlocked("speedster"), Is.True);
            Assert.That(reloaded.NextCar.Id, Is.EqualTo("vintage"));
            Assert.That(reloaded.SelectedCar, Is.EqualTo("speedster"));
        }

        [Test]
        public void OneRoundCanCrossTwoLevelsAndUnlockBothCars()
        {
            storage.SetInt("xp_total", 35);
            var gain = progress.Record(Result(RoundOutcome.Won, 1, 4, 3));
            Assert.That(gain.Total, Is.EqualTo(70));
            Assert.That(gain.TotalBefore, Is.EqualTo(35));
            Assert.That(gain.TotalAfter, Is.EqualTo(105));
            Assert.That(gain.Before.Level, Is.EqualTo(1));
            Assert.That(gain.Before.Xp, Is.EqualTo(35));
            Assert.That(gain.Before.Cost, Is.EqualTo(40));
            Assert.That(gain.After.Level, Is.EqualTo(3));
            Assert.That(gain.After.Xp, Is.EqualTo(5));
            Assert.That(gain.After.Cost, Is.EqualTo(80));
            Assert.That(gain.UnlockedCarIds, Is.EqualTo(new[] { "speedster", "vintage" }));
            Assert.That(progress.NextCar, Is.Null);
            Assert.That(progress.SelectedCar, Is.EqualTo("vintage"));
            Assert.That(progress.Record(Result(RoundOutcome.Out, 4, 4, 0)).UnlockedCarIds, Is.Empty);
            Assert.That(progress.SelectedCar, Is.EqualTo("vintage"));
        }

        [Test]
        public void SelectionOnlyAcceptsUnlockedCarsAndPersists()
        {
            Assert.That(progress.SelectedCar, Is.EqualTo("racer"));
            Assert.That(progress.Select("speedster"), Is.False);
            Assert.That(progress.Select("unknown"), Is.False);
            Assert.That(storage.HasKey("car_selected"), Is.False);
            storage.SetInt("xp_total", 100);
            Assert.That(progress.Select("vintage"), Is.True);
            Assert.That(new PlayerProgress(storage, tuning, catalog).SelectedCar, Is.EqualTo("vintage"));
            Assert.That(progress.Select("unknown"), Is.False);
            Assert.That(progress.SelectedCar, Is.EqualTo("vintage"));
        }

        [TestCase("unknown", 100)]
        [TestCase("vintage", 0)]
        public void InvalidSavedSelectionFallsBackToFirstCar(string id, int xp)
        {
            storage.SetString("car_selected", id);
            storage.SetInt("xp_total", xp);
            Assert.That(progress.SelectedCar, Is.EqualTo("racer"));
        }

        [Test]
        public void BotCarCanBeLockedAndStaysWithItsNameWithoutMoreRandomDraws()
        {
            var randomState = Random.state;
            try {
                JsonUtility.FromJsonOverwrite("{\"cars\":[{\"id\":\"locked\",\"unlockLevel\":99}]}", catalog);
                var roster = new SessionRoster(catalog);
                Assert.That(roster.NameOf(0), Is.EqualTo("YOU"));
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(JsonUtility.ToJson(randomState)));
                var car = roster.CarOf(1);
                var name = roster.NameOf(1);
                Assert.That(car.Id, Is.EqualTo("locked"));
                Assert.That(progress.IsUnlocked(car.Id), Is.False);
                var assignedState = Random.state;
                Assert.That(roster.CarOf(1), Is.SameAs(car));
                Assert.That(roster.NameOf(1), Is.EqualTo(name));
                Assert.That(JsonUtility.ToJson(Random.state), Is.EqualTo(JsonUtility.ToJson(assignedState)));
            }
            finally {
                Random.state = randomState;
            }
        }

        [TestCase(0, 1, 0, 40)]
        [TestCase(39, 1, 39, 40)]
        [TestCase(40, 2, 0, 60)]
        [TestCase(99, 2, 59, 60)]
        [TestCase(100, 3, 0, 80)]
        [TestCase(539, 6, 139, 140)]
        [TestCase(540, 7, 0, 140)]
        [TestCase(680, 8, 0, 140)]
        [TestCase(825, 9, 5, 140)]
        public void LevelPricesHaveExactBoundariesAndRepeatTheLast(int total, int level, int xp, int cost)
        {
            storage.SetInt("xp_total", total);
            var value = progress.At(progress.TotalXp);
            Assert.That(value.Level, Is.EqualTo(level));
            Assert.That(value.Xp, Is.EqualTo(xp));
            Assert.That(value.Cost, Is.EqualTo(cost));
        }

        [Test]
        public void UnlocksFollowStorageWithoutAnotherSavedList()
        {
            Assert.That(progress.IsUnlocked("racer"), Is.True);
            Assert.That(progress.IsUnlocked("unknown"), Is.False);
            storage.SetInt("xp_total", 100);
            Assert.That(progress.IsUnlocked("vintage"), Is.True);
            storage.DeleteAll();
            Assert.That(progress.Level, Is.EqualTo(1));
            Assert.That(progress.IsUnlocked("vintage"), Is.False);
            Assert.That(progress.NextCar.Id, Is.EqualTo("speedster"));
        }

        [Test]
        public void SurvivingOrPlayingAloneDoesNotCreateAWinBonus()
        {
            var survived = progress.Record(Result(RoundOutcome.Survived, 2, 4, 0));
            Assert.That(survived.Total, Is.EqualTo(20));
            Assert.That(survived.WinXp, Is.Zero);
            var alone = progress.Record(Result(RoundOutcome.Out, 1, 1, 0));
            Assert.That(alone.Total, Is.EqualTo(10));
        }

        private static RoundResult Result(RoundOutcome outcome, int place, int cars, int knockouts) =>
            new(outcome, place, cars, 20f, knockouts, RoundResult.Nobody, FallCause.None, false, false);

        private sealed class MemoryStorage : IKeyValueStorage
        {
            private readonly Dictionary<string, string> values = new();

            public bool HasKey(string key) => values.ContainsKey(key);
            public string GetString(string key, string defaultValue = "") =>
                values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetString(string key, string value) => values[key] = value;
            public void DeleteKey(string key) => values.Remove(key);
            public void DeleteAll() => values.Clear();
        }
    }
}
