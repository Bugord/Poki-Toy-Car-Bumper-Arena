using System.Collections;
using System.Linq;
using Bugord.Core.Storage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using MemoryStorage = ToyCarArena.Tests.PlayMode.RoundOverScreenshots.MemoryStorage;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// CLAIM ×2 on the result and the commercial break between rounds: the bonus is the round's own XP and coins once
    /// more, saved with its unlocks, paid only by an ad that gave its reward, once per result, and never offered with ads
    /// blocked; the break waits for the visit's free play.
    /// </summary>
    public sealed class DoubleRewardTests
    {
        private GameTuning tuning;
        private CarCatalog catalog;
        private ArenaCatalog arenas;
        private MemoryStorage storage;
        private PlayerProgress progress;
        private PlayerWallet wallet;
        private MatchTests.RecordingPlatform platform;

        [SetUp]
        public void SetUp()
        {
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            arenas = AssetDatabase.LoadAssetAtPath<ArenaCatalog>("Assets/Data/Arenas/data_arena_catalog.asset");
            storage = new MemoryStorage();
            progress = new PlayerProgress(storage, tuning, catalog);
            wallet = new PlayerWallet(storage, tuning);
            platform = new MatchTests.RecordingPlatform();
        }

        [Test]
        public void TheBonusXpIsSavedWithTheCarsItsLevelsOpen()
        {
            var speedster = catalog.Cars.First(car => car.Id == "speedster");
            storage.SetInt("xp_total", progress.At(0).Cost + 1);
            var before = progress.TotalXp;
            var toSpeedster = TotalAt(speedster.UnlockLevel) - before;

            var bonus = progress.AddBonus(toSpeedster);

            Assert.That(bonus.TotalBefore, Is.EqualTo(before));
            Assert.That(bonus.TotalAfter, Is.EqualTo(before + toSpeedster));
            Assert.That(bonus.Total, Is.EqualTo(toSpeedster));
            Assert.That(bonus.After.Level, Is.EqualTo(speedster.UnlockLevel));
            Assert.That(bonus.UnlockedCarIds, Does.Contain("speedster"));
            Assert.That(new PlayerProgress(storage, tuning, catalog).TotalXp, Is.EqualTo(before + toSpeedster));
            // The car the bonus opens waits for the player, as after a round.
            Assert.That(progress.SelectedCar, Is.EqualTo("racer"));
        }

        [Test]
        public void TheBonusCoinsAreSaved()
        {
            wallet.SetCoins(40);
            var bonus = wallet.AddBonus(55);
            Assert.That(bonus.Before, Is.EqualTo(40));
            Assert.That(bonus.After, Is.EqualTo(95));
            Assert.That(bonus.Total, Is.EqualTo(55));
            Assert.That(new PlayerWallet(storage, tuning).Coins, Is.EqualTo(95));
        }

        [UnityTest]
        public IEnumerator AGrantedAdPaysTheRoundsOwnRewardOnceMore() => UniTask.ToCoroutine(async () => {
            platform.Rewards = true;
            var model = Result(out var xpAfterRound, out var coinsAfterRound);
            Assert.That(model.CanDouble, Is.True);
            var roundXp = model.RoundXp;
            var roundCoins = model.RoundCoins;
            Assert.That(roundXp, Is.EqualTo(model.XpGain.Total - model.XpGain.MissionXp));

            Assert.That(await model.Double(default), Is.True);
            Assert.That(model.IsDoubled, Is.True);
            // The bonus counts towards the result's one level: what passes it is lost, as the round's own XP.
            Assert.That(model.BonusXp.Total, Is.InRange(0, roundXp));
            Assert.That(model.BonusCoins.Total, Is.EqualTo(roundCoins));
            Assert.That(progress.TotalXp, Is.EqualTo(xpAfterRound + model.BonusXp.Total));
            Assert.That(wallet.Coins, Is.EqualTo(coinsAfterRound + roundCoins));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The game is held only while the ad plays.");
            Assert.That(platform.Events, Does.Contain(("ad", "double", "interact")));
            Assert.That(platform.Events, Does.Contain(("ad", "double", "granted")));

            // Once per result: a second press neither plays an ad nor pays.
            Assert.That(model.CanDouble, Is.False);
            Assert.That(await model.Double(default), Is.False);
            Assert.That(platform.RewardedBreaks, Is.EqualTo(1));
            Assert.That(progress.TotalXp, Is.EqualTo(xpAfterRound + model.BonusXp.Total));
        });

        [UnityTest]
        public IEnumerator AnAdWithoutItsRewardPaysNothingAndTakesTheOfferAway() => UniTask.ToCoroutine(async () => {
            platform.Rewards = false;
            var model = Result(out var xpAfterRound, out var coinsAfterRound);

            Assert.That(await model.Double(default), Is.False);
            Assert.That(model.IsDoubled, Is.False);
            Assert.That(model.CanDouble, Is.False);
            Assert.That(progress.TotalXp, Is.EqualTo(xpAfterRound));
            Assert.That(wallet.Coins, Is.EqualTo(coinsAfterRound));
            // Back at once: no ad played.
            Assert.That(platform.Events, Does.Contain(("ad", "double", "unavailable")));
        });

        [UnityTest]
        public IEnumerator TheMenusBoostAdTurnsTheBoostOn() => UniTask.ToCoroutine(async () => {
            platform.Rewards = true;
            var menu = Menu();
            Assert.That(menu.CanOfferBoost, Is.True);

            Assert.That(await menu.WatchBoost(default), Is.True);
            Assert.That(progress.BoostMatchesLeft, Is.EqualTo(tuning.Ads.BoostMatches));
            Assert.That(menu.IsBoosted, Is.True);
            Assert.That(menu.CanOfferBoost, Is.False, "a running boost offers no other");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The game is held only while the ad plays.");
            Assert.That(platform.Events, Does.Contain(("ad", "boost", "interact")));
            Assert.That(platform.Events, Does.Contain(("ad", "boost", "granted")));
            Assert.That(await menu.WatchBoost(default), Is.False);
            Assert.That(platform.RewardedBreaks, Is.EqualTo(1));
        });

        [UnityTest]
        public IEnumerator AMenuAdWithoutItsRewardGivesNoBoost() => UniTask.ToCoroutine(async () => {
            platform.Rewards = false;
            var menu = Menu();
            Assert.That(await menu.WatchBoost(default), Is.False);
            Assert.That(progress.IsBoosted, Is.False);
            Assert.That(platform.Events, Does.Contain(("ad", "boost", "unavailable")));
            Assert.That(menu.CanOfferBoost, Is.False, "the offer goes until the menu opens again");
            Assert.That(Menu().CanOfferBoost, Is.True);
        });

        [Test]
        public void BlockedAdsOfferNoBoostButShowARunningOne()
        {
            platform.IsAdBlocked = true;
            Assert.That(Menu().CanOfferBoost, Is.False);
            progress.StartBoost();
            Assert.That(Menu().IsBoosted, Is.True);
        }

        [Test]
        public void ABoostedRoundStampsItsXpAndSendsItsEvent()
        {
            progress.StartBoost();
            var model = Result(out _, out _);
            Assert.That(model.XpGain.BoostXp, Is.GreaterThan(0));
            Assert.That(model.RoundXp, Is.EqualTo(model.XpGain.Total - model.XpGain.MissionXp),
                "CLAIM ×2 doubles what the sticker shows, the boost's share with it");

            var result = new RoundResult(RoundOutcome.Out, 3, 4, 40f, 0, RoundResult.Nobody, FallCause.Drive, false,
                false);
            platform.MeasureMeta(2, new RoundRecord(result, 1, 2, false, false).WithXp(progress.Record(result)), 0);
            Assert.That(platform.Events, Does.Contain(("result", "boosted-r2", "xp-boost")));
            progress.SetBoost(0);
            platform.Events.Clear();
            platform.MeasureMeta(3, new RoundRecord(result, 1, 2, false, false).WithXp(progress.Record(result)), 0);
            Assert.That(platform.Events, Has.No.Member(("result", "boosted-r3", "xp-boost")));
        }

        private MenuViewModel Menu() => new(progress, wallet, new MissionTracker(storage, tuning), catalog, arenas, tuning,
            null, platform, null, new PlayerDaily(storage, tuning, wallet), new DailyTasks(storage, tuning, progress));

        [Test]
        public void BlockedAdsOfferNoDouble()
        {
            platform.IsAdBlocked = true;
            Assert.That(Result(out _, out _).CanDouble, Is.False);
        }

        [Test]
        public void AGiftLeftUnseenWaitsForTheNextResult()
        {
            var gift = wallet.TryGift(tuning.Toppers.GiftLevel);
            Assert.That(Result(out _, out _).Gift, Is.EqualTo(gift));
            // NEXT MATCH before its popup: the next result offers it again, and once shown it is gone.
            var next = Result(out _, out _);
            Assert.That(next.Gift, Is.EqualTo(gift));
            next.GiftShown();
            Assert.That(platform.Events, Does.Contain(("gift", gift, "visible")));
            Assert.That(Result(out _, out _).Gift, Is.Null);
        }

        [Test]
        public void AGiftIsNotOfferedOverAWornHat()
        {
            var gift = wallet.TryGift(tuning.Toppers.GiftLevel);
            Assert.That(wallet.Wear(gift), Is.True);
            Assert.That(Result(out _, out _).Gift, Is.Null);
        }

        [Test]
        public void TheCommercialBreakWaitsForTheFreePlay()
        {
            var breaks = new AdBreaks();
            Assert.That(breaks.IsDue(tuning.Ads.FreePlaySeconds), Is.True, "no free play: every NEXT MATCH asks");
            const float free = 180f;
            Assert.That(breaks.IsDue(free), Is.False);
            breaks.AddPlay(free - 1f);
            Assert.That(breaks.IsDue(free), Is.False);
            breaks.AddPlay(-5f);
            breaks.AddPlay(1f);
            Assert.That(breaks.IsDue(free), Is.True);
        }

        // A won round of four with a knock-out and a mission, saved as RoundState saves it.
        private RoundOverViewModel Result(out int xpAfterRound, out int coinsAfterRound)
        {
            var result = new RoundResult(RoundOutcome.Won, 1, 4, 40f, 1, RoundResult.Nobody, FallCause.Drive, false,
                false);
            var xp = progress.Record(result, null, 30);
            var coins = wallet.Record(result, false, 40);
            xpAfterRound = progress.TotalXp;
            coinsAfterRound = wallet.Coins;
            var record = new RoundRecord(result, 1, 2, false, false).WithXp(xp)
                .WithRoster(new[] { "YOU", "ZOOMER", "BONKY", "SKID" }, new[] { 1, 1, 1, 1 }, RoundResult.Nobody)
                .WithMeta(coins, null, new StreakChange(0, 1, 2));
            var model = new RoundOverViewModel(catalog, arenas, tuning, progress, null, platform, wallet, null);
            model.SetPayload(record);
            return model;
        }

        private int TotalAt(int level)
        {
            var total = 0;
            while (progress.At(total).Level < level) {
                total += progress.At(total).Cost - progress.At(total).Xp;
            }

            return total;
        }
    }
}
