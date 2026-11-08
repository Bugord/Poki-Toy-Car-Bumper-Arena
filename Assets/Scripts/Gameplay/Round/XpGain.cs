using System.Collections.Generic;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// A saved reward's before/after snapshot, so leaving the result screen early cannot lose XP or unlocks.
    /// </summary>
    public readonly struct XpGain
    {
        public readonly int TotalBefore;
        public readonly int TotalAfter;
        public readonly int RoundXp;
        public readonly int PlaceXp;
        public readonly int WinXp;
        public readonly int KnockoutXp;

        /// <summary>A streak round's extra on the round's own XP.</summary>
        public readonly int StreakXp;

        /// <summary>The XP of the missions the round completed.</summary>
        public readonly int MissionXp;

        /// <summary>
        /// The XP boost's extra on the round's own XP (its streak's share included, its missions not), while a boost
        /// bought with an ad runs; 0 otherwise.
        /// </summary>
        public readonly int BoostXp;

        /// <summary>
        /// The first win of the day's extra on the round's own XP (the streak's and the boost's shares included, the
        /// missions' and daily tasks' not); 0 on any other round.
        /// </summary>
        public readonly int FirstWinXp;
        public readonly XpLevel Before;
        public readonly XpLevel After;
        public readonly IReadOnlyList<string> UnlockedCarIds;

        public int Total => RoundXp + PlaceXp + WinXp + KnockoutXp + StreakXp + BoostXp + FirstWinXp + MissionXp;

        public XpGain(int totalBefore, int totalAfter, int roundXp, int placeXp, int winXp, int knockoutXp,
            XpLevel before, XpLevel after, IReadOnlyList<string> unlockedCarIds, int streakXp = 0, int missionXp = 0,
            int boostXp = 0, int firstWinXp = 0)
        {
            this.TotalBefore = totalBefore;
            this.TotalAfter = totalAfter;
            this.RoundXp = roundXp;
            this.PlaceXp = placeXp;
            this.WinXp = winXp;
            this.KnockoutXp = knockoutXp;
            this.StreakXp = streakXp;
            this.MissionXp = missionXp;
            this.BoostXp = boostXp;
            this.FirstWinXp = firstWinXp;
            this.Before = before;
            this.After = after;
            this.UnlockedCarIds = unlockedCarIds;
        }
    }

    /// <summary>
    /// Progress inside a level, kept beside its price so the result can animate across level boundaries.
    /// </summary>
    public readonly struct XpLevel
    {
        public readonly int Level;
        public readonly int Xp;
        public readonly int Cost;

        public XpLevel(int level, int xp, int cost)
        {
            this.Level = level;
            this.Xp = xp;
            this.Cost = cost;
        }
    }
}
