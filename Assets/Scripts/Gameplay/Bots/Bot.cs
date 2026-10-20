using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// One bot's car and what it has in mind: the car it is after, when it looks for another, and how careful it is near
    /// the edge. The care differs from bot to bot, so they do not all drive alike. <see cref="BotDriver"/> picks the
    /// targets; the bot steers at its own with the same <see cref="CarMovement.Drive"/> and
    /// <see cref="CarMovement.TryBoost"/> the player's input goes through.
    /// </summary>
    public sealed class Bot
    {
        private readonly GameTuning tuning;
        private readonly GameTuning.BotSettings settings;
        private readonly float caution;

        private float retargetTimer;

        // Seconds the bot has pushed its target for, and how long it pushes before it boosts, picked for each push.
        private float pushTime;
        private float boostDelay;

        // Heading back to the middle, and whether this escape may still boost (rolled once as it starts).
        private bool isEscaping;
        private bool mayEscapeBoost;

        public Bot(Car car, GameTuning tuning)
        {
            this.Car = car;
            this.tuning = tuning;
            this.settings = tuning.Bots;
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
        /// <para>
        /// The boost is the player's: a bot that has pushed its target for a moment
        /// (<see cref="GameTuning.BotSettings.BoostDelay"/>) boosts into it, unless the boost would carry the bot itself
        /// past the margin; and a bot already past the margin and still sliding out may boost back towards the middle
        /// (<see cref="GameTuning.BotSettings.EscapeBoostChance"/>), as a player saves a car on the edge.
        /// </para>
        /// </summary>
        public void Steer(float arenaRadius, float deltaTime)
        {
            retargetTimer -= deltaTime;

            var position = Flat(Car.Position);
            var velocity = Flat(Car.Movement.Velocity);
            var safeRadius = Mathf.Max(0f, arenaRadius - settings.EdgeMargin);
            var isPushing = IsPushing(position);
            // Pushing a car, the bot is slow and the target is between it and the edge, so it keeps on to the margin.
            var lookAhead = isPushing ? 0f : settings.LookAhead * caution;
            var ahead = position + velocity * lookAhead;
            if (position.sqrMagnitude > safeRadius * safeRadius || ahead.sqrMagnitude > safeRadius * safeRadius) {
                pushTime = 0f;
                Car.Movement.Drive(-position, 1f);
                Escape(position, velocity, safeRadius);
                return;
            }

            isEscaping = false;

            if (Target == null) {
                pushTime = 0f;
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

            if (isPushing) {
                Push(position, aim - position, safeRadius, deltaTime);
            }
            else {
                pushTime = 0f;
            }
        }

        /// <summary>
        /// Boosts into the target once the bot has pushed it long enough, unless the boost would take the bot past the
        /// margin: at the raised top speed a boost carries the car about (top speed + impulse) × duration, which a bold
        /// bot counts short and a careful one long.
        /// </summary>
        private void Push(Vector3 position, Vector3 heading, float safeRadius, float deltaTime)
        {
            if (pushTime <= 0f) {
                boostDelay = Random.Range(settings.BoostDelay.x, settings.BoostDelay.y);
            }

            pushTime += deltaTime;
            if (pushTime < boostDelay || !Car.Movement.IsBoostReady || heading.sqrMagnitude < 0.0001f) {
                return;
            }

            var reach = (tuning.Car.MaxSpeed + tuning.Boost.Impulse) * tuning.Boost.Duration * caution;
            var landing = position + heading.normalized * reach;
            if (landing.sqrMagnitude <= safeRadius * safeRadius && Car.Movement.TryBoost()) {
                pushTime = 0f;
            }
        }

        /// <summary>
        /// Heading back to the middle: once per escape the bot rolls whether it may boost, and it does once it is past
        /// the margin and still sliding outward, where turning round alone would come too late.
        /// </summary>
        private void Escape(Vector3 position, Vector3 velocity, float safeRadius)
        {
            if (!isEscaping) {
                isEscaping = true;
                mayEscapeBoost = Random.value < settings.EscapeBoostChance;
            }

            var isSlidingOut = position.sqrMagnitude > safeRadius * safeRadius && Vector3.Dot(velocity, position) > 0f;
            if (mayEscapeBoost && isSlidingOut && Car.Movement.TryBoost()) {
                mayEscapeBoost = false;
            }
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
