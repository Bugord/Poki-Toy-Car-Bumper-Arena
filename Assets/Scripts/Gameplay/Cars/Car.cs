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
    /// other car drove into this one and with its mass, and a boosted hitter pushes harder still, up to
    /// <see cref="GameTuning.CarSettings.MaxBumpSpeed"/>. Each car pushes only itself, from its own collision report,
    /// so the car that rams wins: it gets back only what the physics gives. The hit car is also knocked off balance for
    /// a moment (<see cref="CarMovement.Stagger"/>), so the push carries it rather than dying down in its grip. The
    /// player's hits push harder and the player is knocked off balance for less, so the player's shoves are the ones
    /// that send cars off.
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

        /// <summary>
        /// Whether the player drives this car; set by the round when it places the car.
        /// </summary>
        public bool IsPlayer { get; set; }

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

        /// <summary>
        /// When a floor ring last fell under this car, in <see cref="Time.time"/>, to recognise a fall with the floor.
        /// </summary>
        public float LastRingFallTime { get; private set; } = float.NegativeInfinity;

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
        /// Records the floor giving way while the car is over the falling ring, before the car can slide elsewhere.
        /// </summary>
        public void NoteRingFall()
        {
            LastRingFallTime = Time.time;
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

            // The push grows with how fast the other car drove into this one, but never past how fast the two closed
            // in: a car tapped from behind by a slightly faster one is barely pushed, and a contact lost and found again
            // while one car pushes another does not kick it again at full speed.
            var isOtherBoosting = other.Movement.IsBoosting;
            var multiplier = (isOtherBoosting ? boostSettings.BumpMultiplier : 1f)
                * (other.IsPlayer ? carSettings.PlayerBumpMultiplier : 1f);
            var drive = Mathf.Min(otherApproach, strength);
            var push = carSettings.BumpForce * drive * multiplier * other.Movement.Mass / Movement.Mass;
            // The solver has already bounced the car; the push only tops that up to the most a hit may leave a car
            // going, so a boosted hit or a heavy car cannot fling a car across the arena.
            var speedAway = Vector3.Dot(Movement.Velocity, normal);
            push = Mathf.Min(push, Mathf.Max(0f, carSettings.MaxBumpSpeed - speedAway));
            if (push > 0f) {
                Movement.Push(normal * push);
            }

            // Off balance for longer the harder the other car drove in, up to a hit at full speed.
            var stagger = carSettings.HitStagger * Mathf.Clamp01(drive * multiplier / Mathf.Max(0.01f, carSettings.MaxSpeed));
            if (IsPlayer) {
                stagger *= carSettings.PlayerStaggerScale;
            }

            if (stagger > 0f) {
                Movement.Stagger(stagger);
            }

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

        /// <summary>
        /// Keeps the credit for a long push: a car shoved all the way to the edge in one contact was last hit by the
        /// pusher however long the push took, so the knockout window counts from the end of the push, not its start.
        /// </summary>
        private void OnCollisionStay(Collision collision)
        {
            var otherBody = collision.rigidbody;
            if (IsOut || otherBody == null || !otherBody.TryGetComponent<Car>(out var other) || other.IsOut) {
                return;
            }

            var away = Flat(Position - other.Position);
            if (away.sqrMagnitude < 0.0001f) {
                return;
            }

            if (Vector3.Dot(other.Movement.Velocity, away.normalized) > 0f) {
                LastHitBy = other;
                LastHitTime = Time.time;
            }
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
