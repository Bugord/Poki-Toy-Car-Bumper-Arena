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
        public readonly XpLevel Before;
        public readonly XpLevel After;
        public readonly IReadOnlyList<string> UnlockedCarIds;

        public int Total => RoundXp + PlaceXp + WinXp + KnockoutXp;

        public XpGain(int totalBefore, int totalAfter, int roundXp, int placeXp, int winXp, int knockoutXp,
            XpLevel before, XpLevel after, IReadOnlyList<string> unlockedCarIds)
        {
            this.TotalBefore = totalBefore;
            this.TotalAfter = totalAfter;
            this.RoundXp = roundXp;
            this.PlaceXp = placeXp;
            this.WinXp = winXp;
            this.KnockoutXp = knockoutXp;
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
