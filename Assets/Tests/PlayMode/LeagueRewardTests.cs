using System.Linq;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEditor;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The leagues' prizes: each league past the first gives a car and a hat that the level alone opens, which no coins
    /// buy and no gift hands out; the real catalog and tuning agree on them.
    /// </summary>
    public sealed class LeagueRewardTests
    {
        private GameTuning tuning;
        private RoundOverScreenshots.MemoryStorage storage;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            storage = new RoundOverScreenshots.MemoryStorage();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void ALeaguesHatIsTheirsFromItsLevelAndNeverForCoinsOrAsAGift()
        {
            var player = new Leveled { Level = 9 };
            var wallet = new PlayerWallet(storage, tuning, player);
            wallet.SetCoins(100000);
            Assert.That(wallet.IsOwned("ember"), Is.False);
            Assert.That(wallet.Buy("ember"), Is.False, "Not for sale.");
            Assert.That(wallet.Give("ember"), Is.False, "Not a gift either.");
            Assert.That(wallet.Wear("ember"), Is.False);
            Assert.That(wallet.HasAnyTopper, Is.False);
            Assert.That(wallet.AffordableTopper(), Is.Not.EqualTo("ember"));
            Assert.That(wallet.Coins, Is.EqualTo(100000));

            player.Level = 10;
            Assert.That(wallet.IsOwned("ember"), Is.True);
            Assert.That(wallet.IsOwned("silver-torch"), Is.False);
            Assert.That(wallet.HasAnyTopper, Is.True);
            Assert.That(wallet.Wear("ember"), Is.True);
            Assert.That(wallet.SelectedTopper, Is.EqualTo("ember"));

            player.Level = 100;
            foreach (var id in CarTopper.LeagueIds) {
                Assert.That(wallet.IsOwned(id), Is.True, id);
            }
        }

        [Test]
        public void ALeaguesHatUnlockIsMeasuredOnTheResultThatReachesIt()
        {
            var catalog = ScriptableObject.CreateInstance<CarCatalog>();
            JsonUtility.FromJsonOverwrite("{\"cars\":[{\"id\":\"racer\"}]}", catalog);
            try {
                var progress = new PlayerProgress(storage, tuning, catalog);
                var platform = new MatchTests.RecordingPlatform();
                while (progress.Level < 26) {
                    platform.MeasureMapUnlocks(progress.AddBonus(1000), tuning);
                }

                var hats = platform.Events.Where(e => e.Item1 == "progress" && e.Item2.StartsWith("hat-"))
                    .Select(e => e.Item2).ToArray();
                Assert.That(hats, Is.EqualTo(new[] { "hat-ember", "hat-silver-torch" }));
                Assert.That(platform.Events.Where(e => e.Item2.StartsWith("hat-")).All(e => e.Item3 == "unlock"));
            }
            finally {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TheCatalogAndTheTuningAgreeOnEveryLeaguesPrize()
        {
            var real = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            Assert.That(real, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
            var leagues = real.LeagueRewards.All;
            Assert.That(leagues.Select(league => league.Level), Is.EqualTo(new[] { 10, 25, 50, 100 }),
                "The leagues past ROOKIE.");
            Assert.That(leagues.Select(league => league.Topper), Is.EqualTo(CarTopper.LeagueIds));
            foreach (var league in leagues) {
                var car = catalog.Find(league.Car);
                Assert.That(car, Is.Not.Null, league.Car);
                Assert.That(car.Id, Is.EqualTo(league.Car), "Find falls back to the first car; the id must be there.");
                Assert.That(car.UnlockLevel, Is.EqualTo(league.Level), league.Car);
                Assert.That(car.Model, Is.Not.Null, league.Car);
                Assert.That(real.Coins.PriceOf(league.Car), Is.Zero, $"{league.Car} is a prize, not for sale.");
                Assert.That(real.Toppers.IsLeague(league.Topper), Is.True, league.Topper);
                Assert.That(real.Toppers.IsOnSale(league.Topper), Is.False, league.Topper);
                Assert.That(CarTopper.Ids, Does.Not.Contain(league.Topper), "Bots wear it only from its league.");
                Assert.That(league.Car.Any(char.IsDigit) || league.Topper.Any(char.IsDigit), Is.False,
                    "Analytics ids carry no digits.");
            }
        }

        private sealed class Leveled : IPlayerCar
        {
            public string SelectedCar => "racer";
            public int SelectedColor => 0;
            public int Level { get; set; } = 1;
#pragma warning disable CS0067
            public event System.Action<int> ColorChanged;
#pragma warning restore CS0067

            public bool SelectColor(int id) => false;
        }
    }
}
