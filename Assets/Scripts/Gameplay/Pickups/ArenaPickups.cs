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
    /// The abilities that lie on the floor of a long match for a car to drive over (<see cref="PickupKind"/>). A pickup
    /// appears every <see cref="GameTuning.PickupSettings.Interval"/> seconds, one or two at a time, only while the floor
    /// is whole or shrinking (<see cref="FloorPhase.Open"/> and <see cref="FloorPhase.Shrink"/>) and only on floor that is
    /// safe with room round it; at the peak and as the floor comes back none lie there, and one whose floor begins to
    /// shake fades away. Where it appears leans towards the cars behind in knock-outs, so a losing player can catch up.
    /// A car on the floor, in play and not just back from a fall, takes it by driving within
    /// <see cref="GameTuning.PickupSettings.PickRadius"/>; the ability starts at once (<see cref="Car.TakePickup"/>), and
    /// a shockwave is the ring this class sends out from the picker.
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
        // Metres above the floor a car may be and still take a pickup it drives over.
        private const float TakeHeight = 1.2f;
        private const float PickupVolume = 0.7f;

        /// <summary>
        /// Raised when a car takes a pickup, with the kind and the car's place; after the ability has started.
        /// </summary>
        public event Action<Car, PickupKind, Vector3> Collected;

        /// <summary>
        /// Raised when a pickup appears on the floor.
        /// </summary>
        public event Action<FloorPickup> Spawned;

        private readonly GameTuning tuning;
        private readonly GameTuning.PickupSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaCrumble crumble;
        private readonly IArenaEdge edge;
        private readonly ArenaHazards hazards;
        private readonly MechanicArt mechanicArt;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly bool isEnabled;
        private readonly List<FloorPickup> live = new();
        private readonly List<(Car car, Action handler)> shieldHandlers = new();

        private FloorPickup[] slots;
        private Transform root;
        private PickupArt art;
        private AbilityLook look;
        private EffectPlayer effects;
        private System.Random random;
        private float nextSpawn;
        private int version;
        private bool isBuilt;

        public ArenaPickups(GameTuning tuning, ArenaRound round, ArenaCrumble crumble, IArenaEdge edge, ArenaHazards hazards,
            MechanicArt mechanicArt, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups;
            this.round = round;
            this.crumble = crumble;
            this.edge = edge;
            this.hazards = hazards;
            this.mechanicArt = mechanicArt;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            // The one place the mode is read: a round of last-car-standing never has a pickup, or an object for one.
            this.isEnabled = settings.Enabled && tuning.IsMatch;
        }

        /// <summary>Whether this scene has pickups at all: a long match with them switched on.</summary>
        public bool IsEnabled => isEnabled;

        /// <summary>The pickups lying on the floor to be taken now.</summary>
        public IReadOnlyList<FloorPickup> Live => live;

        /// <summary>Counts up each time a pickup appears, so a bot can tell there is a new one to look at.</summary>
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
            if (!round.IsRunning) {
                if (round.IsFinished) {
                    RemoveAll();
                }

                return;
            }

            var phase = crumble.Phase;
            if (phase is FloorPhase.Peak or FloorPhase.Return) {
                RemoveAll();
                return;
            }

            TickLive(deltaTime);
            TrySpawn();
        }

        /// <summary>
        /// Puts a pickup of <paramref name="kind"/> on the floor at <paramref name="position"/> (flat), as the spawner
        /// does when it has chosen; null when every slot is taken. For the spawner, tests and the dev console.
        /// </summary>
        public FloorPickup Spawn(PickupKind kind, Vector3 position)
        {
            EnsureBuilt();
            foreach (var slot in slots) {
                if (slot.State != FloorPickup.Stage.Free) {
                    continue;
                }

                slot.Kind = kind;
                slot.Position = new Vector3(position.x, 0f, position.z);
                slot.Age = 0f;
                slot.StageTime = 0f;
                slot.WasTaken = false;
                slot.State = FloorPickup.Stage.Live;
                slot.Phase = (float)random.NextDouble() * Mathf.PI * 2f;
                slot.Root.position = slot.Position;
                slot.Root.localScale = Vector3.zero;
                slot.Root.gameObject.SetActive(true);
                for (var i = 0; i < slot.Tokens.Length; i++) {
                    if (slot.Tokens[i] != null) {
                        slot.Tokens[i].gameObject.SetActive(i == (int)kind);
                    }
                }

                art.Tint(slot, kind, 0.5f);
                live.Add(slot);
                version++;
                Spawned?.Invoke(slot);
                return slot;
            }

            return null;
        }

        /// <summary>
        /// Gives <paramref name="car"/> the ability of <paramref name="kind"/> at once, with the effects of taking it. The
        /// spawner calls this when a car drives over a pickup.
        /// </summary>
        public void Grant(Car car, PickupKind kind)
        {
            EnsureBuilt();
            car.TakePickup(kind);
            if (kind == PickupKind.Shockwave) {
                SendShockwave(car);
            }

            if (effects != null) {
                effects.PlayPickup(car.Position);
            }

            audioService?.Play(audioService.Library.KnockoutCredit, car.IsPlayer ? PickupVolume : PickupVolume * 0.5f);
            Collected?.Invoke(car, kind, car.Position);
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
        /// Builds the pool of pickups and the look of the abilities, once, when the cars are placed: the bots' seed is set
        /// after the scene loads, and the choices come from it.
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

            slots = new FloorPickup[settings.MaxCount];
            for (var i = 0; i < slots.Length; i++) {
                var slot = new FloorPickup();
                slot.Root = new GameObject("Pickup").transform;
                slot.Root.SetParent(root, false);
                slot.Hover = new GameObject("Hover").transform;
                slot.Hover.SetParent(slot.Root, false);
                slot.Tokens = art.BuildTokens(slot.Hover);
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

        /// <summary>Each cycle of the floor starts its pickups afresh: the first one a little after the floor is whole.</summary>
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

        // --- Life of a pickup ---

        /// <summary>
        /// Ages the pickups lying there, fades the ones whose time is up or whose floor is going, and gives the rest to
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
                    Grant(taker, pickup.Kind);
                }
            }
        }

        private Car NearestTaker(FloorPickup pickup)
        {
            Car best = null;
            var bestDistance = settings.PickRadius * settings.PickRadius;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                // A car that is safe after coming back is a ghost, and a car in the air takes nothing.
                if (!car.IsTargetable || !car.Movement.IsGrounded || car.Position.y > TakeHeight) {
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

        /// <summary>Takes a pickup off the floor: it pops if a car took it, or fades if it ran out or lost its floor.</summary>
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

            if (TryPickSpot(out var spot) && Spawn(PickKind(), spot) != null) {
                var jitter = ((float)random.NextDouble() * 2f - 1f) * settings.Jitter;
                nextSpawn = round.Elapsed + Mathf.Max(0.5f, settings.Interval + jitter);
            }
            else {
                nextSpawn = round.Elapsed + RetryDelay;
            }
        }

        private PickupKind PickKind()
        {
            var total = 0f;
            for (var kind = PickupKind.Heavy; kind <= PickupKind.Turbo; kind++) {
                total += settings.WeightOf(kind);
            }

            var roll = (float)random.NextDouble() * total;
            for (var kind = PickupKind.Heavy; kind <= PickupKind.Turbo; kind++) {
                roll -= settings.WeightOf(kind);
                if (roll < 0f) {
                    return kind;
                }
            }

            return PickupKind.Heavy;
        }

        /// <summary>
        /// A spot of safe floor with room round it, clear of every car and of the pickups already lying there; near a car
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
        /// The car the next pickup leans towards, or null for none: the more the car furthest behind trails the leader in
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
        /// The ring of a shockwave: every other car in play within its reach is pushed away from the picker, harder the
        /// nearer, and thrown a little up and off balance, credited to the picker so a fall that follows is the picker's
        /// knock-out. A car just back from a fall is left alone, a shield takes the ring and is spent, and no car is sent
        /// faster than a hit may.
        /// </summary>
        private void SendShockwave(Car picker)
        {
            var wave = settings.Shockwave;
            var origin = picker.Position;
            var isPlayerHit = picker.IsPlayer;
            foreach (var car in round.Cars) {
                if (car == picker || !car.IsTargetable) {
                    continue;
                }

                var offset = car.Position - origin;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > wave.Radius) {
                    continue;
                }

                if (car.TryAbsorbHit()) {
                    continue;
                }

                // Straight out; from the very spot, the way the car faces away from the picker.
                var direction = distance > 0.05f ? offset / distance : car.transform.forward;
                direction.y = 0f;
                direction.Normalize();
                var share = Mathf.Lerp(1f, wave.EdgeShare, distance / wave.Radius);
                var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
                var push = Mathf.Min(wave.Push * share, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
                car.Movement.Push(direction * push + Vector3.up * wave.Lift);
                car.Movement.Stagger(wave.Stagger);
                car.HitByShockwave(picker);
                isPlayerHit |= car.IsPlayer;
            }

            if (effects != null) {
                effects.PlayShockwave(origin, wave.Radius);
            }

            audioService?.Play(audioService.Library.HitHeavy, isPlayerHit ? 0.9f : 0.45f);
            if (arenaCamera != null && isPlayerHit) {
                arenaCamera.Shake(0.5f);
            }
        }

        // --- Looks ---

        /// <summary>
        /// Floats, spins and sizes the pickups: popping in with an overshoot, blinking as their time runs out, and popping
        /// or shrinking away.
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
                slot.Hover.localRotation = Quaternion.Euler(0f, slot.Phase * 140f, 0f);
                art.Tint(slot, slot.Kind, 0.5f + 0.5f * Mathf.Sin(slot.Phase * 4f));
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
