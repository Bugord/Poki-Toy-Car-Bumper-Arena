using System;
using System.Collections;
using System.Linq;
using Bugord.App.Platform;
using Bugord.Core.Storage;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The daily gift (<see cref="PlayerDaily"/>): one claim a calendar day, the days in a row climbing the calendar to
    /// the exclusive hat on the last, a missed day starting it again, a clock turned back giving nothing; the hat is
    /// never for sale and shows in the garage once owned; CLAIM ×2 in the menu after a rewarded ad.
    /// </summary>
    public sealed class PlayerDailyTests
    {
        private static readonly DateTime Monday = new(2026, 11, 2, 23, 50, 0);

        private RoundOverScreenshots.MemoryStorage storage;
        private GameTuning tuning;
        private PlayerWallet wallet;
        private PlayerDaily daily;

        [SetUp]
        public void SetUp()
        {
            storage = new RoundOverScreenshots.MemoryStorage();
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            wallet = new PlayerWallet(storage, tuning);
            daily = new PlayerDaily(storage, tuning, wallet);
            DailyGift.ForgetOffer();
            RoundOverViewModel.ForgetVisit();
        }

        [TearDown]
        public void TearDown()
        {
            DailyGift.ForgetOffer();
            RoundOverViewModel.ForgetVisit();
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void TheFirstClaimIsDayOneAndTodaysGiftIsTakenOnce()
        {
            Assert.That(daily.CanClaim(Monday), Is.True);
            Assert.That(daily.Slot(Monday), Is.EqualTo(0));

            var claim = daily.Claim(Monday);

            Assert.That(claim.Streak, Is.EqualTo(1));
            Assert.That(claim.Coins, Is.EqualTo(tuning.Daily.CoinsOf(0)));
            Assert.That(claim.IsReset, Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(claim.Coins));
            Assert.That(daily.CanClaim(Monday.AddMinutes(5)), Is.False);
            Assert.That(daily.Claim(Monday.AddMinutes(5)), Is.Null);
            Assert.That(wallet.Coins, Is.EqualTo(claim.Coins));
        }

        [Test]
        public void TheNextCalendarDayClimbsAndTheTimerRunsToMidnight()
        {
            daily.Claim(Monday);

            // Ten minutes later it is Tuesday: a calendar day, not 24 hours.
            Assert.That(daily.TimeToNext(Monday), Is.EqualTo(TimeSpan.FromMinutes(10)));
            var tuesday = Monday.AddMinutes(20);
            Assert.That(daily.TimeToNext(tuesday), Is.EqualTo(TimeSpan.Zero));
            Assert.That(daily.Streak(tuesday), Is.EqualTo(2));
            Assert.That(daily.NextSlot(tuesday), Is.EqualTo(2));
            var claim = daily.Claim(tuesday);

            Assert.That(claim.Streak, Is.EqualTo(2));
            Assert.That(claim.Slot, Is.EqualTo(1));
            Assert.That(claim.Coins, Is.EqualTo(tuning.Daily.CoinsOf(1)));
        }

        [Test]
        public void AMissedDayStartsTheCalendarAgain()
        {
            daily.Claim(Monday);
            daily.Claim(Monday.AddDays(1));

            var later = Monday.AddDays(3);
            Assert.That(daily.IsBroken(later), Is.True);
            Assert.That(daily.Streak(later), Is.EqualTo(1));
            var claim = daily.Claim(later);

            Assert.That(claim.Streak, Is.EqualTo(1));
            Assert.That(claim.Slot, Is.EqualTo(0));
            Assert.That(claim.IsReset, Is.True);
        }

        [Test]
        public void AClockTurnedBackGivesNothingAndKeepsTheStreak()
        {
            daily.Claim(Monday);
            daily.Claim(Monday.AddDays(1));

            var earlier = Monday.AddDays(-3);
            Assert.That(daily.CanClaim(earlier), Is.False);
            Assert.That(daily.Claim(earlier), Is.Null);
            Assert.That(daily.Streak(earlier), Is.EqualTo(2));
            Assert.That(daily.Claim(Monday.AddDays(2)).Streak, Is.EqualTo(3));
        }

        [Test]
        public void TheLastDayGivesTheHatAndTheNextWeekCoins()
        {
            var days = tuning.Daily.Days;
            for (var day = 0; day < days - 1; day++) {
                Assert.That(daily.Claim(Monday.AddDays(day)).Hat, Is.Null);
            }

            var hatDay = Monday.AddDays(days - 1);
            Assert.That(daily.HatOf(daily.Slot(hatDay)), Is.EqualTo(tuning.Daily.Hat));
            var before = wallet.Coins;
            var hat = daily.Claim(hatDay, 2);

            Assert.That(hat.Hat, Is.EqualTo(tuning.Daily.Hat));
            Assert.That(hat.Coins, Is.EqualTo(0));
            Assert.That(wallet.Coins, Is.EqualTo(before));
            Assert.That(wallet.IsOwned(tuning.Daily.Hat), Is.True);
            Assert.That(wallet.SelectedTopper, Is.EqualTo(tuning.Daily.Hat));

            // The calendar goes round: day 1 again, and the hat's day pays coins to its owner.
            Assert.That(daily.Claim(Monday.AddDays(days)).Slot, Is.EqualTo(0));
            for (var day = days + 1; day < 2 * days - 1; day++) {
                daily.Claim(Monday.AddDays(day));
            }

            var again = daily.Claim(Monday.AddDays(2 * days - 1));
            Assert.That(again.Slot, Is.EqualTo(days - 1));
            Assert.That(again.Hat, Is.Null);
            Assert.That(again.Coins, Is.EqualTo(tuning.Daily.HatCoins));
        }

        [Test]
        public void TheMultiplierDoublesTheCoins()
        {
            var claim = daily.Claim(Monday, 2);

            Assert.That(claim.Coins, Is.EqualTo(2 * tuning.Daily.CoinsOf(0)));
            Assert.That(claim.CoinsAfter - claim.CoinsBefore, Is.EqualTo(claim.Coins));
        }

        [Test]
        public void ADisabledGiftIsNeverDue()
        {
            var field = typeof(GameTuning.DailySettings).GetField("enabled",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            field.SetValue(tuning.Daily, false);

            Assert.That(daily.CanClaim(Monday), Is.False);
            Assert.That(daily.Claim(Monday), Is.Null);
        }

        [Test]
        public void TheExclusiveHatIsNotForSaleButCanBeGiven()
        {
            var hat = tuning.Daily.Hat;
            wallet.SetCoins(10000);

            Assert.That(tuning.Toppers.All.Any(t => t.Id == hat), Is.False);
            Assert.That(tuning.Toppers.Find(hat), Is.Not.Null);
            Assert.That(CarTopper.Ids, Does.Not.Contain(hat));
            Assert.That(CarTopper.ExclusiveIds, Does.Contain(hat));
            Assert.That(wallet.Buy(hat), Is.False);
            Assert.That(wallet.Coins, Is.EqualTo(10000));
            Assert.That(GarageViewModel.CanBuyAnyTopper(new PlayerWallet(new RoundOverScreenshots.MemoryStorage(), tuning),
                tuning), Is.False);

            Assert.That(wallet.Give(hat), Is.True);
            Assert.That(wallet.Give(hat), Is.False);
            Assert.That(wallet.IsOwned(hat), Is.True);
            Assert.That(wallet.SelectedTopper, Is.EqualTo(hat));
        }

        [Test]
        public void TheGarageShowsTheExclusiveHatOnlyOnceOwned()
        {
            var garage = new GarageViewModel(null, null, new MatchTests.RecordingPlatform(), null, wallet, tuning, null);
            var hat = tuning.Daily.Hat;

            Assert.That(garage.Toppers.Any(t => t.Id == hat), Is.False);
            wallet.Give(hat);
            Assert.That(garage.Toppers.Last().Id, Is.EqualTo(hat));
        }

        [Test]
        public void ClaimEventsNameTheDayAndKeepWithinPokisLimit()
        {
            var platform = new MatchTests.RecordingPlatform();
            var days = tuning.Daily.Days;
            for (var day = 0; day < days; day++) {
                platform.MeasureDailyClaimed(daily.Claim(Monday.AddDays(day)));
            }

            platform.MeasureDailyClaimed(daily.Claim(Monday.AddDays(days + 3)));
            platform.MeasureDailyShown();
            platform.MeasureDailyAdShown();
            platform.MeasureDailyAdPressed();
            platform.MeasureDailyAdEnded("granted");

            var names = platform.Events.Select(e => $"{e.Item1} / {e.Item2} / {e.Item3}").ToList();
            Assert.That(names, Does.Contain("daily / day-1 / claim"));
            Assert.That(names, Does.Contain($"daily / day-{days} / claim"));
            Assert.That(names, Does.Contain("daily / hat / claim"));
            Assert.That(names, Does.Contain("daily / reset / claim"));
            Assert.That(names, Does.Contain("daily / offer / visible"));
            Assert.That(names, Does.Contain("ad / daily / visible"));
            Assert.That(names, Does.Contain("ad / daily / interact"));
            Assert.That(names, Does.Contain("ad / daily / granted"));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        [UnityTest]
        public IEnumerator ClaimTwiceInTheMenuDoublesTheCoinsAfterTheAd() => UniTask.ToCoroutine(async () => {
            var platform = new MatchTests.RecordingPlatform { Rewards = true };
            var menu = Menu(platform);

            Assert.That(menu.CanDoubleDaily, Is.True);
            var claim = await menu.WatchDaily(default);

            Assert.That(claim, Is.Not.Null);
            Assert.That(claim.Coins, Is.EqualTo(tuning.Daily.AdMultiplier * tuning.Daily.CoinsOf(0)));
            Assert.That(menu.CanClaimDaily, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        });

        [UnityTest]
        public IEnumerator AnAdWithoutItsRewardLeavesTheGiftToClaim() => UniTask.ToCoroutine(async () => {
            var platform = new MatchTests.RecordingPlatform { Rewards = false };
            var menu = Menu(platform);

            Assert.That(await menu.WatchDaily(default), Is.Null);
            Assert.That(menu.CanClaimDaily, Is.True);
            Assert.That(menu.CanDoubleDaily, Is.False);
            Assert.That(platform.Events, Does.Contain(("ad", "daily", "unavailable")));

            var claim = menu.ClaimDaily();
            Assert.That(claim.Coins, Is.EqualTo(tuning.Daily.CoinsOf(0)));
        });

        [Test]
        public void NoAdsMeansNoClaimTwiceAndTheHatDayHasNone()
        {
            var blocked = Menu(new MatchTests.RecordingPlatform { IsAdBlocked = true });
            Assert.That(blocked.CanDoubleDaily, Is.False);

            daily.SetLastClaim(Monday, 1, tuning.Daily.Days - 1);
            var menu = Menu(new MatchTests.RecordingPlatform());
            Assert.That(menu.DailyHatOf(menu.DailySlot), Is.EqualTo(tuning.Daily.Hat));
            Assert.That(menu.CanDoubleDaily, Is.False);
        }

        [Test]
        public void AVisitsFirstResultOffersTheGiftOnce()
        {
            var platform = new MatchTests.RecordingPlatform();
            var first = Result(platform);

            Assert.That(first.TakeDailyOffer(), Is.True, "a first visit's first result, with no menu before it");
            first.DailyShown();
            first.DailyDeclined();
            Assert.That(Result(platform).TakeDailyOffer(), Is.False, "a later result of the same visit");
            Assert.That(platform.Events, Does.Contain(("daily", "offer", "visible")));
            Assert.That(platform.Events, Does.Contain(("daily", "offer-result", "visible")));
            Assert.That(platform.Events, Does.Contain(("result", "daily-no", "interact")));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        [Test]
        public void AResultAfterTheMenusOfferDoesNotAskAgain()
        {
            Assert.That(Menu(new MatchTests.RecordingPlatform()).TakeDailyOffer(), Is.True);
            Assert.That(Result(new MatchTests.RecordingPlatform()).TakeDailyOffer(), Is.False);
        }

        [Test]
        public void AResultOffersNothingWithTodaysGiftTakenOrTheTuningOff()
        {
            daily.Claim(Monday);
            Assert.That(Result(new MatchTests.RecordingPlatform()).TakeDailyOffer(), Is.False, "taken today");

            var tomorrow = Result(new MatchTests.RecordingPlatform());
            tomorrow.Daily.Clock = () => Monday.AddDays(1);
            typeof(GameTuning.DailySettings).GetField("onResult",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(tuning.Daily, false);
            Assert.That(tomorrow.TakeDailyOffer(), Is.False, "the tuning keeps it to the menu");
            Assert.That(Result(new MatchTests.RecordingPlatform(), withDaily: false).TakeDailyOffer(), Is.False);
        }

        [Test]
        public void ClaimingOverTheResultPaysAsTheMenuDoes()
        {
            var result = Result(new MatchTests.RecordingPlatform());
            Assert.That(result.TakeDailyOffer(), Is.True);

            var claim = result.Daily.Claim();
            Assert.That(claim.Coins, Is.EqualTo(tuning.Daily.CoinsOf(0)));
            Assert.That(result.CoinsNow, Is.EqualTo(claim.CoinsAfter));
            Assert.That(result.Daily.CanClaim, Is.False);
        }

        [Test]
        public void TheDailyGiftWaitsForAQuietResult()
        {
            var car = Payload(3, 4, "speedster");
            Assert.That(car.TakeDailyOffer(), Is.False, "a result that opens a car keeps to its popup");
            car.PopupShown();
            Assert.That(Payload(4, 5).TakeDailyOffer(), Is.False, "the result after a popup shows none that can wait");
            Assert.That(Payload(9, 10).TakeDailyOffer(), Is.False, "a result that opens a league keeps to NEW LEAGUE!");
            Assert.That(Payload(5, 6).TakeDailyOffer(), Is.True, "a quiet result after a quiet one");
        }

        [Test]
        public void TheFreeHatComesFirstAndTakesTheResultsRoom()
        {
            var gift = wallet.TryGift(tuning.Toppers.GiftLevel);
            var car = Payload(2, 3, "vintage");
            Assert.That(car.Gift, Is.EqualTo(gift));
            Assert.That(car.IsGiftDue, Is.False);
            car.PopupShown();
            Assert.That(Payload(3, 4).IsGiftDue, Is.False);

            var quiet = Payload(4, 5);
            Assert.That(quiet.IsGiftDue, Is.True, "kept owned and unseen until a quiet result");
            Assert.That(quiet.TakeDailyOffer(), Is.False, "the hat goes before the daily gift");
            quiet.GiftShown();
            Assert.That(quiet.HatOffer(), Is.Null, "one waiting popup a result");
            quiet.PopupShown();

            var after = Payload(5, 6);
            Assert.That(after.Gift, Is.Null);
            Assert.That(after.TakeDailyOffer(), Is.False);
            Assert.That(Payload(6, 7).TakeDailyOffer(), Is.True);
        }

        [Test]
        public void TheHatOfferWaitsBehindTheDailyGift()
        {
            storage.SetInt("xp_total", 1250);
            storage.SetInt("coins", 500);
            storage.SetString("toppers_owned", "top-hat");

            Assert.That(Payload(9, 10, "hot-rod").HatOffer(), Is.Null, "not beside a car's popup");
            var quiet = Payload(11, 12);
            Assert.That(quiet.HatOffer(), Is.Not.Null);
            Assert.That(quiet.TakeDailyOffer(), Is.True);
            quiet.DailyShown();
            Assert.That(quiet.HatOffer(), Is.Null, "the daily gift took the result's room");
        }

        // A result whose reward climbed from one level to the next, opening the given cars.
        private RoundOverViewModel Payload(int from, int to, params string[] cars)
        {
            var model = Result(new MatchTests.RecordingPlatform());
            var result = new RoundResult(RoundOutcome.Won, 1, 4, 40f, 0, RoundResult.Nobody, FallCause.Drive, false,
                false);
            var xp = new XpGain(0, 0, 10, 0, 0, 0, new XpLevel(from, 0, 50), new XpLevel(to, 0, 60), cars);
            model.SetPayload(new RoundRecord(result, 1, 2, false, false).WithXp(xp));
            return model;
        }

        private RoundOverViewModel Result(IGamePlatform platform, bool withDaily = true)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            var arenas = AssetDatabase.LoadAssetAtPath<ToyCarArena.Gameplay.Arena.ArenaCatalog>(
                "Assets/Data/Arenas/data_arena_catalog.asset");
            var model = new RoundOverViewModel(catalog, arenas, tuning, new PlayerProgress(storage, tuning, catalog),
                null, platform, wallet, null, withDaily ? daily : null);
            if (model.Daily != null) {
                model.Daily.Clock = () => Monday;
            }

            return model;
        }

        private MenuViewModel Menu(IGamePlatform platform)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            return new MenuViewModel(new PlayerProgress(storage, tuning, catalog), wallet,
                new MissionTracker(storage, tuning), catalog, null, tuning, null, platform, null, daily) {
                Clock = () => Monday,
            };
        }
    }
}
