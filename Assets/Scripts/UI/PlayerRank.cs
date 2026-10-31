using Bugord.App.Localization;
namespace ToyCarArena.UI
{
    /// <summary>
    /// The colour of a level badge. Levels never end, so the rank is what tells a long-time player at a glance: rookie
    /// white, then bronze, silver, gold and legend. Only the look; nothing in the game reads it.
    /// </summary>
    internal static class PlayerRank
    {
        private static readonly int[] Starts = { 1, 10, 25, 50, 100 };
        private static readonly string[] Names = { "ROOKIE", "BRONZE", "SILVER", "GOLD", "LEGEND" };

        /// <summary>0 rookie .. 4 legend.</summary>
        public static int TierOf(int level)
        {
            var tier = 0;
            for (var i = 1; i < Starts.Length; i++) {
                if (level >= Starts[i]) {
                    tier = i;
                }
            }
            return tier;
        }

        /// <summary>The rank's name in the player's language; <see cref="ClassOf"/> keeps the English one.</summary>
        public static string NameOf(int level) => Strings.Get("rank." + Names[TierOf(level)].ToLowerInvariant());

        /// <summary>The level the next rank starts at, or 0 past the last one.</summary>
        public static int NextRankLevel(int level)
        {
            var tier = TierOf(level);
            return tier + 1 < Starts.Length ? Starts[tier + 1] : 0;
        }

        public static string NextRankName(int level)
        {
            var tier = TierOf(level);
            return tier + 1 < Names.Length ? Strings.Get("rank." + Names[tier + 1].ToLowerInvariant()) : null;
        }

        /// <summary>The badge class for the level, level-badge--rookie and the rest (ui_screens).</summary>
        public static string ClassOf(int level) => "level-badge--" + Names[TierOf(level)].ToLowerInvariant();
    }
}
