using System;
using System.Collections.Generic;
using Bugord.Core;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// One round on the arena: <see cref="Begin"/> puts the cars on the spawn circle, facing the middle, and from then
    /// on a car that falls below <see cref="GameTuning.RoundSettings.FallHeight"/> is out. The round finishes when the
    /// player is out, when the player is the last car left, or when time runs out. It runs once: the next round is a
    /// freshly loaded scene. Who drives the cars is not its business; it only says which one is the player's.
    /// </summary>
    public sealed class ArenaRound : ITickable
    {
        /// <summary>
        /// Raised for each car that falls off, the player's included, before the round finishes on it.
        /// </summary>
        public event Action<Car> CarOut;

        /// <summary>
        /// Raised once, when the round is decided.
        /// </summary>
        public event Action<RoundResult> Finished;

        private readonly GameTuning tuning;
        private readonly IObjectResolver resolver;
        private readonly Car carPrefab;
        private readonly Transform carParent;
        private readonly List<Car> cars = new();

        private bool hasBegun;
        private float elapsed;

        public ArenaRound(GameTuning tuning, IObjectResolver resolver, Car carPrefab, Transform carParent)
        {
            this.tuning = tuning;
            this.resolver = resolver;
            this.carPrefab = carPrefab;
            this.carParent = carParent;
        }

        public IReadOnlyList<Car> Cars => cars;

        /// <summary>
        /// The player's car, null before the round begins.
        /// </summary>
        public Car Player { get; private set; }

        /// <summary>
        /// Between <see cref="Begin"/> and <see cref="Finished"/>.
        /// </summary>
        public bool IsRunning { get; private set; }

        public float TimeLeft => Mathf.Max(0f, tuning.Round.Duration - elapsed);

        public int CarsLeft { get; private set; }

        /// <summary>
        /// How far from the middle the arena reaches now, in metres. The one place the bots read the edge from: the
        /// crumbling edge (step 5 of the brief) will shrink it, until then it is the starting radius.
        /// </summary>
        public float ArenaRadius => tuning.Arena.Radius;

        public void Begin()
        {
            if (hasBegun) {
                throw new InvalidOperationException("A round runs once; load the scene again for the next one.");
            }

            hasBegun = true;
            Spawn(1 + tuning.Bots.Count);
            Player = cars[0];
            CarsLeft = cars.Count;
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
                    car.TakeOut();
                    CarsLeft--;
                    CarOut?.Invoke(car);
                }
            }

            // Cars that fell on the same frame as the player still count as behind it.
            if (Player.IsOut) {
                Finish(RoundOutcome.Out, CarsLeft + 1);
            }
            else if (cars.Count > 1 && CarsLeft == 1) {
                Finish(RoundOutcome.Won, 1);
            }
            else if (elapsed >= tuning.Round.Duration) {
                Finish(CarsLeft == 1 ? RoundOutcome.Won : RoundOutcome.Survived, 1);
            }
        }

        private void Finish(RoundOutcome outcome, int place)
        {
            IsRunning = false;
            Finished?.Invoke(new RoundResult(outcome, place, cars.Count, elapsed));
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
                car.name = i == 0 ? "Player Car" : $"Bot Car {i}";
                car.GetComponent<CarLook>().Apply(i);
                cars.Add(car);
            }
        }
    }
}
