using System;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// What fired a boost the game fired for the player.
    /// </summary>
    public enum AutoBoostKind
    {
        None,
        Ram,
        Rescue,
    }

    /// <summary>
    /// Fires the player's boost on its own at the two moments a boost wins, as often as the round's
    /// <see cref="RoundDifficulty.AutoBoost"/> says: a ram, when the player keeps driving at a car right ahead and
    /// closes on it, and a rescue, when a hit pushes the player's car outward near the edge, which it boosts back
    /// inward. On the Poki tests about a fifth to a quarter of new players never boosted in their first game, so they
    /// pushed nobody off; the game aims for them, but the player still picks the car and drives at it, so the knock-out
    /// is theirs. A ram never throws the car past where it would hit off the floor. The boost button and keys work the
    /// same either way and share the cooldown. Each moment is rolled once; with a chance of 0 or 1 nothing is drawn,
    /// so a simulation on easy or normal replays as before.
    /// </summary>
    public sealed class AutoBoost
    {
        private readonly GameTuning tuning;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaRound round;
        private readonly System.Random random = new(0x5EED);

        private Car ramTarget;
        private float ramTime;
        private bool mayRam;

        private float rescueHitTime = float.NegativeInfinity;
        private bool mayRescue;

        public AutoBoost(GameTuning tuning, RoundDifficulty difficulty, ArenaRound round)
        {
            this.tuning = tuning;
            this.difficulty = difficulty;
            this.round = round;
        }

        /// <summary>
        /// Fires the boost of the player's <paramref name="car"/> if this is a moment for it.
        /// </summary>
        /// <param name="steer">Where the player steers, flat, zero when they do not.</param>
        /// <param name="throttle">How hard they steer, from 0 to 1.</param>
        /// <returns>What fired it, or <see cref="AutoBoostKind.None"/>.</returns>
        public AutoBoostKind Tick(Car car, Vector3 steer, float throttle, float deltaTime)
        {
            if (difficulty.AutoBoost <= 0f) {
                return AutoBoostKind.None;
            }

            var movement = car.Movement;
            if (!movement.IsGrounded || !movement.IsBoostReady || movement.IsBoosting) {
                ramTarget = null;
                return AutoBoostKind.None;
            }

            if (TryRescue(car)) {
                return AutoBoostKind.Rescue;
            }

            return TryRam(car, steer, throttle, deltaTime) ? AutoBoostKind.Ram : AutoBoostKind.None;
        }

        /// <summary>
        /// A hit pushes the car outward near the edge: boost back inward, as a player saves a car on the edge. Rolled
        /// once for each hit, later hits of a long shove included.
        /// </summary>
        private bool TryRescue(Car car)
        {
            var controls = tuning.Controls;
            if (Time.time - car.LastHitTime > controls.AutoRescueWindow) {
                return false;
            }

            if (car.LastHitTime - rescueHitTime > controls.AutoRescueWindow) {
                mayRescue = Roll();
            }

            rescueHitTime = car.LastHitTime;
            if (!mayRescue) {
                return false;
            }

            var edge = round.Edge;
            var position = Flat(car.Position);
            if (edge.DistanceToEdge(position, controls.AutoRescueMargin) >= 0f) {
                return false;
            }

            var inward = edge.Inward(position);
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.0001f) {
                return false;
            }

            inward.Normalize();
            if (Vector3.Dot(car.Movement.Velocity, -inward) < controls.AutoRescueSpeed) {
                return false;
            }

            // The boost goes where the car is driven; the player's next steer takes over once it is fired.
            car.Movement.Drive(inward, 1f);
            if (!car.Movement.TryBoost()) {
                return false;
            }

            mayRescue = false;
            return true;
        }

        /// <summary>
        /// The player drives hard at a car right ahead and closes on it: boost into it once they have kept at it for a
        /// moment. Rolled once for each car the player goes for. Not when the player's car would stop where it meets
        /// the other (or where the boost runs out) past the edge holding line, so it never throws the player off.
        /// </summary>
        private bool TryRam(Car car, Vector3 steer, float throttle, float deltaTime)
        {
            var controls = tuning.Controls;
            var target = steer != Vector3.zero && throttle >= controls.AutoRamThrottle ? RamTarget(car, Flat(steer)) : null;
            if (target == null) {
                ramTarget = null;
                return false;
            }

            if (target != ramTarget) {
                ramTarget = target;
                ramTime = 0f;
                mayRam = Roll();
            }

            ramTime += deltaTime;
            if (!mayRam || ramTime < controls.AutoRamHold) {
                return false;
            }

            var position = Flat(car.Position);
            var toTarget = Flat(target.Position) - position;
            var reach = (tuning.Car.MaxSpeed + tuning.Boost.Impulse) * tuning.Boost.Duration;
            var landing = position + Flat(steer).normalized *
                Mathf.Max(0f, Mathf.Min(reach, toTarget.magnitude - controls.AutoRamContact));
            if (round.Edge.DistanceToEdge(landing, controls.EdgeHoldMargin) < 0f || !car.Movement.TryBoost()) {
                return false;
            }

            mayRam = false;
            return true;
        }

        /// <returns>The nearest car in the game ahead along the steer within reach that the player closes on.</returns>
        private Car RamTarget(Car car, Vector3 steer)
        {
            var controls = tuning.Controls;
            var position = Flat(car.Position);
            var velocity = Flat(car.Movement.Velocity);
            Car best = null;
            var bestDistance = controls.AutoRamRange;
            foreach (var other in round.Cars) {
                if (other == car || !other.IsTargetable) {
                    continue;
                }

                var toOther = Flat(other.Position) - position;
                var distance = toOther.magnitude;
                if (distance >= bestDistance || distance < 0.01f || Vector3.Angle(steer, toOther) > controls.AutoRamAngle) {
                    continue;
                }

                var closing = Vector3.Dot(velocity - Flat(other.Movement.Velocity), toOther / distance);
                if (closing < controls.AutoRamClosing) {
                    continue;
                }

                best = other;
                bestDistance = distance;
            }

            return best;
        }

        private bool Roll()
        {
            var chance = difficulty.AutoBoost;
            return chance >= 1f || (chance > 0f && random.NextDouble() < chance);
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
