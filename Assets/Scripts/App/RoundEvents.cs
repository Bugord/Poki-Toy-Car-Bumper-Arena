using Bugord.App.Platform;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// The round's analytics, Poki's Game Events through <see cref="IGamePlatform.Measure"/>, to see where a session
    /// ends: which round, how it went, and whether PLAY AGAIN was pressed. The names are kept stable, since the
    /// portal's reports are read by them. Poki drops an event with more than <see cref="GameEvent.MaxNumbers"/> numbers
    /// in it, so every event holds at most two, and the round goes in buckets.
    /// <list type="bullet">
    /// <item><c>round / 1..10, 10plus / start</c>, then <c>complete</c> (a win or a round survived) or <c>fail</c> (a fall)</item>
    /// <item><c>result / won, out, survived / outcome</c></item>
    /// <item><c>result / place-N / place</c>, unless the player was alone</item>
    /// <item><c>result / knockouts-N / knockouts</c></item>
    /// <item><c>result / 0-15 … 60-75 / time</c>: seconds on the arena, in buckets of 15</item>
    /// <item><c>result / play-again / visible</c>, then <c>interact</c> when pressed</item>
    /// </list>
    /// </summary>
    public static class RoundEvents
    {
        private const string RoundCategory = "round";
        private const string ResultCategory = "result";
        private const string PlayAgain = "play-again";

        // Rounds past this one share one bucket: few sessions get there, and the event names stay few.
        private const int LastRoundBucket = 10;

        // Seconds on the arena are counted in buckets this wide, the round's last bucket ending at its full length.
        private const int TimeBucket = 15;

        public static void MeasureRoundStart(this IGamePlatform platform, int round)
        {
            platform.MeasureStart(RoundCategory, RoundBucket(round));
        }

        /// <summary>
        /// The end of the round started as <paramref name="round"/>, and the milestones of its result.
        /// </summary>
        public static void MeasureRoundEnd(this IGamePlatform platform, int round, RoundResult result, float duration)
        {
            var bucket = RoundBucket(round);
            if (result.Outcome == RoundOutcome.Out) {
                platform.MeasureFail(RoundCategory, bucket);
            }
            else {
                platform.MeasureComplete(RoundCategory, bucket);
            }

            platform.Measure(ResultCategory, GameEvent.Clean(result.Outcome.ToString()), "outcome");
            if (result.CarCount > 1) {
                platform.Measure(ResultCategory, $"place-{result.Place}", "place");
            }

            platform.Measure(ResultCategory, $"knockouts-{result.Knockouts}", "knockouts");
            platform.Measure(ResultCategory, TimeBucketOf(result.Seconds, duration), "time");
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
