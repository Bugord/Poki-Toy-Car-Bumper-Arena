using Bugord.App.Platform;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// The round's analytics, Poki's Game Events through <see cref="IGamePlatform.Measure"/>, to see where a session
    /// ends: which round, how it went, and whether PLAY AGAIN was pressed, and why: what the player steers with,
    /// whether they found the controls and the boost, and whether they were pushed off or drove off. The names are
    /// kept stable, since the portal's reports are read by them. Poki drops an event with more than
    /// <see cref="GameEvent.MaxNumbers"/> numbers in it, so every event holds at most two, and the round goes in
    /// buckets.
    /// <list type="bullet">
    /// <item><c>round / 1..10, 10plus / start</c>, then <c>complete</c> (a win or a round survived) or <c>fail</c> (a fall)</item>
    /// <item><c>result / won, out, survived / outcome</c></item>
    /// <item><c>result / place-N-rR / place</c>, unless the player was alone, and <c>result / knockouts-N-rR /
    /// knockouts</c>, R the round 1, 2, 3 or 4plus, so the first round can be told from the later ones</item>
    /// <item><c>result / pushed, fell / out-cause</c> when the player is out: pushed off by a car, or drove off alone</item>
    /// <item><c>result / boosts-0, boosts-1-2, boosts-3plus / boosts</c></item>
    /// <item>first round only: <c>controls / keyboard, mouse, touch, gamepad, none / input</c>, what the player first
    /// steered with, and <c>controls / move-0-3, move-3-10, move-10plus, never / first-move</c>, seconds from the cars
    /// standing on the arena to it (the round waits for the player's first steer or boost, so mostly the wait)</item>
    /// <item>since v0.2.0, first round only: <c>controls / boost-button, tap, finger, key, mouse, gamepad, none /
    /// boost-input</c>, how the first successful boost was fired</item>
    /// <item><c>result / 0-15 … 60-75 / time</c>: seconds on the arena, in buckets of 15</item>
    /// <item>by round, R as above, since the time and the cause of a fall differ most between the first round and the
    /// later ones: <c>result / cars-N-rR / cars</c>, the cars in the round (the difficulty brings fewer early on, so a
    /// place reads against it), <c>result / early, mid, late, full-rR / round-time</c>, seconds on the arena (under 15,
    /// under 30, under 60, 60 and more; no more numbers fit), and <c>result / pushed, fell-rR / round-out-cause</c></item>
    /// <item>since v0.2.0: <c>result / fell-ring, boost, slide, drive-rR / fall-cause</c> when the player falls without
    /// knockout credit: the floor gave way, a recent boost, an older hit, or driving off, in that order</item>
    /// <item><c>result / play-again / visible</c>, then <c>interact</c> when pressed</item>
    /// <item>since v0.2.0, only with a marked rival present: <c>result / revenge-got, missed-rR / revenge</c></item>
    /// <item><c>garage / button / visible, interact</c> and <c>garage / car-id / equip</c></item>
    /// <item><c>progress / level-2..10, level-10plus / level-up</c> for each crossed level, and
    /// <c>progress / car-id / unlock</c> from the saved reward</item>
    /// <item><c>result / car-car-id-rR / car</c>: the car played, before an unlock auto-equips the next one;
    /// catalog IDs contain no numbers, including drag-racer and monster-truck</item>
    /// </list>
    /// </summary>
    public static class RoundEvents
    {
        private const string RoundCategory = "round";
        private const string ResultCategory = "result";
        private const string ControlsCategory = "controls";
        private const string PlayAgain = "play-again";

        // Rounds past this one share one bucket: few sessions get there, and the event names stay few.
        private const int LastRoundBucket = 10;
        private const int LastLevelBucket = 10;

        // Place and knockouts carry the round up to this one, the rest share a bucket: one more number fits in them.
        private const int LastTaggedRound = 4;

        // Seconds on the arena are counted in buckets this wide, the round's last bucket ending at its full length.
        private const int TimeBucket = 15;

        public static void MeasureRoundStart(this IGamePlatform platform, int round)
        {
            platform.MeasureStart(RoundCategory, RoundBucket(round));
        }

        /// <summary>
        /// The end of the round started as <paramref name="round"/>, and the milestones of its result.
        /// </summary>
        public static void MeasureRoundEnd(this IGamePlatform platform, int round, RoundResult result, float duration,
            PlayerDriver driver)
        {
            var bucket = RoundBucket(round);
            if (result.Outcome == RoundOutcome.Out) {
                platform.MeasureFail(RoundCategory, bucket);
            }
            else {
                platform.MeasureComplete(RoundCategory, bucket);
            }

            platform.Measure(ResultCategory, GameEvent.Clean(result.Outcome.ToString()), "outcome");
            var tag = RoundTag(round);
            if (result.CarCount > 1) {
                platform.Measure(ResultCategory, $"place-{result.Place}-{tag}", "place");
            }

            platform.Measure(ResultCategory, $"knockouts-{result.Knockouts}-{tag}", "knockouts");
            platform.Measure(ResultCategory, TimeBucketOf(result.Seconds, duration), "time");
            platform.Measure(ResultCategory, $"cars-{result.CarCount}-{tag}", "cars");
            platform.Measure(ResultCategory, $"{TimeSpanOf(result.Seconds)}-{tag}", "round-time");
            if (result.HadNemesis) {
                var revenge = result.RevengeTaken ? "got" : "missed";
                platform.Measure(ResultCategory, $"revenge-{revenge}-{tag}", "revenge");
            }
            if (result.Outcome == RoundOutcome.Out) {
                var cause = result.KnockedOutBy == RoundResult.Nobody ? "fell" : "pushed";
                platform.Measure(ResultCategory, cause, "out-cause");
                platform.Measure(ResultCategory, $"{cause}-{tag}", "round-out-cause");
                if (result.KnockedOutBy == RoundResult.Nobody) {
                    var fallCause = GameEvent.Clean(result.FallCause.ToString());
                    platform.Measure(ResultCategory, $"fell-{fallCause}-{tag}", "fall-cause");
                }
            }

            platform.Measure(ResultCategory, BoostBucketOf(driver.Boosts), "boosts");
            if (round <= 1) {
                MeasureControls(platform, driver);
            }
        }

        /// <summary>
        /// Whether a new player found the controls, and with what: the first round tells it, later ones only repeat it.
        /// </summary>
        private static void MeasureControls(IGamePlatform platform, PlayerDriver driver)
        {
            var input = driver.FirstInput;
            platform.Measure(ControlsCategory, input == null ? "none" : GameEvent.Clean(input.Value.ToString()), "input");

            var boost = driver.FirstBoostInput;
            var boostInput = boost == null ? "none" : GameEvent.Clean(boost.Value.ToString());
            platform.Measure(ControlsCategory, $"boost-{boostInput}", "boost-input");

            var seconds = driver.FirstMoveTime;
            var move = seconds < 0f ? "never" : seconds < 3f ? "move-0-3" : seconds < 10f ? "move-3-10" : "move-10plus";
            platform.Measure(ControlsCategory, move, "first-move");
        }

        // In words, so the round still fits beside it: a time bucket's two numbers would make three.
        private static string TimeSpanOf(float seconds)
        {
            return seconds < 15f ? "early" : seconds < 30f ? "mid" : seconds < 60f ? "late" : "full";
        }

        private static string BoostBucketOf(int boosts)
        {
            return boosts == 0 ? "boosts-0" : boosts <= 2 ? "boosts-1-2" : "boosts-3plus";
        }

        public static void MeasurePlayAgainShown(this IGamePlatform platform)
        {
            platform.MeasureVisible(ResultCategory, PlayAgain);
        }

        public static void MeasurePlayAgainPressed(this IGamePlatform platform)
        {
            platform.MeasureInteract(ResultCategory, PlayAgain);
        }

        public static void MeasureGarageShown(this IGamePlatform platform) => platform.MeasureVisible("garage", "button");

        public static void MeasureGaragePressed(this IGamePlatform platform) => platform.MeasureInteract("garage", "button");

        public static void MeasureGarageEquip(this IGamePlatform platform, string carId) =>
            platform.Measure("garage", carId, "equip");

        public static void MeasureRoundCar(this IGamePlatform platform, int round, string carId) =>
            platform.Measure(ResultCategory, $"car-{carId}-{RoundTag(round)}", "car");

        /// <summary>Sent from the saved reward, independently of how soon the result screen is dismissed.</summary>
        public static void MeasureProgress(this IGamePlatform platform, XpGain gain)
        {
            for (var level = gain.Before.Level + 1; level <= gain.After.Level; level++) {
                var bucket = level > LastLevelBucket ? "10plus" : level.ToString();
                platform.Measure("progress", $"level-{bucket}", "level-up");
            }

            foreach (var id in gain.UnlockedCarIds) {
                platform.Measure("progress", id, "unlock");
            }
        }

        private static string RoundTag(int round) =>
            round >= LastTaggedRound ? $"r{LastTaggedRound}plus" : $"r{Mathf.Max(1, round)}";

        private static string RoundBucket(int round)
        {
            return round > LastRoundBucket ? $"{LastRoundBucket}plus" : Mathf.Max(1, round).ToString();
        }

        private static string TimeBucketOf(float seconds, float duration)
        {
            // A round that ran its full length lands in the last bucket, not one of its own.
            var lastBucket = Mathf.Max(0, Mathf.CeilToInt(duration / TimeBucket) - 1);
            var bucket = Mathf.Clamp(Mathf.FloorToInt(seconds / TimeBucket), 0, lastBucket);
            return $"{bucket * TimeBucket}-{(bucket + 1) * TimeBucket}";
        }
    }
}
