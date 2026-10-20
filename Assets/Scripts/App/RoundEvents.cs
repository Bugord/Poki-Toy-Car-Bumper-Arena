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
    /// <item><c>result / 0-15 … 60-75 / time</c>: seconds on the arena, in buckets of 15</item>
    /// <item><c>result / play-again / visible</c>, then <c>interact</c> when pressed</item>
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
            var tag = round >= LastTaggedRound ? $"r{LastTaggedRound}plus" : $"r{Mathf.Max(1, round)}";
            if (result.CarCount > 1) {
                platform.Measure(ResultCategory, $"place-{result.Place}-{tag}", "place");
            }

            platform.Measure(ResultCategory, $"knockouts-{result.Knockouts}-{tag}", "knockouts");
            platform.Measure(ResultCategory, TimeBucketOf(result.Seconds, duration), "time");
            if (result.Outcome == RoundOutcome.Out) {
                var cause = result.KnockedOutBy == RoundResult.Nobody ? "fell" : "pushed";
                platform.Measure(ResultCategory, cause, "out-cause");
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

            var seconds = driver.FirstMoveTime;
            var move = seconds < 0f ? "never" : seconds < 3f ? "move-0-3" : seconds < 10f ? "move-3-10" : "move-10plus";
            platform.Measure(ControlsCategory, move, "first-move");
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
