using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a round: how it went for the player and Play Again. The result is true once the player asks for
    /// another round.
    /// </summary>
    public sealed class RoundOverViewModel : ResultViewModel<bool>, IPayloadReceiver<RoundResult>
    {
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

        public void SetPayload(RoundResult payload)
        {
            Outcome = payload.Outcome;
            Place = payload.Place;
            CarCount = payload.CarCount;
            Seconds = payload.Seconds;
        }

        public void PlayAgain() => SetResult(true);
    }
}
