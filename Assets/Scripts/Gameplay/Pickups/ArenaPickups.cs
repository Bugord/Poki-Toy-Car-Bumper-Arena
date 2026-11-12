using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The mystery boxes of a long match and the items they give (<see cref="ItemKind"/>). A box appears every
    /// <see cref="GameTuning.PickupSettings.Interval"/> seconds, only while the floor is whole or shrinking
    /// (<see cref="FloorPhase.Open"/> and <see cref="FloorPhase.Shrink"/>) and only on floor that is safe with room round
    /// it; at the peak and as the floor comes back none lie there, and one whose floor begins to shake fades away. Where
    /// it appears leans towards the cars behind in knock-outs. A car on the floor, in play and not just back from a fall,
    /// breaks it by driving within <see cref="GameTuning.PickupSettings.PickRadius"/>: an empty slot spins for
    /// <see cref="GameTuning.PickupSettings.RollTime"/> and lands on an item drawn by <see cref="ItemRoll"/> (the car
    /// behind gets the items that knock cars off more often); a car that already holds one drives through the box and
    /// leaves it for a car that can take it. The item is kept until its car uses it (<see cref="Use"/>) or falls.
    /// <para>
    /// Used, mega, shield, turbo and balloon start running on the car, each beside the others (<see cref="Car.StartAbility"/>;
    /// a balloon's catch is <see cref="ItemBalloons"/>); the shockwave is a ring sent from the car at once, the bomb is thrown ahead
    /// (<see cref="ItemBombs"/>), the glove punches from the nose (<see cref="ItemGloves"/>), the bowling ball rolls out
    /// of it (<see cref="ItemBowls"/>) and the oil is spilled behind (<see cref="ItemOil"/>, a layer of the floor). Every hit of an item goes through <see cref="ItemBlow"/>: credited to its user, taken
    /// by a shield, passing a car just back from a fall.
    /// </para>
    /// <para>
    /// Registered in every scope, it does nothing and builds nothing outside a long match, so a round of last-car-standing
    /// is as it was. What it builds hangs under one root and is freed with it. All its choices come from the bots' seed
    /// (<see cref="ArenaHazards.Seed"/>), read when the cars are placed, so a headless simulation repeats them.
    /// </para>
    /// </summary>
    public sealed class ArenaPickups : ITickable, IDisposable
    {
        private const float PopTime = 0.35f;
        private const float LeaveTime = 0.25f;
        private const float BlinkTime = 3f;
        private const float BlinkRate = 5f;
        private const float RetryDelay = 1f;
        private const int SpotAttempts = 60;
        // Metres above the floor a car may be and still break a box it drives through.
        private const float TakeHeight = 1.2f;
        private const float PickupVolume = 0.7f;

        /// <summary>
        /// Raised when a car breaks a box, with what it got and the car's place; after the slot has started to spin.
        /// </summary>
        public event Action<Car, ItemKind, Vector3> Collected;

        /// <summary>
        /// Raised when a box appears on the floor.
        /// </summary>
        public event Action<FloorPickup> Spawned;

        private readonly GameTuning tuning;
        private readonly GameTuning.PickupSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaCrumble crumble;
        private readonly IArenaEdge edge;
        private readonly ArenaHazards hazards;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt mechanicArt;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly ArenaSurface surface;
        private readonly Transform parent;
        private readonly bool isEnabled;
        private readonly float pace;
        private readonly int maxCount;
        private readonly float rollTime;
        private readonly List<FloorPickup> live = new();
        private readonly List<(Car car, Action handler)> shieldHandlers = new();

        private FloorPickup[] slots;
        private Transform root;
        private PickupArt art;
        private AbilityLook look;
        private ItemBlow blow;
        private ItemBombs bombs;
        private ItemGloves gloves;
        private ItemBowls bowls;
        private ItemOil oil;
        private ItemBalloons balloons;
        private EffectPlayer effects;
        private System.Random random;
        private float nextSpawn;
        private int version;
        private bool isBuilt;

        public ArenaPickups(GameTuning tuning, ArenaRound round, ArenaCrumble crumble, IArenaEdge edge, ArenaHazards hazards,
            RoundDifficulty difficulty, MechanicArt mechanicArt, ArenaCamera arenaCamera, IAudioService audioService,
            ISessionProgress progress, ArenaSurface surface, RoundRules rules, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups;
            this.round = round;
            this.crumble = crumble;
            this.edge = edge;
            this.hazards = hazards;
            this.difficulty = difficulty;
            this.mechanicArt = mechanicArt;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.surface = surface;
            this.parent = parent;
            // The one place the mode is read: a match (and a mode that says so) always has boxes, a plain round from the
            // visit's round the tuning says, and a mode may have none (HOT POTATO: the bomb is its only item). A scene
            // without them never builds an object for one.
            this.isEnabled = settings.Enabled && rules.Mode.Pickups switch {
                PickupRule.Always => true,
                PickupRule.Usual => settings.FirstRound > 0 && progress.Round >= settings.FirstRound,
                _ => false,
            };
            // A mode may bring boxes faster and more of them at once (ITEM FRENZY).
            this.pace = Mathf.Max(0.01f, rules.Mode.PickupPace);
            this.maxCount = rules.Mode.PickupMax > 0 ? rules.Mode.PickupMax : settings.MaxCount;
            this.rollTime = rules.Mode.PickupRollTime > 0f ? rules.Mode.PickupRollTime : settings.RollTime;
        }

        /// <summary>
        /// Whether this scene has boxes and items at all: a long match, or a round from
        /// <see cref="GameTuning.PickupSettings.FirstRound"/> of the visit, with them switched on.
        /// </summary>
        public bool IsEnabled => isEnabled;

        /// <summary>The boxes lying on the floor to be broken now.</summary>
        public IReadOnlyList<FloorPickup> Live => live;

        /// <summary>Counts up each time a box appears, so a bot can tell there is a new one to look at.</summary>
        public int Version => version;

        public void Tick()
        {
            if (!isEnabled) {
                return;
            }

            if (!isBuilt) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            var deltaTime = Time.deltaTime;
            look.Tick(round.Cars, deltaTime);
            AnimateSlots(deltaTime);
            bombs.Tick(deltaTime);
            gloves.Tick(deltaTime);
            bowls.Tick(deltaTime);
            oil.Tick(deltaTime);
            balloons.Tick(deltaTime);
            if (!round.IsRunning) {
                if (round.IsFinished) {
                    RemoveAll();
                    bombs.Clear();
                    gloves.Clear();
                    bowls.Clear();
                    oil.Clear();
                }

                return;
            }

            var phase = crumble.Phase;
            if (phase is FloorPhase.Peak or FloorPhase.Return) {
                RemoveAll();
                oil.Clear();
                return;
            }

            TickLive(deltaTime);
            TrySpawn();
        }

        /// <summary>
        /// Puts a box on the floor at <paramref name="position"/> (flat), as the spawner does when it has chosen a spot;
        /// null when every slot is taken. For the spawner, tests and the dev console.
        /// </summary>
        public FloorPickup Spawn(Vector3 position)
        {
            EnsureBuilt();
            foreach (var slot in slots) {
                if (slot.State != FloorPickup.Stage.Free) {
                    continue;
                }

                slot.Position = new Vector3(position.x, 0f, position.z);
                slot.Age = 0f;
                slot.StageTime = 0f;
                slot.WasTaken = false;
                slot.State = FloorPickup.Stage.Live;
                slot.Phase = (float)random.NextDouble() * Mathf.PI * 2f;
                slot.Root.position = slot.Position;
                slot.Root.localScale = Vector3.zero;
                slot.Root.gameObject.SetActive(true);
                art.TintBox(slot, slot.Phase);
                live.Add(slot);
                version++;
                Spawned?.Invoke(slot);
                return slot;
            }

            return null;
        }

        /// <summary>
        /// Puts <paramref name="kind"/> straight into <paramref name="car"/>'s slot, landed and ready to use, without a box
        /// or a roll; false while the slot is full. For tests and the dev console.
        /// </summary>
        public bool Give(Car car, ItemKind kind)
        {
            if (!isEnabled) {
                return false;
            }

            EnsureBuilt();
            return car.GiveItem(kind, 0f);
        }

        /// <summary>
        /// Uses <paramref name="car"/>'s landed item: false, and nothing done, when there is no match running, the car is
        /// out, or its slot is empty or still spins. The player's driver and the bots both call this.
        /// </summary>
        public bool Use(Car car)
        {
            if (!isEnabled || car == null || car.IsOut || !car.IsItemReady || !round.IsRunning) {
                return false;
            }

            EnsureBuilt();
            var kind = car.UseItem();
            switch (kind) {
                case ItemKind.Mega:
                case ItemKind.Shield:
                case ItemKind.Turbo:
                case ItemKind.Balloon:
                    car.StartAbility(kind);
                    break;
                case ItemKind.Bowling:
                    bowls.Roll(car);
                    break;
                case ItemKind.Oil:
                    oil.Spill(car);
                    break;
                case ItemKind.Shockwave:
                    SendShockwave(car);
                    break;
                case ItemKind.Bomb:
                    bombs.Throw(car);
                    break;
                case ItemKind.Glove:
                    gloves.Punch(car);
                    break;
                default:
                    return false;
            }

            if (kind is ItemKind.Mega or ItemKind.Shield or ItemKind.Turbo or ItemKind.Balloon) {
                if (effects != null) {
                    effects.PlayPickup(car.Position);
                }

                audioService?.Play(audioService.Library.KnockoutCredit, car.IsPlayer ? PickupVolume : PickupVolume * 0.4f);
            }

            return true;
        }

        /// <summary>Whether a balloon is carrying <paramref name="car"/> back to the floor after it went over the edge.</summary>
        public bool IsCarrying(Car car)
        {
            return balloons != null && balloons.IsCarrying(car);
        }

        /// <summary>Whether <paramref name="car"/>'s glove is out, so a bot does not punch twice in a row.</summary>
        public bool IsPunching(Car car)
        {
            return gloves != null && gloves.IsPunching(car);
        }

        public void Dispose()
        {
            foreach (var (car, handler) in shieldHandlers) {
                if (car != null) {
                    car.ShieldPopped -= handler;
                }
            }

            shieldHandlers.Clear();
            if (isBuilt) {
                crumble.CycleStarted -= OnCycleStarted;
            }

            look?.Dispose();
            oil?.Dispose();
            balloons?.Dispose();
            art?.Dispose();
            if (root != null) {
                Object.Destroy(root.gameObject);
            }
        }

        private void EnsureBuilt()
        {
            if (!isBuilt) {
                Build();
            }
        }

        /// <summary>
        /// Builds the pool of boxes, bombs and gloves and the look of the abilities, once, when the cars are placed: the
        /// bots' seed is set after the scene loads, and the choices come from it.
        /// </summary>
        private void Build()
        {
            isBuilt = true;
            random = new System.Random(hazards.Seed ^ 0x6A09);
            root = new GameObject("ArenaPickups").transform;
            root.SetParent(parent, false);
            art = new PickupArt(mechanicArt);
            // The effects live in the scene, not the container.
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            look = new AbilityLook(mechanicArt, root, effects);
            blow = new ItemBlow(settings, difficulty);
            bombs = new ItemBombs(tuning, round, edge, blow, effects, audioService, arenaCamera, art, root);
            gloves = new ItemGloves(tuning, round, blow, effects, audioService, arenaCamera, art, root);
            bowls = new ItemBowls(tuning, round, edge, blow, effects, audioService, arenaCamera, art, root);
            oil = new ItemOil(tuning, round, edge, effects, audioService, art, root);
            surface.Add(oil);
            balloons = new ItemBalloons(tuning, round, edge, effects, audioService, art, root);

            slots = new FloorPickup[maxCount];
            for (var i = 0; i < slots.Length; i++) {
                var slot = new FloorPickup();
                slot.Root = new GameObject("Box").transform;
                slot.Root.SetParent(root, false);
                slot.Hover = new GameObject("Hover").transform;
                slot.Hover.SetParent(slot.Root, false);
                slot.BoxRenderer = art.BuildBox(slot.Hover);
                art.BuildMarker(slot);
                slot.Root.gameObject.SetActive(false);
                slots[i] = slot;
            }

            foreach (var car in round.Cars) {
                var popped = car;
                Action handler = () => OnShieldPopped(popped);
                car.ShieldPopped += handler;
                shieldHandlers.Add((car, handler));
            }

            nextSpawn = settings.FirstDelay;
            crumble.CycleStarted += OnCycleStarted;
        }

        /// <summary>Each cycle of the floor starts its boxes afresh: the first one a little after the floor is whole.</summary>
        private void OnCycleStarted(int cycle)
        {
            nextSpawn = round.Elapsed + settings.FirstDelay;
        }

        private void OnShieldPopped(Car car)
        {
            if (effects != null) {
                effects.PlayShieldPop(car.Position);
            }

            audioService?.Play(audioService.Library.HitLight, car.IsPlayer ? 0.8f : 0.4f);
        }

        // --- Life of a box ---

        /// <summary>
        /// Ages the boxes lying there, fades the ones whose time is up or whose floor is going, and breaks the rest under
        /// the nearest car that can take them.
        /// </summary>
        private void TickLive(float deltaTime)
        {
            for (var i = live.Count - 1; i >= 0; i--) {
                var pickup = live[i];
                pickup.Age += deltaTime;
                // The safe view of the edge drops a wave as soon as it shakes, so this also catches a rim, a pit and a hole.
                if (pickup.Age >= settings.Lifetime || edge.DistanceToEdge(pickup.Position, settings.VanishMargin) < 0f) {
                    Leave(pickup, false);
                    continue;
                }

                var taker = NearestTaker(pickup);
                if (taker != null) {
                    Leave(pickup, true);
                    Break(taker);
                }
            }
        }

        /// <summary>
        /// A car broke a box: its empty slot starts to spin on an item drawn for how far the car trails the leader.
        /// </summary>
        private void Break(Car car)
        {
            var trailing = ItemRoll.TrailingOf(round.Score, round.IndexOf(car), settings);
            var kind = ItemRoll.Choose(random, settings, trailing);
            car.GiveItem(kind, rollTime);
            if (effects != null) {
                effects.PlayPickup(car.Position);
            }

            audioService?.Play(audioService.Library.KnockoutCredit, car.IsPlayer ? PickupVolume : PickupVolume * 0.5f);
            Collected?.Invoke(car, kind, car.Position);
        }

        private Car NearestTaker(FloorPickup pickup)
        {
            Car best = null;
            var bestDistance = settings.PickRadius * settings.PickRadius;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                // A car that is safe after coming back is a ghost, a car in the air takes nothing, and a car that holds
                // an item (its slot spinning too) leaves the box lying for one that can take it.
                if (!car.IsTargetable || car.HasItem || !car.Movement.IsGrounded || car.Position.y > TakeHeight) {
                    continue;
                }

                var offset = car.Position - pickup.Position;
                offset.y = 0f;
                var distance = offset.sqrMagnitude;
                if (distance <= bestDistance) {
                    bestDistance = distance;
                    best = car;
                }
            }

            return best;
        }

        /// <summary>Takes a box off the floor: it bursts if a car broke it, or fades if it ran out or lost its floor.</summary>
        private void Leave(FloorPickup pickup, bool wasTaken)
        {
            live.Remove(pickup);
            pickup.State = FloorPickup.Stage.Leaving;
            pickup.StageTime = 0f;
            pickup.WasTaken = wasTaken;
            if (!wasTaken && effects != null) {
                effects.PlayPickupFade(pickup.Position);
            }
        }

        private void RemoveAll()
        {
            for (var i = live.Count - 1; i >= 0; i--) {
                Leave(live[i], false);
            }
        }

        // --- Appearing ---

        private void TrySpawn()
        {
            if (live.Count >= slots.Length || round.Elapsed < nextSpawn) {
                return;
            }

            if (TryPickSpot(out var spot) && Spawn(spot) != null) {
                var jitter = ((float)random.NextDouble() * 2f - 1f) * settings.Jitter;
                nextSpawn = round.Elapsed + Mathf.Max(0.5f, settings.Interval + jitter) / pace;
            }
            else {
                nextSpawn = round.Elapsed + RetryDelay;
            }
        }

        /// <summary>
        /// A spot of safe floor with room round it, clear of every car and of the boxes already lying there; near a car
        /// behind in knock-outs when the dice say so (<see cref="ChooseLean"/>), else anywhere on the safe floor.
        /// </summary>
        private bool TryPickSpot(out Vector3 spot)
        {
            var lean = ChooseLean();
            var extent = edge.SafeExtent;
            for (var attempt = 0; attempt < SpotAttempts; attempt++) {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (lean != null && attempt < SpotAttempts * 2 / 3) {
                    var range = settings.LeanRange;
                    var near = new Vector3(lean.Position.x, 0f, lean.Position.z);
                    spot = near + direction * Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
                }
                else {
                    spot = direction * (Mathf.Sqrt((float)random.NextDouble()) * extent);
                }

                if (edge.DistanceToEdge(spot, settings.EdgeMargin) < 0f || IsCrowded(spot)) {
                    continue;
                }

                return true;
            }

            spot = default;
            return false;
        }

        /// <summary>
        /// The car the next box leans towards, or null for none: the more the car furthest behind trails the leader in
        /// knock-outs, the likelier one is picked, and then it is a car in play chosen by how far behind it is, so the one
        /// losing most is the one most often helped.
        /// </summary>
        private Car ChooseLean()
        {
            var score = round.Score;
            if (score == null) {
                return null;
            }

            var cars = round.Cars;
            var most = 0;
            var least = int.MaxValue;
            for (var i = 0; i < cars.Count; i++) {
                most = Mathf.Max(most, score.KnockoutsOf(i));
                least = Mathf.Min(least, score.KnockoutsOf(i));
            }

            var chance = Mathf.Min(settings.LeanMax, (most - least) * settings.LeanPerKnockout);
            if (chance <= 0f || random.NextDouble() >= chance) {
                return null;
            }

            var total = 0;
            for (var i = 0; i < cars.Count; i++) {
                if (!cars[i].IsOut) {
                    total += most - score.KnockoutsOf(i);
                }
            }

            if (total <= 0) {
                return null;
            }

            var roll = random.Next(total);
            for (var i = 0; i < cars.Count; i++) {
                if (cars[i].IsOut) {
                    continue;
                }

                roll -= most - score.KnockoutsOf(i);
                if (roll < 0) {
                    return cars[i];
                }
            }

            return null;
        }

        private bool IsCrowded(Vector3 spot)
        {
            var carReach = settings.CarClearance * settings.CarClearance;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                if (cars[i].IsOut) {
                    continue;
                }

                var offset = cars[i].Position - spot;
                offset.y = 0f;
                if (offset.sqrMagnitude < carReach) {
                    return true;
                }
            }

            var apart = settings.Spacing * settings.Spacing;
            foreach (var pickup in live) {
                if ((pickup.Position - spot).sqrMagnitude < apart) {
                    return true;
                }
            }

            return false;
        }

        // --- The shockwave ---

        /// <summary>
        /// The ring of a shockwave: every other car in play within its reach is pushed away from the user, harder the
        /// nearer, and thrown a little up and off balance, credited to the user (<see cref="ItemBlow"/>).
        /// </summary>
        private void SendShockwave(Car user)
        {
            var wave = settings.Shockwave;
            var origin = user.Position;
            var isPlayerHit = user.IsPlayer;
            foreach (var car in round.Cars) {
                if (car == user) {
                    continue;
                }

                var offset = car.Position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > wave.Radius) {
                    continue;
                }

                // Straight out; from the very spot, the way the car faces away from the user.
                var direction = distance > 0.05f ? offset / distance : car.transform.forward;
                var share = ItemBlow.Share(distance, wave.Radius, wave.EdgeShare, wave.Falloff);
                var cap = tuning.Car.MaxBumpSpeed * wave.MaxSpeedShare;
                if (!blow.Apply(car, user, direction, wave.Push * share, cap, wave.Lift * share, wave.Stagger)) {
                    continue;
                }

                user.ReportItemHit(ItemKind.Shockwave, car.Position);
                isPlayerHit |= car.IsPlayer;
            }

            if (effects != null) {
                effects.PlayShockwave(origin, wave.Radius);
            }

            audioService?.Play(audioService.Library.HitHeavy, isPlayerHit ? 0.9f : 0.45f);
            // The player feels their own wave too, not only one that hits them.
            if (arenaCamera != null && (isPlayerHit || user.IsPlayer)) {
                arenaCamera.Shake(0.5f);
            }
        }

        // --- Looks ---

        /// <summary>
        /// Floats, spins, colours and sizes the boxes: popping in with an overshoot, blinking as their time runs out, and
        /// bursting or shrinking away.
        /// </summary>
        private void AnimateSlots(float deltaTime)
        {
            foreach (var slot in slots) {
                if (slot.State == FloorPickup.Stage.Free) {
                    continue;
                }

                slot.Phase += deltaTime;
                var scale = 1f;
                if (slot.State == FloorPickup.Stage.Live) {
                    scale = EaseOutBack(Mathf.Clamp01(slot.Age / PopTime));
                    var isBlinking = settings.Lifetime - slot.Age < BlinkTime;
                    slot.Hover.gameObject.SetActive(!isBlinking || Mathf.Repeat(slot.Phase * BlinkRate, 1f) < 0.7f);
                }
                else {
                    slot.StageTime += deltaTime;
                    var t = Mathf.Clamp01(slot.StageTime / LeaveTime);
                    scale = slot.WasTaken ? (1f + 0.4f * t) * (1f - t * t) : 1f - t;
                    slot.Hover.gameObject.SetActive(true);
                    if (t >= 1f) {
                        slot.State = FloorPickup.Stage.Free;
                        slot.Root.gameObject.SetActive(false);
                        continue;
                    }
                }

                slot.Root.localScale = Vector3.one * Mathf.Max(0f, scale);
                slot.Hover.localPosition = Vector3.up * (PickupArt.HoverHeight + Mathf.Sin(slot.Phase * 3f) * 0.18f);
                // A tumble as well as a spin, so the box reads as a lucky box and not a crate.
                slot.Hover.localRotation = Quaternion.Euler(Mathf.Sin(slot.Phase * 2f) * 12f, slot.Phase * 140f,
                    Mathf.Cos(slot.Phase * 1.7f) * 12f);
                art.TintBox(slot, slot.Phase);
            }
        }

        private static float EaseOutBack(float t)
        {
            const float Overshoot = 1.70158f;
            var x = t - 1f;
            return 1f + (Overshoot + 1f) * x * x * x + Overshoot * x * x;
        }
    }
}
