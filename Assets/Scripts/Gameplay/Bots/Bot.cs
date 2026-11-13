using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
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

        // The hot potato: the bomb as the bot last noticed it, when it reacts to a new one, the holder it runs from, and
        // where it wanders while nobody holds the bomb (and until when).
        private int seenBomb = -1;
        private float bombReactAt;
        private Car fleeFrom;
        private Vector3 wander;
        private float wanderUntil;

        // The item the bot has made up its mind about, how long its moment must last, and how long it has so far.
        private ItemKind heldItem;
        private float useDelay;
        private float useMoment;

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
        /// Times the throttle the bot attacks at, 1 by default: a round in teams softens one team's bots with it on the
        /// easiest rounds (<see cref="GameTuning.TeamSettings"/>). The way back from the edge is never slowed.
        /// </summary>
        public float Pace { get; set; } = 1f;

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
        public bool WantsTarget => Target == null || !Target.IsTargetable || retargetTimer <= 0f
            || (Pickup != null && !Pickup.IsLive);

        /// <summary>
        /// The pickup on the floor this bot is driving to in a long match, null for none. It goes there instead of at its
        /// target, but only when the edge is no worry and no arena hazard is on it, and it is dropped when the pickup is
        /// gone (<see cref="WantsTarget"/>).
        /// </summary>
        public FloorPickup Pickup { get; private set; }

        public void SetPickup(FloorPickup pickup)
        {
            Pickup = pickup;
        }

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

            // A pickup is worth driving to, but not at the cost of a car it is already pushing.
            if (Pickup != null && !isPushing && DriveToPickup(edge, position, margin, deltaTime)) {
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
            Car.Movement.Drive(heading, (Target == player ? playerPace : 1f) * Pace);

            if (isPushing) {
                Push(edge, position, heading, margin, deltaTime);
            }
            else {
                pushTime = 0f;
            }
        }

        /// <summary>
        /// Steers in the HOT POTATO mode, where nobody falls and the bomb is all there is. With the bomb the bot drives at
        /// its target (the car <see cref="BotDriver"/> picked to pass it to) straight on, seen as any target is, with the
        /// reaction and the aim error of the difficulty, and at the player at the difficulty's pace, a little faster as
        /// the fuse runs short. Without it the bot runs from the holder once it has noticed who holds it (a reaction
        /// drawn per bomb, later for the careless), along the fence rather than into it; at the player's bomb at the
        /// difficulty's pace, so the player can catch someone. With no bomb about it wanders the floor.
        /// </summary>
        public void SteerPotato(IArenaEdge edge, HotPotato potato, float deltaTime)
        {
            retargetTimer -= deltaTime;
            sinceSeen += deltaTime;
            pushTime = 0f;
            var position = Flat(Car.Position);
            var potatoSettings = tuning.SpecialRounds.HotPotato;
            var holder = potato.Holder;
            var isHurried = holder != null && potato.FuseLeft <= potatoSettings.Hurry;

            if (holder == Car) {
                if (Target == null || !Target.IsTargetable) {
                    Car.Movement.Drive(Vector3.zero, 0f);
                    return;
                }

                Look();
                var aim = seenPosition + seenVelocity * (sinceSeen + settings.AimLead);
                var heading = Quaternion.AngleAxis(aimError, Vector3.up) * (aim - position);
                heading = GoRoundPits(edge, position, heading, settings.EdgeMargin, deltaTime);
                var pace = Target == player ? Mathf.Min(1f, difficulty.PlayerPace + (isHurried ? 0.1f : 0f)) : 1f;
                Car.Movement.Drive(heading, pace);
                // Close and facing it, a boost carries the bomb over the last metres, where the boost is on at all.
                if (Vector3.Distance(Flat(Target.Position), position) < settings.PushDistance * 2f
                    && Vector3.Angle(Flat(Car.transform.forward), heading) < settings.PushAngle) {
                    Car.Movement.TryBoost();
                }

                return;
            }

            // A new bomb, or the bomb on a new car: noticed a moment later, longer for the careless.
            if (potato.Version != seenBomb) {
                seenBomb = potato.Version;
                var reaction = difficulty.ReactionTime;
                bombReactAt = Time.time + Range(reaction.x, reaction.y) / Mathf.Max(0.1f, Traits.Caution);
            }

            if (Time.time >= bombReactAt) {
                fleeFrom = holder;
            }

            if (fleeFrom != null && (fleeFrom.IsOut || fleeFrom != holder && Time.time >= bombReactAt)) {
                fleeFrom = null;
            }

            if (fleeFrom != null) {
                var away = position - Flat(fleeFrom.Position);
                var distance = away.magnitude;
                var reach = potatoSettings.FleeRadius * (isHurried ? 1.5f : 1f);
                if (distance < reach) {
                    var heading = Flee(edge, position, distance > 0.01f ? away / distance : Flat(Car.transform.forward),
                        potatoSettings.FenceMargin);
                    var pace = fleeFrom == player ? difficulty.PlayerPace : 1f;
                    Car.Movement.Drive(heading, pace);
                    return;
                }
            }

            Wander(edge, position, potato, deltaTime);
        }

        /// <summary>
        /// The way to run from the holder: straight away, or as near it as the floor allows. Each try is scored by how
        /// well it leads away and by how much floor lies ahead; a way into the fence (or a pit's) within
        /// <paramref name="fenceMargin"/> scores low, so a cornered bot slides along the fence and round the holder.
        /// </summary>
        private Vector3 Flee(IArenaEdge edge, Vector3 position, Vector3 away, float fenceMargin)
        {
            var best = away;
            var bestScore = float.NegativeInfinity;
            for (var step = -4; step <= 4; step++) {
                var option = Quaternion.AngleAxis(step * 30f, Vector3.up) * away;
                var room = edge.DistanceToEdge(position + option * fenceMargin, 0f);
                var score = Vector3.Dot(option, away) + Mathf.Clamp(room, -fenceMargin, fenceMargin) / fenceMargin * 1.5f;
                if (score > bestScore) {
                    bestScore = score;
                    best = option;
                }
            }

            return best;
        }

        /// <summary>
        /// With no holder to run from, drives at an easy pace to a spot on the floor away from the walls, a new one now
        /// and then, the far side from the holder if there is one.
        /// </summary>
        private void Wander(IArenaEdge edge, Vector3 position, HotPotato potato, float deltaTime)
        {
            if (Time.time >= wanderUntil || Vector3.Distance(position, wander) < 2f) {
                wanderUntil = Time.time + Range(1.5f, 3f);
                // Where the bot is, if no try finds floor: the middle may be a hole (the candy arena's).
                wander = position;
                var extent = edge.SafeExtent;
                for (var attempt = 0; attempt < 8; attempt++) {
                    var angle = Range(0f, Mathf.PI * 2f);
                    var spot = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Range(0.2f, 0.7f) * extent;
                    if (potato.Holder != null && Vector3.Dot(spot - position, Flat(potato.Holder.Position) - position) > 0f) {
                        continue;
                    }

                    if (edge.DistanceToEdge(spot, 3f) >= 0f) {
                        wander = spot;
                        break;
                    }
                }
            }

            var heading = GoRoundPits(edge, position, wander - position, settings.EdgeMargin, deltaTime);
            Car.Movement.Drive(heading, wander == position ? 0f : 0.6f);
        }

        /// <summary>
        /// Drives at the pickup at full throttle, round pits and holes in the way; false (and the pickup dropped) when it
        /// is gone.
        /// </summary>
        private bool DriveToPickup(IArenaEdge edge, Vector3 position, float margin, float deltaTime)
        {
            if (!Pickup.IsLive) {
                Pickup = null;
                return false;
            }

            var heading = GoRoundPits(edge, position, Flat(Pickup.Position) - position, margin, deltaTime);
            Car.Movement.Drive(heading, 1f);
            return true;
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

        /// <summary>
        /// Whether the bot uses the item it holds now, in a long match. Each item has its moment: mega or turbo with a
        /// target ahead within push range, the shockwave with enough cars round it, the shield and the balloon when a car
        /// drives at it or it is near the edge, the bomb at a car the throw's reach ahead, the glove at a car right in
        /// front of the nose, the bowling ball at a car dead ahead down the floor, the oil at a car chasing it. The moment has to last a delay drawn once for each item (<see cref="GameTuning.PickupBotSettings.UseDelay"/>),
        /// so a bot is not frame-perfect; no item is used that would carry the bot over the edge. Nothing is drawn from
        /// the bot's dice while it holds nothing, so rounds without items replay as before.
        /// <para>
        /// A <paramref name="spared"/> car (the player in the round's first seconds, <see cref="RoundDifficulty.PlayerGrace"/>)
        /// is neither aimed at nor caught: no moment comes while it is in the item's reach, so the grace that keeps the
        /// bots off the player keeps their items off too.
        /// </para>
        /// <para>
        /// A <paramref name="waiting"/> car (the player while another bot's item is on its way to them,
        /// <see cref="PlayerItemTurn"/>) is not aimed at either, by the item's own reach (<see cref="Threatens(Car)"/>): the
        /// bot waits for its turn at that car, or uses the item on another car meanwhile.
        /// </para>
        /// </summary>
        public bool WantsToUseItem(IArenaEdge edge, IReadOnlyList<Car> cars, float deltaTime, Car spared = null,
            Car waiting = null)
        {
            if (!Car.IsItemReady || Car.IsOut) {
                heldItem = ItemKind.None;
                return false;
            }

            var use = tuning.Pickups.Bots;
            if (Car.HeldItem != heldItem) {
                heldItem = Car.HeldItem;
                useDelay = Range(use.UseDelay.x, use.UseDelay.y);
                useMoment = 0f;
            }

            if (!IsMoment(heldItem, edge, cars, use) || Endangers(heldItem, spared) || Threatens(heldItem, waiting)) {
                useMoment = 0f;
                return false;
            }

            useMoment += deltaTime;
            if (useMoment < useDelay) {
                return false;
            }

            heldItem = ItemKind.None;
            return true;
        }

        private bool IsMoment(ItemKind item, IArenaEdge edge, IReadOnlyList<Car> cars,
            GameTuning.PickupBotSettings use)
        {
            var position = Flat(Car.Position);
            var forward = Flat(Car.transform.forward);
            var isSafe = edge.DistanceToEdge(position, settings.EdgeMargin) >= 0f;
            switch (item) {
                case ItemKind.Mega:
                case ItemKind.Turbo:
                    // A turbo boosts often: only with room to boost into.
                    return isSafe && IsAhead(Target, position, forward, 0f, use.PushRange, use.AheadAngle);
                case ItemKind.Shield:
                case ItemKind.Balloon:
                    return !isSafe || IsThreatened(position, cars, use);
                case ItemKind.Bowling:
                    for (var i = 0; i < cars.Count; i++) {
                        if (cars[i] != Car && IsAhead(cars[i], position, forward, use.BowlingRange.x, use.BowlingRange.y,
                                use.BowlingAngle)) {
                            return true;
                        }
                    }

                    return false;
                case ItemKind.Oil:
                    // The slick goes under the nose of the nearest car, so a car ahead is as good a moment as one behind.
                    return IsChased(position, forward, cars, use) || HasAhead(position, forward, cars, use);
                case ItemKind.Shockwave:
                    return CountNear(position, cars, tuning.Pickups.Shockwave.Radius) >= use.ShockwaveCars;
                case ItemKind.Bomb:
                    // Thrown only where it lands on floor, at a car about where it lands.
                    var landing = position + forward.normalized * tuning.Pickups.Bomb.ThrowDistance;
                    if (!edge.IsSolid(landing)) {
                        return false;
                    }

                    for (var i = 0; i < cars.Count; i++) {
                        if (cars[i] != Car && IsAhead(cars[i], position, forward, use.BombRange.x, use.BombRange.y, use.AheadAngle)) {
                            return true;
                        }
                    }

                    return false;
                case ItemKind.Glove:
                    var reach = tuning.Pickups.Glove.Reach + 1f;
                    for (var i = 0; i < cars.Count; i++) {
                        if (cars[i] != Car && IsAhead(cars[i], position, forward, 0f, reach, use.PunchAngle)) {
                            return true;
                        }
                    }

                    return false;
                default:
                    return false;
            }
        }

        // Metres past an item's own reach a spared car is still kept clear of: it moves while the item flies.
        private const float SpareMargin = 1.5f;
        // The spared car's top speed against the tuned one: its speed stat makes it a few percent faster.
        private const float SpareTopSpeed = 1.1f;

        /// <summary>
        /// Whether using <paramref name="item"/> now would hit (or be aimed at) the <paramref name="spared"/> car: within
        /// the shockwave's ring, round where the bomb lands, on the bowling ball's or the glove's line, within the oil's
        /// reach. Mega, shield, turbo and balloon act on the bot itself and spare everyone.
        /// </summary>
        private bool Endangers(ItemKind item, Car spared)
        {
            if (spared == null || spared == Car || !spared.IsTargetable) {
                return false;
            }

            var position = Flat(Car.Position);
            var forward = Flat(Car.transform.forward).normalized;
            var offset = Flat(spared.Position) - position;
            var items = tuning.Pickups;
            switch (item) {
                case ItemKind.Shockwave:
                    return offset.magnitude <= items.Shockwave.Radius + SpareMargin;
                case ItemKind.Bomb:
                    // The bomb goes off a flight and a fuse later: as far again as the spared car can drive by then.
                    var landing = position + forward * items.Bomb.ThrowDistance;
                    var drive = ReachSpeed(spared) * (items.Bomb.FlightTime + items.Bomb.Fuse);
                    return (Flat(spared.Position) - landing).magnitude <= items.Bomb.Radius + SpareMargin + drive;
                case ItemKind.Bowling:
                    // The ball reaches the spared car a roll later: wider by as far as it can drive across by then.
                    var roll = ReachSpeed(spared) * Mathf.Max(0f, Vector3.Dot(offset, forward)) / items.Bowling.Speed;
                    return IsOnLine(offset, forward, items.Bowling.Range,
                        items.Bowling.Radius + items.Bowling.HitReach + SpareMargin + roll);
                case ItemKind.Glove:
                    return IsOnLine(offset, forward, items.Glove.Reach + SpareMargin, items.Glove.HitRadius + SpareMargin);
                case ItemKind.Oil:
                    return offset.magnitude <= items.Oil.TargetRange + SpareMargin;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the item the bot holds, used now, would go at <paramref name="car"/>: within the shockwave's ring, round
        /// where the bomb lands, on the bowling ball's or the glove's line, within the oil's reach. By the item's own reach,
        /// not the grace's wider one (<see cref="Endangers"/>, which also counts where the car can drive to), so a car
        /// whose turn is taken keeps only the items that would really go at it off. A car of the bot's own team never.
        /// </summary>
        public bool Threatens(Car car) => Threatens(Car.HeldItem, car);

        private bool Threatens(ItemKind item, Car car)
        {
            if (car == null || car == Car || !car.IsTargetable || Car.IsTeammateOf(car)) {
                return false;
            }

            var position = Flat(Car.Position);
            var forward = Flat(Car.transform.forward).normalized;
            var offset = Flat(car.Position) - position;
            var items = tuning.Pickups;
            switch (item) {
                case ItemKind.Shockwave:
                    return offset.magnitude <= items.Shockwave.Radius + SpareMargin;
                case ItemKind.Bomb:
                    var landing = position + forward * items.Bomb.ThrowDistance;
                    return (Flat(car.Position) - landing).magnitude <= items.Bomb.Radius + SpareMargin;
                case ItemKind.Bowling:
                    return IsOnLine(offset, forward, items.Bowling.Range,
                        items.Bowling.Radius + items.Bowling.HitReach + SpareMargin);
                case ItemKind.Glove:
                    return IsOnLine(offset, forward, items.Glove.Reach + SpareMargin, items.Glove.HitRadius + SpareMargin);
                case ItemKind.Oil:
                    return offset.magnitude <= items.Oil.TargetRange + SpareMargin;
                default:
                    return false;
            }
        }

        /// <summary>
        /// How fast the spared car may drive while an item is on its way: its top speed, or faster if it goes faster now.
        /// Not its speed now: a car starting off reaches the top in a fraction of a second, and drove into a bomb thrown
        /// when it was slow.
        /// </summary>
        private float ReachSpeed(Car spared) =>
            Mathf.Max(Flat(spared.Movement.Velocity).magnitude, tuning.Car.MaxSpeed * SpareTopSpeed);

        /// <summary>Whether a point <paramref name="offset"/> from the bot lies on a line from its nose, as long and as wide as given.</summary>
        private static bool IsOnLine(Vector3 offset, Vector3 forward, float length, float halfWidth)
        {
            var along = Vector3.Dot(offset, forward);
            var side = (offset - forward * along).magnitude;
            return along > -halfWidth && along < length && side < halfWidth;
        }

        private static bool IsAhead(Car car, Vector3 position, Vector3 forward, float near, float far, float angle)
        {
            if (car == null || !car.IsTargetable) {
                return false;
            }

            var offset = Flat(car.Position) - position;
            var distance = offset.magnitude;
            return distance >= near && distance <= far && Vector3.Angle(forward, offset) <= angle;
        }

        /// <summary>
        /// Whether a car in play is behind the bot, within the oil's reach, and gaining on it: spilled now, the slick lies
        /// in its way.
        /// </summary>
        private bool HasAhead(Vector3 position, Vector3 forward, IReadOnlyList<Car> cars, GameTuning.PickupBotSettings use)
        {
            for (var i = 0; i < cars.Count; i++) {
                if (cars[i] != Car && IsAhead(cars[i], position, forward, 1f, use.OilChaser, use.AheadAngle)) {
                    return true;
                }
            }

            return false;
        }

        private bool IsChased(Vector3 position, Vector3 forward, IReadOnlyList<Car> cars, GameTuning.PickupBotSettings use)
        {
            var velocity = Flat(Car.Movement.Velocity);
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == Car || !IsAhead(car, position, -forward, 1f, use.OilChaser, use.AheadAngle)) {
                    continue;
                }

                var offset = position - Flat(car.Position);
                var closing = Vector3.Dot(Flat(car.Movement.Velocity) - velocity, offset.normalized);
                if (closing > 0.5f) {
                    return true;
                }
            }

            return false;
        }

        private bool IsThreatened(Vector3 position, IReadOnlyList<Car> cars,
            GameTuning.PickupBotSettings use)
        {
            var velocity = Flat(Car.Movement.Velocity);
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == Car || !car.IsTargetable) {
                    continue;
                }

                var offset = position - Flat(car.Position);
                var distance = offset.magnitude;
                if (distance > use.ShieldThreat || distance < 0.01f) {
                    continue;
                }

                var closing = Vector3.Dot(Flat(car.Movement.Velocity) - velocity, offset / distance);
                if (closing >= use.ShieldThreatSpeed) {
                    return true;
                }
            }

            return false;
        }

        private int CountNear(Vector3 position, IReadOnlyList<Car> cars, float radius)
        {
            var count = 0;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car != Car && car.IsTargetable && (Flat(car.Position) - position).sqrMagnitude <= radius * radius) {
                    count++;
                }
            }

            return count;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
