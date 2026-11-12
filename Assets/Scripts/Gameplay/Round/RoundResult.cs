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
        /// The build's long match (<see cref="GameTuning.IsMatch"/>) rather than a round: the analytics name its events
        /// by it. A match is also timed (<see cref="IsTimed"/>).
        /// </summary>
        public readonly bool IsMatch;

        /// <summary>
        /// A timed round decided by its score rather than a last-car-standing round, the long match and a mode that
        /// scores points alike: the place is by the score, <see cref="Outcome"/> is <see cref="RoundOutcome.Won"/> for
        /// first place and <see cref="RoundOutcome.Survived"/> otherwise, <see cref="Standings"/> holds the score, and
        /// the player is not out at the end, so <see cref="KnockedOutBy"/> is <see cref="Nobody"/>.
        /// </summary>
        public readonly bool IsTimed;

        /// <summary>The kind of round played (<see cref="RoundRules.Kind"/>): a special round's, an event mode's or none.</summary>
        public readonly SpecialRound Kind;

        /// <summary>The most knock-outs the reward pays for (<see cref="ModeRules.KnockoutRewardCap"/>); 0 for no cap.</summary>
        public readonly int KnockoutRewardCap;

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

        /// <summary>
        /// Knock-outs the reward counts on top of <see cref="Knockouts"/>: a boss knocked off by the player pays
        /// <see cref="GameTuning.SpecialRoundSettings.BossReward"/> in all. The stats, missions and bests do not count them.
        /// </summary>
        public readonly int BonusKnockouts;

        /// <summary>The knock-outs the round's XP and coins pay for.</summary>
        public int RewardKnockouts => KnockoutRewardCap > 0
            ? Math.Min(KnockoutRewardCap, Knockouts + BonusKnockouts)
            : Knockouts + BonusKnockouts;

        public IReadOnlyList<RoundStanding> Standings { get; }

        /// <summary>
        /// The car the result shows on its podium: a match's winner; after a round the player's own car on a win, the
        /// car that knocked the player off on a loss to one, and the player's own car otherwise.
        /// </summary>
        public int PodiumCar => IsTimed ? Standings.Count > 0 ? Standings[0].Index : Nobody
            : Outcome == RoundOutcome.Out && KnockedOutBy != Nobody ? KnockedOutBy : 0;
        public IReadOnlyList<int> Colors { get; }
        public int ColorOf(int index) => Colors[index];

        public RoundResult(RoundOutcome outcome, int place, int carCount, float seconds, int knockouts, int knockedOutBy,
            FallCause fallCause, bool hadNemesis, bool revengeTaken, IReadOnlyList<RoundStanding> standings = null,
            IReadOnlyList<int> colors = null, bool isMatch = false, int falls = 0, int knockedOff = 0, bool? isLoss = null,
            int bonusKnockouts = 0, bool? isTimed = null, SpecialRound kind = SpecialRound.None, int knockoutRewardCap = 0)
        {
            IsTimed = isTimed ?? isMatch;
            Kind = kind;
            KnockoutRewardCap = knockoutRewardCap;
            BonusKnockouts = bonusKnockouts;
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
