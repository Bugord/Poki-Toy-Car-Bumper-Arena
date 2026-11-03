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
            Assert.That(reloaded.SelectedCar, Is.EqualTo("racer"), "The speedster has a price: it waits to be bought.");
        }

        [Test]
        public void AnOpenCarWithAPriceIsBoughtWithCoinsAndDrivesNext()
        {
            var wallet = new PlayerWallet(storage, tuning);
            var price = tuning.Coins.PriceOf("speedster");
            Assert.That(price, Is.GreaterThan(0));
            Assert.That(progress.IsForSale("speedster"), Is.False, "Not before its level.");
            progress.Record(Result(RoundOutcome.Won, 1, 4, 2));
            Assert.That(progress.IsUnlocked("speedster"), Is.True);
            Assert.That(progress.IsOwned("speedster"), Is.False);
            Assert.That(progress.IsForSale("speedster"), Is.True);
            Assert.That(progress.Select("speedster"), Is.False);

            wallet.SetCoins(price - 1);
            Assert.That(progress.Buy("speedster", wallet), Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(price - 1));

            wallet.SetCoins(price + 7);
            Assert.That(progress.Buy("speedster", wallet), Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(7));
            Assert.That(progress.SelectedCar, Is.EqualTo("speedster"));
            Assert.That(progress.Buy("speedster", wallet), Is.False, "Bought once.");
            var reloaded = new PlayerProgress(storage, tuning, catalog);
            Assert.That(reloaded.IsOwned("speedster"), Is.True);
            Assert.That(reloaded.SelectedCar, Is.EqualTo("speedster"));
        }

        [Test]
        public void PlayersFromBeforeThePricesKeepTheCarsTheirLevelGave()
        {
            storage.DeleteAll();
            storage.SetInt("xp_total", 100);
            var reloaded = new PlayerProgress(storage, tuning, catalog);
            Assert.That(reloaded.IsOwned("speedster"), Is.True);
            Assert.That(reloaded.IsForSale("speedster"), Is.False);
        }

        [Test]
        public void TheFirstRoundPaysForTheFirstPaidCarWithCoinsToSpare()
        {
            // The weakest first round: last place, no knock-outs, only the first-round mission done.
            var coins = tuning.Coins.RoundCoins + tuning.Missions.CoinsOf(MissionTier.Easy);
            var xp = tuning.Progress.RoundXp + tuning.Missions.XpOf(MissionTier.Easy);
            Assert.That(xp, Is.GreaterThanOrEqualTo(tuning.Progress.LevelXp[0]), "The first round reaches level 2.");
            Assert.That(coins, Is.GreaterThan(tuning.Coins.PriceOf("speedster")), "Coins are left after buying it.");
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
            Assert.That(progress.SelectedCar, Is.EqualTo("racer"), "A new car waits for the player to equip it.");
            Assert.That(progress.Record(Result(RoundOutcome.Out, 4, 4, 0)).UnlockedCarIds, Is.Empty);
            Assert.That(progress.Select("vintage"), Is.True);
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
                using var roster = new SessionRoster(catalog, new FirstPlayerCar(catalog));
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

        [Test]
        public void ColorDefaultsToGreenWithoutSavingAnything()
        {
            Assert.That(progress.SelectedColor, Is.Zero);
            Assert.That(storage.HasKey("car_color"), Is.False);
        }

        [TestCase(-1)]
        [TestCase(8)]
        [TestCase(int.MaxValue)]
        public void InvalidStoredColorFallsBackToGreen(int id)
        {
            storage.SetInt("car_color", id);
            Assert.That(new PlayerProgress(storage, tuning, catalog).SelectedColor, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void EveryColorIsFreeAndPersistsAtLevelOne(int id)
        {
            progress.SelectColor((id + 1) % CarPaint.Count);
            Assert.That(progress.SelectColor(id), Is.True);
            Assert.That(storage.GetInt("car_color"), Is.EqualTo(id));
            Assert.That(new PlayerProgress(storage, tuning, catalog).SelectedColor, Is.EqualTo(id));
            Assert.That(progress.TotalXp, Is.Zero);
            Assert.That(progress.SelectedCar, Is.EqualTo("racer"));
        }

        [Test]
        public void InvalidOrRepeatedColorDoesNotSaveOrNotify()
        {
            var changes = new List<int>();
            progress.ColorChanged += changes.Add;
            Assert.That(progress.SelectColor(0), Is.False);
            Assert.That(progress.SelectColor(-1), Is.False);
            Assert.That(progress.SelectColor(8), Is.False);
            Assert.That(storage.HasKey("car_color"), Is.False);
            Assert.That(changes, Is.Empty);
            Assert.That(progress.SelectColor(4), Is.True);
            Assert.That(progress.SelectColor(4), Is.False);
            Assert.That(progress.SelectColor(8), Is.False);
            Assert.That(changes, Is.EqualTo(new[] { 4 }));
            Assert.That(storage.GetInt("car_color"), Is.EqualTo(4));
            progress.Record(Result(RoundOutcome.Won, 1, 4, 3));
            Assert.That(progress.SelectedColor, Is.EqualTo(4), "A round that opens cars keeps the colour.");
        }

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
