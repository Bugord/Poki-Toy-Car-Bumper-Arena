using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Holds an imperfect decision between looks instead of reacting to every physics step like a bot.
    /// These defaults need calibration against Poki; they describe a model, not a measured human player.
    /// </summary>
    public sealed class NoviceDriver
    {
        // Calibration parameters: seconds, metres, degrees, and probabilities per look.
        public float ReactionMin = 0.35f;
        public float ReactionMax = 0.6f;
        public float OtherTargetChance = 0.1f;
        public float HeadingError = 20f;
        public float EdgeFear = 4f;
        public float EdgeFearChance = 0.7f;
        public float BoostRange = 7f;
        public float BoostAngle = 35f;
        public float BoostChance = 0.35f;
        public float RandomBoostChance = 0.025f;
        public float IdleChance = 0.08f;

        private readonly System.Random random;
        private readonly ArenaRound round;
        private readonly ArenaCamera arenaCamera;
        private readonly SimulationInput input;
        private readonly List<Car> targets = new();
        private float nextLook;

        public NoviceDriver(int seed, ArenaRound round, ArenaCamera arenaCamera, SimulationInput input)
        {
            this.random = new System.Random(seed);
            this.round = round;
            this.arenaCamera = arenaCamera;
            this.input = input;
        }

        public void Tick()
        {
            if (!round.IsRunning || round.Elapsed < nextLook) {
                return;
            }

            nextLook = round.Elapsed + Between(ReactionMin, ReactionMax);
            if (Chance(IdleChance)) {
                input.SetMove(Vector2.zero);
                return;
            }

            var player = round.Player;
            var position = Flat(player.Position);
            var target = PickTarget(player);
            var toTarget = target != null ? Flat(target.Position) - position : -position;
            var heading = toTarget.normalized;
            if (round.ArenaRadius - position.magnitude < EdgeFear && Chance(EdgeFearChance)) {
                heading = -position.normalized;
            }

            heading = Quaternion.Euler(0f, Between(-HeadingError, HeadingError), 0f) * heading;
            // Inverse of ArenaCamera.ScreenToGroundDirection: keys point relative to the camera's yaw.
            var local = Quaternion.Inverse(Quaternion.Euler(0f, arenaCamera.transform.eulerAngles.y, 0f)) * heading;
            input.SetMove(new Vector2(local.x, local.z));

            var canAttack = target != null && toTarget.magnitude < BoostRange &&
                Vector3.Angle(Flat(player.transform.forward), toTarget) < BoostAngle;
            if ((canAttack && Chance(BoostChance)) || Chance(RandomBoostChance)) {
                input.PressBoost();
            }
        }

        private Car PickTarget(Car player)
        {
            targets.Clear();
            Car nearest = null;
            var distance = float.PositiveInfinity;
            foreach (var car in round.Cars) {
                if (car == player || car.IsOut) {
                    continue;
                }

                targets.Add(car);
                var candidate = (Flat(car.Position) - Flat(player.Position)).sqrMagnitude;
                if (candidate < distance) {
                    nearest = car;
                    distance = candidate;
                }
            }

            if (targets.Count > 1 && Chance(OtherTargetChance)) {
                targets.Remove(nearest);
                return targets[random.Next(targets.Count)];
            }

            return nearest;
        }

        private bool Chance(float probability)
        {
            return random.NextDouble() < probability;
        }

        private float Between(float min, float max)
        {
            return min + (max - min) * (float)random.NextDouble();
        }

        private static Vector3 Flat(Vector3 value)
        {
            value.y = 0f;
            return value;
        }
    }
}
