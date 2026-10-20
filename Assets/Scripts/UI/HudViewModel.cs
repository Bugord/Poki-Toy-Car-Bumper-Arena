using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The in-round overlay: the time left, how many cars are still on the arena and, on a touch screen, the boost button
    /// with the boost's cooldown. The round state passes the round in; the view model reads it on demand, so the view
    /// polls instead of subscribing to every change.
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

        /// <summary>
        /// How far the player's boost has cooled down, from 0 just after a boost to 1 once it is ready again.
        /// </summary>
        public float BoostReadiness => round != null && round.IsRunning ? round.Player.Movement.BoostReadiness : 0f;

        /// <summary>
        /// Whether the player's boost can fire now: cooled down and the round on.
        /// </summary>
        public bool IsBoostReady => round != null && round.IsRunning && round.Player.Movement.IsBoostReady;

        public void SetPayload(ArenaRound payload)
        {
            round = payload;
        }
    }
}
