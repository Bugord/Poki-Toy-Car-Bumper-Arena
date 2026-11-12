using Bugord.App.Platform;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
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
    /// <item>after v0.7.1, for a round and a match alike, only while the build has boosts
    /// (<see cref="GameTuning.BoostSettings.IsEnabled"/>): <c>result / boosts-none, few, many-rR / round-boosts</c>, the
    /// player's own boosts by round (none, 1 or 2, 3 and more), and <c>result / auto-none, few, many-rR /
    /// auto-boosts</c>, the boosts the game fired for the player (<see cref="AutoBoost"/>)</item>
    /// <item>first round only: <c>controls / keyboard, mouse, touch, gamepad, none / input</c>, what the player first
    /// steered with, and <c>controls / move-0-3, move-3-10, move-10plus, never / first-move</c>, seconds from the cars
    /// standing on the arena to it (the round waits for the player's first steer or boost, so mostly the wait)</item>
    /// <item>since v0.2.0, first round only and only while the build has boosts: <c>controls / boost-button, tap, finger,
    /// key, mouse, gamepad, none / boost-input</c>, how the first successful boost was fired</item>
    /// <item>first round only: <c>controls / swipes-none, few, many / swipes</c>, flicks of the steering finger (none, 1
    /// to 3, 4 and more): how many players stroke the screen instead of holding the joystick</item>
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
    /// <item>after v0.6.0, after the first round or match of a visit only: <c>controls / again-input-boost, noboost /
    /// visible, interact</c>, the same press by what the player first steered with and whether they boosted at all, so
    /// the visits lost after the first round read against a boost never found</item>
    /// <item>after v0.14.1: <c>session / time-Nm / reached</c> and <c>session / play-Nm / reached</c>, minutes on the
    /// page and in rounds (<see cref="SessionTime"/>): Poki's dashboard shows the session length and the share past
    /// three minutes only for a Player Fit Test</item>
    /// <item>after v0.5.1: <c>lobby / slow-arena-id / visible</c> when the next arena is still loading
    /// <see cref="SlowLobbySeconds"/> after the lobby came up, then <c>interact</c> when that round comes up after all,
    /// so a slow load can be told from a wait the player gave up on</item>
    /// <item>after v0.5.1: <c>arena / arena-id-variant-id / start</c>, then <c>complete</c> or <c>fail</c> beside the
    /// round's, so Poki counts the visits that leave in the middle of a round per arena variant; a long match sends it per
    /// cycle of the floor, each cycle's variant completing as the next one starts</item>
    /// <item>since v0.2.0, only with a marked rival present: <c>result / revenge-got, missed-rR / revenge</c></item>
    /// <item>after v0.13.5, the visit's first round only: <c>controls / start / visible</c> when the arena and TAP TO
    /// START are up, then <c>interact</c> on the first input, and <c>controls / ready-0-3, 3-6, 6-10, 10plus /
    /// start-time</c>, seconds from the engine starting to that screen; Poki counts the game loaded once the engine is,
    /// so this tells the visits that left while the arena still loaded from those that saw it and did not touch</item>
    /// <item>after v0.16, a returning visit's main menu (<see cref="MenuState"/>): <c>menu / shown / visible</c> each time
    /// it comes up, on the visit's first <c>menu / ready-0-3, 3-6, 6-10, 10plus / start-time</c> as for the start screen,
    /// and <c>menu / play, garage, missions, mode / interact</c>; <c>result / home / visible, interact</c> for HOME on
    /// the result</item>
    /// <item><c>garage / button / visible, interact</c> and <c>garage / car-id / equip</c></item>
    /// <item><c>garage / color-name / color</c> on colour selection and
    /// <c>result / color-name-rR / color</c> for the colour captured by the played round</item>
    /// <item>a build of long matches (<see cref="RoundResult.IsMatch"/>) sends <c>match / 1..10, 10plus / start</c>,
    /// then <c>complete</c> (a match always runs its time), and with it <c>match / place-N-rR / place</c>,
    /// <c>match / knockouts-N-rR / knockouts</c>, <c>match / falls-N-rR / falls</c> (every fall is followed by a
    /// comeback, but for one at the very end) and <c>match / pushed-N-rR / pushed</c> (the falls a car got the credit
    /// for), N from 0 to 9 or 10plus, and <c>match / revenge-got, missed-rR / revenge</c> with a rival marked during the
    /// match; and <c>match / item-kind-rR / item-get, item-use</c> for the player's items and
    /// <c>match / pickups-N-rR / pickups</c> for how many boxes the player broke. It sends no <c>round / … / start, complete, fail</c>, and of the round's result only what still means
    /// the same in a match (<see cref="MeasureMatchEnd"/>).</item>
    /// <item><c>progress / level-2..10, level-10plus / level-up</c> for each crossed level, and
    /// <c>progress / car-id / unlock</c> from the saved reward</item>
    /// <item><c>result / map-arena-id-rR / map</c>: the arena of the round, and <c>progress / map-arena-id / unlock</c>
    /// for each arena a crossed level opened; <c>progress / hat-topper-id / unlock</c> for each league's hat a crossed
    /// level reached (after v0.17.4)</item>
    /// <item><c>result / car-car-id-rR / car</c>: the car played, before an unlock auto-equips the next one;
    /// catalog IDs contain no numbers, including drag-racer and monster-truck</item>
    /// <item>since v0.10: <c>mission / mission-id / complete</c> for each done mission, <c>mission / done-N-rR /
    /// missions</c> at the end of every round (missions done this visit, N 0..9 or 10plus) and <c>mission / shown /
    /// visible</c> the first time a player sees missions; <c>result / streak-N-rR / streak</c> (the win streak going
    /// into the next round, N 0..4 or 5plus) and <c>result / streak-lost-rR / streak</c> when a streak ended;
    /// <c>shop / topper-id / buy</c>, <c>garage / topper-topper-id / equip</c> and <c>garage / toppers / visible,
    /// interact</c> for the toppers tab</item>
    /// <item>since v0.10, beside <c>again-outcome-rR</c>, the same NEXT MATCH press by whether the round completed a
    /// mission (<c>result / again-outcome-mission, nomission-rR</c>) and by the streak (<c>result /
    /// again-outcome-streak, nostreak, lost-rR</c>): Poki does not link a visit's events, so this is the one way to tell
    /// the missions' and the streaks' part apart</item>
    /// <item>after v0.15.0: <c>session / visit-1, 2, 3, 4plus / visit</c> once per visit and,
    /// from the second, <c>session / back-d0, d1, d2-7, d8plus, unknown / visit</c>, calendar days since the last one
    /// (<see cref="Visits"/>)</item>
    /// <item>after v0.15.0: <c>special / mega, hot-potato, boss, ice, low-gravity / start</c> at GO! of a special round,
    /// then <c>complete</c> or <c>fail</c>, <c>special / id-won, out, survived / outcome</c> and in a boss round
    /// <c>special / boss-taken, boss-missed / boss</c></item>
    /// <item>with the event modes: <c>special / &lt;id&gt;-place-N / place</c> for every event mode, and a timed mode
    /// fails on a place that is a loss; a timed mode sends <c>round / N / complete</c>, colour, cars, boosts and the
    /// first round's controls, but not the outcome, place, knock-outs, time, round time, revenge or out causes
    /// (<see cref="MeasureTimedEnd"/>)</item>
    /// <item>after v0.17.9, once a visit (<see cref="ScreenFrameRate"/>): <c>fps / screen-touch, mouse-N / screen</c>,
    /// the frames per second of each screen (round, result, curtain, menu, garage, ad), and <c>fps / visit-touch,
    /// mouse-N / visit</c>, the median of the visit's first minute of seconds as Poki's Player Fit Test counts it; N is
    /// rounded to 5, 0 to 65 or 70plus. And <c>ad / commercial-none, short, mid, long / length</c> after each commercial
    /// break: back under 2 seconds (no ad shown), under 10, under 25, longer</item>
    /// </list>
    /// Removed after v0.14.1, as they only repeated others or no longer told anything: <c>result / pushed, fell /
    /// out-cause</c> (the same as <c>round-out-cause</c> without the round), <c>result / boosts-0, 1-2, 3plus /
    /// boosts</c> (<c>round-boosts</c> has the round), <c>lobby / shown / visible</c> (one per NEXT MATCH pressed),
    /// <c>result / coins-few, mid, many-rR / coins</c> and, in a build without boosts, the boost events, which only
    /// said none.
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
        private const string MissionCategory = "mission";
        private const string SessionCategory = "session";
        private const string SpecialCategory = "special";
        private const string MenuCategory = "menu";

        // A streak this long or longer goes in one bucket, so its number and the round's still fit.
        private const int LastStreakBucket = 5;

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

        private const string AdCategory = "ad";
        private const string FpsCategory = "fps";
        private const string DoubleReward = "double";
        private const string XpBoost = "boost";
        private const string DailyCategory = "daily";
        private const string DailyAd = "daily";

        /// <summary>
        /// A content bundle (<see cref="ContentBundles"/>) that never came (<c>failed</c>), or that a round or result
        /// screen stopped waiting for (<c>late</c>): <c>content / late-floors-tabletop / load</c>. The bundle names
        /// carry no numbers.
        /// </summary>
        public static void MeasureContent(this IGamePlatform platform, string how, string bundle) =>
            platform.Measure("content", $"{how}-{bundle}", "load");

        /// <summary>
        /// The visit has been on the page <paramref name="minutes"/> minutes (<see cref="SessionTime"/>):
        /// <c>session / time-3m / reached</c>, one number.
        /// </summary>
        public static void MeasureSessionTime(this IGamePlatform platform, int minutes) =>
            platform.Measure(SessionCategory, $"time-{minutes}m", "reached");

        /// <summary>
        /// The visit has played <paramref name="minutes"/> minutes of rounds (<see cref="SessionTime"/>):
        /// <c>session / play-3m / reached</c>.
        /// </summary>
        public static void MeasureSessionPlay(this IGamePlatform platform, int minutes) =>
            platform.Measure(SessionCategory, $"play-{minutes}m", "reached");

        public static void MeasureLobbySlow(this IGamePlatform platform, string mapId) =>
            platform.MeasureVisible(LobbyCategory, $"slow-{mapId}");

        public static void MeasureLobbySlowDone(this IGamePlatform platform, string mapId) =>
            platform.MeasureInteract(LobbyCategory, $"slow-{mapId}");

        /// <summary>
        /// The visit's first start screen is up, <paramref name="seconds"/> after the engine started: what the player
        /// waited for between Poki's loading bar and the arena.
        /// </summary>
        public static void MeasureStartShown(this IGamePlatform platform, float seconds)
        {
            platform.MeasureVisible(ControlsCategory, "start");
            platform.Measure(ControlsCategory, ReadyBucket(seconds), "start-time");
        }

        public static void MeasureStartPressed(this IGamePlatform platform) =>
            platform.MeasureInteract(ControlsCategory, "start");

        /// <summary>
        /// The main menu is up: <c>menu / shown / visible</c> each time, and on the visit's first menu (a returning
        /// visit opens on it) <c>menu / ready-0-3, 3-6, 6-10, 10plus / start-time</c>, <paramref name="seconds"/> after
        /// the engine started, as <see cref="MeasureStartShown"/> for the start screen of a first visit.
        /// </summary>
        public static void MeasureMenuShown(this IGamePlatform platform, float? seconds)
        {
            platform.MeasureVisible(MenuCategory, "shown");
            if (seconds is { } value) {
                platform.Measure(MenuCategory, ReadyBucket(value), "start-time");
            }
        }

        /// <summary>
        /// A press in the main menu: <c>menu / play, garage, missions, mode, leagues / interact</c> (garage both from its
        /// tile and from the car, leagues from the level).
        /// </summary>
        public static void MeasureMenuPressed(this IGamePlatform platform, string what) =>
            platform.MeasureInteract(MenuCategory, what);

        /// <summary>
        /// Something the main menu shows, for a press of the same name to count: <c>menu / event-hot-potato / visible</c>
        /// (after v0.17.9, the HOT POTATO tile; its press is <see cref="MeasureMenuPressed"/> with the same name).
        /// </summary>
        public static void MeasureMenuVisible(this IGamePlatform platform, string what) =>
            platform.MeasureVisible(MenuCategory, what);

        /// <summary>
        /// The menu greeted a new league with its popup: <c>menu / league-bronze, silver, gold, legend / visible</c>.
        /// The leagues list opening is <c>menu / leagues / interact</c> (<see cref="MeasureMenuPressed"/>).
        /// </summary>
        public static void MeasureLeagueShown(this IGamePlatform platform, string league) =>
            platform.MeasureVisible(MenuCategory, "league-" + league);

        /// <summary>
        /// <c>result / league-&lt;id&gt; / visible</c>: NEW LEAGUE! over the result of the round that reached the league
        /// (after the branch feature/result-popups). The menu's <c>menu / league-&lt;id&gt;</c> then stays for a league
        /// the result did not greet.
        /// </summary>
        public static void MeasureResultLeagueShown(this IGamePlatform platform, string league) =>
            platform.MeasureVisible(ResultCategory, "league-" + league);

        /// <summary><c>result / home / visible</c> on every result, <c>interact</c> when HOME is pressed.</summary>
        public static void MeasureHomeShown(this IGamePlatform platform) => platform.MeasureVisible(ResultCategory, "home");

        public static void MeasureHomePressed(this IGamePlatform platform) =>
            platform.MeasureInteract(ResultCategory, "home");

        private static string ReadyBucket(float seconds) =>
            seconds < 3f ? "ready-0-3" : seconds < 6f ? "ready-3-6" : seconds < 10f ? "ready-6-10" : "ready-10plus";

        /// <summary>A commercial break is asked for under the curtain after NEXT MATCH (Poki may still show none).</summary>
        public static void MeasureCommercialBreak(this IGamePlatform platform) =>
            platform.MeasureVisible(AdCategory, "commercial");

        /// <summary>
        /// How long a commercial break held the game, in real seconds: <c>ad / commercial-none, short, mid, long /
        /// length</c>, none for one back too soon to have played (as a rewarded one's unavailable), then under 10, under
        /// 25 and longer. Sent after the break, so nothing goes out while it plays.
        /// </summary>
        public static void MeasureCommercialLength(this IGamePlatform platform, float seconds) =>
            platform.Measure(AdCategory, seconds < 2f ? "commercial-none" : seconds < 10f ? "commercial-short"
                : seconds < 25f ? "commercial-mid" : "commercial-long", "length");

        /// <summary>
        /// A screen's frames per second over its sample (<see cref="ScreenFrameRate"/>): <c>fps / round-touch-55 /
        /// screen</c>, one number.
        /// </summary>
        public static void MeasureScreenFps(this IGamePlatform platform, ShownScreen screen, bool isTouch, float fps) =>
            platform.Measure(FpsCategory, $"{ScreenName(screen)}-{Device(isTouch)}-{FpsBucket(fps)}", "screen");

        /// <summary>
        /// The median of the frames counted in each of the visit's first minute of seconds, as Poki's Player Fit Test
        /// counts its own: <c>fps / visit-mouse-60 / visit</c>.
        /// </summary>
        public static void MeasureVisitFps(this IGamePlatform platform, bool isTouch, float fps) =>
            platform.Measure(FpsCategory, $"visit-{Device(isTouch)}-{FpsBucket(fps)}", "visit");

        /// <summary>
        /// Frames per second rounded to 5, so 58 to 62 read 60, and 70plus above: a screen refreshed faster than 60
        /// times a second counts more frames, as Poki's own counter does.
        /// </summary>
        public static string FpsBucket(float fps)
        {
            var bucket = Mathf.Clamp(Mathf.RoundToInt(fps / 5f) * 5, 0, 70);
            return bucket >= 70 ? "70plus" : bucket.ToString();
        }

        private static string Device(bool isTouch) => isTouch ? "touch" : "mouse";

        private static string ScreenName(ShownScreen screen)
        {
            switch (screen) {
                case ShownScreen.Round:
                    return "round";
                case ShownScreen.Result:
                    return "result";
                case ShownScreen.Curtain:
                    return "curtain";
                case ShownScreen.Menu:
                    return "menu";
                case ShownScreen.Garage:
                    return "garage";
                case ShownScreen.Ad:
                    return "ad";
                default:
                    return "none";
            }
        }

        /// <summary>The result offers CLAIM ×2, once per result.</summary>
        public static void MeasureDoubleShown(this IGamePlatform platform) =>
            platform.MeasureVisible(AdCategory, DoubleReward);

        public static void MeasureDoublePressed(this IGamePlatform platform) =>
            platform.MeasureInteract(AdCategory, DoubleReward);

        /// <summary>
        /// How the rewarded ad of CLAIM ×2 ended: granted, unavailable (back without a reward too soon to have played)
        /// or dismissed.
        /// </summary>
        public static void MeasureDoubleEnded(this IGamePlatform platform, string how) =>
            platform.Measure(AdCategory, DoubleReward, how);

        /// <summary>
        /// XP BOOST in the main menu: <c>ad / boost / visible</c> each time the menu shows its offer, <c>interact</c> on
        /// WATCH in its popup, then how the rewarded ad ended, as for CLAIM ×2 (<see cref="MeasureDoubleEnded"/>):
        /// granted, unavailable or dismissed. Opening the popup and NO THANKS are <c>menu / boost, boost-no /
        /// interact</c> (<see cref="MeasureMenuPressed"/>).
        /// </summary>
        public static void MeasureBoostShown(this IGamePlatform platform) =>
            platform.MeasureVisible(AdCategory, XpBoost);

        public static void MeasureBoostPressed(this IGamePlatform platform) =>
            platform.MeasureInteract(AdCategory, XpBoost);

        public static void MeasureBoostEnded(this IGamePlatform platform, string how) =>
            platform.Measure(AdCategory, XpBoost, how);

        /// <summary>
        /// The daily gift in the main menu: <c>daily / offer / visible</c> each time its popup shows a gift still to
        /// claim (by itself on the menu or from the DAILY tile, which is <c>menu / daily / interact</c>), then on the
        /// claim <c>daily / day-N / claim</c>, N the calendar day 1..7 (one number), with <c>daily / reset / claim</c>
        /// beside it when a missed day started the calendar again and <c>daily / hat / claim</c> when the last day gave the
        /// hat. Closing it without claiming is <c>menu / daily-no / interact</c>. Over a visit's first result (no menu
        /// offered it) the popup also sends <c>daily / offer-result / visible</c>, and closing it there unclaimed is
        /// <c>result / daily-no / interact</c>.
        /// </summary>
        public static void MeasureDailyShown(this IGamePlatform platform) =>
            platform.MeasureVisible(DailyCategory, "offer");

        public static void MeasureDailyResultShown(this IGamePlatform platform) =>
            platform.MeasureVisible(DailyCategory, "offer-result");

        public static void MeasureDailyResultDeclined(this IGamePlatform platform) =>
            platform.MeasureInteract(ResultCategory, "daily-no");

        public static void MeasureDailyClaimed(this IGamePlatform platform, DailyClaim claim)
        {
            const string action = "claim";
            platform.Measure(DailyCategory, $"day-{claim.Slot + 1}", action);
            if (claim.IsReset) {
                platform.Measure(DailyCategory, "reset", action);
            }

            if (claim.Hat != null) {
                platform.Measure(DailyCategory, "hat", action);
            }
        }

        /// <summary>
        /// CLAIM ×2 on the daily gift: <c>ad / daily / visible</c> when the popup offers it, <c>interact</c> on the press,
        /// then how the rewarded ad ended, as for CLAIM ×2 on the result: granted, unavailable or dismissed.
        /// </summary>
        public static void MeasureDailyAdShown(this IGamePlatform platform) =>
            platform.MeasureVisible(AdCategory, DailyAd);

        public static void MeasureDailyAdPressed(this IGamePlatform platform) =>
            platform.MeasureInteract(AdCategory, DailyAd);

        public static void MeasureDailyAdEnded(this IGamePlatform platform, string how) =>
            platform.Measure(AdCategory, DailyAd, how);

        /// <summary>
        /// A car opened by a rewarded ad in the garage (BUGGY): <c>shop / ad-&lt;id&gt; / visible</c> when its UNLOCK? window
        /// opens, <c>ad / car-&lt;id&gt; / interact</c> on WATCH AD, then how the ad ended (granted, unavailable or dismissed,
        /// as for CLAIM ×2).
        /// </summary>
        public static void MeasureAdCarShown(this IGamePlatform platform, string carId) =>
            platform.MeasureVisible("shop", $"ad-{carId}");

        public static void MeasureAdCarPressed(this IGamePlatform platform, string carId) =>
            platform.MeasureInteract(AdCategory, $"car-{carId}");

        public static void MeasureAdCarEnded(this IGamePlatform platform, string carId, string how) =>
            platform.Measure(AdCategory, $"car-{carId}", how);

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

        /// <summary>A special round starting at GO!; a plain round sends nothing.</summary>
        public static void MeasureSpecialStart(this IGamePlatform platform, SpecialRound kind)
        {
            if (kind != SpecialRound.None) {
                platform.MeasureStart(SpecialCategory, RoundRules.IdOf(kind));
            }
        }

        /// <summary>
        /// A special round's end: <c>complete</c> or <c>fail</c> beside the round's, how it went, and in a boss round
        /// whether the player knocked the boss off.
        /// </summary>
        public static void MeasureSpecialEnd(this IGamePlatform platform, SpecialRound kind, RoundResult result,
            bool bossTaken)
        {
            if (kind == SpecialRound.None) {
                return;
            }

            var id = RoundRules.IdOf(kind);
            // A timed round never ends with the player out: it fails when its place is a loss.
            if (result.IsTimed ? result.IsLoss : result.Outcome == RoundOutcome.Out) {
                platform.MeasureFail(SpecialCategory, id);
            }
            else {
                platform.MeasureComplete(SpecialCategory, id);
            }

            platform.Measure(SpecialCategory, $"{id}-{GameEvent.Clean(result.Outcome.ToString())}", "outcome");
            // An event mode's place by its own id: the round's place event is not sent for a timed mode (it means a
            // score there, not the order the cars fell in), and one table reads every mode the same way.
            if (EventModes.IsEvent(kind) && result.CarCount > 1) {
                platform.Measure(SpecialCategory, $"{id}-place-{result.Place}", "place");
            }
            if (kind == SpecialRound.Boss) {
                platform.Measure(SpecialCategory, bossTaken ? "boss-taken" : "boss-missed", "boss");
            }

            platform.MeasureTeamEnd(kind, result);
        }

        /// <summary>
        /// The end of the round started as <paramref name="round"/>, and the milestones of its result.
        /// </summary>
        /// <remarks>
        /// The first-round controls events (input, first move, boost input, swipes) go out only for a round that waited
        /// on the start screen, <paramref name="isStartRound"/>: the menu's first round counts down instead, and its
        /// first move would read the countdown as the player's hesitation. Null keeps the old rule, the first round.
        /// In a mode where nobody falls for good (<paramref name="hasFalls"/> false: HOT POTATO's fence, where the player
        /// is out only by the blast) the out causes (<c>round-out-cause</c>, <c>fall-cause</c>) are not sent: the mode's
        /// own are the <c>special / &lt;id&gt;…</c> events.
        /// </remarks>
        public static void MeasureRoundEnd(this IGamePlatform platform, int round, RoundResult result, float duration,
            PlayerDriver driver, bool hasBoost, bool? isStartRound = null, bool hasFalls = true)
        {
            var measuresControls = isStartRound ?? round <= 1;
            if (result.IsMatch) {
                MeasureMatchEnd(platform, round, result, driver, hasBoost, measuresControls);
                return;
            }

            if (result.IsTimed) {
                MeasureTimedEnd(platform, round, result, driver, hasBoost, measuresControls);
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
            if (result.Outcome == RoundOutcome.Out && hasFalls) {
                var cause = result.KnockedOutBy == RoundResult.Nobody ? "fell" : "pushed";
                platform.Measure(ResultCategory, $"{cause}-{tag}", "round-out-cause");
                if (result.KnockedOutBy == RoundResult.Nobody) {
                    var fallCause = GameEvent.Clean(result.FallCause.ToString());
                    platform.Measure(ResultCategory, $"fell-{fallCause}-{tag}", "fall-cause");
                }
            }

            MeasureBoosts(platform, tag, driver, hasBoost);
            if (measuresControls) {
                MeasureControls(platform, driver, hasBoost);
            }
        }

        /// <summary>
        /// The end of a timed event mode's round (<see cref="ModeRules.IsTimed"/>), in a build of rounds. It keeps the
        /// round's funnel, <c>round / N / complete</c> (it always runs its time, as the match does), and the events that
        /// still mean the same: the colour, the cars in it, the boosts and, in the first, the controls. The outcome,
        /// place, knock-outs, time, round time, revenge and out causes mean a score or never change there, so they are
        /// not sent: the mode's own are <c>special / &lt;id&gt;…</c> (<see cref="MeasureSpecialEnd"/>).
        /// </summary>
        private static void MeasureTimedEnd(IGamePlatform platform, int round, RoundResult result, PlayerDriver driver,
            bool hasBoost, bool measuresControls)
        {
            platform.MeasureComplete(RoundCategory, RoundBucket(round));
            var tag = RoundTag(round);
            // A round in teams paints the player's car in the team's colour: that is not a colour the player chose.
            if (result.Teams == 0) {
                platform.Measure(ResultCategory, $"color-{CarPaint.NameOf(result.ColorOf(0))}-{tag}", "color");
            }

            platform.Measure(ResultCategory, $"cars-{result.CarCount}-{tag}", "cars");
            MeasureBoosts(platform, tag, driver, hasBoost);
            if (measuresControls) {
                MeasureControls(platform, driver, hasBoost);
            }
        }

        /// <summary>
        /// The end of a long match. Of the round's result events it keeps those that still mean the same: the colour,
        /// the cars in it and, in the first, the controls. The outcome, place, knock-outs, time, round time, revenge,
        /// out causes and boosts mean something else over a whole match or never change (it always runs its time), so
        /// the match sends its own in their place, and a version's reports never mix the two.
        /// </summary>
        public static void MeasureMatchEnd(this IGamePlatform platform, int match, RoundResult result, PlayerDriver driver,
            bool hasBoost, bool? isStartMatch = null)
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
            MeasureBoosts(platform, tag, driver, hasBoost);
            if (isStartMatch ?? match <= 1) {
                MeasureControls(platform, driver, hasBoost);
            }
        }

        /// <summary>
        /// The roll of a box the player broke landed on an item, in match or round number <paramref name="match"/>:
        /// <c>match / item-mega, shockwave, shield, turbo, bomb, glove, bowling, balloon, oil-rR / item-get</c>, R as for the match's other
        /// events; in a round of last-car-standing (after v0.7.1) the same under <c>result</c>. Replaces v0.6's
        /// <c>pickup</c> event, when a pickup was its ability.
        /// </summary>
        public static void MeasureItemGet(this IGamePlatform platform, int match, ItemKind kind, bool isMatch = true) =>
            platform.Measure(ItemCategory(isMatch), $"item-{ItemId(kind)}-{RoundTag(match)}", "item-get");

        /// <summary>
        /// The player used the item in the slot: <c>match / item-kind-rR / item-use</c> (<c>result</c> in a round);
        /// against item-get it tells which items the players find and which they forget to use.
        /// </summary>
        public static void MeasureItemUse(this IGamePlatform platform, int match, ItemKind kind, bool isMatch = true) =>
            platform.Measure(ItemCategory(isMatch), $"item-{ItemId(kind)}-{RoundTag(match)}", "item-use");

        private static string ItemCategory(bool isMatch) => isMatch ? MatchCategory : ResultCategory;

        // The kind as the events spell it, without digits.
        private static string ItemId(ItemKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>
        /// How many boxes the player broke in the match, an item in them or not, at its end:
        /// <c>match / pickups-N-rR / pickups</c>, N from 0 to 9 or 10plus; <c>result</c> in a round with boxes.
        /// </summary>
        public static void MeasureMatchPickups(this IGamePlatform platform, int match, int count, bool isMatch = true) =>
            platform.Measure(ItemCategory(isMatch), $"pickups-{CountBucket(count)}-{RoundTag(match)}", "pickups");

        // One number, so the match's tag still fits beside it.
        private static string CountBucket(int count)
        {
            return count >= LastCountBucket ? $"{LastCountBucket}plus" : Mathf.Max(0, count).ToString();
        }

        /// <summary>
        /// Whether a new player found the controls, and with what: the first round tells it, later ones only repeat it.
        /// </summary>
        private static void MeasureControls(IGamePlatform platform, PlayerDriver driver, bool hasBoost)
        {
            if (driver == null) {
                return;
            }

            var input = driver.FirstInput;
            platform.Measure(ControlsCategory, input == null ? "none" : GameEvent.Clean(input.Value.ToString()), "input");

            if (hasBoost) {
                var boost = driver.FirstBoostInput;
                var boostInput = boost == null ? "none" : GameEvent.Clean(boost.Value.ToString());
                platform.Measure(ControlsCategory, $"boost-{boostInput}", "boost-input");
            }

            var seconds = driver.FirstMoveTime;
            var move = seconds < 0f ? "never" : seconds < 3f ? "move-0-3" : seconds < 10f ? "move-3-10" : "move-10plus";
            platform.Measure(ControlsCategory, move, "first-move");

            var swipes = driver.Swipes == 0 ? "none" : driver.Swipes < 4 ? "few" : "many";
            platform.Measure(ControlsCategory, $"swipes-{swipes}", "swipes");
        }

        // In words, so the round still fits beside it: a time bucket's two numbers would make three.
        private static string TimeSpanOf(float seconds)
        {
            return seconds < 15f ? "early" : seconds < 30f ? "mid" : seconds < 60f ? "late" : "full";
        }

        /// <summary>
        /// The player's own boosts and the game's for them in a round or match tagged <paramref name="tag"/>, without
        /// numbers but the round's, so neither event goes over <see cref="GameEvent.MaxNumbers"/>. A build without boosts
        /// sends neither: they could only say none.
        /// </summary>
        private static void MeasureBoosts(IGamePlatform platform, string tag, PlayerDriver driver, bool hasBoost)
        {
            if (driver == null || !hasBoost) {
                return;
            }

            platform.Measure(ResultCategory, $"boosts-{AmountOf(driver.Boosts)}-{tag}", "round-boosts");
            platform.Measure(ResultCategory, $"auto-{AmountOf(driver.AutoBoosts)}-{tag}", "auto-boosts");
        }

        private static string AmountOf(int count) => count == 0 ? "none" : count <= 2 ? "few" : "many";

        public static void MeasurePlayAgainShown(this IGamePlatform platform, RoundOutcome outcome, int round,
            RoundRecord record = default)
        {
            platform.MeasureVisible(ResultCategory, PlayAgain);
            platform.MeasureVisible(ResultCategory, AgainOf(outcome, round));
            if (record.Missions != null) {
                platform.MeasureVisible(ResultCategory, AgainOfMissions(outcome, round, record));
                platform.MeasureVisible(ResultCategory, AgainOfStreak(outcome, round, record.Streak));
            }
        }

        public static void MeasurePlayAgainPressed(this IGamePlatform platform, RoundOutcome outcome, int round,
            RoundRecord record = default)
        {
            platform.MeasureInteract(ResultCategory, PlayAgain);
            platform.MeasureInteract(ResultCategory, AgainOf(outcome, round));
            if (record.Missions != null) {
                platform.MeasureInteract(ResultCategory, AgainOfMissions(outcome, round, record));
                platform.MeasureInteract(ResultCategory, AgainOfStreak(outcome, round, record.Streak));
            }
        }

        private static string AgainOfMissions(RoundOutcome outcome, int round, RoundRecord record) =>
            $"again-{GameEvent.Clean(outcome.ToString())}-{(record.Missions.Completed.Count > 0 ? "mission" : "nomission")}-{RoundTag(round)}";

        private static string AgainOfStreak(RoundOutcome outcome, int round, StreakChange streak) =>
            $"again-{GameEvent.Clean(outcome.ToString())}-{(streak.IsLost ? "lost" : streak.IsStreak ? "streak" : "nostreak")}-{RoundTag(round)}";

        /// <summary>
        /// What a decided round did for the missions, the streak and the XP boost (<see cref="RoundRecord.Missions"/>,
        /// <see cref="RoundRecord.Streak"/>, <see cref="XpGain.BoostXp"/>), from the saved record:
        /// <c>result / boosted-rR / xp-boost</c> for a boosted round.
        /// </summary>
        public static void MeasureMeta(this IGamePlatform platform, int round, RoundRecord record, int missionsDone)
        {
            var tag = RoundTag(round);
            if (record.Missions != null) {
                foreach (var mission in record.Missions.Completed) {
                    platform.Measure(MissionCategory, mission.Id, "complete");
                }

                platform.Measure(MissionCategory, $"done-{CountBucket(missionsDone)}-{tag}", "missions");
            }

            var streak = record.Streak.After;
            var length = streak >= LastStreakBucket ? $"{LastStreakBucket}plus" : Mathf.Max(0, streak).ToString();
            platform.Measure(ResultCategory, $"streak-{length}-{tag}", "streak");
            if (record.Streak.IsLost) {
                platform.Measure(ResultCategory, $"streak-lost-{tag}", "streak");
            }

            // A round the XP boost paid for: how many rounds the bought boosts reach, against the boosts granted.
            if (record.XpGain.BoostXp > 0) {
                platform.Measure(ResultCategory, $"boosted-{tag}", "xp-boost");
            }
        }

        /// <summary>
        /// What a decided round did for the day: <c>daily / first-win-rR / win</c> when it was the day's first win and
        /// paid twice (R the round of the visit, one number), <c>daily / &lt;task-id&gt; / complete</c> for each daily task
        /// it completed and then <c>daily / tasks-N / done</c>, N the day's tasks done by then (1..3, one number): how many
        /// players reach one, two and all three tasks a day.
        /// </summary>
        public static void MeasureDailyRound(this IGamePlatform platform, int round, bool isFirstWin,
            DailyTaskRound tasks)
        {
            if (isFirstWin) {
                platform.Measure(DailyCategory, $"first-win-{RoundTag(round)}", "win");
            }

            if (tasks.Completed.Count == 0) {
                return;
            }

            foreach (var task in tasks.Completed) {
                platform.Measure(DailyCategory, $"task-{task.Id}", "complete");
            }

            platform.Measure(DailyCategory, $"tasks-{tasks.DoneToday}", "done");
        }

        /// <summary>The daily popup's TASKS tab shown: <c>daily / tasks / visible</c>.</summary>
        public static void MeasureDailyTasksShown(this IGamePlatform platform) =>
            platform.MeasureVisible(DailyCategory, "tasks");

        public static void MeasureMissionsShown(this IGamePlatform platform) =>
            platform.Measure(MissionCategory, "shown", "visible");

        public static void MeasureTopperBought(this IGamePlatform platform, string topperId) =>
            platform.Measure("shop", topperId, "buy");

        /// <summary>A car bought for coins, in the garage or from its level's popup.</summary>
        public static void MeasureCarBought(this IGamePlatform platform, string carId) =>
            platform.Measure("shop", $"car-{carId}", "buy");

        /// <summary>The popup that offers a car its level just opened, and its BUY pressed.</summary>
        public static void MeasureCarOfferShown(this IGamePlatform platform, string carId) =>
            platform.MeasureVisible("shop", $"offer-{carId}");

        public static void MeasureCarOfferPressed(this IGamePlatform platform, string carId) =>
            platform.MeasureInteract("shop", $"offer-{carId}");

        /// <summary>The popup of a free car its level just opened, and its EQUIP pressed (CONTINUE leaves the car).</summary>
        public static void MeasureCarEquipShown(this IGamePlatform platform, string carId) =>
            platform.MeasureVisible("shop", $"equip-{carId}");

        public static void MeasureCarEquipPressed(this IGamePlatform platform, string carId) =>
            platform.MeasureInteract("shop", $"equip-{carId}");

        /// <summary>The result's one popup that offers a topper for coins, and its BUY &amp; EQUIP pressed.</summary>
        public static void MeasureHatOfferShown(this IGamePlatform platform, string topperId) =>
            platform.MeasureVisible("shop", $"hat-offer-{topperId}");

        public static void MeasureHatOfferPressed(this IGamePlatform platform, string topperId) =>
            platform.MeasureInteract("shop", $"hat-offer-{topperId}");

        /// <summary>The result's popup shows the free topper (<c>gift / &lt;id&gt; / visible</c>), and EQUIP on it.</summary>
        public static void MeasureGiftShown(this IGamePlatform platform, string topperId) =>
            platform.MeasureVisible("gift", topperId);

        public static void MeasureGiftWorn(this IGamePlatform platform, string topperId) =>
            platform.MeasureInteract("gift", topperId);

        public static void MeasureTopperEquip(this IGamePlatform platform, string topperId) =>
            platform.Measure("garage", $"topper-{(string.IsNullOrEmpty(topperId) ? "none" : topperId)}", "equip");

        public static void MeasureToppersShown(this IGamePlatform platform) => platform.MeasureVisible("garage", "toppers");

        public static void MeasureToppersPressed(this IGamePlatform platform) =>
            platform.MeasureInteract("garage", "toppers");

        /// <summary>
        /// The NEXT MATCH press after the first round or match of a visit, by what the player first steered with and
        /// whether they boosted: <c>controls / again-touch-noboost / visible, interact</c>. Poki counts each event on
        /// its own, so this is the one way to see whether the visits that leave there are the ones that never found the
        /// boost. Read from the driver before the next arena replaces it.
        /// </summary>
        public static string FirstAgainOf(PlayerDriver driver)
        {
            var input = driver.FirstInput == null ? "none" : GameEvent.Clean(driver.FirstInput.Value.ToString());
            return $"again-{input}-{(driver.FirstBoostInput == null ? "noboost" : "boost")}";
        }

        public static void MeasureFirstAgainShown(this IGamePlatform platform, string firstAgain) =>
            platform.MeasureVisible(ControlsCategory, firstAgain);

        public static void MeasureFirstAgainPressed(this IGamePlatform platform, string firstAgain) =>
            platform.MeasureInteract(ControlsCategory, firstAgain);

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

        /// <summary>
        /// The arenas the levels of this reward opened, from the tuning, as the cars are; and the leagues' hats it
        /// reached (<c>progress / hat-id / unlock</c>; a league's car is already among the cars).
        /// </summary>
        public static void MeasureMapUnlocks(this IGamePlatform platform, XpGain gain, GameTuning tuning)
        {
            foreach (var id in ArenaRotation.OpenedBetween(tuning, gain.Before.Level, gain.After.Level)) {
                platform.Measure("progress", $"map-{id}", "unlock");
            }

            foreach (var league in tuning.LeagueRewards.All) {
                if (league.Level > gain.Before.Level && league.Level <= gain.After.Level) {
                    platform.Measure("progress", $"hat-{league.Topper}", "unlock");
                }
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
