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
    /// The HOT POTATO mode's bomb (<see cref="RoundRules.IsHotPotato"/>, once a special round): a lit bomb rides on one
    /// car and jumps to any car that car bumps into, though never straight back to the car that just passed it
    /// (<see cref="GameTuning.HotPotatoSettings.PassBackTime"/>). When its fuse runs out its holder is out of the round
    /// (<see cref="ArenaRound.KnockOut"/>, to the credit of the car that passed it): it pops up and is gone, and the cars
    /// beside it are nudged. A new bomb lands on another car a moment later, never on the player as the round's first.
    /// The floor is fenced (<see cref="Arena.ArenaFence"/>), so the bomb is the only way out; the last car left wins.
    /// The bots chase with it and run from it (<see cref="Bots.Bot.SteerPotato"/>). In any other round it builds nothing.
    /// The look is the item bomb's, the nudge the items' (<see cref="ItemBlow"/>).
    /// </summary>
    public sealed class HotPotato : ITickable, IArenaHazard, IDisposable
    {
        // Metres above the car's middle the bomb rides, at the car's own size, and how much it bobs.
        private const float RideHeight = 1.7f;
        private const float Bob = 0.12f;
        // Cars this far below or above the floor are not in the blast: one falling, or one flying over it.
        private const float BlastDepth = -1f;
        private const float BlastHeight = 3f;
        // Metres a fleeing bot looks ahead for floor before it runs that way.
        private const float FleeLook = 3f;

        /// <summary>Raised when the bomb lands on a car: a new one, or passed on a bump.</summary>
        public event Action<Car> HolderChanged;

        /// <summary>Raised when the bomb goes off on its holder.</summary>
        public event Action<Car> Blown;

        private readonly GameTuning tuning;
        private readonly GameTuning.HotPotatoSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly ArenaHazards hazards;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt mechanicArt;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly bool isActive;
        private readonly List<(Car, Action<CarHit>)> hitHandlers = new();

        private bool isBuilt;
        private System.Random random;
        private PickupArt art;
        private ItemBlow blow;
        private EffectPlayer effects;
        private Transform root;
        private ThrownBomb look;
        private float waitLeft;
        private float passReadyTime;
        private Car lastBlown;
        private float passBackUntil;
        private int bombs;
        // The holders blown out of the round, popping up until they are gone: the car and when it goes.
        private readonly List<(Car, float)> leaving = new();

        public HotPotato(GameTuning tuning, RoundRules rules, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            RoundDifficulty difficulty, MechanicArt mechanicArt, ArenaCamera arenaCamera, IAudioService audioService,
            Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.SpecialRounds.HotPotato;
            this.round = round;
            this.edge = edge;
            this.hazards = hazards;
            this.difficulty = difficulty;
            this.mechanicArt = mechanicArt;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            this.isActive = rules.Kind == SpecialRound.HotPotato;
        }

        /// <summary>Whether this scene's round is a hot potato round.</summary>
        public bool IsActive => isActive;

        /// <summary>The car the bomb rides on now, null between bombs.</summary>
        public Car Holder { get; private set; }

        /// <summary>The car that passed the bomb to its holder, null for a bomb that landed on it.</summary>
        public Car Passer { get; private set; }

        /// <summary>Seconds the bomb on the holder has left to burn.</summary>
        public float FuseLeft { get; private set; }

        public int Version { get; private set; }

        /// <summary>
        /// Whether the bomb cannot pass to <paramref name="car"/> now: it just came from it. The bots holding it look
        /// past that car.
        /// </summary>
        public bool IsPassBack(Car car) => car != null && car == Passer && Time.time < passBackUntil;

        public void Tick()
        {
            if (!isActive) {
                return;
            }

            if (!isBuilt) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            TickLeaving();

            if (!round.IsRunning) {
                if (round.IsFinished && Holder != null) {
                    Drop();
                }

                return;
            }

            var deltaTime = Time.deltaTime;
            if (Holder == null) {
                waitLeft -= deltaTime;
                if (waitLeft <= 0f) {
                    GiveNew();
                }

                return;
            }

            // The holder fell some other way: the bomb goes with it, and a new one comes.
            if (Holder.IsOut) {
                Drop();
                return;
            }

            FuseLeft -= deltaTime;
            Animate(deltaTime);
            if (FuseLeft <= 0f) {
                Explode();
            }
        }

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge floor, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (Holder == null) {
                return false;
            }

            var offset = Flat(position - Holder.Position);
            var distance = offset.magnitude;
            // The holder's own place is not one to run from.
            if (distance < 0.5f || distance > settings.FleeRadius + margin) {
                return false;
            }

            var away = offset / distance;
            var side = Vector3.Cross(Vector3.up, away);
            // Straight away if there is floor that way, else away and round the holder to either side.
            return TryFlee(away, position, floor, edgeMargin, ref heading)
                || TryFlee((away + side).normalized, position, floor, edgeMargin, ref heading)
                || TryFlee((away - side).normalized, position, floor, edgeMargin, ref heading);
        }

        private static bool TryFlee(Vector3 option, Vector3 position, IArenaEdge floor, float edgeMargin,
            ref Vector3 heading)
        {
            if (floor.DistanceToEdge(position + option * FleeLook, edgeMargin) < 0f) {
                return false;
            }

            heading = option;
            return true;
        }

        public void Dispose()
        {
            foreach (var (car, handler) in hitHandlers) {
                if (car != null) {
                    car.Hit -= handler;
                }
            }

            hitHandlers.Clear();
            hazards.Remove(this);
            art?.Dispose();
        }

        private void Build()
        {
            isBuilt = true;
            random = new System.Random(hazards.Seed ^ 0x707A);
            root = new GameObject("HotPotato").transform;
            root.SetParent(parent, false);
            art = new PickupArt(mechanicArt);
            // The effects live in the scene, not the container.
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            blow = new ItemBlow(tuning.Pickups, difficulty);
            look = new ThrownBomb();
            art.BuildBomb(root, look);
            waitLeft = settings.FirstDelay;
            hazards.Add(this);
            foreach (var car in round.Cars) {
                var self = car;
                Action<CarHit> handler = hit => OnHit(self, hit);
                car.Hit += handler;
                hitHandlers.Add((car, handler));
            }
        }

        // The holder bumped a car: the bomb jumps to it. Both cars hear a hit, so only the holder's side counts.
        private void OnHit(Car car, CarHit hit)
        {
            if (car != Holder || !round.IsRunning || Time.time < passReadyTime || hit.Strength < settings.PassStrength
                || hit.Other == null || !hit.Other.IsTargetable || IsPassBack(hit.Other)) {
                return;
            }

            Passer = car;
            passBackUntil = Time.time + settings.PassBackTime;
            Hold(hit.Other);
            audioService?.Play(audioService.Library.HitLight, IsNearPlayer(hit.Point) ? 0.8f : 0.4f);
            if (effects != null) {
                effects.PlayPickup(hit.Other.Position);
            }
        }

        // A new bomb on a car in play, another than the one just blown away when there is one.
        private void GiveNew()
        {
            var candidates = new List<Car>();
            foreach (var car in round.Cars) {
                // The round's first bomb is never the player's: one in four rounds of four cars would end in seconds.
                if (car.IsTargetable && car != lastBlown && (bombs > 0 || car != round.Player)) {
                    candidates.Add(car);
                }
            }

            if (candidates.Count == 0 && bombs == 0 && round.Player != null && round.Player.IsTargetable) {
                candidates.Add(round.Player);
            }

            if (candidates.Count == 0 && lastBlown != null && lastBlown.IsTargetable) {
                candidates.Add(lastBlown);
            }

            if (candidates.Count == 0) {
                waitLeft = settings.NextDelay;
                return;
            }

            Passer = null;
            bombs++;
            FuseLeft = settings.Fuse;
            look.Blink = 0f;
            Hold(candidates[random.Next(candidates.Count)]);
            if (effects != null) {
                effects.PlayPickupFade(Holder.Position);
            }
        }

        private void Hold(Car car)
        {
            Holder = car;
            passReadyTime = Time.time + settings.PassCooldown;
            Version++;
            look.Root.gameObject.SetActive(true);
            look.Mark.gameObject.SetActive(true);
            Animate(0f);
            HolderChanged?.Invoke(car);
        }

        // Rides on the holder, blinking faster as the fuse burns, the ring under it growing to the blast's reach.
        private void Animate(float deltaTime)
        {
            var share = 1f - Mathf.Clamp01(FuseLeft / settings.Fuse);
            var isHurried = FuseLeft <= settings.Hurry;
            look.Blink += deltaTime * (isHurried ? Mathf.Lerp(8f, 16f, share) : Mathf.Lerp(1.5f, 4f, share));
            var isLit = Mathf.Repeat(look.Blink, 1f) < 0.5f;
            var position = Holder.Position;
            var height = RideHeight * Holder.Growth + Mathf.Sin(Time.time * 6f) * Bob;
            look.Root.position = position + Vector3.up * height;
            look.Root.rotation = Quaternion.LookRotation(Flat(Holder.transform.forward).sqrMagnitude > 0.0001f
                ? Flat(Holder.transform.forward) : Vector3.forward);
            look.Root.localScale = Vector3.one * ((isLit ? 1.15f : 1f) * Mathf.Max(1f, Holder.Growth * 0.8f));
            look.Flash.SetActive(isLit);
            look.Spark.localScale = Vector3.one * (0.14f * (isLit ? 1.5f : 0.9f));
            var radius = settings.Radius * Mathf.Lerp(0.35f, 1f, share);
            look.Mark.position = new Vector3(position.x, 0f, position.z);
            look.Mark.localScale = new Vector3(radius, 1f, radius);
        }

        /// <summary>
        /// The blast: the holder is out of the round, credited to the car that passed it the bomb; the other cars within
        /// reach are pushed straight away from it, harder the nearer, into the fence at worst.
        /// </summary>
        private void Explode()
        {
            var holder = Holder;
            var origin = holder.Position;
            var outward = -edge.Inward(origin);
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f) {
                outward = Flat(holder.transform.forward);
            }

            // The holder is out of the round, to the credit of the car that passed the bomb, and pops up and away.
            var isPlayerHit = holder.IsPlayer;
            if (round.KnockOut(holder, Passer)) {
                BlowAway(holder);
            }

            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == holder || car.Position.y < BlastDepth || car.Position.y > BlastHeight) {
                    continue;
                }

                var offset = Flat(car.Position - origin);
                var distance = offset.magnitude;
                if (distance > settings.Radius) {
                    continue;
                }

                var direction = distance > 0.05f ? offset / distance : outward;
                var share = ItemBlow.Share(distance, settings.Radius, 0.35f, 1.5f);
                if (blow.Apply(car, holder, direction, settings.Push * share, tuning.Car.MaxBumpSpeed,
                        settings.Lift * share, settings.Stagger, isItem: false)) {
                    isPlayerHit |= car.IsPlayer;
                }
            }

            if (effects != null) {
                effects.PlayShockwave(origin, settings.Radius);
                effects.PlayKnockout(origin + Vector3.up * 0.4f, false);
                effects.PlayHit(origin, outward, 1f, true, false);
            }

            var isNear = isPlayerHit || IsNearPlayer(origin);
            audioService?.Play(audioService.Library.HitHeavy, isNear ? 1f : 0.5f);
            if (arenaCamera != null) {
                arenaCamera.Shake(isPlayerHit ? 0.8f : isNear ? 0.45f : 0.2f);
            }

            lastBlown = holder;
            Drop();
            Blown?.Invoke(holder);
        }

        /// <summary>
        /// Sends a holder that is out of the round up and spinning, through the fence and the other cars, until it is gone
        /// in a puff a moment later.
        /// </summary>
        private void BlowAway(Car car)
        {
            foreach (var collider in car.GetComponentsInChildren<Collider>()) {
                collider.enabled = false;
            }

            car.Movement.Launch(settings.OutLift, 720f);
            leaving.Add((car, Time.time + settings.OutTime));
        }

        private void TickLeaving()
        {
            for (var i = leaving.Count - 1; i >= 0; i--) {
                var (car, until) = leaving[i];
                if (car == null) {
                    leaving.RemoveAt(i);
                    continue;
                }

                if (Time.time < until) {
                    continue;
                }

                if (effects != null) {
                    effects.PlayPickupFade(car.Position);
                    effects.PlayKnockout(car.Position, car.IsPlayer);
                }

                car.gameObject.SetActive(false);
                leaving.RemoveAt(i);
            }
        }

        // Takes the bomb away without a blast; the next one comes after the delay.
        private void Drop()
        {
            Holder = null;
            Passer = null;
            waitLeft = settings.NextDelay;
            Version++;
            look.Root.gameObject.SetActive(false);
            look.Mark.gameObject.SetActive(false);
            look.Flash.SetActive(false);
            HolderChanged?.Invoke(null);
        }

        private bool IsNearPlayer(Vector3 point)
        {
            var player = round.Player;
            var reach = settings.Radius * 2f;
            return player != null && !player.IsOut && Flat(player.Position - point).sqrMagnitude < reach * reach;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
