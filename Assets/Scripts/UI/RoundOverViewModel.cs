using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a round: how it went for the player, against the player's bests, and Play Again. The result is true
    /// once the player asks for another round.
    /// </summary>
    public sealed class RoundOverViewModel : ResultViewModel<bool>, IPayloadReceiver<RoundRecord>
    {
        private readonly ISessionRoster roster;

        public RoundOverViewModel(ISessionRoster roster)
        {
            this.roster = roster;
        }

        public string KnockedOutByName => KnockedOutBy == RoundResult.Nobody ? "" : roster.NameOf(KnockedOutBy);

        public bool RevengeTaken { get; private set; }

        public RoundOutcome Outcome { get; private set; }

        /// <summary>
        /// The player's place, 1 being the winner.
        /// </summary>
        public int Place { get; private set; }

        /// <summary>
        /// How many cars started the round.
        /// </summary>
        public int CarCount { get; private set; }

        public float Seconds { get; private set; }

        /// <summary>
        /// Cars the player knocked out.
        /// </summary>
        public int Knockouts { get; private set; }

        /// <summary>
        /// The index of the car that knocked the player out, which tells its colour, or
        /// <see cref="RoundResult.Nobody"/>.
        /// </summary>
        public int KnockedOutBy { get; private set; }

        /// <summary>
        /// The best place before this round, 0 for none yet.
        /// </summary>
        public int BestPlace { get; private set; }

        /// <summary>
        /// The most knock-outs in a round before this one.
        /// </summary>
        public int BestKnockouts { get; private set; }

        public bool IsBestPlace { get; private set; }

        public bool IsBestKnockouts { get; private set; }

        public void SetPayload(RoundRecord payload)
        {
            var result = payload.Result;
            Outcome = result.Outcome;
            Place = result.Place;
            CarCount = result.CarCount;
            Seconds = result.Seconds;
            Knockouts = result.Knockouts;
            KnockedOutBy = result.KnockedOutBy;
            RevengeTaken = result.RevengeTaken;
            BestPlace = payload.BestPlace;
            BestKnockouts = payload.BestKnockouts;
            IsBestPlace = payload.IsBestPlace;
            IsBestKnockouts = payload.IsBestKnockouts;
        }

        public void PlayAgain() => SetResult(true);
    }
}
