using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The balloons of a match. While a car runs a balloon (<see cref="ItemKind.Balloon"/>, a timed item) it floats on a
    /// string over the car, and a fall off the floor is caught: once the car has sunk
    /// <see cref="GameTuning.BalloonSettings.CatchDepth"/> below the floor with no floor under it, well before the round
    /// takes it out, the balloon swells and carries it (<see cref="CarMovement.Carry"/>) up over the other cars, across to
    /// the nearest safe floor clear of them, and lets it drop there, then pops. The car is not in play while it is carried,
    /// and nothing of the fall counts: no fall, no knock-out. One catch, then the balloon is spent.
    /// </summary>
    internal sealed class ItemBalloons : IDisposable
    {
        // Metres the balloon floats over the car, how fast it bobs and sways, and how much it swells to carry.
        private const float FloatHeight = 3.3f;
        private const float CarrySwell = 1.35f;
        // Seconds before it runs out that a balloon starts to blink, and how fast.
        private const float BlinkTime = 2f;
        private const float BlinkRate = 6f;
        // The search for a place to set the car down: metres between the spots tried, along the way in and on a grid.
        private const float WalkStep = 0.5f;
        private const int WalkSteps = 80;
        private const float GridStep = 1f;

        private sealed class Slot
        {
            public Car Car;
            public Transform Root;
            public Transform String;
            public bool IsCarrying;
            public Action Ended;
        }

        private readonly GameTuning tuning;
        private readonly GameTuning.BalloonSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly EffectPlayer effects;
        private readonly IAudioService audioService;
        private readonly PickupArt art;
        private readonly Transform parent;
        private readonly List<Slot> slots = new();
        private float time;

        /// <summary>Raised when a balloon catches a car's fall, with the car, as it starts to carry it back.</summary>
        public event Action<Car> Caught;

        public ItemBalloons(GameTuning tuning, ArenaRound round, IArenaEdge edge, EffectPlayer effects,
            IAudioService audioService, PickupArt art, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups.Balloon;
            this.round = round;
            this.edge = edge;
            this.effects = effects;
            this.audioService = audioService;
            this.art = art;
            this.parent = parent;
        }

        /// <summary>Whether a balloon is carrying <paramref name="car"/> back right now.</summary>
        public bool IsCarrying(Car car)
        {
            foreach (var slot in slots) {
                if (slot.Car == car) {
                    return slot.IsCarrying;
                }
            }

            return false;
        }

        public void Tick(float deltaTime)
        {
            time += deltaTime;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                var isArmed = !car.IsOut && car.Ability == ItemKind.Balloon;
                if (!isArmed && (i >= slots.Count || slots[i] == null)) {
                    continue;
                }

                var slot = SlotFor(i, car);
                if (slot.IsCarrying && (car.IsOut || !car.Movement.IsCarried)) {
                    // A respawn or the end of the round took over from the carry.
                    slot.IsCarrying = false;
                }

                if (isArmed && !slot.IsCarrying && IsFalling(car)) {
                    Catch(slot);
                }

                Show(slot, isArmed || slot.IsCarrying);
            }
        }

        public void Dispose()
        {
            foreach (var slot in slots) {
                if (slot == null) {
                    continue;
                }

                if (slot.Car != null) {
                    slot.Car.Movement.CarryEnded -= slot.Ended;
                }

                if (slot.Root != null) {
                    Object.Destroy(slot.Root.gameObject);
                }
            }

            slots.Clear();
        }

        /// <summary>Off the floor, with nothing under it, and sunk below the top of the floor: going over the edge.</summary>
        private bool IsFalling(Car car)
        {
            var position = car.Position;
            return !car.Movement.IsGrounded && position.y < -settings.CatchDepth
                && position.y > tuning.Round.FallHeight && !edge.IsSolid(new Vector3(position.x, 0f, position.z));
        }

        /// <summary>
        /// The balloon takes the car: the item is spent, the car stops being in play, and it is carried to a spot of
        /// safe floor; if there is none it falls as it would have.
        /// </summary>
        private void Catch(Slot slot)
        {
            var car = slot.Car;
            if (!TryFindLanding(car, out var landing)) {
                return;
            }

            car.ClearAbility();
            slot.IsCarrying = true;
            car.Movement.Carry(landing + Vector3.up * settings.DropHeight, settings.CarryHeight, settings.CarryTime);
            Caught?.Invoke(car);
            car.ReportItemHit(ItemKind.Balloon, car.Position);
            audioService?.Play(audioService.Library.KnockoutCredit, car.IsPlayer ? 0.8f : 0.35f);
        }

        /// <summary>The carry is over: the car drops the last bit to the floor and the balloon pops.</summary>
        private void OnCarryEnded(Slot slot)
        {
            if (!slot.IsCarrying) {
                return;
            }

            slot.IsCarrying = false;
            if (effects != null) {
                effects.PlayShieldPop(slot.Root.position);
            }

            audioService?.Play(audioService.Library.HitLight, slot.Car.IsPlayer ? 0.8f : 0.35f);
        }

        /// <summary>
        /// Where to set the car down: in along the floor from where it went off until the floor is safe with room, then
        /// the nearest such spot clear of the other cars; false when the floor has no such spot at all.
        /// </summary>
        private bool TryFindLanding(Car car, out Vector3 landing)
        {
            var start = new Vector3(car.Position.x, 0f, car.Position.z);
            var point = start;
            for (var step = 0; step < WalkSteps; step++) {
                if (edge.DistanceToEdge(point, settings.LandMargin) >= 0f) {
                    break;
                }

                var inward = edge.Inward(point);
                inward.y = 0f;
                if (inward.sqrMagnitude < 0.0001f) {
                    inward = -point;
                    inward.y = 0f;
                }

                if (inward.sqrMagnitude < 0.0001f) {
                    break;
                }

                point += inward.normalized * WalkStep;
            }

            if (IsClear(point, car)) {
                landing = point;
                return true;
            }

            // The nearest free spot to that one on a grid over the floor.
            var extent = edge.FullExtent;
            var best = float.PositiveInfinity;
            landing = default;
            for (var x = -extent; x <= extent; x += GridStep) {
                for (var z = -extent; z <= extent; z += GridStep) {
                    var spot = new Vector3(x, 0f, z);
                    var distance = (spot - point).sqrMagnitude;
                    if (distance >= best || !IsClear(spot, car)) {
                        continue;
                    }

                    best = distance;
                    landing = spot;
                }
            }

            return !float.IsPositiveInfinity(best);
        }

        private bool IsClear(Vector3 spot, Car car)
        {
            if (edge.DistanceToEdge(spot, settings.LandMargin) < 0f) {
                return false;
            }

            var clearance = settings.LandClearance * settings.LandClearance;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var other = cars[i];
                if (other == car || other.IsOut) {
                    continue;
                }

                var offset = other.Position - spot;
                offset.y = 0f;
                if (offset.sqrMagnitude < clearance) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// The balloon over the car, bobbing on its string, swollen while it carries and blinking as it runs out; hidden
        /// when there is none.
        /// </summary>
        private void Show(Slot slot, bool isShown)
        {
            var car = slot.Car;
            if (isShown && !slot.IsCarrying && car.AbilityTimeLeft < BlinkTime) {
                isShown = Mathf.Repeat(time * BlinkRate, 1f) < 0.5f;
            }

            if (slot.Root.gameObject.activeSelf != isShown) {
                slot.Root.gameObject.SetActive(isShown);
            }

            if (!isShown) {
                return;
            }

            var growth = car.Growth;
            var swell = slot.IsCarrying ? CarrySwell : 1f;
            var bob = Mathf.Sin(time * 2.6f + slot.GetHashCode() % 7) * 0.15f;
            var sway = Mathf.Sin(time * 1.9f) * 6f;
            var top = car.Position + Vector3.up * ((FloatHeight + bob) * growth);
            slot.Root.SetPositionAndRotation(top, Quaternion.Euler(sway, car.transform.eulerAngles.y, 0f));
            slot.Root.localScale = Vector3.one * (growth * swell);
            // The string reaches down to the car's roof whatever the balloon's size.
            var roof = car.Position + Vector3.up * (1.2f * growth);
            var length = Mathf.Max(0.1f, Vector3.Distance(top, roof)) / slot.Root.localScale.y;
            slot.String.localScale = new Vector3(1f, length, 1f);
        }

        private Slot SlotFor(int index, Car car)
        {
            while (slots.Count <= index) {
                slots.Add(null);
            }

            if (slots[index] != null) {
                return slots[index];
            }

            var slot = new Slot { Car = car };
            slot.Root = art.BuildBalloon(parent, out slot.String);
            slot.Root.gameObject.SetActive(false);
            slot.Ended = () => OnCarryEnded(slot);
            car.Movement.CarryEnded += slot.Ended;
            slots[index] = slot;
            return slot;
        }
    }
}
