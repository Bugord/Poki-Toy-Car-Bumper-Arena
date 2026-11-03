using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Round;
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

        /// <summary>
        /// Raised when a mystery box in a long match gives the car an item: the slot spins for
        /// <see cref="GameTuning.PickupSettings.RollTime"/> before it lands (<see cref="ItemLanded"/>). The HUD plays its
        /// roulette on the player's.
        /// </summary>
        public event Action<ItemKind> ItemRolled;

        /// <summary>
        /// Raised when the roll lands and the item can be used; the HUD and the analytics listen to the player's.
        /// </summary>
        public event Action<ItemKind> ItemLanded;

        /// <summary>
        /// Raised when the car uses its item, before what it does; the HUD shows its sticker and the analytics count it.
        /// </summary>
        public event Action<ItemKind> ItemUsed;

        /// <summary>
        /// Raised when the car's item hits another car (a glove's punch, a bomb's blast, a shockwave), with the point
        /// it hit; the HUD shows POW! for the player's punch.
        /// </summary>
        public event Action<ItemKind, Vector3> ItemHit;

        /// <summary>
        /// Raised when the car's shield takes a hit and is gone, for its effect.
        /// </summary>
        public event Action ShieldPopped;

        private GameTuning.RoundSettings settings;
        private GameTuning.PickupSettings pickupSettings;
        private GameTuning.CarSettings carSettings;
        private GameTuning.BoostSettings boostSettings;
        private RoundDifficulty difficulty;

        public CarMovement Movement { get; private set; }

        /// <summary>
        /// Whether the car has fallen off and left the round.
        /// </summary>
        public bool IsOut { get; private set; }

        /// <summary>
        /// Whether a car that came back in a match is still safe: it passes through the other cars, neither pushing nor
        /// pushed, so it earns and gives no knock-out credit, hazards leave it be and bots do not pick it.
        /// </summary>
        public bool IsInvulnerable { get; private set; }

        /// <summary>
        /// Whether the car is in play for others to go after: on the arena and not safe.
        /// </summary>
        public bool IsTargetable => !IsOut && !IsInvulnerable && !Movement.IsCarried;

        /// <summary>
        /// Whether the player drives this car; set by the round when it places the car.
        /// </summary>
        public bool IsPlayer { get; set; }

        /// <summary>
        /// Times the push this car's hits give, from the player's car's power (<see cref="GameTuning.CarStatSettings"/>);
        /// 1 for a bot.
        /// </summary>
        public float PowerScale { get; set; } = 1f;

        public Vector3 Position => transform.position;

        /// <summary>
        /// The last car that drove into this one, null until one does; with <see cref="LastHitTime"/>, what a knock-out
        /// is credited by.
        /// </summary>
        public Car LastHitBy { get; private set; }

        /// <summary>
        /// Whether that last hit was an item of <see cref="LastHitBy"/> rather than the car itself, for the item missions.
        /// </summary>
        public bool LastHitByItem { get; private set; }

        /// <summary>
        /// When <see cref="LastHitBy"/> last drove into this car, in <see cref="Time.time"/>.
        /// </summary>
        public float LastHitTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// When a floor ring last fell under this car, in <see cref="Time.time"/>, to recognise a fall with the floor.
        /// </summary>
        public float LastRingFallTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// Whether the timed item <paramref name="kind"/> (mega, shield, turbo or balloon) the car used is still running.
        /// Each runs on its own clock beside the others: using one never ends another, so the car goes on taking boxes
        /// and using what they give without losing what runs. One ends with its time, a shield also with the hit it
        /// takes, a balloon with the fall it catches, and a fall of the car ends all.
        /// </summary>
        public bool IsRunning(ItemKind kind) => Time.time < abilityEnds[(int)kind];

        /// <summary>Seconds the running <paramref name="kind"/> has left; 0 when it does not run.</summary>
        public float TimeLeft(ItemKind kind) => Mathf.Max(0f, abilityEnds[(int)kind] - Time.time);

        /// <summary>How much of the running <paramref name="kind"/> is left, from 1 when it was used to 0.</summary>
        public float ShareLeft(ItemKind kind)
        {
            var length = pickupSettings.DurationOf(kind);
            return length > 0f ? Mathf.Clamp01(TimeLeft(kind) / length) : 0f;
        }

        /// <summary>
        /// The timed item used last that still runs, else any other that runs (<see cref="ItemKind.None"/> for none):
        /// the one the player's badge shows.
        /// </summary>
        public ItemKind Ability
        {
            get
            {
                if (IsRunning(lastAbility)) {
                    return lastAbility;
                }

                for (var i = 1; i < abilityEnds.Length; i++) {
                    if (IsRunning((ItemKind)i)) {
                        return (ItemKind)i;
                    }
                }

                return ItemKind.None;
            }
        }

        /// <summary>How much of <see cref="Ability"/> is left, from 1 to 0; 0 for none.</summary>
        public float AbilityShare => ShareLeft(Ability);

        /// <summary>Seconds <see cref="Ability"/> has left; 0 for none.</summary>
        public float AbilityTimeLeft => TimeLeft(Ability);

        /// <summary>
        /// Whether a shield is up: the next real hit, of a car, an item or the arena, is taken whole.
        /// </summary>
        public bool IsShielded => IsRunning(ItemKind.Shield);

        /// <summary>
        /// The item in the car's slot (<see cref="ItemKind.None"/> for an empty one): one at a time, kept until used or
        /// until the car falls. A car holding one still breaks the boxes it drives through but gets nothing from them,
        /// as in a kart racer.
        /// </summary>
        public ItemKind HeldItem { get; private set; }

        /// <summary>Whether the slot holds an item, spinning or landed.</summary>
        public bool HasItem => HeldItem != ItemKind.None;

        /// <summary>Whether the slot still spins: the item is decided but cannot be used yet.</summary>
        public bool IsItemRolling => HasItem && Time.time < itemReadyTime;

        /// <summary>Whether the item has landed and can be used now.</summary>
        public bool IsItemReady => HasItem && Time.time >= itemReadyTime;

        private float itemReadyTime;
        // The roll has not been reported landed yet.
        private bool isRollPending;

        private static readonly int AbilityKinds = Enum.GetValues(typeof(ItemKind)).Length;

        // When each timed item ends, by kind (None never runs), and whether its effect on the car is still on: it is
        // taken off in the frame after its time is up.
        private readonly float[] abilityEnds = NoAbilities();
        private readonly bool[] abilityOn = new bool[AbilityKinds];
        private ItemKind lastAbility;
        // Until this time (Time.time) the car that made a hit the shield took is not credited, however long it presses on.
        private float shieldGraceEnd = float.NegativeInfinity;

        [Inject]
        private void Construct(GameTuning tuning, RoundDifficulty difficulty)
        {
            this.pickupSettings = tuning.Pickups;
            this.settings = tuning.Round;
            this.carSettings = tuning.Car;
            this.boostSettings = tuning.Boost;
            this.difficulty = difficulty;
        }

        private Collider[] colliders;
        private CarLook look;
        private CarShadow shadow;
        // The car's box colliders at its own size, grown with a mega item.
        private BoxCollider[] boxes;
        private Vector3[] boxSizes;
        private Vector3[] boxCenters;
        // How many times its own size the car is now, and is growing or shrinking to.
        private float growth = 1f;
        private float growthTarget = 1f;

        /// <summary>How many times its own size the car is now: above 1 while a mega item runs, or as it shrinks back.</summary>
        public float Growth => growth;

        private void Awake()
        {
            Movement = GetComponent<CarMovement>();
            colliders = GetComponents<Collider>();
            look = GetComponent<CarLook>();
            shadow = GetComponent<CarShadow>();
            boxes = GetComponents<BoxCollider>();
            boxSizes = new Vector3[boxes.Length];
            boxCenters = new Vector3[boxes.Length];
            for (var i = 0; i < boxes.Length; i++) {
                boxSizes[i] = boxes[i].size;
                boxCenters[i] = boxes[i].center;
            }
        }

        private void Update()
        {
            if (IsOut && transform.position.y < settings.RemoveHeight) {
                gameObject.SetActive(false);
            }

            for (var i = 1; i < AbilityKinds; i++) {
                if (abilityOn[i] && !IsRunning((ItemKind)i)) {
                    ClearAbility((ItemKind)i);
                }
            }

            if (growth != growthTarget && pickupSettings != null) {
                var mega = pickupSettings.Mega;
                var rate = Mathf.Max(0.01f, mega.SizeScale - 1f) / mega.GrowTime;
                SetGrowth(Mathf.MoveTowards(growth, growthTarget, rate * Time.deltaTime));
            }

            if (isRollPending && Time.time >= itemReadyTime) {
                Land();
            }
        }

        /// <summary>
        /// Leaves the round: nobody drives the car any more, and it falls on, losing its item and whatever ran.
        /// </summary>
        public void TakeOut()
        {
            IsOut = true;
            ClearAbilities();
            ClearItem();
            Movement.StopCarry();
            Movement.Drive(Vector3.zero, 0f);
        }

        /// <summary>
        /// A mystery box gives the car <paramref name="kind"/>: the slot spins for <paramref name="rollTime"/> seconds and
        /// then holds it until it is used. False, and nothing given, while the slot is full (or for no kind).
        /// </summary>
        public bool GiveItem(ItemKind kind, float rollTime)
        {
            if (kind == ItemKind.None || HasItem) {
                return false;
            }

            HeldItem = kind;
            itemReadyTime = Time.time + Mathf.Max(0f, rollTime);
            isRollPending = true;
            ItemRolled?.Invoke(kind);
            if (rollTime <= 0f) {
                Land();
            }

            return true;
        }

        private void Land()
        {
            isRollPending = false;
            ItemLanded?.Invoke(HeldItem);
        }

        /// <summary>
        /// Takes the landed item out of the slot to be used and reports it (<see cref="ItemUsed"/>); what it does is the
        /// arena's business. <see cref="ItemKind.None"/>, and nothing used, while the slot is empty or still spins.
        /// </summary>
        public ItemKind UseItem()
        {
            if (!IsItemReady) {
                return ItemKind.None;
            }

            var kind = HeldItem;
            ClearItem();
            ItemUsed?.Invoke(kind);
            return kind;
        }

        /// <summary>
        /// A used timed item starts running: mega, shield, turbo or balloon, beside whatever else runs; one already running
        /// starts its time again. The other kinds leave nothing running.
        /// </summary>
        public void StartAbility(ItemKind kind)
        {
            var length = pickupSettings.DurationOf(kind);
            if (length <= 0f) {
                return;
            }

            abilityEnds[(int)kind] = Time.time + length;
            abilityOn[(int)kind] = true;
            lastAbility = kind;
            switch (kind) {
                case ItemKind.Mega:
                    Movement.SetMassScale(pickupSettings.Mega.MassScale);
                    growthTarget = pickupSettings.Mega.SizeScale;
                    break;
                case ItemKind.Turbo:
                    Movement.SetBoostCooldown(pickupSettings.Turbo.BoostCooldown);
                    break;
            }
        }

        /// <summary>
        /// Reports that this car's <paramref name="kind"/> hit another car at <paramref name="point"/>; for a balloon,
        /// that it caught this car's own fall, where the car was.
        /// </summary>
        public void ReportItemHit(ItemKind kind, Vector3 point)
        {
            ItemHit?.Invoke(kind, point);
        }

        private void ClearItem()
        {
            HeldItem = ItemKind.None;
            isRollPending = false;
        }

        /// <summary>
        /// Ends the timed item <paramref name="kind"/>, if it runs, and takes its effect off the car: a mega's weight and
        /// size, a turbo's quick boost.
        /// </summary>
        public void ClearAbility(ItemKind kind)
        {
            var index = (int)kind;
            if (index <= 0 || !abilityOn[index]) {
                return;
            }

            abilityEnds[index] = float.NegativeInfinity;
            abilityOn[index] = false;
            switch (kind) {
                case ItemKind.Mega:
                    Movement.SetMassScale(1f);
                    // Shrinks back over the grow time: at once, a car grown over the edge of another would leave a gap.
                    growthTarget = 1f;
                    break;
                case ItemKind.Turbo:
                    Movement.SetBoostCooldown(-1f);
                    break;
            }
        }

        /// <summary>Ends every timed item that runs: the car fell, or is back from a fall.</summary>
        public void ClearAbilities()
        {
            for (var i = 1; i < AbilityKinds; i++) {
                ClearAbility((ItemKind)i);
            }
        }

        private static float[] NoAbilities()
        {
            var ends = new float[AbilityKinds];
            for (var i = 0; i < ends.Length; i++) {
                ends[i] = float.NegativeInfinity;
            }

            return ends;
        }

        /// <summary>
        /// Makes the car <paramref name="value"/> times its own size at once: the model, its shadow and its colliders,
        /// grown from the floor under its middle, so the wheels stay on the floor.
        /// </summary>
        private void SetGrowth(float value)
        {
            growth = value;
            for (var i = 0; i < boxes.Length; i++) {
                boxes[i].size = boxSizes[i] * value;
                boxes[i].center = boxCenters[i] * value;
            }

            if (look != null) {
                look.SetGrowth(value);
            }

            if (shadow != null) {
                shadow.Growth = value;
            }
        }

        /// <summary>
        /// Takes a hit into the shield if one is up: true, the shield is spent and the caller must do no harm to the
        /// car; false when there is none. For the hits that are not the car's own collisions (an item's).
        /// </summary>
        public bool TryAbsorbHit()
        {
            if (!IsShielded) {
                return false;
            }

            ClearAbility(ItemKind.Shield);
            shieldGraceEnd = Time.time + pickupSettings.Shield.Grace;
            ShieldPopped?.Invoke();
            return true;
        }

        /// <summary>
        /// An item of <paramref name="user"/> (a shockwave, a bomb, a glove) knocked this car: it counts as hit by that
        /// car, so a fall that follows is the user's knock-out, the way a push of a car is.
        /// </summary>
        public void HitByItem(Car user)
        {
            LastHitBy = user;
            LastHitByItem = true;
            LastHitTime = Time.time;
        }

        /// <summary>
        /// Brings the car back into a match after a fall, at rest at <paramref name="position"/>, as fresh as when it was
        /// placed: nobody hit it, it has not boosted, it is not off balance, and it is safe until the round says
        /// otherwise (<see cref="SetInvulnerable"/>). The same object comes back, so everything that holds the car
        /// (the drivers, the HUD, the effects) keeps it.
        /// </summary>
        public void Respawn(Vector3 position, Quaternion rotation, IReadOnlyList<Car> cars)
        {
            // Put away far below the arena, it is woken up first: a collider out of play has nothing to ignore.
            if (!gameObject.activeSelf) {
                gameObject.SetActive(true);
            }

            IsOut = false;
            LastHitBy = null;
            LastHitByItem = false;
            LastHitTime = float.NegativeInfinity;
            LastHazardTime = float.NegativeInfinity;
            LastRingFallTime = float.NegativeInfinity;
            ClearAbilities();
            ClearItem();
            growthTarget = 1f;
            SetGrowth(1f);
            Movement.Respawn(position, rotation);
            IsInvulnerable = false;
            SetInvulnerable(true, cars);
        }

        /// <summary>
        /// Makes the car safe or ends it: while safe, the physics ignores it against every other car in
        /// <paramref name="cars"/>, so it neither pushes nor is pushed, and it is drawn half seen through. A pair of two
        /// safe cars keeps ignoring each other until the second ends.
        /// </summary>
        public void SetInvulnerable(bool value, IReadOnlyList<Car> cars)
        {
            if (IsInvulnerable == value) {
                return;
            }

            IsInvulnerable = value;
            for (var i = 0; i < cars.Count; i++) {
                var other = cars[i];
                // An inactive collider forgets what it ignores anyway, and the physics refuses to be told about it.
                if (other == this || !other.gameObject.activeInHierarchy || (!value && other.IsInvulnerable)) {
                    continue;
                }

                foreach (var mine in colliders) {
                    foreach (var theirs in other.colliders) {
                        Physics.IgnoreCollision(mine, theirs, value);
                    }
                }
            }

            if (look != null) {
                look.SetGhost(value);
            }
        }

        /// <summary>
        /// Whether the car's colliders overlap another solid car still in play, where ending its safe time would make the
        /// physics push the two apart at once. Another safe car does not count: the pair keeps ignoring each other until
        /// the second ends (<see cref="SetInvulnerable"/>), which then waits for this one, so two safe cars that came back
        /// on top of each other never wait for each other for good.
        /// </summary>
        public bool OverlapsCar(IReadOnlyList<Car> cars)
        {
            for (var i = 0; i < cars.Count; i++) {
                var other = cars[i];
                if (other == this || other.IsOut || other.IsInvulnerable) {
                    continue;
                }

                foreach (var mine in colliders) {
                    foreach (var theirs in other.colliders) {
                        if (mine.bounds.Intersects(theirs.bounds)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// When an arena hazard (the Ball's ball) last hit this car, in <see cref="Time.time"/>, to recognise a fall it
        /// caused.
        /// </summary>
        public float LastHazardTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// A hazard of the arena knocked this car: it counts as hit lately (the player's edge hold stands down, bots
        /// see a car sliding), but by nobody, so a fall that follows is no one's knockout unless a car pushes it again.
        /// </summary>
        public void HitByHazard()
        {
            // A hazard pushes and staggers before it reports the hit: a shield takes all of it back.
            if (IsShielded) {
                Movement.UndoHazardKick();
                TryAbsorbHit();
                return;
            }

            LastHitBy = null;
            LastHitByItem = false;
            LastHitTime = Time.time;
            LastHazardTime = Time.time;
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
            if (IsOut || otherBody == null || !otherBody.TryGetComponent<Car>(out var other) || other.IsOut
                || IsInvulnerable || other.IsInvulnerable) {
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

            // A shield takes a car driving into it whole (the physics' own bounce stays): no push, no loss of balance and
            // no credit for the hit. A touch below the shield's minimum is let through unmarked and keeps it, so a tap
            // cannot be turned into a knock-out. A car that rams with a shield is hit by nothing, and keeps it.
            if (otherApproach > 0f) {
                if (IsShielded) {
                    if (strength >= pickupSettings.Shield.MinStrength) {
                        TryAbsorbHit();
                    }

                    return;
                }

                if (Time.time < shieldGraceEnd) {
                    return;
                }
            }

            // The push grows with how fast the other car drove into this one, but never past how fast the two closed
            // in: a car tapped from behind by a slightly faster one is barely pushed, and a contact lost and found again
            // while one car pushes another does not kick it again at full speed.
            var isOtherBoosting = other.Movement.IsBoosting;
            var multiplier = (isOtherBoosting ? boostSettings.BumpMultiplier : 1f)
                * (other.IsPlayer ? difficulty.PlayerBump * other.PowerScale : 1f);
            var drive = Mathf.Min(otherApproach, strength);
            var push = carSettings.BumpForce * drive * multiplier * other.Movement.Mass / Movement.Mass
                * Movement.Surface.Push;
            // The solver has already bounced the car; the push only tops that up to the most a hit may leave a car
            // going, so a boosted hit or a heavy car cannot fling a car across the arena.
            var speedAway = Vector3.Dot(Movement.Velocity, normal);
            push = Mathf.Min(push, Mathf.Max(0f, carSettings.MaxBumpSpeed - speedAway));
            if (push > 0f) {
                Movement.Push(normal * push);
            }

            // Off balance for longer the harder the other car drove in, capped at a hit at full speed before the
            // difficulty scales the player's time and the time caused by the player's hits.
            var stagger = carSettings.HitStagger * Mathf.Clamp01(drive * multiplier / Mathf.Max(0.01f, carSettings.MaxSpeed));
            stagger *= Movement.Surface.Stagger;
            if (IsPlayer) {
                stagger *= difficulty.PlayerStaggerScale;
            }

            if (other.IsPlayer) {
                stagger *= difficulty.StaggerFromPlayer;
            }

            if (stagger > 0f) {
                Movement.Stagger(stagger);
            }

            // The player's good hits make the car hop and spin away like a toy: whoever won the bump reads at a glance.
            // Only from the floor, so a car already flying is not lifted again by the same pile-up.
            var share = Mathf.Clamp01(drive * multiplier / Mathf.Max(0.01f, carSettings.MaxSpeed));
            if (other.IsPlayer && Movement.IsGrounded && share >= carSettings.PlayerHitLiftFrom) {
                var side = Vector3.Cross(normal, otherVelocity).y;
                Movement.Launch(carSettings.PlayerHitLift * share,
                    carSettings.PlayerHitSpin * share * (side >= 0f ? 1f : -1f));
            }

            if (otherApproach > 0f) {
                LastHitBy = other;
                LastHitByItem = false;
                LastHitTime = Time.time;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Movement.FallTelemetry?.Contact(other, Vector3.Dot(otherVelocity, normal),
                -Vector3.Dot(velocity, normal), otherApproach > 0f, true);
#endif

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
            if (IsOut || otherBody == null || !otherBody.TryGetComponent<Car>(out var other) || other.IsOut
                || IsInvulnerable || other.IsInvulnerable) {
                return;
            }

            var away = Flat(Position - other.Position);
            if (away.sqrMagnitude < 0.0001f) {
                return;
            }

            // Still pressing on a shield that took the hit, or on one that stands: no credit.
            if (IsShielded || Time.time < shieldGraceEnd) {
                return;
            }

            if (Vector3.Dot(other.Movement.Velocity, away.normalized) > 0f) {
                LastHitBy = other;
                LastHitByItem = false;
                LastHitTime = Time.time;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Movement.FallTelemetry?.Contact(other, Vector3.Dot(other.Movement.Velocity, away.normalized),
                -Vector3.Dot(Movement.Velocity, away.normalized),
                Vector3.Dot(other.Movement.Velocity, away.normalized) > 0f, false);
#endif
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
