using Bugord.App.Platform;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.App
{
    /// <summary>
    /// The analytics of a round in teams (<see cref="ModeRules.Teams"/>), sent beside the mode's own end
    /// (<see cref="RoundEvents.MeasureSpecialEnd"/>), by the mode's id so any mode in teams reads the same way:
    /// <list type="bullet">
    /// <item><c>special / &lt;id&gt;-team-won|draw|lost-close|clear|big / team</c>: how the player's team ended, and by how
    /// much (close: 2 points or less apart, clear: 3 to 5, big: 6 and more; a draw is always close). It says whether the
    /// teams are even and the novice's team wins as often as it should.</item>
    /// <item><c>special / &lt;id&gt;-ko-N / team-knockouts</c>: the player's knock-outs in the round, N 0..9 or 10plus,
    /// whether the player plays a part in the team's points or watches the bots.</item>
    /// </list>
    /// No numbers in the ids, at most one in an event (<see cref="GameEvent.MaxNumbers"/>).
    /// </summary>
    public static class TeamEvents
    {
        private const string Category = "special";

        // Knock-outs past this many share one bucket.
        private const int LastKnockoutBucket = 10;

        // Points apart at most for a close end, and at most for a clear one; more is big.
        private const int CloseMargin = 2;
        private const int ClearMargin = 5;

        public static void MeasureTeamEnd(this IGamePlatform platform, SpecialRound kind, RoundResult result)
        {
            if (kind == SpecialRound.None || result.Teams <= 0 || result.TeamPoints.Count < 2) {
                return;
            }

            var id = RoundRules.IdOf(kind);
            platform.Measure(Category, $"{id}-team-{OutcomeOf(result)}-{MarginOf(result)}", "team");
            var knockouts = result.Knockouts >= LastKnockoutBucket ? $"{LastKnockoutBucket}plus" : result.Knockouts.ToString();
            platform.Measure(Category, $"{id}-ko-{knockouts}", "team-knockouts");
        }

        /// <summary>won, draw or lost: the player's team against the other.</summary>
        public static string OutcomeOf(RoundResult result) =>
            result.IsDraw ? "draw" : result.WinningTeam == PlayerTeamOf(result) ? "won" : "lost";

        /// <summary>close, clear or big: how far apart the two teams ended.</summary>
        public static string MarginOf(RoundResult result)
        {
            var margin = System.Math.Abs(result.TeamPointsOf(RoundTeams.Blue) - result.TeamPointsOf(RoundTeams.Red));
            return margin <= CloseMargin ? "close" : margin <= ClearMargin ? "clear" : "big";
        }

        private static int PlayerTeamOf(RoundResult result)
        {
            foreach (var standing in result.Standings) {
                if (standing.Index == 0) {
                    return standing.Team;
                }
            }

            return RoundTeams.Blue;
        }
    }
}
