using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// One bot's car and what it has in mind: the car it is after, when it looks for another, and how careful it is near
    /// the edge. The care differs from bot to bot, so they do not all drive alike. <see cref="BotDriver"/> picks the
    /// targets; the bot steers at its own with the same <see cref="CarMovement.Drive"/> the player's input goes through.
    /// </summary>
    public sealed class Bot
    {
        private readonly GameTuning.BotSettings settings;
        private readonly float caution;

        private float retargetTimer;

        public Bot(Car car, GameTuning.BotSettings settings)
        {
            this.Car = car;
            this.settings = settings;
            this.caution = Random.Range(settings.Caution.x, settings.Caution.y);
        }

        public Car Car { get; }

        /// <summary>
        /// The car this bot is after, null until it picks one.
        /// </summary>
        public Car Target { get; private set; }

        /// <summary>
        /// Whether it is time to pick a target: there is none, it fell off, or the bot has chased it long enough.
        /// </summary>
        public bool WantsTarget => Target == null || Target.IsOut || retargetTimer <= 0f;

        public void SetTarget(Car target)
        {
            Target = target;
            var jitter = settings.RetargetJitter;
            retargetTimer = settings.RetargetInterval * (1f + Random.Range(-jitter, jitter));
        }

        /// <summary>
        /// Heads back towards the middle when the car is near the edge, or will be soon at its speed; otherwise drives
        /// at full throttle at a point just behind the target on the side facing the middle, so the hit sends the
        /// target outward. Staying on the arena comes first: a bot that pushes a car off and follows it loses as much.
        /// While it pushes its target the bot stops looking ahead, or it would turn back before the target got to the
        /// edge; pushing, it is slow enough to stop at the margin.
        /// </summary>
        public void Steer(float arenaRadius, float deltaTime)
        {
            retargetTimer -= deltaTime;

            var position = Flat(Car.Position);
            var safeRadius = Mathf.Max(0f, arenaRadius - settings.EdgeMargin);
            // Pushing a car, the bot is slow and the target is between it and the edge, so it keeps on to the margin.
            var lookAhead = IsPushing(position) ? 0f : settings.LookAhead * caution;
            var ahead = position + Flat(Car.Movement.Velocity) * lookAhead;
            if (position.sqrMagnitude > safeRadius * safeRadius || ahead.sqrMagnitude > safeRadius * safeRadius) {
                Car.Movement.Drive(-position, 1f);
                return;
            }

            if (Target == null) {
                Car.Movement.Drive(Vector3.zero, 0f);
                return;
            }

            var targetPosition = Flat(Target.Position) + Flat(Target.Movement.Velocity) * settings.AimLead;
            var toTarget = targetPosition - position;
            // A target at the very middle has no outward side; the bot then just drives through it.
            var outward = targetPosition.sqrMagnitude > 0.0001f ? targetPosition.normalized : toTarget.normalized;
            // Halved close up, so the aim closes in on the target instead of circling the point behind it.
            var behind = Mathf.Min(settings.AimBehind, toTarget.magnitude * 0.5f);
            var aim = targetPosition - outward * behind;
            Car.Movement.Drive(aim - position, 1f);
        }

        /// <summary>
        /// Whether the target is right in front of the car's nose, close enough to be in contact.
        /// </summary>
        private bool IsPushing(Vector3 position)
        {
            if (Target == null) {
                return false;
            }

            var toTarget = Flat(Target.Position) - position;
            return toTarget.magnitude < settings.PushDistance
                && Vector3.Angle(Flat(Car.transform.forward), toTarget) < settings.PushAngle;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
