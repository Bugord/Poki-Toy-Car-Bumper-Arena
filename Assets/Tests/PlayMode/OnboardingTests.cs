using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEditor;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Onboarding happens once per player, not once per visit: the easy first rounds and the teaching hints count the
    /// rounds the player has finished on this device, so a returning player's first round of a new visit is a short
    /// warm-up rather than the newcomer's easy round again, and shows none of the tutorial; a new player still gets both.
    /// </summary>
    public sealed class OnboardingTests
    {
        private const string TuningPath = "Assets/Data/Gameplay/data_game_tuning.asset";

        private GameTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null);
        }

        [Test]
        public void ANewPlayersFirstRoundIsEasy()
        {
            var progress = new SimulationProgress { Round = 1 };
            var difficulty = new RoundDifficulty(tuning, progress);

            Assert.That(difficulty.Level, Is.EqualTo(0f));
            Assert.That(difficulty.BotCount, Is.EqualTo(tuning.Bots.Easy.Bots));
            Assert.That(RoundDifficulty.BotCountFor(tuning, progress), Is.EqualTo(difficulty.BotCount));
            Assert.That(difficulty.EdgeAssist, Is.EqualTo(tuning.Bots.Easy.EdgeAssist));
            Assert.That(difficulty.AutoBoost, Is.EqualTo(tuning.Bots.Easy.AutoBoost));
        }

        [Test]
        public void ANewPlayerStillRampsOverTheirFirstRounds()
        {
            var bots = tuning.Bots;
            var halfway = new RoundDifficulty(tuning, new SimulationProgress { Round = 1 + bots.RampRounds / 2 });
            var normal = new RoundDifficulty(tuning, new SimulationProgress { Round = 1 + bots.RampRounds });

            Assert.That(halfway.Level, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(normal.Level, Is.EqualTo(1f));
            Assert.That(normal.BotCount, Is.EqualTo(bots.Normal.Bots));
        }

        [Test]
        public void AReturningPlayersFirstRoundOfAVisitIsAWarmUpNotEasy()
        {
            var bots = tuning.Bots;
            var progress = new SimulationProgress { Round = 1, RoundsBefore = 20 };
            var difficulty = new RoundDifficulty(tuning, progress);

            Assert.That(difficulty.Level, Is.EqualTo(bots.ReturnLevel).Within(1e-4f));
            Assert.That(difficulty.Level, Is.GreaterThan(0f), "not the newcomer's easy round");
            Assert.That(difficulty.BotCount, Is.GreaterThan(bots.Easy.Bots));
            Assert.That(RoundDifficulty.BotCountFor(tuning, progress), Is.EqualTo(difficulty.BotCount),
                "the lobby counts the same bots as the arena");
            Assert.That(difficulty.EdgeAssist, Is.LessThan(bots.Easy.EdgeAssist));

            progress.Round = 1 + bots.ReturnRounds;
            var warmed = new RoundDifficulty(tuning, progress);
            Assert.That(warmed.Level, Is.EqualTo(1f));
            Assert.That(warmed.BotCount, Is.EqualTo(bots.Normal.Bots));
        }

        [Test]
        public void APlayerWithARoundBeforeGoesOnFromWhereTheRampWas()
        {
            var bots = tuning.Bots;
            var difficulty = new RoundDifficulty(tuning, new SimulationProgress { Round = 1, RoundsBefore = 1 });

            Assert.That(difficulty.Level, Is.EqualTo(1f / bots.RampRounds).Within(1e-4f));
        }

        [Test]
        public void ALosingStreakStillEasesAReturningPlayer()
        {
            var bots = tuning.Bots;
            var progress = new SimulationProgress {
                Round = 1 + bots.ReturnRounds, RoundsBefore = 20, LosingStreak = bots.StreakStart + 1,
            };

            Assert.That(new RoundDifficulty(tuning, progress).Level, Is.LessThan(1f));
        }

        [Test]
        public void TheRoundsFinishedAreSavedForTheNextVisit()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            var first = new SessionProgress(storage, tuning);
            Assert.That(first.RoundsBefore, Is.EqualTo(0));
            first.Record(Result());
            first.Record(Result());
            Assert.That(first.RoundsBefore, Is.EqualTo(0), "fixed for the visit");
            Assert.That(storage.GetInt(SessionProgress.RoundsKey), Is.EqualTo(2));

            var second = new SessionProgress(storage, tuning);
            Assert.That(second.Round, Is.EqualTo(1));
            Assert.That(second.RoundsBefore, Is.EqualTo(2));
            second.Record(Result());
            Assert.That(storage.GetInt(SessionProgress.RoundsKey), Is.EqualTo(3));
        }

        [Test]
        public void APlayerFromBeforeTheCountIsPastTheRamps()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetInt("xp_total", 40);
            var progress = new SessionProgress(storage, tuning);

            Assert.That(progress.RoundsBefore, Is.GreaterThanOrEqualTo(tuning.Bots.RampRounds));
            Assert.That(progress.RoundsBefore, Is.GreaterThanOrEqualTo(tuning.Bots.EdgeAssistRounds));
            Assert.That(progress.RoundsBefore, Is.GreaterThanOrEqualTo(tuning.Controls.GhostStickRounds));
            Assert.That(new SessionProgress().RoundsBefore, Is.EqualTo(0), "nothing saved, a new player");
        }

        [Test]
        public void ANewPlayersFirstRoundTeaches()
        {
            var model = Hud(roundNumber: 1, playerRound: 1);

            Assert.That(model.IsTutorial, Is.True);
            Assert.That(model.KeepsGhostStick, Is.True);
            Assert.That(model.IsFirstRound, Is.True);
        }

        [Test]
        public void AReturningPlayersFirstRoundOfAVisitTeachesNothing()
        {
            var model = Hud(roundNumber: 1, playerRound: 21);

            Assert.That(model.IsTutorial, Is.False, "no ghost demo, idle reminders, boost hint or hidden handles");
            Assert.That(model.KeepsGhostStick, Is.False);
            Assert.That(model.IsFirstRound, Is.True, "the visit's first round still waits for the player");
        }

        [Test]
        public void TheGhostStickStaysForThePlayersFirstRoundsAcrossVisits()
        {
            var rounds = tuning.Controls.GhostStickRounds;
            Assert.That(Hud(roundNumber: 1, playerRound: rounds).KeepsGhostStick, Is.True);
            Assert.That(Hud(roundNumber: 1, playerRound: rounds + 1).KeepsGhostStick, Is.False);
            Assert.That(Hud(roundNumber: 2, playerRound: 0).PlayerRound, Is.EqualTo(2),
                "without a player's round the visit's stands in");
        }

        [Test]
        public void TheItemHintIsShownOncePerPlayer()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            var visit = new HudControls(storage);
            Assert.That(visit.HasShownItemHint, Is.False);
            visit.HasShownItemHint = true;

            Assert.That(new HudControls(storage).HasShownItemHint, Is.True, "the next visit remembers it");
            Assert.That(new HudControls().HasShownItemHint, Is.False);
        }

        private HudViewModel Hud(int roundNumber, int playerRound)
        {
            var model = new HudViewModel(null, new HudControls());
            model.SetPayload(new HudRound(null, null, tuning.Controls, roundNumber == 1, roundNumber,
                playerRound <= 1, playerRound: playerRound));
            return model;
        }

        private static RoundResult Result() =>
            new RoundResult(RoundOutcome.Survived, 2, 4, 30f, 0, RoundResult.Nobody, FallCause.None, true, false);
    }
}
