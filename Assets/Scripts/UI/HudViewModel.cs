using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The in-round overlay: the time left and how many cars are still on the arena. The round state passes the round
    /// in; the view model reads it on demand, so the view polls instead of subscribing to every change.
    /// </summary>
    public sealed class HudViewModel : ViewModel, IPayloadReceiver<ArenaRound>
    {
        private ArenaRound round;

        /// <summary>
        /// Seconds left in the round.
        /// </summary>
        public float TimeLeft => round?.TimeLeft ?? 0f;

        public int CarsLeft => round?.CarsLeft ?? 0;

        /// <summary>
        /// How many cars started the round.
        /// </summary>
        public int CarCount => round?.Cars.Count ?? 0;

        public void SetPayload(ArenaRound payload)
        {
            round = payload;
        }
    }
}
