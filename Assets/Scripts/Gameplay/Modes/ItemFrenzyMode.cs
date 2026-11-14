using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The ITEM FRENZY event mode's own logic: boxes everywhere, items all the time. The shared round does most of it by
    /// the mode's rules (<see cref="GameTuning.ItemFrenzySettings.Rules"/>: boxes from the first round of the visit, faster,
    /// more at once and a quicker roll, no variant mechanic); the mode adds what makes it read at once:
    /// <list type="bullet">
    /// <item>as the cars are placed, a box waits in front of every car, towards the middle, so the countdown already shows
    /// what the round is about and the player's first move takes an item;</item>
    /// <item>a bot with an empty slot drives for the nearest box it can have before anything else (the edge and the
    /// arena's hazards still come first, as in <see cref="Bot.Steer"/>), so the bots hold items as often as the player and
    /// the round is a frenzy of them; a bot holding an item plays the usual round and uses it when its moment comes.</item>
    /// </list>
    /// It counts what the player and the bots used, for the round's events and the tests.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.ItemFrenzy"/>), silent in every other: it builds nothing and draws no dice there.
    /// </para>
    /// </summary>
    public sealed class ItemFrenzyMode : IRoundMode, IBotSteering, ITickable, IDisposable
    {
        // How many steps the opening box may come back towards its car where the floor ends sooner.
        private const int OpeningTries = 6;
        // Metres more than an item's reach a car spared by the grace is kept from it, as in Bot.WantsToUseItem.
        private const float SpareMargin = 1.5f;
        // The spared car's top speed against the tuned one, as in Bot.
        private const float SpareTopSpeed = 1.1f;

        private readonly GameTuning.ItemFrenzySettings settings;
        private readonly GameTuning tuning;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaRound round;
        private readonly ArenaPickups pickups;
        private readonly PlayerItemTurn itemTurn;
        private readonly Dictionary<Car, FloorPickup> chasing = new();
        private readonly Dictionary<Car, float> readySince = new();
        private readonly List<Vector3> opening = new();

        private bool isOpened;
        private bool isCounting;

        public ItemFrenzyMode(GameTuning tuning, RoundRules rules, RoundDifficulty difficulty, ArenaRound round,
            ArenaPickups pickups, PlayerItemTurn itemTurn)
        {
            this.settings = tuning.ItemFrenzy;
            this.itemTurn = itemTurn;
            this.tuning = tuning;
            this.difficulty = difficulty;
            this.round = round;
            this.pickups = pickups;
            this.IsActive = rules.Kind == SpecialRound.ItemFrenzy;
        }

        public SpecialRound Kind => SpecialRound.ItemFrenzy;

        /// <summary>A car with a mystery box riding on its roof, the box the round is full of.</summary>
        public ModeCard Card { get; } = new("vintage", 2, "Hover", 0.62f);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.ItemFrenzySettings Settings => settings;

        /// <summary>Where the boxes waiting for the cars at the start were laid, the player's first.</summary>
        public IReadOnlyList<Vector3> Opening => opening;

        /// <summary>Items the player used this round.</summary>
        public int PlayerItemsUsed { get; private set; }

        /// <summary>Items the bots used this round, all of them together.</summary>
        public int BotItemsUsed { get; private set; }

        /// <summary>Seconds into the round the player used their first item; negative while they have not.</summary>
        public float PlayerFirstUse { get; private set; } = -1f;

        public void Tick()
        {
            if (!IsActive || !pickups.IsEnabled || !round.IsPlaced) {
                return;
            }

            if (!isCounting) {
                isCounting = true;
                foreach (var car in round.Cars) {
                    if (car == round.Player) {
                        car.ItemUsed += OnPlayerUsed;
                    }
                    else {
                        car.ItemUsed += OnBotUsed;
                    }
                }
            }

            if (!isOpened) {
                isOpened = true;
                LayOpeningBoxes();
            }
        }

        public void Dispose()
        {
            if (!isCounting) {
                return;
            }

            foreach (var car in round.Cars) {
                if (car == null) {
                    continue;
                }

                car.ItemUsed -= OnPlayerUsed;
                car.ItemUsed -= OnBotUsed;
            }
        }

        /// <summary>
        /// A bot with an empty slot drives for the nearest box within <see cref="GameTuning.ItemFrenzySettings.BotBoxRange"/>
        /// that no bot nearer to it is going for, through the usual steering (the edge and hazards first, a car it is
        /// pushing kept on). A bot holding an item plays the usual round and uses it when its moment comes, but one that
        /// has held a ready item for <see cref="GameTuning.ItemFrenzySettings.BotHoldMax"/> seconds lets it go at the
        /// first moment that does it no harm (<see cref="CanLetGo"/>), so the items keep going off.
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime)
        {
            if (!IsActive) {
                return false;
            }

            var car = bot.Car;
            if (!car.IsTargetable || !pickups.IsEnabled || !round.IsRunning) {
                chasing.Remove(car);
                readySince.Remove(car);
                return false;
            }

            if (car.HasItem) {
                chasing.Remove(car);
                LetGoOfHeldItem(bot, edge);
                return false;
            }

            readySince.Remove(car);
            var box = ChooseBox(car);
            if (box == null) {
                chasing.Remove(car);
                return false;
            }

            chasing[car] = box;
            bot.SetPickup(box);
            bot.Steer(edge, deltaTime, round.IsDuel);
            return true;
        }

        private void LetGoOfHeldItem(Bot bot, IArenaEdge edge)
        {
            var car = bot.Car;
            if (!car.IsItemReady) {
                readySince.Remove(car);
                return;
            }

            if (!readySince.TryGetValue(car, out var since)) {
                readySince[car] = round.Elapsed;
                return;
            }

            // Not at the player while another bot's item is on its way to them (one at a time, as an aimed one).
            if (round.Elapsed - since >= settings.BotHoldMax && CanLetGo(car, car.HeldItem, edge)
                && itemTurn.MayLetGo(bot)) {
                readySince.Remove(car);
                itemTurn.Use(bot, true);
            }
        }

        /// <summary>
        /// Whether a bot may let its item go now with no aim: the mega off the edge's band (it makes the bot heavy and
        /// big there), the balloon any time, the shockwave with a car to push, the bomb onto floor ahead, the bowling
        /// ball, the oil; never while the player is in the item's reach in the round's first seconds
        /// (<see cref="RoundDifficulty.PlayerGrace"/>), nor in a round easier than
        /// <see cref="GameTuning.ItemFrenzySettings.SpareBelow"/>: the bots aim their items at the player as in any round
        /// with boxes, but the frenzy's extra items do not rain on a new player.
        /// </summary>
        private bool CanLetGo(Car car, ItemKind kind, IArenaEdge edge)
        {
            var position = Flat(car.Position);
            var forward = Flat(car.transform.forward).normalized;
            // An aimless item never catches the player in the grace, nor at all in a round on the easy side.
            var isSpared = round.Elapsed < difficulty.PlayerGrace || difficulty.Level < settings.SpareBelow;
            var spared = isSpared && round.Player.IsTargetable ? round.Player : null;
            var toSpared = spared != null ? Flat(spared.Position) - position : Vector3.zero;
            var items = tuning.Pickups;
            switch (kind) {
                case ItemKind.Mega:
                    return edge.DistanceToEdge(position, tuning.Bots.EdgeMargin) >= 0f;
                case ItemKind.Balloon:
                    return true;
                case ItemKind.Shockwave:
                    return (spared == null || toSpared.magnitude > items.Shockwave.Radius + SpareMargin)
                        && HasCarWithin(car, position, items.Shockwave.Radius);
                case ItemKind.Bomb:
                    var landing = position + forward * items.Bomb.ThrowDistance;
                    if (edge.DistanceToEdge(landing) < 0f) {
                        return false;
                    }

                    if (spared == null) {
                        return true;
                    }

                    var drive = ReachSpeed(spared) * (items.Bomb.FlightTime + items.Bomb.Fuse);
                    return (Flat(spared.Position) - landing).magnitude > items.Bomb.Radius + SpareMargin + drive;
                case ItemKind.Bowling:
                    var along = Vector3.Dot(toSpared, forward);
                    var side = (toSpared - forward * along).magnitude;
                    // As far wider as the spared car can drive across before the ball rolls to it (as in Bot).
                    var roll = spared != null ? ReachSpeed(spared) * Mathf.Max(0f, along) / items.Bowling.Speed : 0f;
                    return spared == null || along < 0f
                        || side > items.Bowling.Radius + items.Bowling.HitReach + SpareMargin + roll;
                case ItemKind.Oil:
                    return spared == null || toSpared.magnitude > items.Oil.TargetRange + SpareMargin;
                default:
                    return false;
            }
        }

        // How fast the spared car may drive while an item is on its way, as in Bot: its top speed, or faster if it goes
        // faster now; a car starting off is at the top at once.
        private float ReachSpeed(Car spared) =>
            Mathf.Max(Flat(spared.Movement.Velocity).magnitude, tuning.Car.MaxSpeed * SpareTopSpeed);

        private bool HasCarWithin(Car self, Vector3 position, float radius)
        {
            foreach (var other in round.Cars) {
                if (other != self && other.IsTargetable && (Flat(other.Position) - position).magnitude <= radius) {
                    return true;
                }
            }

            return false;
        }

        private FloorPickup ChooseBox(Car car)
        {
            var position = Flat(car.Position);
            var range = settings.BotBoxRange * settings.BotBoxRange;
            FloorPickup best = null;
            var bestDistance = float.MaxValue;
            var live = pickups.Live;
            for (var i = 0; i < live.Count; i++) {
                var box = live[i];
                var distance = (Flat(box.Position) - position).sqrMagnitude;
                if (distance > range || distance >= bestDistance || !box.IsFor(car) || IsTakenFrom(car, box, distance)) {
                    continue;
                }

                best = box;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>Whether another bot is going for the box and is not much further from it than this car.</summary>
        private bool IsTakenFrom(Car car, FloorPickup box, float distance)
        {
            var nearer = settings.BotBoxNearer * settings.BotBoxNearer;
            foreach (var (other, chased) in chasing) {
                if (other == car || chased != box || other.IsOut || other.HasItem) {
                    continue;
                }

                if ((Flat(box.Position) - Flat(other.Position)).sqrMagnitude * nearer > distance) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A box in front of every car, towards the middle, as many as the floor holds at once (the player's first): on
        /// safe floor and apart from each other, brought back towards its car where the floor ends sooner.
        /// </summary>
        private void LayOpeningBoxes()
        {
            var edge = round.Edge;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                var forward = Flat(car.transform.forward);
                if (forward.sqrMagnitude < 0.01f) {
                    forward = -Flat(car.Position);
                }

                forward.Normalize();
                for (var step = 0; step < OpeningTries; step++) {
                    var distance = Mathf.Lerp(settings.OpeningDistance, settings.OpeningSpacing, step / (OpeningTries - 1f));
                    var spot = Flat(car.Position) + forward * distance;
                    if (edge.DistanceToEdge(spot, settings.OpeningSpacing) < 0f || IsCrowded(spot)) {
                        continue;
                    }

                    if (pickups.Spawn(spot) == null) {
                        return;
                    }

                    opening.Add(spot);
                    break;
                }
            }
        }

        private bool IsCrowded(Vector3 spot)
        {
            var apart = settings.OpeningSpacing * settings.OpeningSpacing;
            foreach (var other in opening) {
                if ((other - spot).sqrMagnitude < apart) {
                    return true;
                }
            }

            return false;
        }

        private void OnPlayerUsed(ItemKind kind)
        {
            if (!round.IsRunning) {
                return;
            }

            PlayerItemsUsed++;
            if (PlayerFirstUse < 0f) {
                PlayerFirstUse = round.Elapsed;
            }
        }

        private void OnBotUsed(ItemKind kind)
        {
            if (round.IsRunning) {
                BotItemsUsed++;
            }
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
