using ToyCarArena.Gameplay;

namespace ToyCarArena.UI
{
    /// <summary>
    /// What a league gives, by its rank tier (<see cref="PlayerRank"/>): the league's car and hat, or null for a league
    /// that gives none (the first). The popup that welcomes the player to a new league and the league list read it;
    /// the rewards themselves live in <see cref="GameTuning.LeagueRewardSettings"/>, by level.
    /// </summary>
    internal static class LeagueRewards
    {
        public static GameTuning.LeagueReward Of(GameTuning tuning, int tier) =>
            tier > 0 ? tuning.LeagueRewards.AtLevel(PlayerRank.StartOf(tier)) : null;
    }
}
