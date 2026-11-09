using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The daily gift of the main menu (<see cref="DailySettings"/>), apart from the main file as it only reaches the
    /// players who come back: a calendar of days in a row, coins on each and a hat sold nowhere else on the last.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Daily")]
        [Tooltip("The daily gift: coins for each day in a row and the hat that closes the week")]
        [SerializeField]
        private DailySettings daily = new();

        public DailySettings Daily => daily;

        /// <summary>
        /// One gift a calendar day, claimed in the main menu (or over a visit's first result, <see cref="IsOnResult"/>).
        /// Days in a row climb the calendar; a missed day starts it again from the first. The last day gives <see cref="Hat"/>, one of the exclusive toppers, and once the player
        /// owns it, <see cref="HatCoins"/> instead; the week after the last day starts again from the first.
        /// </summary>
        [Serializable]
        public sealed class DailySettings
        {
            [Tooltip("Whether the main menu has the daily gift at all")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("Coins on each day in a row but the last, from the first: about a round's at first, " +
                "rising so a week in a row is worth coming back for")]
            [SerializeField]
            private int[] dayCoins = { 40, 60, 80, 100, 120, 160 };

            [Tooltip("The exclusive topper the last day gives (TopperSettings' exclusive list)")]
            [SerializeField]
            private string hat = "wizard-hat";

            [Tooltip("Coins the last day gives once the player owns the hat")]
            [Min(0)]
            [SerializeField]
            private int hatCoins = 300;

            [Tooltip("What CLAIM ×2 multiplies a day's coins by, for a rewarded ad; not on the hat's day")]
            [Min(1)]
            [SerializeField]
            private int adMultiplier = 2;

            [Tooltip("Whether the first result of a visit offers today's gift too, when no menu has: a first visit " +
                "goes straight to its round, so without it a new player would not hear of the calendar")]
            [SerializeField]
            private bool onResult = true;

            public bool IsEnabled => enabled && dayCoins.Length > 0;

            /// <summary>The days of the calendar: those with coins and the hat's.</summary>
            public int Days => dayCoins.Length + 1;

            public string Hat => hat;
            public int HatCoins => hatCoins;
            public int AdMultiplier => Math.Max(1, adMultiplier);
            public bool IsOnResult => onResult;

            /// <summary>The calendar slot (0 for the first day) of the <paramref name="streak"/>-th day in a row, from 1.</summary>
            public int SlotOf(int streak) => (Math.Max(1, streak) - 1) % Days;

            /// <summary>Whether the slot is the last, the hat's.</summary>
            public bool IsHatSlot(int slot) => slot == Days - 1;

            /// <summary>The coins of a slot but the last.</summary>
            public int CoinsOf(int slot) => slot >= 0 && slot < dayCoins.Length ? Math.Max(0, dayCoins[slot]) : HatCoins;
        }
    }
}
