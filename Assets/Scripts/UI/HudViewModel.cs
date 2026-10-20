using System;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The in-round overlay: the time left, how many cars are still on the arena, the player's knock-outs and, on a
    /// touch screen, the boost button with the boost's cooldown. The round state passes the round in; the view model
    /// reads it on demand, so the view polls instead of subscribing to every change. A knock-out by the player is the
    /// one thing passed on as it happens, since its KNOCKOUT! belongs to the moment the car falls.
    /// </summary>
    public sealed class HudViewModel : ViewModel, IPayloadReceiver<ArenaRound>
    {
        /// <summary>
        /// Raised when the player knocks a car out, with the point on the arena's edge where it went over: where the
        /// KNOCKOUT! sticker goes.
        /// </summary>
        public event Action<Vector3> PlayerKnockout;

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
        /// Cars the player has knocked out this round.
        /// </summary>
        public int Knockouts => round?.PlayerKnockouts ?? 0;

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
            if (round != null) {
                round.KnockedOut -= OnKnockedOut;
            }

            round = payload;
            if (round != null) {
                round.KnockedOut += OnKnockedOut;
            }
        }

        public override void Dispose()
        {
            if (round != null) {
                round.KnockedOut -= OnKnockedOut;
            }

            base.Dispose();
        }

        private void OnKnockedOut(Car car, Car knockedOutBy)
        {
            if (knockedOutBy != round.Player || car == round.Player) {
                return;
            }

            // By the time it counts as out the car is below the arena and flying on; the point where it went over the
            // edge, under it, is still on the screen and tells which car it was.
            var position = car.Position;
            var flat = new Vector3(position.x, 0f, position.z);
            PlayerKnockout?.Invoke(Vector3.ClampMagnitude(flat, round.ArenaRadius));
        }
    }
}
