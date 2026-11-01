using Bugord.App.Platform;
using ToyCarArena.Gameplay;
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
    /// <item>since v0.2.0: <c>result / fell-ring, boost, ball, slide, drive-rR / fall-cause</c> when the player falls without
    /// knockout credit: the floor gave way, a recent boost, the Ball arena's ball (no car pushed the player after it), an
    /// older hit, or driving off, in that order</item>
    /// <item><c>result / play-again / visible</c>, then <c>interact</c> when pressed; after v0.5.1 also
    /// <c>result / again-won, out, survived-rR / visible, interact</c>, the same press by how the round went, so the
    /// visits that end on a result screen read by outcome and round</item>
    /// <item>after v0.5.1: <c>lobby / slow-arena-id / visible</c> when the next arena is still loading
    /// <see cref="SlowLobbySeconds"/> after the lobby came up, then <c>interact</c> when that round comes up after all,
    /// so a slow load can be told from a wait the player gave up on</item>
    /// <item>after v0.5.1: <c>arena / arena-id-variant-id / start</c>, then <c>complete</c> or <c>fail</c> beside the
    /// round's, so Poki counts the visits that leave in the middle of a round per arena variant; a long match sends it per
    /// cycle of the floor, each cycle's variant completing as the next one starts</item>
    /// <item>since v0.2.0, only with a marked rival present: <c>result / revenge-got, missed-rR / revenge</c></item>
    /// <item><c>garage / button / visible, interact</c> and <c>garage / car-id / equip</c></item>
    /// <item><c>garage / color-name / color</c> on colour selection and
    /// <c>result / color-name-rR / color</c> for the colour captured by the played round</item>
    /// <item>a build of long matches (<see cref="RoundResult.IsMatch"/>) sends <c>match / 1..10, 10plus / start</c>,
    /// then <c>complete</c> (a match always runs its time), and with it <c>match / place-N-rR / place</c>,
    /// <c>match / knockouts-N-rR / knockouts</c>, <c>match / falls-N-rR / falls</c> (every fall is followed by a
    /// comeback, but for one at the very end) and <c>match / pushed-N-rR / pushed</c> (the falls a car got the credit
    /// for), N from 0 to 9 or 10plus, and <c>match / revenge-got, missed-rR / revenge</c> with a rival marked during the
    /// match; and <c>match / pickup-heavy, shockwave, shield, turbo-rR / pickup</c> for each pickup the player takes and
    /// <c>match / pickups-N-rR / pickups</c> for how many at the end. It sends no <c>round / … / start, complete, fail</c>, and of the round's result only what still means
    /// the same in a match (<see cref="MeasureMatchEnd"/>).</item>
    /// <item><c>progress / level-2..10, level-10plus / level-up</c> for each crossed level, and
    /// <c>progress / car-id / unlock</c> from the saved reward</item>
    /// <item><c>result / map-arena-id-rR / map</c>: the arena of the round, and <c>progress / map-arena-id / unlock</c>
    /// for each arena a crossed level opened</item>
    /// <item><c>result / car-car-id-rR / car</c>: the car played, before an unlock auto-equips the next one;
    /// catalog IDs contain no numbers, including drag-racer and monster-truck</item>
    /// </list>
    /// </summary>
    public static class RoundEvents
    {
        private const string RoundCategory = "round";
        private const string MatchCategory = "match";
        private const string ResultCategory = "result";
        private const string ControlsCategory = "controls";
        private const string PlayAgain = "play-again";
        private const string LobbyCategory = "lobby";
        private const string ArenaCategory = "arena";

        // Rounds past this one share one bucket: few sessions get there, and the event names stay few.
        private const int LastRoundBucket = 10;
        private const int LastLevelBucket = 10;

        // A match's knock-outs and falls past this many share one bucket.
        private const int LastCountBucket = 10;

        // Place and knockouts carry the round up to this one, the rest share a bucket: one more number fits in them.
        private const int LastTaggedRound = 4;

        // Seconds on the arena are counted in buckets this wide, the round's last bucket ending at its full length.
        private const int TimeBucket = 15;

        /// <summary>
        /// Seconds of loading under the lobby after which the load counts as slow: the lobby itself shows for 1.6 s, and
        /// a desktop loads the next arena well within it.
        /// </summary>
        public const float SlowLobbySeconds = 4f;

        public static void MeasureLobbyShown(this IGamePlatform platform) => platform.Measure(LobbyCategory, "shown", "visible");

        public static void MeasureLobbySlow(this IGamePlatform platform, string mapId) =>
            platform.MeasureVisible(LobbyCategory, $"slow-{mapId}");

        public static void MeasureLobbySlowDone(this IGamePlatform platform, string mapId) =>
            platform.MeasureInteract(LobbyCategory, $"slow-{mapId}");

        public static void MeasureScoreboardShown(this IGamePlatform platform) =>
            platform.Measure("scoreboard", "shown", "visible");

        public static void MeasureRoundStart(this IGamePlatform platform, int round)
        {
            platform.MeasureStart(RoundCategory, RoundBucket(round));
        }

        /// <summary>
        /// The start of the visit's <paramref name="match"/>-th long match, in place of <see cref="MeasureRoundStart"/>.
        /// </summary>
        public static void MeasureMatchStart(this IGamePlatform platform, int match)
        {
            platform.MeasureStart(MatchCategory, RoundBucket(match));
        }

        /// <summary>
        /// The variant's own start, beside <see cref="MeasureRoundStart"/>: the round's lefts say how many leave
        /// mid-round, these say on which arena. Ids have no digits, so no number is spent.
        /// </summary>
        public static void MeasureVariantStart(this IGamePlatform platform, string mapId, string variantId)
        {
            if (!string.IsNullOrEmpty(variantId)) {
                platform.MeasureStart(ArenaCategory, $"{mapId}-{variantId}");
            }
        }

        /// <summary>
        /// A match moving on to the next variant of its arena with the floor's cycle: the one played completes and the
        /// next starts, so Poki counts the visits that leave in the middle of a match per variant, as in a round.
        /// </summary>
        public static void MeasureVariantChange(this IGamePlatform platform, string mapId, string fromVariantId,
            string toVariantId)
        {
            if (!string.IsNullOrEmpty(fromVariantId)) {
                platform.MeasureComplete(ArenaCategory, $"{mapId}-{fromVariantId}");
            }

            platform.MeasureVariantStart(mapId, toVariantId);
        }

        public static void MeasureVariantEnd(this IGamePlatform platform, string mapId, string variantId,
            RoundOutcome outcome)
        {
            if (string.IsNullOrEmpty(variantId)) {
                return;
            }

            if (outcome == RoundOutcome.Out) {
                platform.MeasureFail(ArenaCategory, $"{mapId}-{variantId}");
            }
            else {
                platform.MeasureComplete(ArenaCategory, $"{mapId}-{variantId}");
            }
        }

        /// <summary>
        /// The end of the round started as <paramref name="round"/>, and the milestones of its result.
        /// </summary>
        public static void MeasureRoundEnd(this IGamePlatform platform, int round, RoundResult result, float duration,
            PlayerDriver driver)
        {
            if (result.IsMatch) {
                MeasureMatchEnd(platform, round, result, driver);
                return;
            }

            var bucket = RoundBucket(round);
            if (result.Outcome == RoundOutcome.Out) {
                platform.MeasureFail(RoundCategory, bucket);
            }
            else {
                platform.MeasureComplete(RoundCategory, bucket);
            }

            platform.Measure(ResultCategory, GameEvent.Clean(result.Outcome.ToString()), "outcome");
            var tag = RoundTag(round);
            platform.Measure(ResultCategory, $"color-{CarPaint.NameOf(result.ColorOf(0))}-{tag}", "color");
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
        /// The end of a long match. Of the round's result events it keeps those that still mean the same: the colour,
        /// the cars in it and, in the first, the controls. The outcome, place, knock-outs, time, round time, revenge,
        /// out causes and boosts mean something else over a whole match or never change (it always runs its time), so
        /// the match sends its own in their place, and a version's reports never mix the two.
        /// </summary>
        public static void MeasureMatchEnd(this IGamePlatform platform, int match, RoundResult result, PlayerDriver driver)
        {
            platform.MeasureComplete(MatchCategory, RoundBucket(match));
            var tag = RoundTag(match);
            platform.Measure(MatchCategory, $"place-{result.Place}-{tag}", "place");
            platform.Measure(MatchCategory, $"knockouts-{CountBucket(result.Knockouts)}-{tag}", "knockouts");
            platform.Measure(MatchCategory, $"falls-{CountBucket(result.Falls)}-{tag}", "falls");
            platform.Measure(MatchCategory, $"pushed-{CountBucket(result.KnockedOff)}-{tag}", "pushed");
            if (result.HadNemesis) {
                var revenge = result.RevengeTaken ? "got" : "missed";
                platform.Measure(MatchCategory, $"revenge-{revenge}-{tag}", "revenge");
            }

            platform.Measure(ResultCategory, $"color-{CarPaint.NameOf(result.ColorOf(0))}-{tag}", "color");
            platform.Measure(ResultCategory, $"cars-{result.CarCount}-{tag}", "cars");
            if (match <= 1) {
                MeasureControls(platform, driver);
            }
        }

        /// <summary>
        /// The roll of a box the player broke landed on an item, in match number <paramref name="match"/>:
        /// <c>match / item-heavy, shockwave, shield, turbo, bomb, glove-rR / item-get</c>, R as for the match's other
        /// events. Replaces v0.6's <c>pickup</c> event, when a pickup was its ability.
        /// </summary>
        public static void MeasureItemGet(this IGamePlatform platform, int match, ItemKind kind) =>
            platform.Measure(MatchCategory, $"item-{ItemId(kind)}-{RoundTag(match)}", "item-get");

        /// <summary>
        /// The player used the item in the slot: <c>match / item-kind-rR / item-use</c>; against item-get it tells which
        /// items the players find and which they forget to use.
        /// </summary>
        public static void MeasureItemUse(this IGamePlatform platform, int match, ItemKind kind) =>
            platform.Measure(MatchCategory, $"item-{ItemId(kind)}-{RoundTag(match)}", "item-use");

        // The kind as the events spell it, without digits.
        private static string ItemId(ItemKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>
        /// How many boxes the player broke in the match, an item in them or not, at its end:
        /// <c>match / pickups-N-rR / pickups</c>, N from 0 to 9 or 10plus.
        /// </summary>
        public static void MeasureMatchPickups(this IGamePlatform platform, int match, int count) =>
            platform.Measure(MatchCategory, $"pickups-{CountBucket(count)}-{RoundTag(match)}", "pickups");

        // One number, so the match's tag still fits beside it.
        private static string CountBucket(int count)
        {
            return count >= LastCountBucket ? $"{LastCountBucket}plus" : Mathf.Max(0, count).ToString();
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

        public static void MeasurePlayAgainShown(this IGamePlatform platform, RoundOutcome outcome, int round)
        {
            platform.MeasureVisible(ResultCategory, PlayAgain);
            platform.MeasureVisible(ResultCategory, AgainOf(outcome, round));
        }

        public static void MeasurePlayAgainPressed(this IGamePlatform platform, RoundOutcome outcome, int round)
        {
            platform.MeasureInteract(ResultCategory, PlayAgain);
            platform.MeasureInteract(ResultCategory, AgainOf(outcome, round));
        }

        private static string AgainOf(RoundOutcome outcome, int round) =>
            $"again-{GameEvent.Clean(outcome.ToString())}-{RoundTag(round)}";

        public static void MeasureGarageShown(this IGamePlatform platform) => platform.MeasureVisible("garage", "button");

        public static void MeasureGaragePressed(this IGamePlatform platform) => platform.MeasureInteract("garage", "button");

        public static void MeasureGarageEquip(this IGamePlatform platform, string carId) =>
            platform.Measure("garage", carId, "equip");

        public static void MeasureGarageColor(this IGamePlatform platform, int colorId) =>
            platform.Measure("garage", $"color-{CarPaint.NameOf(colorId)}", "color");

        public static void MeasureRoundCar(this IGamePlatform platform, int round, string carId) =>
            platform.Measure(ResultCategory, $"car-{carId}-{RoundTag(round)}", "car");

        public static void MeasureRoundMap(this IGamePlatform platform, int round, string mapId) =>
            platform.Measure(ResultCategory, $"map-{mapId}-{RoundTag(round)}", "map");

        /// <summary>
        /// The variant played on the arena, besides <see cref="MeasureRoundMap"/>, which keeps its name for the reports
        /// built on it. Ids have no digits, so the round is the only number.
        /// </summary>
        public static void MeasureRoundVariant(this IGamePlatform platform, int round, string mapId, string variantId)
        {
            if (!string.IsNullOrEmpty(variantId)) {
                platform.Measure(ResultCategory, $"variant-{mapId}-{variantId}-{RoundTag(round)}", "map-variant");
            }
        }

        /// <summary>The arenas the levels of this reward opened, from the tuning, as the cars are.</summary>
        public static void MeasureMapUnlocks(this IGamePlatform platform, XpGain gain, GameTuning tuning)
        {
            foreach (var id in ArenaRotation.OpenedBetween(tuning, gain.Before.Level, gain.After.Level)) {
                platform.Measure("progress", $"map-{id}", "unlock");
            }
        }

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
