using System;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// FOOTBALL's ball: a light, bouncy rigidbody that remembers which car touched it last, of each team, and gives a
    /// car that drives into it a kick on top of the bounce, so a ram shoots it. Built by <see cref="FootballPitch"/>; the
    /// mode keeps it on the pitch and under its top speed (<see cref="FootballMode"/>).
    /// </summary>
    public sealed class FootballBall : MonoBehaviour
    {
        private readonly Car[] lastOfTeam = new Car[2];
        private readonly float[] lastTimeOfTeam = { float.NegativeInfinity, float.NegativeInfinity };
        private GameTuning.FootballSettings settings;
        private Rigidbody body;

        /// <summary>The car that touched the ball last, or null since the last kick-off.</summary>
        public Car LastTouch { get; private set; }

        /// <summary>Time of the last touch by a car.</summary>
        public float LastTouchTime { get; private set; } = float.NegativeInfinity;

        public Rigidbody Body => body;

        /// <summary>Raised on every new contact of a car with the ball, with the car and how hard it came in.</summary>
        public event Action<Car, float> Kicked;

        public void Setup(GameTuning.FootballSettings settings, Rigidbody body)
        {
            this.settings = settings;
            this.body = body;
        }

        /// <summary>The car of <paramref name="team"/> that touched the ball last since the kick-off, or null.</summary>
        public Car LastOf(int team) => team >= 0 && team < lastOfTeam.Length ? lastOfTeam[team] : null;

        /// <summary>When a car of <paramref name="team"/> last touched the ball (game time), or minus infinity.</summary>
        public float LastTimeOf(int team) =>
            team >= 0 && team < lastTimeOfTeam.Length ? lastTimeOfTeam[team] : float.NegativeInfinity;

        /// <summary>Forgets every touch: a kick-off starts with nobody's ball.</summary>
        public void Forget()
        {
            LastTouch = null;
            LastTouchTime = float.NegativeInfinity;
            lastOfTeam[0] = null;
            lastOfTeam[1] = null;
            lastTimeOfTeam[0] = float.NegativeInfinity;
            lastTimeOfTeam[1] = float.NegativeInfinity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var car = CarOf(collision);
            if (car == null) {
                return;
            }

            Touch(car);
            // The car's speed into the ball, flat: the bounce of the physics alone barely moves a light ball off a car
            // that is braking, so a car that drives in kicks it on, harder the faster it came.
            var towards = body.position - car.Position;
            towards.y = 0f;
            if (towards.sqrMagnitude < 0.0001f) {
                return;
            }

            towards.Normalize();
            var carVelocity = car.Movement.Velocity;
            var approach = Vector3.Dot(carVelocity - body.linearVelocity, towards);
            if (approach <= 0f) {
                return;
            }

            var kick = approach * settings.Kick * (car.Movement.IsBoosting ? settings.BoostKick : 1f);
            body.AddForce(towards * kick + Vector3.up * (kick * settings.KickLift), ForceMode.VelocityChange);
            Kicked?.Invoke(car, approach);
        }

        private void OnCollisionStay(Collision collision)
        {
            var car = CarOf(collision);
            if (car != null) {
                Touch(car);
            }
        }

        /// <summary>Counts <paramref name="car"/> as the last to touch the ball (a contact; the tests call it too).</summary>
        public void Touch(Car car)
        {
            LastTouch = car;
            LastTouchTime = Time.time;
            if (car.Team >= 0 && car.Team < lastOfTeam.Length) {
                lastOfTeam[car.Team] = car;
                lastTimeOfTeam[car.Team] = Time.time;
            }
        }

        private static Car CarOf(Collision collision)
        {
            var other = collision.rigidbody;
            return other != null ? other.GetComponent<Car>() : null;
        }
    }
}
