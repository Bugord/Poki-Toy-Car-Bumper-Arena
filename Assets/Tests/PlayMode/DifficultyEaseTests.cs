using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEditor;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The round eases for a player who is having a hard time: a visit lost more often than not keeps the bots easier
    /// even through a lone win, falls off on one's own add help at the edge without easing the bots, and the first
    /// rounds of an event mode the player has never played are easier, fading out as the player learns it.
    /// </summary>
    public sealed class DifficultyEaseTests
    {
        private const string TuningPath = "Assets/Data/Gameplay/data_game_tuning.asset";

        private GameTuning tuning;

        [SetUp]
        public void SetUp()
        {
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(TuningPath);
            Assert.That(tuning, Is.Not.Null);
            // Plain rounds unless a test picks a mode: the rotation's event slots would hang the outcome on the date.
            tuning.SpecialRounds.Force(SpecialRound.None);
        }

        [TearDown]
        public void TearDown() => tuning.SpecialRounds.Force(null);

        [Test]
        public void TheNewNumbersLoadWithTheirDefaults()
        {
            var bots = tuning.Bots;
            Assert.That(bots.SelfFallAssist, Is.EqualTo(0.2f));
            Assert.That(bots.MaxSelfFallAssist, Is.EqualTo(0.6f));
            Assert.That(bots.ModeEase, Is.EqualTo(0.4f));
            Assert.That(bots.ModeEaseRounds, Is.EqualTo(2));
        }

        // --- Losing a lot ---

        [Test]
        public void ALoneWinBetweenLossesKeepsTheEase()
        {
            var progress = new SessionProgress(new RoundOverScreenshots.MemoryStorage(), tuning);
            progress.Record(Pushed());
            progress.Record(Pushed());
            progress.Record(Won());
            progress.Record(Pushed());

            Assert.That(progress.LosingStreak, Is.EqualTo(1), "the streak starts again after the win");
            Assert.That(progress.Struggle, Is.EqualTo(2), "two losses, a win, a loss");
            var eased = new RoundDifficulty(tuning, Normal(struggle: progress.Struggle));
            var plain = new RoundDifficulty(tuning, Normal(struggle: 0));
            Assert.That(eased.Level, Is.LessThan(plain.Level), "the bots ease off");
            Assert.That(eased.PlayerBump, Is.GreaterThan(plain.PlayerBump), "the player hits harder");
            Assert.That(eased.BotCount, Is.LessThanOrEqualTo(plain.BotCount));
        }

        [Test]
        public void TheStruggleStopsWhereTheEaseIsFullAndRoundsNotLostBringItDown()
        {
            var progress = new SessionProgress(new RoundOverScreenshots.MemoryStorage(), tuning);
            for (var round = 0; round < 12; round++) {
                progress.Record(Pushed());
            }

            var bots = tuning.Bots;
            var full = bots.StreakStart - 1 + (int)System.Math.Ceiling(bots.MaxStreakEase / bots.StreakEase);
            Assert.That(progress.Struggle, Is.EqualTo(full));
            progress.Record(Won());
            progress.Record(Survived());
            Assert.That(progress.Struggle, Is.EqualTo(full - 2));
        }

        // --- Falls off on one's own ---

        [Test]
        public void ASelfFallAddsEdgeHelpAndLeavesTheBotsAlone()
        {
            var plain = new RoundDifficulty(tuning, Normal());
            var fell = new RoundDifficulty(tuning, Normal(selfFalls: 1));

            Assert.That(fell.EdgeAssist, Is.EqualTo(plain.EdgeAssist + tuning.Bots.SelfFallAssist).Within(1e-4f));
            Assert.That(fell.Level, Is.EqualTo(plain.Level));
            Assert.That(fell.BotCount, Is.EqualTo(plain.BotCount));
            Assert.That(fell.PlayerBump, Is.EqualTo(plain.PlayerBump));

            var many = new RoundDifficulty(tuning, Normal(selfFalls: 20));
            Assert.That(many.EdgeAssist,
                Is.EqualTo(Mathf01(plain.EdgeAssist + tuning.Bots.MaxSelfFallAssist)).Within(1e-4f), "capped");
        }

        [Test]
        public void OnlyAFallOnOnesOwnCountsAndAWinTakesOneBack()
        {
            var progress = new SessionProgress(new RoundOverScreenshots.MemoryStorage(), tuning);
            progress.Record(SelfFall(FallCause.Drive));
            progress.Record(SelfFall(FallCause.Boost));
            Assert.That(progress.SelfFalls, Is.EqualTo(2));
            progress.Record(SelfFall(FallCause.Ball));
            progress.Record(Pushed());
            progress.Record(Survived());
            Assert.That(progress.SelfFalls, Is.EqualTo(2), "the ball, a push and a round survived do not count");
            progress.Record(Won());
            Assert.That(progress.SelfFalls, Is.EqualTo(1));
        }

        // --- The first rounds of a mode ---

        [Test]
        public void AModesFirstRoundsAreEasierAndFadeOut()
        {
            var plain = new RoundDifficulty(tuning, Normal()).Level;
            var first = new RoundDifficulty(tuning, Normal(mode: EventMode.Crown, modeRounds: 0)).Level;
            var second = new RoundDifficulty(tuning, Normal(mode: EventMode.Crown, modeRounds: 1)).Level;
            var learned = new RoundDifficulty(tuning, Normal(mode: EventMode.Crown, modeRounds: 2)).Level;

            Assert.That(plain, Is.EqualTo(1f));
            Assert.That(first, Is.EqualTo(1f - tuning.Bots.ModeEase).Within(1e-4f));
            Assert.That(second, Is.EqualTo(1f - tuning.Bots.ModeEase / 2f).Within(1e-4f));
            Assert.That(learned, Is.EqualTo(1f));
        }

        [Test]
        public void TheRoundsOfAModeAreSavedForGood()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            var progress = new SessionProgress(storage, tuning) { Mode = EventMode.Crown };
            Assert.That(progress.ModeRoundsOf(SpecialRound.Crown), Is.Zero);
            progress.Record(Survived());
            progress.Record(Survived());
            Assert.That(progress.ModeRoundsOf(SpecialRound.Crown), Is.EqualTo(2));
            Assert.That(progress.ModeRoundsOf(SpecialRound.GoldRush), Is.Zero, "each mode its own");

            progress.Mode = EventMode.None;
            progress.Record(Survived());
            Assert.That(new SessionProgress(storage, tuning).ModeRoundsOf(SpecialRound.Crown), Is.EqualTo(2),
                "a plain round counts no mode, and the count outlives the visit");
        }

        private static float Mathf01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        /// <summary>A round well past both ramps and the warm-up of a returning visit, on Normal unless eased.</summary>
        private SimulationProgress Normal(int struggle = 0, int selfFalls = 0, EventMode mode = EventMode.None,
            int modeRounds = int.MaxValue) => new() {
            Round = 1 + tuning.Bots.ReturnRounds, RoundsBefore = 40, Struggle = struggle, SelfFalls = selfFalls,
            Mode = mode, ModeRounds = modeRounds,
        };

        private static RoundResult Won() =>
            new(RoundOutcome.Won, 1, 4, 40f, 3, RoundResult.Nobody, FallCause.None, false, false);

        private static RoundResult Survived() =>
            new(RoundOutcome.Survived, 2, 4, 75f, 0, RoundResult.Nobody, FallCause.None, false, false);

        private static RoundResult Pushed() =>
            new(RoundOutcome.Out, 4, 4, 20f, 0, 2, FallCause.None, false, false);

        private static RoundResult SelfFall(FallCause cause) =>
            new(RoundOutcome.Out, 4, 4, 20f, 0, RoundResult.Nobody, cause, false, false);
    }
}
