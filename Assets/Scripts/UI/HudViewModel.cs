using System;
using ToyCarArena.Gameplay;
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
    /// <para>
    /// The boost button is there from the visit's first round on: the boost is the one way to dodge a hit and to save
    /// the car at the edge, and the first test on Poki showed touch players losing the first round without it.
    /// </para>
    /// </summary>
    public sealed class HudViewModel : ViewModel, IPayloadReceiver<HudRound>
    {
        /// <summary>
        /// Raised when the player knocks a car out, with the point on the arena's edge where it went over: where the
        /// KNOCKOUT! sticker goes; true for REVENGE! on the marked rival instead.
        /// </summary>
        public event Action<Vector3, bool> PlayerKnockout;

        /// <summary>
        /// Raised when a car falls off, the player's included, with its index in the round (0 the player, 1..N the
        /// bots, independently of their colours).
        /// </summary>
        public event Action<int> CarOut;
        public event Action<int, int> Eliminated;

        private ArenaRound round;
        private PlayerDriver driver;

        public GameTuning.ControlSettings Controls { get; private set; }

        public bool IsFirstRound { get; private set; }

        /// <summary>The round of the visit this HUD shows, 1 for the first, captured before progress moves on.</summary>
        public int RoundNumber { get; private set; }

        public bool IsRunning => round != null && round.IsRunning && !round.Player.IsOut;

        public float LastSteerTime => driver?.LastSteerTime ?? float.NegativeInfinity;

        public float LastTouchTime => driver?.LastTouchTime ?? float.NegativeInfinity;

        /// <summary>
        /// Suggest a boost only before the first successful one, when it can fire and another living car is nearby.
        /// </summary>
        public bool CanHintBoost
        {
            get
            {
                if (!IsFirstRound || !IsRunning || driver.Boosts > 0 || !IsBoostReady) {
                    return false;
                }

                var distanceSquared = Controls.BoostHintDistance * Controls.BoostHintDistance;
                for (var index = 0; index < round.Cars.Count; index++) {
                    var car = round.Cars[index];
                    if (car != round.Player && !car.IsOut &&
                        (car.Position - round.Player.Position).sqrMagnitude < distanceSquared) {
                        return true;
                    }
                }

                return false;
            }
        }

        public string NameOf(int index) => round.NameOf(index);
        public int ColorOf(int index) => round.ColorOf(index);

        public Vector3 PositionOf(int index) => round.Cars[index].Position;

        public float LastBoostOf(int index) => round.Cars[index].Movement.LastBoostTime;
        public float LastHitOf(int index) => round.Cars[index].LastHitTime;

        // Observation only: these sources never steer a car or consume the bot driver's random sequence.
        public bool IsNearEdge(int index)
        {
            var position = round.Cars[index].Position;
            position.y = 0f;
            return round.Edge.Nearness(position) >= 0.85f;
        }

        public bool HasRecovered(int index)
        {
            var car = round.Cars[index];
            var position = car.Position;
            position.y = 0f;
            return !car.IsOut && car.Movement.IsGrounded && !car.Movement.IsStaggered &&
                (round.Edge.Nearness(position) < 0.85f || Vector3.Dot(round.Edge.Inward(position), car.Movement.Velocity) >= 0f);
        }

        public bool IsNemesis(int index) => round != null && round.Nemesis != null &&
            round.IndexOf(round.Nemesis) == index;

        public bool HasBegun => round != null && round.HasBegun;

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
        /// The index of the player's car; its white ring and place on the HUD set it apart.
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

        public void SetPayload(HudRound payload)
        {
            Unsubscribe();
            round = payload.Round;
            driver = payload.Driver;
            Controls = payload.Controls;
            // Capture it now: session progress already advances as this round finishes.
            IsFirstRound = payload.IsFirstRound;
            RoundNumber = payload.RoundNumber;
            if (round != null) {
                round.CarOut += OnCarOut;
                round.KnockedOut += OnKnockedOut;
                round.Eliminated += OnEliminated;
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
                round.Eliminated -= OnEliminated;
            }
        }

        private void OnCarOut(Car car)
        {
            CarOut?.Invoke(round.IndexOf(car));
        }

        private void OnEliminated(int index, int by) => Eliminated?.Invoke(index, by);

        private void OnKnockedOut(Car car, Car knockedOutBy)
        {
            if (knockedOutBy != round.Player || car == round.Player) {
                return;
            }

            // By the time it counts as out the car is below the arena and flying on; the point where it went over the
            // edge, under it, is still on the screen and tells which car it was.
            var position = car.Position;
            var flat = new Vector3(position.x, 0f, position.z);
            PlayerKnockout?.Invoke(round.Edge.ClampToFloor(flat), car == round.Nemesis);
        }
    }

    /// <summary>
    /// The state hands the app-scoped HUD this arena's read-only sources together; the view never resolves services
    /// from a child scope or keeps using the previous round's driver after a restart.
    /// </summary>
    public readonly struct HudRound
    {
        public ArenaRound Round { get; }
        public PlayerDriver Driver { get; }
        public GameTuning.ControlSettings Controls { get; }
        public bool IsFirstRound { get; }
        public int RoundNumber { get; }

        public HudRound(ArenaRound round, PlayerDriver driver, GameTuning.ControlSettings controls, bool isFirstRound,
            int roundNumber)
        {
            Round = round;
            Driver = driver;
            Controls = controls;
            IsFirstRound = isFirstRound;
            RoundNumber = roundNumber;
        }
    }
}
