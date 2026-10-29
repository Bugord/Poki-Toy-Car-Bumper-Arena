using System;
using System.Collections.Generic;
using Bugord.Core;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// One round on the arena: <see cref="Place"/> puts the cars on the spawn circle, facing the middle, where they wait
    /// for the player; <see cref="Begin"/> starts the clock, and from then on a car that falls below
    /// <see cref="GameTuning.RoundSettings.FallHeight"/> is out. The round finishes when the
    /// player is out, when the player is the last car left, or when time runs out. It runs once: the next round is a
    /// freshly loaded scene. Who drives the cars is not its business; it only says which one is the player's.
    /// <para>
    /// A car that falls within <see cref="GameTuning.RoundSettings.KnockoutCreditWindow"/> of another car driving into
    /// it was knocked out by that car (<see cref="KnockedOut"/>): the brief's "I pushed him off", which the HUD, the
    /// result screen and the player's best count. A car that drove off on its own, or slid off long after a hit, was
    /// knocked out by nobody. Player hits use <see cref="RoundDifficulty.PlayerKnockoutWindow"/>, so their credit can
    /// be more forgiving without changing which bot knocked the player out.
    /// </para>
    /// </summary>
    public sealed class ArenaRound : ITickable
    {
        /// <summary>
        /// Raised for each car that falls off, the player's included, before the round finishes on it.
        /// </summary>
        public event Action<Car> CarOut;

        /// <summary>
        /// Raised for each car that falls off soon enough after another car drove into it, with that car: the one
        /// knocked out first, then who gets the credit. Right after <see cref="CarOut"/> for the same car; bots knocked
        /// out by bots and the player knocked out by a bot included.
        /// </summary>
        public event Action<Car, Car> KnockedOut;

        /// <summary>
        /// Raised once, when the round is decided.
        /// </summary>
        public event Action<RoundResult> Finished;

        private readonly GameTuning tuning;
        private readonly IObjectResolver resolver;
        private readonly Car carPrefab;
        private readonly Transform carParent;
        private readonly ArenaFloor floor;
        private readonly RoundDifficulty difficulty;
        private readonly ISessionRoster roster;
        private readonly CarCatalog catalog;
        private readonly IPlayerCar playerCar;
        private readonly int nemesisIndex;
        private readonly List<Car> cars = new();

        private float elapsed;
        private FallCause playerFallCause;

        public ArenaRound(GameTuning tuning, IObjectResolver resolver, Car carPrefab, Transform carParent, ArenaFloor floor,
            RoundDifficulty difficulty, ISessionProgress progress, ISessionRoster roster, CarCatalog catalog,
            IPlayerCar playerCar)
        {
            this.tuning = tuning;
            this.resolver = resolver;
            this.carPrefab = carPrefab;
            this.carParent = carParent;
            this.floor = floor;
            this.difficulty = difficulty;
            this.roster = roster;
            this.catalog = catalog;
            this.playerCar = playerCar;
            this.nemesisIndex = progress.Nemesis;
        }

        public IReadOnlyList<Car> Cars => cars;

        /// <summary>
        /// The player's car, null before the cars are placed.
        /// </summary>
        public Car Player { get; private set; }

        /// <summary>
        /// This round's marked rival, null if its colour is absent. Kept after a knockout so the HUD can name it.
        /// </summary>
        public Car Nemesis { get; private set; }

        public bool RevengeTaken { get; private set; }

        /// <summary>
        /// The cars are on the arena (<see cref="Place"/>), whether the round has begun or not.
        /// </summary>
        public bool IsPlaced { get; private set; }

        /// <summary>
        /// <see cref="Begin"/> has been called: the round runs, or ran and is over.
        /// </summary>
        public bool HasBegun { get; private set; }

        /// <summary>
        /// Between <see cref="Begin"/> and <see cref="Finished"/>.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// <see cref="Finished"/> has been raised.
        /// </summary>
        public bool IsFinished { get; private set; }

        public float TimeLeft => Mathf.Max(0f, tuning.Round.Duration - elapsed);

        /// <summary>
        /// Seconds since the round began.
        /// </summary>
        public float Elapsed => elapsed;

        public int CarsLeft { get; private set; }

        /// <summary>
        /// Only the player and one bot remain: the final fight can be gentler without changing the earlier crowd.
        /// </summary>
        public bool IsDuel => Player != null && !Player.IsOut && CarsLeft == 2;

        /// <summary>
        /// Cars the player has knocked out this round.
        /// </summary>
        public int PlayerKnockouts { get; private set; }

        /// <summary>
        /// The car that knocked the player out, null while the player is on the arena or after a fall with no one to
        /// credit.
        /// </summary>
        public Car PlayerKnockedOutBy { get; private set; }

        /// <summary>
        /// How far from the middle the arena reaches now, in metres. The one place the bots read the edge from: it
        /// shrinks as the edge crumbles, and a ring counts as gone as soon as it starts to shake, so the bots get off it.
        /// </summary>
        public float ArenaRadius => floor.Radius;

        /// <summary>
        /// Puts the cars on the arena, still: nobody drives, the clock and the crumbling edge wait for
        /// <see cref="Begin"/>, so the arena can show before the player is ready. The player and as many bots as the
        /// round's difficulty brings: few in a visit's first rounds.
        /// </summary>
        public void Place()
        {
            if (IsPlaced) {
                throw new InvalidOperationException("A round runs once; load the scene again for the next one.");
            }

            IsPlaced = true;
            Spawn(1 + difficulty.BotCount);
            Player = cars[0];
            Nemesis = nemesisIndex > 0 && nemesisIndex < cars.Count ? cars[nemesisIndex] : null;
            CarsLeft = cars.Count;
        }

        /// <summary>
        /// Starts the round, placing the cars first if they are not on the arena yet.
        /// </summary>
        public void Begin()
        {
            if (HasBegun) {
                throw new InvalidOperationException("A round runs once; load the scene again for the next one.");
            }

            if (!IsPlaced) {
                Place();
            }

            HasBegun = true;
            IsRunning = true;
        }

        public void Tick()
        {
            if (!IsRunning) {
                return;
            }

            elapsed += Time.deltaTime;

            foreach (var car in cars) {
                if (!car.IsOut && car.Position.y < tuning.Round.FallHeight) {
                    var knockedOutBy = FindKnocker(car);
                    if (car == Player && knockedOutBy == null) {
                        playerFallCause = FindFallCause(car);
                    }

                    car.TakeOut();
                    CarsLeft--;
                    CarOut?.Invoke(car);

                    if (knockedOutBy != null) {
                        if (knockedOutBy == Player) {
                            PlayerKnockouts++;
                            if (car == Nemesis) {
                                RevengeTaken = true;
                            }
                        }

                        if (car == Player) {
                            PlayerKnockedOutBy = knockedOutBy;
                        }

                        KnockedOut?.Invoke(car, knockedOutBy);
                    }
                }
            }

            // Cars that fell on the same frame as the player still count as behind it; but a car out never places first,
            // not even when the last of the others fell with it.
            if (Player.IsOut) {
                Finish(RoundOutcome.Out, cars.Count > 1 ? Mathf.Max(2, CarsLeft + 1) : 1);
            }
            else if (cars.Count > 1 && CarsLeft == 1) {
                Finish(RoundOutcome.Won, 1);
            }
            else if (elapsed >= tuning.Round.Duration) {
                Finish(CarsLeft == 1 ? RoundOutcome.Won : RoundOutcome.Survived, 1);
            }
        }

        /// <summary>
        /// The car's place in the round: 0 for the player, 1..N for the bots, as <see cref="CarLook.Apply"/> painted
        /// them; -1 for a car not in the round.
        /// </summary>
        public int IndexOf(Car car)
        {
            return car != null ? cars.IndexOf(car) : -1;
        }

        private void Finish(RoundOutcome outcome, int place)
        {
            IsRunning = false;
            IsFinished = true;
            var knockedOutBy = PlayerKnockedOutBy != null ? IndexOf(PlayerKnockedOutBy) : RoundResult.Nobody;
            Finished?.Invoke(new RoundResult(outcome, place, cars.Count, elapsed, PlayerKnockouts, knockedOutBy,
                playerFallCause, Nemesis != null, RevengeTaken));
        }

        /// <summary>
        /// Explains a fall without knockout credit at the moment the car is taken out, before later events can change
        /// its history. A ring giving way wins over a boost, which wins over sliding after an old hit.
        /// </summary>
        private FallCause FindFallCause(Car car)
        {
            var now = Time.time;
            if (now - car.LastRingFallTime <= tuning.Round.RingFallWindow) {
                return FallCause.Ring;
            }

            if (now - car.Movement.LastBoostTime <= tuning.Round.BoostFallWindow) {
                return FallCause.Boost;
            }

            if (car.LastHitBy != null && car.LastHitBy != car && now - car.LastHitTime <= tuning.Round.SlideWindow) {
                return FallCause.Slide;
            }

            return FallCause.Drive;
        }

        /// <summary>
        /// The car a falling car was knocked out by: the last one that drove into it, if that was recent enough for the
        /// fall to be its doing.
        /// </summary>
        private Car FindKnocker(Car car)
        {
            var hitBy = car.LastHitBy;
            if (hitBy == null || hitBy == car) {
                return null;
            }

            var window = hitBy.IsPlayer ? difficulty.PlayerKnockoutWindow : tuning.Round.KnockoutCreditWindow;
            return Time.time - car.LastHitTime <= window ? hitBy : null;
        }

        /// <summary>
        /// Spreads the cars evenly round the spawn circle, the first one nearest the camera, all facing the middle.
        /// </summary>
        private void Spawn(int count)
        {
            var radius = tuning.Arena.Radius * tuning.Arena.SpawnRadius;
            var height = tuning.Arena.SpawnHeight;
            for (var i = 0; i < count; i++) {
                var angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / count;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                // At the very middle (a radius of 0) a car faces away from the camera.
                var facing = radius > 0f ? -outward : Vector3.forward;
                var position = outward * radius + Vector3.up * height;
                var rotation = Quaternion.LookRotation(facing);
                var car = resolver.InstantiateInjected(carPrefab, position, rotation, carParent);
                // The interpolated Rigidbody wakes up where the prefab was made, at the origin, and puts the transform
                // back there; placing the body itself keeps the cars apart.
                car.Movement.Place(position, rotation);
                car.name = roster.NameOf(i);
                car.IsPlayer = i == 0;
                var entry = i == 0 ? catalog.Find(playerCar.SelectedCar) : roster.CarOf(i);
                car.GetComponent<CarLook>().Apply(i, entry);
                cars.Add(car);
            }
        }
    }
}
