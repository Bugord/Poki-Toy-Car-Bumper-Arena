using System;
using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The daily gift of the main menu: one claim a calendar day, the days in a row climbing a calendar of
    /// <see cref="GameTuning.DailySettings.Days"/> (coins, then the exclusive hat on the last), saved in the kit's
    /// storage so it lives across visits. A missed day starts the calendar again from its first day; past the last day
    /// it starts again too, the hat's day then paying coins to a player who owns the hat. Lives in the app scope beside
    /// <see cref="PlayerWallet"/>, which it pays, and like it saves at once.
    /// </summary>
    /// <remarks>
    /// Days are calendar days on the player's clock (<see cref="CalendarDay"/>), as the visits count them. A clock turned
    /// back before the last claim gives nothing until it passes that day again, and keeps the streak. Every call takes
    /// the time, so the tests drive the days.
    /// </remarks>
    public sealed class PlayerDaily
    {
        private const string DayKey = "daily_day";
        private const string StreakKey = "daily_streak";
        private const string WinDayKey = "daily_win_day";

        private readonly IKeyValueStorage storage;
        private readonly GameTuning tuning;
        private readonly PlayerWallet wallet;

        public PlayerDaily(IKeyValueStorage storage, GameTuning tuning, PlayerWallet wallet)
        {
            this.storage = storage;
            this.tuning = tuning;
            this.wallet = wallet;
        }

        private GameTuning.DailySettings Settings => tuning.Daily;

        public bool IsEnabled => Settings.IsEnabled;

        /// <summary>Whether a gift was ever claimed: a calendar that restarts after a gap is a reset, not a first day.</summary>
        public bool HasClaimed => storage.HasKey(DayKey);

        /// <summary>Whether today's gift is still to claim.</summary>
        public bool CanClaim(DateTime now) => IsEnabled && (!HasClaimed || CalendarDay.Of(now) > storage.GetInt(DayKey));

        /// <summary>Whether a claim now would start the calendar again after a missed day (never on the first claim).</summary>
        public bool IsBroken(DateTime now) => CanClaim(now) && HasClaimed && CalendarDay.Of(now) > storage.GetInt(DayKey) + 1;

        /// <summary>
        /// The days in a row, from 1, that the calendar shows: today's while it is still to claim, otherwise the one
        /// claimed last (today's, or the last before a clock turned back).
        /// </summary>
        public int Streak(DateTime now)
        {
            var saved = Math.Max(0, storage.GetInt(StreakKey));
            if (!CanClaim(now)) {
                return Math.Max(1, saved);
            }

            return HasClaimed && CalendarDay.Of(now) == storage.GetInt(DayKey) + 1 ? saved + 1 : 1;
        }

        /// <summary>The calendar slot of today, 0 for the first day.</summary>
        public int Slot(DateTime now) => Settings.SlotOf(Streak(now));

        /// <summary>The hat the calendar's last day gives, or null once the player owns it (the day pays coins then).</summary>
        public string HatOf(int slot) => Settings.IsHatSlot(slot) && !wallet.IsOwned(Settings.Hat) &&
            tuning.Toppers.Find(Settings.Hat) != null ? Settings.Hat : null;

        /// <summary>The coins of a calendar slot; 0 for the hat's while it still gives the hat.</summary>
        public int CoinsOf(int slot) => HatOf(slot) != null ? 0 : Settings.CoinsOf(slot);

        /// <summary>
        /// The slot the next gift will be on once today's is claimed: tomorrow's, if the player comes back.
        /// </summary>
        public int NextSlot(DateTime now)
        {
            var streak = CanClaim(now) ? Streak(now) : Math.Max(1, storage.GetInt(StreakKey));
            return Settings.SlotOf(streak + 1);
        }

        /// <summary>The time left until the next gift: to the next midnight on the player's clock, zero while one waits.</summary>
        public TimeSpan TimeToNext(DateTime now) => CanClaim(now) ? TimeSpan.Zero : now.Date.AddDays(1) - now;

        /// <summary>
        /// Claims today's gift, saved before anything shows it: the day's coins times <paramref name="multiplier"/> (CLAIM
        /// ×2 after a rewarded ad), or the hat, given and worn. Null, with nothing changed, when today's gift was claimed.
        /// </summary>
        public DailyClaim Claim(DateTime now, int multiplier = 1)
        {
            if (!CanClaim(now)) {
                return null;
            }

            var streak = Streak(now);
            var isReset = IsBroken(now);
            var slot = Settings.SlotOf(streak);
            var hat = HatOf(slot);
            storage.SetInt(DayKey, CalendarDay.Of(now));
            storage.SetInt(StreakKey, streak);

            if (hat != null && wallet.Give(hat)) {
                return new DailyClaim(streak, slot, 0, hat, isReset, wallet.Coins, wallet.Coins);
            }

            var coins = Settings.CoinsOf(slot) * Math.Max(1, multiplier);
            var gain = wallet.AddBonus(coins);
            return new DailyClaim(streak, slot, gain.Total, null, isReset, gain.Before, gain.After);
        }

        /// <summary>
        /// Whether today's first win is still to come: the next round or match the player wins pays
        /// <see cref="GameTuning.DailySettings.FirstWinMultiplier"/> times its own XP and coins. A clock turned back
        /// before the last first win waits for that day to pass again.
        /// </summary>
        public bool HasFirstWin(DateTime now) => Settings.HasFirstWin &&
            (!storage.HasKey(WinDayKey) || CalendarDay.Of(now) > storage.GetInt(WinDayKey));

        /// <summary>The multiplier the first win of the day pays, for the result's stamp and the menu.</summary>
        public int FirstWinMultiplier => Settings.FirstWinMultiplier;

        /// <summary>
        /// The multiplier a decided round's own reward gets: today's first win, saved as taken before it pays, or 1
        /// (a loss or a draw keeps the first win for later; a second win the same day gets nothing more).
        /// </summary>
        public int TakeFirstWin(DateTime now, RoundResult result)
        {
            if (result.Outcome != RoundOutcome.Won || !HasFirstWin(now)) {
                return 1;
            }

            storage.SetInt(WinDayKey, CalendarDay.Of(now));
            return Settings.FirstWinMultiplier;
        }

        /// <summary>Development commands and tests: forgets today's first win (it comes again) or takes it.</summary>
        public void SetFirstWin(DateTime now, bool isTaken)
        {
            if (isTaken) {
                storage.SetInt(WinDayKey, CalendarDay.Of(now));
            }
            else {
                storage.DeleteKey(WinDayKey);
            }
        }

        /// <summary>
        /// Development commands: sets the last claim <paramref name="daysAgo"/> days back with <paramref name="streak"/>
        /// days in a row, so the next claim is due (1 day ago) or starts again (more); a negative value forgets every claim.
        /// </summary>
        public void SetLastClaim(DateTime now, int daysAgo, int streak)
        {
            if (daysAgo < 0) {
                storage.DeleteKey(DayKey);
                storage.DeleteKey(StreakKey);
                return;
            }

            storage.SetInt(DayKey, CalendarDay.Of(now) - daysAgo);
            storage.SetInt(StreakKey, Math.Max(1, streak));
        }
    }

    /// <summary>A claimed daily gift: the day in a row, its slot, the coins it paid (or the hat it gave) and the wallet.</summary>
    public sealed class DailyClaim
    {
        public readonly int Streak;
        public readonly int Slot;
        public readonly int Coins;
        public readonly string Hat;
        public readonly bool IsReset;
        public readonly int CoinsBefore;
        public readonly int CoinsAfter;

        public DailyClaim(int streak, int slot, int coins, string hat, bool isReset, int coinsBefore, int coinsAfter)
        {
            this.Streak = streak;
            this.Slot = slot;
            this.Coins = coins;
            this.Hat = hat;
            this.IsReset = isReset;
            this.CoinsBefore = coinsBefore;
            this.CoinsAfter = coinsAfter;
        }
    }

    /// <summary>Calendar days on the player's clock, counted from day one of the calendar, as the visits and the daily gift count them.</summary>
    public static class CalendarDay
    {
        public static int Of(DateTime date) => (int)(date.Date - DateTime.MinValue.Date).TotalDays;
    }
}
