using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// Drives every car of the round but the player's while the round runs, as the brief asks: pick a target, speed up
    /// at it, do not go off the edge. Each <see cref="Bot"/> steers itself; the driver picks the targets, because a
    /// good pick depends on what the other bots are after. A pick weighs every car still in by how near it is, how
    /// close to the edge it is (easy to push off), whether it is the player's, how many bots already chase it, and a
    /// little chance. Nobody picks the player for the first seconds of a round, so a new player has time to find
    /// their car. Once the round is over the bots roll to a stop, as the player's car does.
    /// </summary>
    public sealed class BotDriver : ITickable
    {
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly List<Bot> bots = new();

        private bool isDriving;

        public BotDriver(GameTuning tuning, ArenaRound round)
        {
            this.tuning = tuning;
            this.round = round;
        }

        public void Tick()
        {
            if (!round.IsRunning) {
                if (isDriving) {
                    isDriving = false;
                    foreach (var bot in bots) {
                        bot.Car.Movement.Drive(Vector3.zero, 0f);
                    }
                }

                return;
            }

            // The round runs once, so its cars are there for good once it begins.
            if (!isDriving) {
                isDriving = true;
                if (bots.Count == 0) {
                    CreateBots();
                }
            }

            var deltaTime = Time.deltaTime;
            var radius = round.ArenaRadius;
            foreach (var bot in bots) {
                if (bot.Car.IsOut) {
                    continue;
                }

                if (bot.WantsTarget) {
                    bot.SetTarget(PickTarget(bot, radius));
                }

                bot.Steer(radius, deltaTime);
            }
        }

        private void CreateBots()
        {
            foreach (var car in round.Cars) {
                if (car != round.Player) {
                    bots.Add(new Bot(car, tuning.Bots));
                }
            }
        }

        /// <summary>
        /// The best scoring car still in, other than the bot's own; null when there is none.
        /// </summary>
        private Car PickTarget(Bot bot, float radius)
        {
            var settings = tuning.Bots;
            var position = bot.Car.Position;
            var diameter = Mathf.Max(1f, radius * 2f);
            var isPlayerSpared = round.Elapsed < settings.PlayerGrace;
            Car best = null;
            var bestScore = float.NegativeInfinity;
            foreach (var car in round.Cars) {
                if (car == bot.Car || car.IsOut || (isPlayerSpared && car == round.Player)) {
                    continue;
                }

                var carPosition = car.Position;
                var distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(carPosition.x, carPosition.z));
                var fromMiddle = new Vector2(carPosition.x, carPosition.z).magnitude;
                var score = settings.EdgeWeight * Mathf.Clamp01(fromMiddle / Mathf.Max(1f, radius))
                    - settings.DistanceWeight * distance / diameter
                    - settings.CrowdPenalty * CountHunters(car, bot)
                    + Random.value * settings.PickNoise;
                if (car == round.Player) {
                    score += settings.PlayerWeight;
                }

                if (score > bestScore) {
                    bestScore = score;
                    best = car;
                }
            }

            return best;
        }

        /// <summary>
        /// How many bots other than <paramref name="except"/> are after <paramref name="car"/>.
        /// </summary>
        private int CountHunters(Car car, Bot except)
        {
            var count = 0;
            foreach (var bot in bots) {
                if (bot != except && !bot.Car.IsOut && bot.Target == car) {
                    count++;
                }
            }

            return count;
        }
    }
}
