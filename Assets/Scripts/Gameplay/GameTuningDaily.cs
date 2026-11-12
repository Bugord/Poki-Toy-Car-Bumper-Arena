using System;
using System.Collections.Generic;
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
        /// Days in a row climb the calendar; a missed day starts it again from the first. The last day gives
        /// <see cref="Hat"/>, one of the exclusive toppers, and once the player owns it, <see cref="HatCoins"/> instead;
        /// the week after the last day starts again from the first. The first win of a calendar day pays twice
        /// (<see cref="FirstWinMultiplier"/>), and each day draws three tasks of its own (<see cref="Tasks"/>), apart
        /// from the missions.
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

            [Header("First win of the day")]
            [Tooltip("What the first round or match the player wins on a calendar day multiplies its own XP and coins " +
                "by (the missions' and tasks' aside); 1 turns it off")]
            [Min(1)]
            [SerializeField]
            private int firstWinMultiplier = 2;

            [Header("Daily tasks")]
            [Tooltip("Whether the three daily tasks run at all")]
            [SerializeField]
            private bool tasksEnabled = true;

            [Tooltip("XP of a done daily task by tier (easy, medium, hard), paid with the round that does it")]
            [SerializeField]
            private int[] taskXp = { 40, 60, 90 };

            [Tooltip("Coins of a done daily task by tier (easy, medium, hard)")]
            [SerializeField]
            private int[] taskCoins = { 40, 60, 100 };

            [Tooltip("The tasks a day draws from, one of each tier: counted over the day's rounds (a Round-slot task " +
                "within one round), with the missions' counters and texts. Ids are saved and measured: no digits")]
            [SerializeField]
            private MissionDefinition[] tasks = DefaultTasks();

            public bool IsEnabled => enabled && dayCoins.Length > 0;

            public int FirstWinMultiplier => Math.Max(1, firstWinMultiplier);
            public bool HasFirstWin => FirstWinMultiplier > 1;

            public bool HasTasks => tasksEnabled && tasks.Length > 0;
            public IReadOnlyList<MissionDefinition> Tasks => tasks;
            public int TaskXpOf(MissionTier tier) => taskXp[Mathf.Clamp((int)tier, 0, taskXp.Length - 1)];
            public int TaskCoinsOf(MissionTier tier) => taskCoins[Mathf.Clamp((int)tier, 0, taskCoins.Length - 1)];

            // A day asks for a little more than a mission: a few rounds' worth, done across the day's visits.
            private static MissionDefinition[] DefaultTasks() => new[] {
                new MissionDefinition("rounds", MissionSlot.Total, MissionCounter.Rounds, 3, MissionTier.Easy),
                new MissionDefinition("knockouts", MissionSlot.Total, MissionCounter.Knockouts, 5, MissionTier.Easy),
                new MissionDefinition("boosts", MissionSlot.Total, MissionCounter.Boosts, 10, MissionTier.Easy),
                new MissionDefinition("boxes", MissionSlot.Total, MissionCounter.Boxes, 6, MissionTier.Easy),
                new MissionDefinition("arena-tabletop", MissionSlot.Total, MissionCounter.ArenaRounds, 2,
                    MissionTier.Easy, map: "tabletop"),
                new MissionDefinition("arena-candy", MissionSlot.Total, MissionCounter.ArenaRounds, 2, MissionTier.Easy,
                    map: "candy"),
                new MissionDefinition("arena-playroom", MissionSlot.Total, MissionCounter.ArenaRounds, 2,
                    MissionTier.Easy, map: "playroom"),
                new MissionDefinition("rounds-more", MissionSlot.Total, MissionCounter.Rounds, 6, MissionTier.Medium),
                new MissionDefinition("knockouts-more", MissionSlot.Total, MissionCounter.Knockouts, 12,
                    MissionTier.Medium),
                new MissionDefinition("win", MissionSlot.Total, MissionCounter.Wins, 1, MissionTier.Medium),
                new MissionDefinition("revenge", MissionSlot.Total, MissionCounter.Revenge, 1, MissionTier.Medium),
                new MissionDefinition("items", MissionSlot.Total, MissionCounter.ItemsUsed, 5, MissionTier.Medium),
                new MissionDefinition("wins", MissionSlot.Total, MissionCounter.Wins, 3, MissionTier.Hard),
                new MissionDefinition("round-knockouts", MissionSlot.Round, MissionCounter.Knockouts, 3,
                    MissionTier.Hard),
                new MissionDefinition("knockouts-most", MissionSlot.Total, MissionCounter.Knockouts, 20,
                    MissionTier.Hard),
                new MissionDefinition("item-knockout", MissionSlot.Total, MissionCounter.ItemKnockouts, 1,
                    MissionTier.Hard),
            };

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
