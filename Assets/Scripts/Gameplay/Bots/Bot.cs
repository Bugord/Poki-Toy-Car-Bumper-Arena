using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// One bot's car and what it has in mind: the car it is after, when it looks for another, and its personality, which
    /// sets how careful it is near the edge, whom it likes to pick and how well it aims, so the bots do not all drive
    /// alike. <see cref="BotDriver"/> picks the targets; the bot steers at its own with the same
    /// <see cref="CarMovement.Drive"/> and <see cref="CarMovement.TryBoost"/> the player's input goes through.
    /// <para>
    /// Every random choice of the bot comes from its own seeded <see cref="System.Random"/>, not Unity's shared one, so a
    /// headless simulation given the same seeds replays the same choices.
    /// </para>
    /// </summary>
    public sealed class Bot
    {
        private readonly GameTuning tuning;
        private readonly GameTuning.BotSettings settings;
        private readonly RoundDifficulty difficulty;
        private readonly Car player;
        private readonly System.Random random;
        // More hazards than this on one arena are not dodged; a mechanic has one or two.
        private const int MaxHazards = 8;

        private readonly BallLane ballLane;
        private readonly ArenaHazards hazards;

        private float retargetTimer;
        private bool isPushing;

        // Where the bot last saw its target and how it was moving then, and how long ago; between two looks it steers by
        // that, so a sharp turn of the target dodges it. The heading's error is picked again at each look, in degrees.
        private Car seenTarget;
        private Vector3 seenPosition;
        private Vector3 seenVelocity;
        private float sinceSeen;
        private float nextLook;
        private float aimError;

        // Seconds the bot has pushed its target for, and how long it pushes before it boosts, picked for each push.
        private float pushTime;
        private float boostDelay;

        // The Ball arena's ball this bot last saw announced, for how long, and how long it takes to react to it.
        private int seenBall;
        private float ballSeenFor;
        private float ballReaction;

        // The same for the hazards of the arena's mechanic, by their place in the list (there are only a few).
        private readonly int[] seenHazard = new int[MaxHazards];
        private readonly float[] hazardSeenFor = new float[MaxHazards];
        private readonly float[] hazardReaction = new float[MaxHazards];

        // The side a bot goes round a pit on (+1 or -1 along the rim) and for how many more seconds it keeps it.
        private float pitSide = 1f;
        private float pitHold;

        // Heading back to the middle, and whether this escape may still boost (rolled once as it starts).
        private bool isEscaping;
        private bool mayEscapeBoost;

        public Bot(Car car, GameTuning.BotPersonality personality, GameTuning tuning, RoundDifficulty difficulty,
            Car player, int seed, BallLane ballLane, ArenaHazards hazards)
        {
            this.ballLane = ballLane;
            this.hazards = hazards;
            this.Car = car;
            this.tuning = tuning;
            this.settings = tuning.Bots;
            this.Traits = settings.Personality(personality);
            this.difficulty = difficulty;
            this.player = player;
            this.random = new System.Random(seed);
        }

        public Car Car { get; }

        /// <summary>
        /// The personality's multipliers.
        /// </summary>
        public GameTuning.PersonalitySettings Traits { get; }

        /// <summary>
        /// The car this bot is after, null until it picks one.
        /// </summary>
        public Car Target { get; private set; }

        /// <summary>
        /// Whether it is time to pick a target: there is none, it fell off (or came back and is safe), or the bot has
        /// chased it long enough.
        /// </summary>
        public bool WantsTarget => Target == null || !Target.IsTargetable || retargetTimer <= 0f;

        public void SetTarget(Car target)
        {
            Target = target;
            var jitter = settings.RetargetJitter;
            retargetTimer = settings.RetargetInterval * (1f + Range(-jitter, jitter));
        }

        /// <summary>
        /// Looks for a new target at the next pick, as a bot that saw a car hit does; not while it pushes its own,
        /// which it sees through.
        /// </summary>
        public void Reconsider()
        {
            if (!isPushing) {
                retargetTimer = 0f;
            }
        }

        /// <summary>
        /// A random number from <paramref name="min"/> to <paramref name="max"/>, from the bot's own generator.
        /// </summary>
        public float Range(float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        /// <summary>
        /// Heads back away from the edge (see <see cref="IArenaEdge.Inward"/>; towards the middle on a round floor) when the car
        /// is near the edge, or will be soon at its speed; otherwise drives
        /// at a point just behind the target on the side facing the middle, so the hit sends the target outward.
        /// Staying on the arena comes first: a bot that pushes a car off and follows it loses as much. It is never
        /// slowed by the difficulty: the bot sees its own car at once, and a careless bot would fall off on its own and
        /// make the rounds swing wildly. While it pushes its target the bot stops looking ahead, or it would turn back
        /// before the target got to the edge; pushing, it is slow enough to stop at the margin.
        /// <para>
        /// The target the bot sees as it was at its last look (<see cref="RoundDifficulty.ReactionTime"/>), carried on
        /// along the way it was going, and its heading is a little off (<see cref="RoundDifficulty.AimError"/>): a player
        /// who turns sharply gets away. At the player's car it drives at <see cref="RoundDifficulty.PlayerPace"/>, and at
        /// the others at full throttle, so the bots' own fights stay as busy.
        /// </para>
        /// <para>
        /// The boost is the player's: a bot that has pushed its target for a moment
        /// (<see cref="RoundDifficulty.BoostDelay"/>) boosts into it, unless the boost would carry the bot itself past the
        /// margin; and a bot already past the margin and still sliding out may boost back towards the middle
        /// (<see cref="RoundDifficulty.EscapeBoostChance"/>), as a player saves a car on the edge.
        /// In a duel the round's duel pace and escape chance take over, so the last bot can be softened separately.
        /// </para>
        /// </summary>
        public void Steer(IArenaEdge edge, float deltaTime, bool isDuel)
        {
            retargetTimer -= deltaTime;
            sinceSeen += deltaTime;

            var position = Flat(Car.Position);
            var velocity = Flat(Car.Movement.Velocity);
            var margin = settings.EdgeMargin;
            isPushing = IsPushing(position);
            // Pushing a car, the bot is slow and the target is between it and the edge, so it keeps on to the margin.
            var lookAhead = isPushing ? 0f : settings.LookAhead * Traits.Caution;
            var ahead = position + velocity * lookAhead;
            if (edge.DistanceToEdge(position, margin) < 0f || edge.DistanceToEdge(ahead, margin) < 0f) {
                pushTime = 0f;
                Car.Movement.Drive(edge.Inward(position), 1f);
                Escape(edge, position, velocity, margin, isDuel);
                return;
            }

            isEscaping = false;

            // Staying clear of the ball comes before any attack, but after staying on the arena.
            if (AvoidBall(edge, position, margin, deltaTime) || AvoidHazards(edge, position, margin, deltaTime)) {
                pushTime = 0f;
                return;
            }

            if (Target == null) {
                pushTime = 0f;
                Car.Movement.Drive(Vector3.zero, 0f);
                return;
            }

            Look();
            var targetPosition = seenPosition + seenVelocity * (sinceSeen + settings.AimLead);
            var toTarget = targetPosition - position;
            // The side of the target facing the middle is the side away from the nearest void (the rim, a pit, a hole).
            // A target with no preferred way out (the middle of a round floor) is just driven through.
            var inwardOfTarget = edge.Inward(targetPosition);
            var outward = inwardOfTarget.sqrMagnitude > 0.0001f ? -inwardOfTarget.normalized : toTarget.normalized;
            // Halved close up, so the aim closes in on the target instead of circling the point behind it.
            var behind = Mathf.Min(settings.AimBehind, toTarget.magnitude * 0.5f);
            var aim = targetPosition - outward * behind;
            var heading = Quaternion.AngleAxis(aimError, Vector3.up) * (aim - position);
            if (!isPushing) {
                heading = GoRoundPits(edge, position, heading, margin, deltaTime);
            }

            var playerPace = isDuel ? difficulty.DuelPace : difficulty.PlayerPace;
            Car.Movement.Drive(heading, Target == player ? playerPace : 1f);

            if (isPushing) {
                Push(edge, position, heading, margin, deltaTime);
            }
            else {
                pushTime = 0f;
            }
        }

        /// <summary>
        /// Bends the line to the target round a pit or a hole in the way, keeping the side it chose for a moment.
        /// </summary>
        private Vector3 GoRoundPits(IArenaEdge edge, Vector3 position, Vector3 heading, float margin, float deltaTime)
        {
            pitHold -= deltaTime;
            return PitDetour.Bend(edge, position, heading, margin, settings.PitProbe, settings.PitSamples,
                settings.PitInward, settings.PitSideHold, ref pitSide, ref pitHold);
        }

        /// <summary>
        /// Steers out of the Ball arena's lane once the bot has noticed the ball: a moment after it is announced, longer
        /// for the careless (a personality's caution divides it), and only if the bot is in the ball's way.
        /// </summary>
        private bool AvoidBall(IArenaEdge edge, Vector3 position, float edgeMargin, float deltaTime)
        {
            if (ballLane == null || !ballLane.IsActive) {
                return false;
            }

            if (ballLane.Pass != seenBall) {
                seenBall = ballLane.Pass;
                ballSeenFor = 0f;
                var reaction = tuning.Ball.BotReaction;
                ballReaction = Range(reaction.x, reaction.y) / Mathf.Max(0.1f, Traits.Caution);
            }

            ballSeenFor += deltaTime;
            if (ballSeenFor < ballReaction
                || !ballLane.TryAvoid(position, tuning.Ball.BotMargin, edge, edgeMargin, out var heading)) {
                return false;
            }

            Car.Movement.Drive(heading, 1f);
            return true;
        }

        /// <summary>
        /// Steers out of the way of the mechanic's hazards, each noticed like the ball: a moment after it starts a new
        /// threat, longer for the careless, and only if the bot is in its way. The first hazard that needs it wins.
        /// </summary>
        private bool AvoidHazards(IArenaEdge edge, Vector3 position, float edgeMargin, float deltaTime)
        {
            if (hazards == null) {
                return false;
            }

            var all = hazards.All;
            var count = Mathf.Min(all.Count, MaxHazards);
            for (var index = 0; index < count; index++) {
                var hazard = all[index];
                if (hazard.Version != seenHazard[index]) {
                    seenHazard[index] = hazard.Version;
                    hazardSeenFor[index] = 0f;
                    var reaction = settings.HazardReaction;
                    hazardReaction[index] = Range(reaction.x, reaction.y) / Mathf.Max(0.1f, Traits.Caution);
                }

                hazardSeenFor[index] += deltaTime;
                if (hazardSeenFor[index] >= hazardReaction[index]
                    && hazard.TryAvoid(position, settings.HazardMargin, edge, edgeMargin, out var heading)) {
                    Car.Movement.Drive(heading, 1f);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takes a new look at the target when it is time, or at once for a new target.
        /// </summary>
        private void Look()
        {
            if (Target == seenTarget && sinceSeen < nextLook) {
                return;
            }

            seenTarget = Target;
            seenPosition = Flat(Target.Position);
            seenVelocity = Flat(Target.Movement.Velocity);
            sinceSeen = 0f;
            var reaction = difficulty.ReactionTime;
            nextLook = Range(reaction.x, reaction.y);
            var error = difficulty.AimError * Traits.AimError;
            aimError = Range(-error, error);
        }

        /// <summary>
        /// Boosts into the target once the bot has pushed it long enough, unless the boost would take the bot past the
        /// margin: at the raised top speed a boost carries the car about (top speed + impulse) × duration, which a bold
        /// bot counts short and a careful one long.
        /// </summary>
        private void Push(IArenaEdge edge, Vector3 position, Vector3 heading, float edgeMargin, float deltaTime)
        {
            if (pushTime <= 0f) {
                var delay = difficulty.BoostDelay * Traits.BoostDelay;
                boostDelay = Range(delay.x, delay.y);
            }

            pushTime += deltaTime;
            if (pushTime < boostDelay || !Car.Movement.IsBoostReady || heading.sqrMagnitude < 0.0001f) {
                return;
            }

            var reach = (tuning.Car.MaxSpeed + tuning.Boost.Impulse) * tuning.Boost.Duration * Traits.Caution;
            var landing = position + heading.normalized * reach;
            if (edge.DistanceToEdge(landing, edgeMargin) >= 0f && Car.Movement.TryBoost()) {
                pushTime = 0f;
            }
        }

        /// <summary>
        /// Heading back inward: once per escape the bot rolls whether it may boost, and it does once it is past
        /// the margin and still sliding outward, where turning round alone would come too late.
        /// </summary>
        private void Escape(IArenaEdge edge, Vector3 position, Vector3 velocity, float edgeMargin, bool isDuel)
        {
            if (!isEscaping) {
                isEscaping = true;
                var chance = isDuel ? difficulty.DuelEscapeBoostChance : difficulty.EscapeBoostChance;
                mayEscapeBoost = Range(0f, 1f) < chance * Traits.EscapeBoost;
            }

            var isSlidingOut = edge.DistanceToEdge(position, edgeMargin) < 0f && Vector3.Dot(velocity, -edge.Inward(position)) > 0f;
            if (mayEscapeBoost && isSlidingOut && Car.Movement.TryBoost()) {
                mayEscapeBoost = false;
            }
        }

        /// <summary>
        /// Whether the target is right in front of the car's nose, close enough to be in contact. Felt, not seen: it
        /// reads where the target really is.
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
