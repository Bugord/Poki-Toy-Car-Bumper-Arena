using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;

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

        /// <summary>
        /// The prize of the league <paramref name="level"/> is in, as NEW LEAGUE! shows it: the car in the player's
        /// <paramref name="color"/>, the hat from <paramref name="stage"/>; null for a league that gives none.
        /// </summary>
        public static NewLeaguePopup.Prize PrizeAt(GameTuning tuning, CarCatalog catalog, int level, int color,
            GarageStage stage)
        {
            var reward = Of(tuning, PlayerRank.TierOf(level));
            if (reward == null) {
                return null;
            }

            var car = catalog.Find(reward.Car);
            return new NewLeaguePopup.Prize {
                Car = car != null && car.Id == reward.Car ? car : null,
                Color = color,
                Topper = reward.Topper,
                Stage = stage,
            };
        }
    }
}
