using System;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Drives a car the arcade way: told where to go and how hard, it turns towards that heading and speeds up along its
    /// nose, while sideways sliding dies down by the grip. The physics stays the Rigidbody's, so hits push cars around
    /// and a car off the edge falls; the car steers only with its wheels on the arena. Whoever drives it, the player or
    /// a bot, calls <see cref="Drive"/> and <see cref="TryBoost"/>; the numbers are <see cref="GameTuning.CarSettings"/>
    /// and <see cref="GameTuning.BoostSettings"/>, read every step so they can be tuned in play mode.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarMovement : MonoBehaviour
    {
        // The ground check casts down from this high above the bottom of the car, this far below it, in metres.
        private const float GroundProbeHeight = 0.5f;
        private const float GroundProbeDepth = 0.2f;

        private static int arenaMask;

        /// <summary>
        /// Raised when a boost fires, for its effects and sound.
        /// </summary>
        public event Action Boosted;

        private GameTuning.CarSettings settings;
        private GameTuning.BoostSettings boostSettings;
        private Rigidbody body;
        private Vector3 direction;
        private float throttle;

        // The boost runs until boostEndTime, along boostDirection; the next one is ready at boostReadyTime (Time.time).
        private Vector3 boostDirection;
        private float boostEndTime = float.NegativeInfinity;
        private float boostReadyTime;

        // Knocked off balance by a hit until this time (Time.time); see Stagger.
        private float staggerEndTime = float.NegativeInfinity;

        /// <summary>
        /// Whether the wheels are on the arena; only then does the car steer, and only then can it boost.
        /// </summary>
        public bool IsGrounded { get; private set; }

        public Vector3 Velocity => body.linearVelocity;

        /// <summary>
        /// The velocity the car went into the last physics step with. By the time a collision is reported the solver
        /// has already changed <see cref="Velocity"/>, so a hit's strength is measured from this.
        /// </summary>
        public Vector3 StepVelocity { get; private set; }

        public float Mass => body.mass;

        /// <summary>
        /// Whether a boost is running: the car holds its line at the raised top speed and its hits push harder.
        /// </summary>
        public bool IsBoosting => Time.time < boostEndTime;

        /// <summary>
        /// The last successful boost, in <see cref="Time.time"/>, to tell a fall soon after a boost from driving off.
        /// </summary>
        public float LastBoostTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// Whether a hit has knocked the car off balance: it does not drive or turn and keeps only a little grip, so the
        /// push of the hit carries it. A boost overrides it.
        /// </summary>
        public bool IsStaggered => Time.time < staggerEndTime && !IsBoosting;

        /// <summary>
        /// Whether the cooldown since the last boost is over. The car also has to be on the arena to boost.
        /// </summary>
        public bool IsBoostReady => Time.time >= boostReadyTime;

        /// <summary>
        /// How far the cooldown has come, from 0 just after a boost to 1 once the next one is ready, for the HUD.
        /// </summary>
        public float BoostReadiness
        {
            get
            {
                var cooldown = boostSettings.Cooldown;
                return cooldown > 0f ? Mathf.Clamp01(1f - (boostReadyTime - Time.time) / cooldown) : 1f;
            }
        }

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Car;
            this.boostSettings = tuning.Boost;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            // A car standing still must never sleep: the edge crumbles away under it (ArenaFloor swaps its collider for
            // a smaller one), and a sleeping body would hang in the air over the gap.
            body.sleepThreshold = 0f;
            if (arenaMask == 0) {
                arenaMask = LayerMask.GetMask("Arena");
            }
        }

        /// <summary>
        /// Where the car should head and how hard: <paramref name="heading"/> on the ground plane (its height is
        /// ignored), <paramref name="amount"/> from 0, which rolls to a stop, to 1, full throttle. Kept until the next
        /// call.
        /// </summary>
        public void Drive(Vector3 heading, float amount)
        {
            heading.y = 0f;
            direction = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector3.zero;
            throttle = direction == Vector3.zero ? 0f : Mathf.Clamp01(amount);
        }

        /// <summary>
        /// Fires the boost if it is ready and the car is on the arena; false otherwise, and nothing happens.
        /// <para>
        /// The boost goes where the car is being driven, not where its nose points: the brief makes it both the attack
        /// and the escape, and a car sliding off the edge nose first can only be saved by a boost back the way the
        /// driver now steers. A driver steering nowhere boosts along the nose. Any speed against the boost's way is
        /// dropped before the impulse is added, so a boost back from the edge is as strong as one forward. While the
        /// boost lasts the car holds that line: speed along it is kept up to the raised top speed and sliding across
        /// it dies down by the grip, while the car turns round to face it, so the boost is not lost to the grip
        /// before the nose comes round.
        /// </para>
        /// </summary>
        public bool TryBoost()
        {
            if (!IsGrounded || !IsBoostReady) {
                return false;
            }

            boostDirection = throttle > 0f ? direction : FlatForward();
            var velocity = body.linearVelocity;
            var along = Vector3.Dot(velocity, boostDirection);
            if (along < 0f) {
                velocity -= boostDirection * along;
            }

            body.linearVelocity = velocity + boostDirection * boostSettings.Impulse;
            boostEndTime = Time.time + boostSettings.Duration;
            // A boost is the way to catch the car after a hit, so it ends being off balance.
            staggerEndTime = float.NegativeInfinity;
            boostReadyTime = Time.time + boostSettings.Cooldown;
            LastBoostTime = Time.time;
            Boosted?.Invoke();
            return true;
        }

        /// <summary>
        /// Adds to the car's velocity at once, as a hit's extra push does.
        /// </summary>
        public void Push(Vector3 velocityChange)
        {
            body.linearVelocity += velocityChange;
        }

        /// <summary>
        /// Knocks the car off balance for <paramref name="seconds"/>, or keeps it so if it already is for longer: the
        /// push of a hit carries it, and the driver cannot steer straight back.
        /// </summary>
        public void Stagger(float seconds)
        {
            staggerEndTime = Mathf.Max(staggerEndTime, Time.time + seconds);
        }

        /// <summary>
        /// Puts the car somewhere at rest, as a spawn does.
        /// </summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        private void FixedUpdate()
        {
            body.mass = settings.Mass;
            body.centerOfMass = new Vector3(0f, settings.CenterOfMassHeight, 0f);

            var up = body.rotation * Vector3.up;
            IsGrounded = Physics.Raycast(body.position + up * GroundProbeHeight, -up, GroundProbeHeight + GroundProbeDepth,
                arenaMask, QueryTriggerInteraction.Ignore);
            if (IsGrounded) {
                Steer(Time.fixedDeltaTime);
            }

            StepVelocity = body.linearVelocity;
        }

        private void Steer(float deltaTime)
        {
            var forward = FlatForward();
            var isStaggered = IsStaggered;
            // Off balance, the car rolls on as if the driver let go, and slides with little grip.
            var drive = isStaggered ? 0f : throttle;
            Turn(forward, drive, deltaTime);

            // The speed is kept along the nose, or along the boost's line while it lasts (see TryBoost).
            float targetSpeed;
            var axis = forward;
            if (IsBoosting) {
                axis = boostDirection;
                targetSpeed = settings.MaxSpeed + boostSettings.Impulse;
            }
            else {
                // Facing away from the heading, the car turns first and speeds up as it comes round.
                var alignment = drive > 0f ? Mathf.Clamp01(Vector3.Dot(forward, direction)) : 0f;
                targetSpeed = settings.MaxSpeed * drive * alignment;
            }

            var right = Vector3.Cross(Vector3.up, axis);
            var velocity = body.linearVelocity;
            var axisSpeed = Vector3.Dot(velocity, axis);
            var rate = targetSpeed > axisSpeed ? settings.Acceleration : settings.Braking;
            var newAxisSpeed = Mathf.MoveTowards(axisSpeed, targetSpeed, rate * deltaTime);

            var sideSpeed = Vector3.Dot(velocity, right);
            var grip = settings.Grip * (isStaggered ? settings.StaggerGrip : 1f);
            var newSideSpeed = sideSpeed * Mathf.Exp(-grip * deltaTime);

            body.linearVelocity = velocity + axis * (newAxisSpeed - axisSpeed) + right * (newSideSpeed - sideSpeed);
        }

        /// <summary>
        /// Turns towards the heading as fast as the turn speed allows, and starts easing off early enough to stop on it,
        /// so it never swings past. A hit's spin dies down at the same rate. While a boost lasts the car turns to face
        /// the boost's line, which is where it was heading when the boost fired.
        /// </summary>
        private void Turn(Vector3 forward, float drive, float deltaTime)
        {
            var targetRate = 0f;
            var isBoosting = IsBoosting;
            if (drive > 0f || isBoosting) {
                var heading = isBoosting ? boostDirection : direction;
                var angle = Vector3.SignedAngle(forward, heading, Vector3.up);
                var stoppingRate = Mathf.Sqrt(2f * settings.TurnAcceleration * Mathf.Abs(angle));
                targetRate = Mathf.Sign(angle) * Mathf.Min(settings.TurnSpeed, stoppingRate);
            }

            var angularVelocity = body.angularVelocity;
            var rate = Mathf.MoveTowards(angularVelocity.y * Mathf.Rad2Deg, targetRate, settings.TurnAcceleration * deltaTime);
            angularVelocity.y = rate * Mathf.Deg2Rad;
            body.angularVelocity = angularVelocity;
        }

        private Vector3 FlatForward()
        {
            return Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
        }
    }
}
