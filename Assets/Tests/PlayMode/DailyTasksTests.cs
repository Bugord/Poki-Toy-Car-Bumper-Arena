using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The day's three tasks (<see cref="DailyTasks"/>) and the first win of the day (<see cref="PlayerDaily"/>): one
    /// set a calendar day, the same all day and new at midnight, never a boost task in a build without boosts, a task
    /// paying once; the first win doubles one won round a day, and its result is not held to one level.
    /// </summary>
    public sealed class DailyTasksTests
    {
        private static readonly DateTime Monday = new(2026, 11, 2, 23, 50, 0);

        private RoundOverScreenshots.MemoryStorage storage;
        private GameTuning tuning;
        private CarCatalog catalog;
        private PlayerProgress progress;
        private PlayerWallet wallet;
        private PlayerDaily daily;
        private DailyTasks tasks;

        [SetUp]
        public void SetUp()
        {
            storage = new RoundOverScreenshots.MemoryStorage();
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            catalog = AssetDatabase.LoadAssetAtPath<CarCatalog>("Assets/Data/Cars/data_car_catalog.asset");
            progress = new PlayerProgress(storage, tuning, catalog);
            wallet = new PlayerWallet(storage, tuning);
            daily = new PlayerDaily(storage, tuning, wallet);
            tasks = new DailyTasks(storage, tuning, progress);
        }

        [TearDown]
        public void TearDown()
        {
            EventSchedule.Forced = null;
            Object.DestroyImmediate(tuning);
        }

        [Test]
        public void TheDaySetIsThreeTasksOfEachTierAndStaysAllDay()
        {
            var today = tasks.Today(Monday.AddHours(-20));

            Assert.That(today.Count, Is.EqualTo(DailyTasks.Count));
            Assert.That(today.Select(task => task.Definition.Tier),
                Is.EqualTo(new[] { MissionTier.Easy, MissionTier.Medium, MissionTier.Hard }));
            Assert.That(today.All(task => task.Progress == 0), Is.True);

            tasks.Record(Stats(RoundOutcome.Out, 1), Monday.AddHours(-20));
            var ids = today.Select(task => task.Definition.Id).ToArray();
            var progressOf = today.Select(task => task.Progress).ToArray();

            // A later visit the same day reads the same set and progress back.
            var again = new DailyTasks(storage, tuning, progress).Today(Monday);
            Assert.That(again.Select(task => task.Definition.Id), Is.EqualTo(ids));
            Assert.That(again.Select(task => task.Progress), Is.EqualTo(progressOf));
        }

        [Test]
        public void MidnightDrawsANewSetFromNothing()
        {
            tasks.Record(Stats(RoundOutcome.Won, 4), Monday);
            Assert.That(tasks.Today(Monday).Any(task => task.Progress > 0), Is.True);

            var tuesday = Monday.AddMinutes(20);
            Assert.That(tasks.Today(tuesday).All(task => task.Progress == 0), Is.True);
            Assert.That(DailyTasks.TimeToNext(Monday), Is.EqualTo(TimeSpan.FromMinutes(10)));

            // The sets change from day to day.
            var sets = new HashSet<string>();
            for (var day = 0; day < 10; day++) {
                sets.Add(string.Join(",", tasks.Today(Monday.AddDays(day)).Select(task => task.Definition.Id)));
            }

            Assert.That(sets.Count, Is.GreaterThan(1));
        }

        [Test]
        public void ADayNeverCountsOneThingTwiceNorBoostsWithoutThem()
        {
            BoostEnabled.SetValue(tuning.Boost, false);
            for (var day = 0; day < 60; day++) {
                var today = tasks.Today(Monday.AddDays(day));
                var counters = today.Select(task => task.Definition.Counter).ToArray();
                Assert.That(counters.Distinct().Count(), Is.EqualTo(DailyTasks.Count), $"day {day}");
                Assert.That(counters, Has.No.Member(MissionCounter.Boosts), $"day {day}");
                // A level 1 player has only the first arena: no arena task.
                Assert.That(counters, Has.No.Member(MissionCounter.ArenaRounds), $"day {day}");
            }
        }

        [Test]
        public void ATaskDonePaysItsRewardOnce()
        {
            var today = tasks.Today(Monday);
            var xp = today.Sum(task => tasks.XpOf(task.Definition));
            var coins = today.Sum(task => tasks.CoinsOf(task.Definition));
            var paidXp = 0;
            var paidCoins = 0;
            var completed = new List<string>();
            for (var round = 0; round < 30 && tasks.DoneCount(Monday) < DailyTasks.Count; round++) {
                // Rounds of the mode of the day, for its task.
                var stats = Stats(RoundOutcome.Won, 5, TodaysEvent(Monday));
                // The HUD's live count agrees with what the round then pays.
                var live = tasks.CountDone(stats, Monday);
                var record = tasks.Record(stats, Monday);
                Assert.That(record.Completed.Count, Is.EqualTo(live));
                paidXp += record.Xp;
                paidCoins += record.Coins;
                completed.AddRange(record.Completed.Select(task => task.Id));
            }

            Assert.That(tasks.DoneCount(Monday), Is.EqualTo(DailyTasks.Count));
            Assert.That(completed, Is.Unique);
            Assert.That(paidXp, Is.EqualTo(xp));
            Assert.That(paidCoins, Is.EqualTo(coins));
            var after = tasks.Record(Stats(RoundOutcome.Won, 5), Monday);
            Assert.That(after.Xp + after.Coins, Is.Zero);
            Assert.That(tasks.CountDone(Stats(RoundOutcome.Won, 5), Monday), Is.Zero);
        }

        [Test]
        public void ARoundTaskStartsAgainEveryRound()
        {
            storage.SetString("daily_tasks", $"{CalendarDay.Of(Monday)}|rounds:0|win:0|round-knockouts:0");
            Assert.That(tasks.Today(Monday)[2].Definition.Id, Is.EqualTo("round-knockouts"));

            tasks.Record(Stats(RoundOutcome.Out, 2), Monday);
            Assert.That(tasks.Today(Monday)[2].Progress, Is.Zero);
            Assert.That(tasks.Today(Monday)[0].Progress, Is.EqualTo(1));

            var record = tasks.Record(Stats(RoundOutcome.Won, 3), Monday);
            Assert.That(record.Completed.Select(task => task.Id), Is.EquivalentTo(new[] { "win", "round-knockouts" }));
            Assert.That(record.Xp, Is.EqualTo(tuning.Daily.TaskXpOf(MissionTier.Medium) +
                tuning.Daily.TaskXpOf(MissionTier.Hard)));
            Assert.That(record.Coins, Is.EqualTo(tuning.Daily.TaskCoinsOf(MissionTier.Medium) +
                tuning.Daily.TaskCoinsOf(MissionTier.Hard)));
            Assert.That(record.DoneToday, Is.EqualTo(2));
        }

        [Test]
        public void TheFirstWinIsOneWonRoundADay()
        {
            Assert.That(daily.HasFirstWin(Monday), Is.True);
            Assert.That(daily.TakeFirstWin(Monday, Result(RoundOutcome.Out)), Is.EqualTo(1));
            Assert.That(daily.TakeFirstWin(Monday, Result(RoundOutcome.Survived)), Is.EqualTo(1));
            Assert.That(daily.HasFirstWin(Monday), Is.True);

            Assert.That(daily.TakeFirstWin(Monday, Result(RoundOutcome.Won)), Is.EqualTo(2));
            Assert.That(daily.HasFirstWin(Monday.AddMinutes(5)), Is.False);
            Assert.That(daily.TakeFirstWin(Monday.AddMinutes(5), Result(RoundOutcome.Won)), Is.EqualTo(1));

            // After midnight it is there again; a clock turned back gives nothing.
            Assert.That(daily.TakeFirstWin(Monday.AddDays(-3), Result(RoundOutcome.Won)), Is.EqualTo(1));
            Assert.That(daily.TakeFirstWin(Monday.AddMinutes(15), Result(RoundOutcome.Won)), Is.EqualTo(2));
        }

        [Test]
        public void TheFirstWinDoublesTheRoundsOwnXpAndCoins()
        {
            var plainStorage = new RoundOverScreenshots.MemoryStorage();
            var start = LevelStart(12);
            plainStorage.SetInt("xp_total", start);
            storage.SetInt("xp_total", start);
            var result = Result(RoundOutcome.Won);

            var plain = new PlayerProgress(plainStorage, tuning, catalog).Record(result, null, 30);
            var doubled = progress.Record(result, null, 30, 2);
            var own = plain.Total - plain.MissionXp;
            Assert.That(doubled.FirstWinXp, Is.EqualTo(own));
            Assert.That(doubled.Total, Is.EqualTo(plain.Total + own));
            Assert.That(doubled.TotalAfter - doubled.TotalBefore, Is.EqualTo(doubled.Total));

            var plainCoins = new PlayerWallet(plainStorage, tuning).Record(result, false, 40);
            var doubledCoins = wallet.Record(result, false, 40, 2);
            Assert.That(doubledCoins.FirstWinCoins, Is.EqualTo(plainCoins.Total - 40));
            Assert.That(doubledCoins.Total, Is.EqualTo(plainCoins.Total * 2 - 40));
        }

        [Test]
        public void TheFirstWinKeepsAllItEarnedAndStacksWithTheBoost()
        {
            // A big win at the start: one level's hold would cut it, the first win keeps all of it.
            var result = new RoundResult(RoundOutcome.Won, 1, 7, 40f, 6, RoundResult.Nobody, FallCause.None, false,
                false);
            var plain = new PlayerProgress(new RoundOverScreenshots.MemoryStorage(), tuning, catalog).Record(result);
            var doubled = progress.Record(result, null, 0, 2);
            Assert.That(doubled.TotalAfter, Is.EqualTo(doubled.Total));
            Assert.That(doubled.TotalAfter, Is.GreaterThan(plain.TotalAfter));
            Assert.That(doubled.After.Level - doubled.Before.Level, Is.GreaterThan(1));

            // Under the menu's XP boost the first win doubles the boosted round again.
            var boosted = new RoundOverScreenshots.MemoryStorage();
            boosted.SetInt("xp_boost", 2);
            var both = new PlayerProgress(boosted, tuning, catalog).Record(result, null, 0, 2);
            var own = both.RoundXp + both.PlaceXp + both.WinXp + both.KnockoutXp;
            Assert.That(both.BoostXp, Is.EqualTo(own * (tuning.Ads.BoostMultiplier - 1)));
            Assert.That(both.FirstWinXp, Is.EqualTo(own + both.BoostXp));
            Assert.That(both.Total, Is.EqualTo(own * tuning.Ads.BoostMultiplier * 2));
        }

        [Test]
        public void TheEasyTaskIsAlwaysToPlayTheModeOfTheDay()
        {
            for (var day = 0; day < 30; day++) {
                var now = Monday.AddDays(day);
                var easy = tasks.Today(now)[0].Definition;
                Assert.That(easy.Id, Is.EqualTo(DailyTasks.EventTaskId), $"day {day}");
                Assert.That(easy.Counter, Is.EqualTo(MissionCounter.EventRounds));
                Assert.That(easy.Tier, Is.EqualTo(MissionTier.Easy));
                Assert.That(easy.Target, Is.EqualTo(1));
                Assert.That(easy.Mode, Is.EqualTo(TodaysEvent(now)));
                Assert.That(tasks.Today(now).Skip(1).Select(task => task.Definition.Tier),
                    Is.EqualTo(new[] { MissionTier.Medium, MissionTier.Hard }));
            }

            // The task follows the mode of the day: one of the build's (HOT POTATO, ITEM FRENZY), or another one played
            // that day.
            Assert.That(TodaysEvent(Monday), Is.EqualTo("hot-potato").Or.EqualTo("item-frenzy"));
            EventSchedule.Forced = EventMode.Crown;
            tasks.Reset(Monday);
            Assert.That(tasks.Today(Monday)[0].Definition.Mode, Is.EqualTo("crown"));
        }

        [Test]
        public void OnlyARoundOfTheModeOfTheDayPlayedToTheEndCountsIt()
        {
            var mode = TodaysEvent(Monday);
            Assert.That(Played(tasks.Record(Stats(RoundOutcome.Out, 0), Monday)), Is.False, "A plain round.");
            Assert.That(Played(tasks.Record(Stats(RoundOutcome.Out, 0, "crown"), Monday)), Is.False, "Another mode.");
            var left = new MissionStats(false, RoundOutcome.Out, 3, 0, 0, false, 0, 0, 0, "sandbox", mode);
            Assert.That(tasks.CountDone(left, Monday), Is.Zero, "A round left in the middle.");
            Assert.That(tasks.Today(Monday)[0].IsDone, Is.False);

            var stats = Stats(RoundOutcome.Out, 0, mode);
            Assert.That(tasks.CountDone(stats, Monday), Is.GreaterThanOrEqualTo(1), "The HUD's DAILY TASK! count.");
            var record = tasks.Record(stats, Monday);
            Assert.That(Played(record), Is.True);
            Assert.That(record.Xp, Is.EqualTo(record.Completed.Sum(tasks.XpOf)));
            Assert.That(record.Coins, Is.EqualTo(record.Completed.Sum(tasks.CoinsOf)));
            Assert.That(tasks.XpOf(tasks.Today(Monday)[0].Definition), Is.EqualTo(tuning.Daily.TaskXpOf(MissionTier.Easy)));
            Assert.That(tasks.CoinsOf(tasks.Today(Monday)[0].Definition),
                Is.EqualTo(tuning.Daily.TaskCoinsOf(MissionTier.Easy)));

            // Saved under its constant id, and the next visit of the day reads it done and pays it no more.
            Assert.That(storage.GetString("daily_tasks"), Does.Contain($"|{DailyTasks.EventTaskId}:1|"));
            var visit = new DailyTasks(storage, tuning, progress);
            Assert.That(visit.Today(Monday)[0].Definition.Id, Is.EqualTo(DailyTasks.EventTaskId));
            Assert.That(visit.Today(Monday)[0].Definition.Mode, Is.EqualTo(mode));
            Assert.That(visit.Today(Monday)[0].IsDone, Is.True);
            Assert.That(Played(visit.Record(stats, Monday)), Is.False);
        }

        [Test]
        public void NoEventNoTaskOfIt()
        {
            var hotPotato = tuning.SpecialRounds.HotPotato;
            var enabled = hotPotato.GetType().GetField("enabled", BindingFlags.Instance | BindingFlags.NonPublic);
            enabled.SetValue(hotPotato, false);
            // Every other finished mode off too, for good in this test: HOT POTATO alone stands for the build's events.
            var frenzy = tuning.ItemFrenzy;
            frenzy.GetType().GetField("enabled", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(frenzy, false);
            for (var day = 0; day < 20; day++) {
                Assert.That(tasks.Today(Monday.AddDays(day)).Select(task => task.Definition.Id),
                    Has.No.Member(DailyTasks.EventTaskId), $"day {day}");
            }

            // A set saved with the task while the build had events is drawn again.
            enabled.SetValue(hotPotato, true);
            tasks.Reset(Monday);
            Assert.That(tasks.Today(Monday)[0].Definition.Id, Is.EqualTo(DailyTasks.EventTaskId));
            enabled.SetValue(hotPotato, false);
            Assert.That(new DailyTasks(storage, tuning, progress).Today(Monday).Select(task => task.Definition.Id),
                Has.No.Member(DailyTasks.EventTaskId));

            // The match build has no events.
            enabled.SetValue(hotPotato, true);
            typeof(GameTuning).GetField("mode", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tuning, GameTuning.GameMode.Match);
            tasks.Reset(Monday);
            Assert.That(tasks.Today(Monday).Select(task => task.Definition.Id), Has.No.Member(DailyTasks.EventTaskId));
        }

        private static bool Played(DailyTaskRound record) =>
            record.Completed.Any(task => task.Id == DailyTasks.EventTaskId);

        private string TodaysEvent(DateTime now) => EventModes.IdOf(EventSchedule.Of(tuning, CalendarDay.Of(now)));

        private static MissionStats Stats(RoundOutcome outcome, int knockouts, string eventMode = "") =>
            new(true, outcome, outcome == RoundOutcome.Won ? 1 : 3, knockouts, 4, outcome == RoundOutcome.Won, 2, 2,
                1, "sandbox", eventMode);

        private static RoundResult Result(RoundOutcome outcome) =>
            new(outcome, outcome == RoundOutcome.Won ? 1 : 3, 4, 40f, 2, RoundResult.Nobody, FallCause.None, false,
                false);

        private int LevelStart(int level)
        {
            var prices = tuning.Progress.LevelXp;
            var total = 0;
            for (var at = 1; at < level; at++) {
                total += prices[Mathf.Min(at - 1, prices.Length - 1)];
            }

            return total;
        }

        private static readonly FieldInfo BoostEnabled =
            typeof(GameTuning.BoostSettings).GetField("enabled", BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
