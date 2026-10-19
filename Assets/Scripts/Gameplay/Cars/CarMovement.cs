using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Drives a car the arcade way: told where to go and how hard, it turns towards that heading and speeds up along its
    /// nose, while sideways sliding dies down by the grip. The physics stays the Rigidbody's, so hits push cars around
    /// and a car off the edge falls; the car steers only with its wheels on the arena. Whoever drives it, the player or
    /// a bot, calls <see cref="Drive"/>; the numbers are <see cref="GameTuning.CarSettings"/>, read every step so they can
    /// be tuned in play mode.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CarMovement : MonoBehaviour
    {
        // The ground check casts down from this high above the bottom of the car, this far below it, in metres.
        private const float GroundProbeHeight = 0.5f;
        private const float GroundProbeDepth = 0.2f;

        private static int arenaMask;

        private GameTuning.CarSettings settings;
        private Rigidbody body;
        private Vector3 direction;
        private float throttle;

        /// <summary>
        /// Whether the wheels are on the arena; only then does the car steer.
        /// </summary>
        public bool IsGrounded { get; private set; }

        public Vector3 Velocity => body.linearVelocity;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Car;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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
            if (!IsGrounded) {
                return;
            }

            var deltaTime = Time.fixedDeltaTime;
            var forward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, forward);

            Turn(forward, deltaTime);

            // Facing away from the heading, the car turns first and speeds up as it comes round.
            var alignment = throttle > 0f ? Mathf.Clamp01(Vector3.Dot(forward, direction)) : 0f;
            var velocity = body.linearVelocity;
            var forwardSpeed = Vector3.Dot(velocity, forward);
            var targetSpeed = settings.MaxSpeed * throttle * alignment;
            var rate = targetSpeed > forwardSpeed ? settings.Acceleration : settings.Braking;
            var newForwardSpeed = Mathf.MoveTowards(forwardSpeed, targetSpeed, rate * deltaTime);

            var sideSpeed = Vector3.Dot(velocity, right);
            var newSideSpeed = sideSpeed * Mathf.Exp(-settings.Grip * deltaTime);

            body.linearVelocity = velocity + forward * (newForwardSpeed - forwardSpeed) + right * (newSideSpeed - sideSpeed);
        }

        /// <summary>
        /// Turns towards the heading as fast as the turn speed allows, and starts easing off early enough to stop on it,
        /// so it never swings past. A hit's spin dies down at the same rate.
        /// </summary>
        private void Turn(Vector3 forward, float deltaTime)
        {
            var targetRate = 0f;
            if (throttle > 0f) {
                var angle = Vector3.SignedAngle(forward, direction, Vector3.up);
                var stoppingRate = Mathf.Sqrt(2f * settings.TurnAcceleration * Mathf.Abs(angle));
                targetRate = Mathf.Sign(angle) * Mathf.Min(settings.TurnSpeed, stoppingRate);
            }

            var angularVelocity = body.angularVelocity;
            var rate = Mathf.MoveTowards(angularVelocity.y * Mathf.Rad2Deg, targetRate, settings.TurnAcceleration * deltaTime);
            angularVelocity.y = rate * Mathf.Deg2Rad;
            body.angularVelocity = angularVelocity;
        }
    }
}
