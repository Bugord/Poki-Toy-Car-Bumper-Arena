using System;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// A car in the round, whoever drives it: the round places it and takes it out when it falls, and the drivers
    /// steer it through <see cref="Movement"/>. Out of the round, it keeps falling in view until it is well below the
    /// arena, and then it is put away.
    /// <para>
    /// A hit by another car gets an extra push on top of the physics (<see cref="GameTuning.CarSettings.BumpForce"/>),
    /// so the brief's "the faster and heavier, the harder the push" reads clearly: the push grows with how fast the
    /// other car drove into this one and with its mass, and a boosted hitter pushes harder still. Each car pushes only
    /// itself, from its own collision report, so the car that rams wins: it gets back only what the physics gives.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(CarMovement))]
    public sealed class Car : MonoBehaviour
    {
        // Approach speeds closer than this, in metres per second, count as the same, so the two sides of a hit agree
        // on which car is the hitter despite rounding.
        private const float SameSpeed = 0.01f;

        /// <summary>
        /// Raised when this car and another run into each other, once per contact, on both cars (see
        /// <see cref="CarHit"/>). Cars out of the round raise none.
        /// </summary>
        public event Action<CarHit> Hit;

        private GameTuning.RoundSettings settings;
        private GameTuning.CarSettings carSettings;
        private GameTuning.BoostSettings boostSettings;

        public CarMovement Movement { get; private set; }

        /// <summary>
        /// Whether the car has fallen off and left the round.
        /// </summary>
        public bool IsOut { get; private set; }

        public Vector3 Position => transform.position;

        /// <summary>
        /// The last car that drove into this one, null until one does; with <see cref="LastHitTime"/>, what a knock-out
        /// is credited by.
        /// </summary>
        public Car LastHitBy { get; private set; }

        /// <summary>
        /// When <see cref="LastHitBy"/> last drove into this car, in <see cref="Time.time"/>.
        /// </summary>
        public float LastHitTime { get; private set; } = float.NegativeInfinity;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Round;
            this.carSettings = tuning.Car;
            this.boostSettings = tuning.Boost;
        }

        private void Awake()
        {
            Movement = GetComponent<CarMovement>();
        }

        private void Update()
        {
            if (IsOut && transform.position.y < settings.RemoveHeight) {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Leaves the round: nobody drives the car any more, and it falls on.
        /// </summary>
        public void TakeOut()
        {
            IsOut = true;
            Movement.Drive(Vector3.zero, 0f);
        }

        /// <summary>
        /// Pushes this car away from a car it ran into and reports the hit. The floor and anything else without a car on
        /// it is not a hit.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            var otherBody = collision.rigidbody;
            if (IsOut || otherBody == null || !otherBody.TryGetComponent<Car>(out var other) || other.IsOut) {
                return;
            }

            var hasContact = collision.contactCount > 0;
            var point = hasContact ? collision.GetContact(0).point : (Position + other.Position) * 0.5f;

            // The contact normal is the way this car is pushed, but which way it points depends on the side reporting
            // it, so it is turned to point from the other car to this one; flat, so a hit pushes and does not lift.
            var away = Flat(Position - other.Position);
            var normal = hasContact ? Flat(collision.GetContact(0).normal) : away;
            if (normal.sqrMagnitude < 0.0001f) {
                normal = away;
            }

            if (Vector3.Dot(normal, away) < 0f) {
                normal = -normal;
            }

            if (normal.sqrMagnitude < 0.0001f) {
                return;
            }

            normal.Normalize();

            var velocity = Movement.StepVelocity;
            var otherVelocity = other.Movement.StepVelocity;
            var approach = Mathf.Max(0f, -Vector3.Dot(velocity, normal));
            var otherApproach = Mathf.Max(0f, Vector3.Dot(otherVelocity, normal));
            var strength = Mathf.Max(0f, Vector3.Dot(otherVelocity - velocity, normal));

            var isOtherBoosting = other.Movement.IsBoosting;
            var multiplier = isOtherBoosting ? boostSettings.BumpMultiplier : 1f;
            var push = carSettings.BumpForce * otherApproach * multiplier * other.Movement.Mass / Movement.Mass;
            Movement.Push(normal * push);

            if (otherApproach > 0f) {
                LastHitBy = other;
                LastHitTime = Time.time;
            }

            // A tie (both standing, or a head-on hit at the same speed) goes to one of the two cars, the same one on
            // both sides: a Unity object's hash is its instance id.
            var isHitter = Mathf.Abs(approach - otherApproach) > SameSpeed
                ? approach > otherApproach
                : GetHashCode() < other.GetHashCode();
            Hit?.Invoke(new CarHit(other, point, normal, strength, isHitter, Movement.IsBoosting, isOtherBoosting));
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
