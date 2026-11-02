using System.Collections.Generic;
using Bugord.Core.Storage;
using NUnit.Framework;
using System.Text.RegularExpressions;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Coins, toppers, win streaks and missions: what a round pays, what is saved and what a streak or a mission adds to
    /// the round's XP.
    /// </summary>
    public sealed class MetaRewardTests
    {
        private GameTuning tuning;
        private MemoryStorage storage;

        [SetUp]
        public void SetUp()
        {
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            storage = new MemoryStorage();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(tuning);

        [Test]
        public void ARoundPaysCoinsForTheRoundKnockoutsAndWinAndSavesThem()
        {
            var wallet = new PlayerWallet(storage, tuning);
            var gain = wallet.Record(Result(RoundOutcome.Won, 1, 2), false, 0);
            Assert.That(gain.RoundCoins, Is.EqualTo(12));
            Assert.That(gain.KnockoutCoins, Is.EqualTo(12));
            Assert.That(gain.WinCoins, Is.EqualTo(12));
            Assert.That(gain.StreakCoins, Is.Zero);
            Assert.That(gain.Total, Is.EqualTo(36));
            Assert.That(new PlayerWallet(storage, tuning).Coins, Is.EqualTo(36));
        }

        [Test]
        public void AStreakRoundMultipliesTheRoundsOwnCoinsAndXpButNotTheMissions()
        {
            var wallet = new PlayerWallet(storage, tuning);
            var coins = wallet.Record(Result(RoundOutcome.Out, 3, 0), true, 20);
            Assert.That(coins.StreakCoins, Is.EqualTo(6));
            Assert.That(coins.Total, Is.EqualTo(12 + 6 + 20));

            var catalog = ScriptableObject.CreateInstance<Gameplay.Cars.CarCatalog>();
            try {
                var progress = new PlayerProgress(storage, tuning, catalog);
                var xp = progress.Record(Result(RoundOutcome.Out, 3, 0, 4), tuning.Streaks, 45);
                // 10 for the round and 5 for the car behind: 15, half of it 8 (rounded), plus the mission's 45.
                Assert.That(xp.StreakXp, Is.EqualTo(8));
                Assert.That(xp.MissionXp, Is.EqualTo(45));
                Assert.That(xp.Total, Is.EqualTo(15 + 8 + 45));
                Assert.That(progress.TotalXp, Is.EqualTo(xp.Total));
            }
            finally {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void TopperIsBoughtOnceWornAndSaved()
        {
            var wallet = new PlayerWallet(storage, tuning);
            Assert.That(wallet.Buy("party-hat"), Is.False, "No coins yet.");
            wallet.SetCoins(120);
            Assert.That(wallet.Buy("party-hat"), Is.True);
            Assert.That(wallet.Coins, Is.EqualTo(70));
            Assert.That(wallet.SelectedTopper, Is.EqualTo("party-hat"));
            Assert.That(wallet.Buy("party-hat"), Is.False, "Owned already.");
            Assert.That(wallet.Buy("crown"), Is.False, "Too dear.");
            Assert.That(wallet.Buy("nothing"), Is.False, "Not on sale.");
            Assert.That(wallet.Wear(""), Is.True);
            Assert.That(wallet.SelectedTopper, Is.Empty);
            Assert.That(wallet.Wear("crown"), Is.False, "Not owned.");

            var reloaded = new PlayerWallet(storage, tuning);
            Assert.That(reloaded.IsOwned("party-hat"), Is.True);
            Assert.That(reloaded.Coins, Is.EqualTo(70));
            Assert.That(reloaded.SelectedTopper, Is.Empty);
        }

        [Test]
        public void TwoWinsInARowStartAStreakAndAFallEndsIt()
        {
            var progress = new SessionProgress();
            progress.Record(Result(RoundOutcome.Won, 1, 0));
            Assert.That(progress.WinStreak, Is.EqualTo(1));
            progress.Record(Result(RoundOutcome.Won, 1, 0));
            Assert.That(progress.WinStreak, Is.EqualTo(2));
            progress.Record(Result(RoundOutcome.Survived, 2, 0));
            Assert.That(progress.WinStreak, Is.EqualTo(2), "A round survived to the time keeps the streak.");
            progress.Record(Result(RoundOutcome.Out, 3, 0));
            Assert.That(progress.WinStreak, Is.Zero);

            var change = new StreakChange(2, 0, 2);
            Assert.That(change.WasStreakRound, Is.True);
            Assert.That(change.IsLost, Is.True);
            Assert.That(new StreakChange(1, 0, 2).IsLost, Is.False, "One win is not a streak to lose.");
        }

        [Test]
        public void RevengeStartsAStreakEvenInARoundLost()
        {
            var progress = new SessionProgress();
            progress.Record(Result(RoundOutcome.Out, 3, 1, revenge: true), 2);
            Assert.That(progress.WinStreak, Is.EqualTo(2));
        }

        [Test]
        public void ANewPlayerStartsOnTheStartersAndTheFirstRoundCompletesOne()
        {
            var missions = new MissionTracker(storage, tuning);
            Assert.That(Ids(missions), Is.EqualTo(new[] { "final-two", "first-round", "revenge" }));

            var round = missions.Record(Stats(RoundOutcome.Out, 3, knockouts: 0), 1, 2);
            Assert.That(round.Completed, Has.Count.EqualTo(1));
            Assert.That(round.Completed[0].Id, Is.EqualTo("first-round"));
            Assert.That(round.Xp, Is.EqualTo(30));
            Assert.That(round.Coins, Is.EqualTo(20));
            Assert.That(round.Lines[1].IsDone, Is.True);
            Assert.That(missions.Slots[1].Definition.Id, Is.Not.EqualTo("first-round"),
                "A starter-only mission is never drawn again.");
            Assert.That(missions.Slots[1].Definition.Slot, Is.EqualTo(MissionSlot.Total));
            Assert.That(missions.DoneThisVisit, Is.EqualTo(1));
        }

        [Test]
        public void ARoundMissionStartsOverButATotalOneAddsUp()
        {
            storage.SetString("missions", "round-knockouts:0|knockouts:0|revenge:0");
            var missions = new MissionTracker(storage, tuning);
            missions.Record(Stats(RoundOutcome.Out, 3, knockouts: 1), 1, 2);
            Assert.That(missions.Slots[0].Definition.Id, Is.EqualTo("round-knockouts"));
            Assert.That(missions.Slots[0].Progress, Is.Zero, "One knock-out of two in a round starts over.");
            Assert.That(missions.Slots[1].Progress, Is.EqualTo(1));

            var reloaded = new MissionTracker(storage, tuning);
            Assert.That(reloaded.Slots[1].Definition.Id, Is.EqualTo("knockouts"));
            Assert.That(reloaded.Slots[1].Progress, Is.EqualTo(1), "Missions are saved between visits.");

            var counted = reloaded.CountDone(Stats(RoundOutcome.Out, 3, knockouts: 2, isFinished: false));
            Assert.That(counted, Is.EqualTo(1), "Two knock-outs mid-round already do the round mission.");
        }

        [Test]
        public void ItemLessonsAreNotDrawnForARoundWithoutBoxes()
        {
            for (var seed = 0; seed < 40; seed++) {
                storage.SetString("missions", "final-two:0|rounds:0|revenge:0");
                var missions = new MissionTracker(storage, tuning);
                missions.Seed(seed);
                missions.Record(Stats(RoundOutcome.Out, 3, revenge: true), 1, 1);
                Assert.That(missions.Slots[2].Definition.NeedsItems, Is.False,
                    $"Seed {seed} drew {missions.Slots[2].Definition.Id} for round 1.");
            }
        }

        [Test]
        public void ADoneMissionIsReplacedByAnotherOfItsSlot()
        {
            storage.SetString("missions", "round-win:0|wins:2|revenge:0");
            var missions = new MissionTracker(storage, tuning);
            missions.Seed(3);
            var round = missions.Record(Stats(RoundOutcome.Won, 1), 1, 3);
            Assert.That(round.Completed, Has.Count.EqualTo(2));
            Assert.That(round.Xp, Is.EqualTo(45 + 60));
            Assert.That(missions.Slots[0].Definition.Id, Is.Not.EqualTo("round-win"));
            Assert.That(missions.Slots[0].Definition.Slot, Is.EqualTo(MissionSlot.Round));
            Assert.That(missions.Slots[1].Definition.Id, Is.Not.EqualTo("wins"));
            Assert.That(missions.Slots[1].Progress, Is.Zero);
            Assert.That(round.Lines[1].Before, Is.EqualTo(2));
            Assert.That(round.Lines[1].After, Is.EqualTo(3));
        }

        [Test]
        public void ArenaMissionsWaitForTheirArena()
        {
            var definition = new GameTuning.MissionDefinition("arena-candy", MissionSlot.Total,
                MissionCounter.ArenaRounds, 2, MissionTier.Easy, map: "candy");
            Assert.That(MissionTracker.Gained(definition, Stats(RoundOutcome.Out, 3, map: "candy")), Is.EqualTo(1));
            Assert.That(MissionTracker.Gained(definition, Stats(RoundOutcome.Out, 3, map: "sandbox")), Is.Zero);
        }

        [Test]
        public void MetaEventsNameTheMissionsTheStreakAndTheCoinsWithinTwoNumbers()
        {
            var missions = new MissionTracker(storage, tuning);
            var round = missions.Record(Stats(RoundOutcome.Out, 3), 1, 7);
            var result = Result(RoundOutcome.Out, 3, 0);
            var record = new RoundRecord(result, 1, 0, false, false).WithMeta(new CoinGain(0, 120, 12, 0, 0, 0, 20),
                round, new StreakChange(6, 0, 2));
            var platform = new MatchTests.RecordingPlatform();
            platform.MeasureMeta(7, record, 12, tuning);
            platform.MeasurePlayAgainShown(RoundOutcome.Out, 7, record);

            Assert.That(platform.Events, Does.Contain(("mission", "first-round", "complete")));
            Assert.That(platform.Events, Does.Contain(("mission", "done-10plus-r4plus", "missions")));
            Assert.That(platform.Events, Does.Contain(("result", "streak-0-r4plus", "streak")));
            Assert.That(platform.Events, Does.Contain(("result", "streak-lost-r4plus", "streak")));
            Assert.That(platform.Events, Does.Contain(("result", "coins-mid-r4plus", "coins")));
            Assert.That(platform.Events, Does.Contain(("result", "again-out-mission-r4plus", "visible")));
            Assert.That(platform.Events, Does.Contain(("result", "again-out-lost-r4plus", "visible")));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(Regex.Matches($"{category}{what}{action}", "[0-9]+").Count, Is.LessThanOrEqualTo(2), what);
            }
        }

        private static string[] Ids(MissionTracker missions)
        {
            var ids = new List<string>();
            foreach (var slot in missions.Slots) {
                ids.Add(slot.Definition.Id);
            }

            return ids.ToArray();
        }

        private static MissionStats Stats(RoundOutcome outcome, int place, int knockouts = 0, bool revenge = false,
            bool isFinished = true, string map = "sandbox") =>
            new(isFinished, outcome, place, knockouts, 0, revenge, 0, 0, 0, map);

        private static RoundResult Result(RoundOutcome outcome, int place, int knockouts, int cars = 4,
            bool revenge = false) =>
            new(outcome, place, cars, 20f, knockouts, RoundResult.Nobody, FallCause.None, revenge, revenge);

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
