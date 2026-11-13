using Bugord.App.Platform;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.App
{
    /// <summary>
    /// The FOOTBALL mode's own analytics, beside the shared ones of a mode and of a round in teams
    /// (<see cref="RoundEvents.MeasureSpecialEnd"/>, <see cref="TeamEvents"/>), at the end of a match of football:
    /// <list type="bullet">
    /// <item><c>special / football-goals-N / goals</c>: the goals of both teams in the match, N 0..9 or 10plus. It says
    /// whether a match has enough goals to be fun (the aim is 3 and more) or ends 0 : 0.</item>
    /// <item><c>special / football-scored-N / player-goals</c>: the player's own goals, N 0..4 or 5plus (the reward's cap),
    /// whether the player scores or watches the bots play.</item>
    /// </list>
    /// No numbers in the ids, one in an event (<see cref="GameEvent.MaxNumbers"/>). The team layer's
    /// <c>football-ko-N</c> is always 0 here: nobody falls on the pitch.
    /// </summary>
    public static class FootballEvents
    {
        private const string Category = "special";

        // Goals past this many share one bucket: the match's, and the player's (the reward's cap).
        private const int LastGoalBucket = 10;
        private const int LastPlayerBucket = 5;

        public static void MeasureFootballEnd(this IGamePlatform platform, SpecialRound kind, RoundResult result)
        {
            if (kind != SpecialRound.Football) {
                return;
            }

            var id = RoundRules.IdOf(kind);
            var goals = result.TeamPointsOf(RoundTeams.Blue) + result.TeamPointsOf(RoundTeams.Red);
            platform.Measure(Category, $"{id}-goals-{Bucket(goals, LastGoalBucket)}", "goals");
            platform.Measure(Category, $"{id}-scored-{Bucket(PlayerGoalsOf(result), LastPlayerBucket)}", "player-goals");
        }

        /// <summary>The player's goals: its points, as nothing else scores on the pitch.</summary>
        public static int PlayerGoalsOf(RoundResult result)
        {
            foreach (var standing in result.Standings) {
                if (standing.Index == 0) {
                    return standing.Points;
                }
            }

            return 0;
        }

        private static string Bucket(int count, int last) => count >= last ? $"{last}plus" : count.ToString();
    }
}
