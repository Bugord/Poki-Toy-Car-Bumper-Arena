using System;
using System.Collections.Generic;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// How a round ended for the player.
    /// </summary>
    public enum RoundOutcome
    {
        /// <summary>The player is the last car on the arena.</summary>
        Won,

        /// <summary>The player fell off.</summary>
        Out,

        /// <summary>Time ran out with the player still on the arena, beside other cars.</summary>
        Survived,
    }

    /// <summary>
    /// What preceded a player's fall when no car gets knockout credit. Checked in this order, so a floor collapse
    /// takes priority over a boost or an old hit.
    /// </summary>
    public enum FallCause
    {
        /// <summary>The player stayed on the arena or another car gets knockout credit.</summary>
        None,

        /// <summary>The floor ring under the player fell away.</summary>
        Ring,

        /// <summary>The player boosted shortly before falling.</summary>
        Boost,

        /// <summary>The Ball arena's ball knocked the player, and no car did after it.</summary>
        Ball,

        /// <summary>Another car hit the player too long ago for knockout credit.</summary>
        Slide,

        /// <summary>No recent ring fall, boost or hit explains the fall.</summary>
        Drive,
    }

    /// <summary>
    /// The end of a round as the result screen shows it: how it ended, the player's place (1 is the winner; a car out
    /// is placed behind every car still on the arena), how many cars started, how many of them the player knocked out
    /// and which car knocked the player out, if one did.
    /// </summary>
    public readonly struct RoundResult
    {
        /// <summary>
        /// <see cref="KnockedOutBy"/> when no car gets the credit: the player is still on the arena, or fell off alone.
        /// </summary>
        public const int Nobody = -1;

        public readonly RoundOutcome Outcome;
        public readonly int Place;
        public readonly int CarCount;
        public readonly float Seconds;

        /// <summary>
        /// Cars the player knocked out (see <see cref="ArenaRound.KnockedOut"/>).
        /// </summary>
        public readonly int Knockouts;

        /// <summary>
        /// The spawn index of the car that knocked the player out (1..N, a bot; see <see cref="ArenaRound.IndexOf"/>),
        /// or <see cref="Nobody"/>. An index rather than the car, because the car goes with its scene and the result
        /// outlives it. Colours are captured separately in Colors.
        /// </summary>
        public readonly int KnockedOutBy;

        /// <summary>
        /// What preceded a fall with no knockout credit; None for a knockout, a win or a round survived.
        /// </summary>
        public readonly FallCause FallCause;

        /// <summary>The visit's marked rival was among this round's cars.</summary>
        public readonly bool HadNemesis;

        /// <summary>The player got knockout credit for the marked rival.</summary>
        public readonly bool RevengeTaken;

        /// <summary>
        /// A long match rather than a last-car-standing round: the place is by knock-outs, <see cref="Outcome"/> is
        /// <see cref="RoundOutcome.Won"/> for first place and <see cref="RoundOutcome.Survived"/> otherwise, and the
        /// player is never out at the end, so <see cref="KnockedOutBy"/> is <see cref="Nobody"/>.
        /// </summary>
        public readonly bool IsMatch;

        /// <summary>
        /// Times the player fell off in a match, knocked off or not; 0 in a round, which ends at the first.
        /// </summary>
        public readonly int Falls;

        /// <summary>
        /// The player's falls in a match that a car got the credit for.
        /// </summary>
        public readonly int KnockedOff;

        /// <summary>
        /// Whether the round counts as lost for the visit's losing streak, which eases the bots: a fall in a round, a
        /// place below <see cref="GameTuning.MatchSettings.TopPlaces"/> in a match.
        /// </summary>
        public readonly bool IsLoss;

        public IReadOnlyList<RoundStanding> Standings { get; }
        public IReadOnlyList<int> Colors { get; }
        public int ColorOf(int index) => Colors[index];

        public RoundResult(RoundOutcome outcome, int place, int carCount, float seconds, int knockouts, int knockedOutBy,
            FallCause fallCause, bool hadNemesis, bool revengeTaken, IReadOnlyList<RoundStanding> standings = null,
            IReadOnlyList<int> colors = null, bool isMatch = false, int falls = 0, int knockedOff = 0, bool? isLoss = null)
        {
            Outcome = outcome;
            Place = place;
            CarCount = carCount;
            Seconds = seconds;
            Knockouts = knockouts;
            KnockedOutBy = knockedOutBy;
            FallCause = fallCause;
            HadNemesis = hadNemesis;
            RevengeTaken = revengeTaken;
            Standings = standings ?? Array.Empty<RoundStanding>();
            Colors = colors ?? Array.Empty<int>();
            IsMatch = isMatch;
            Falls = falls;
            KnockedOff = knockedOff;
            IsLoss = isLoss ?? outcome == RoundOutcome.Out;
        }
    }
}
