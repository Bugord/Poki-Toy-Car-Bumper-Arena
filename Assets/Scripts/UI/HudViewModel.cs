using System;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The in-round overlay: the time left, which cars are still on the arena, the player's knock-outs and, on a touch
    /// screen, the boost button with the boost's cooldown. The round state passes the round in; the view model reads it
    /// on demand, so the view polls instead of subscribing to every change. A car going out and a knock-out by the
    /// player are passed on as they happen, since the car's chip leaving the HUD and the KNOCKOUT! sticker belong to the
    /// moment the car falls.
    /// </summary>
    public sealed class HudViewModel : ViewModel, IPayloadReceiver<ArenaRound>
    {
        /// <summary>
        /// Raised when the player knocks a car out, with the point on the arena's edge where it went over: where the
        /// KNOCKOUT! sticker goes.
        /// </summary>
        public event Action<Vector3> PlayerKnockout;

        /// <summary>
        /// Raised when a car falls off, the player's included, with its index in the round (0 the player, 1..N the
        /// bots, as <see cref="CarLook"/> painted them).
        /// </summary>
        public event Action<int> CarOut;

        private ArenaRound round;

        /// <summary>
        /// Seconds left in the round.
        /// </summary>
        public float TimeLeft => round?.TimeLeft ?? 0f;

        public int CarsLeft => round?.CarsLeft ?? 0;

        /// <summary>
        /// How many cars started the round, so their indices run from 0 to one less.
        /// </summary>
        public int CarCount => round?.Cars.Count ?? 0;

        /// <summary>
        /// The index of the player's car; its colour and place on the HUD set it apart.
        /// </summary>
        public int PlayerIndex => round?.Player != null ? round.IndexOf(round.Player) : 0;

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

        /// <summary>
        /// Whether the cars stand waiting for the player's first steer or boost to begin the round: the start prompt
        /// shows meanwhile.
        /// </summary>
        public bool IsWaitingForPlayer => round != null && round.IsPlaced && !round.HasBegun;

        /// <summary>
        /// Whether the car with this index is still on the arena.
        /// </summary>
        public bool IsCarIn(int index)
        {
            return round != null && index >= 0 && index < round.Cars.Count && !round.Cars[index].IsOut;
        }

        public void SetPayload(ArenaRound payload)
        {
            Unsubscribe();
            round = payload;
            if (round != null) {
                round.CarOut += OnCarOut;
                round.KnockedOut += OnKnockedOut;
            }
        }

        public override void Dispose()
        {
            Unsubscribe();
            base.Dispose();
        }

        private void Unsubscribe()
        {
            if (round != null) {
                round.CarOut -= OnCarOut;
                round.KnockedOut -= OnKnockedOut;
            }
        }

        private void OnCarOut(Car car)
        {
            CarOut?.Invoke(round.IndexOf(car));
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
