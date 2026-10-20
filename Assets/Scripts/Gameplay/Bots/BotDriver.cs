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
    /// close to the edge it is (easy to push off), whether it is the player's, how many bots already chase it, whether
    /// it is the bot's target already (so intent is readable), whether it was just hit and slides outward (easy to
    /// finish), whether it hit this bot lately (a grudge), and a little chance. Each bot boosts on its own, as the
    /// player does: into a car it pushes, and back from the edge (<see cref="Bot.Steer"/>). Once the round is over the
    /// bots roll to a stop, as the player's car does.
    /// <para>
    /// How hard the bots play the player is the round's <see cref="RoundDifficulty"/>, from the player's progress in this
    /// visit: nobody picks the player for the first seconds of a round (longer early on), only so many bots chase the
    /// player at once, and only one bot at a time holds a grudge against the player, so a new player is never mobbed.
    /// The bots' personalities come from <see cref="GameTuning.BotSettings.Mix"/>, shuffled each round.
    /// </para>
    /// <para>
    /// All the randomness comes from <see cref="Seed"/>: the shuffle from a generator seeded with it, each bot from its
    /// own generator seeded from that one. A headless simulation sets the seed before the round begins and replays the
    /// same choices; physics aside, the game is not otherwise deterministic.
    /// </para>
    /// </summary>
    public sealed class BotDriver : ITickable
    {
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly RoundDifficulty difficulty;
        private readonly List<Bot> bots = new();

        private bool isDriving;

        // The one bot whose grudge against the player is counted, while it is after the player.
        private Bot playerGrudge;

        public BotDriver(GameTuning tuning, ArenaRound round, RoundDifficulty difficulty)
        {
            this.tuning = tuning;
            this.round = round;
            this.difficulty = difficulty;
            this.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// What the bots' choices are drawn from; set it before the round begins to replay a round's choices.
        /// </summary>
        public int Seed { get; set; }

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

        /// <summary>
        /// Gives each bot's car a personality from the mix, in a new order each round, so the bully is a different car
        /// every time but always as easy to tell apart within a round.
        /// </summary>
        private void CreateBots()
        {
            var random = new System.Random(Seed);
            var mix = tuning.Bots.Mix;
            var personalities = new List<GameTuning.BotPersonality>();
            for (var i = 0; i < round.Cars.Count - 1; i++) {
                personalities.Add(mix != null && mix.Length > 0 ? mix[i % mix.Length] : GameTuning.BotPersonality.Careful);
            }

            for (var i = personalities.Count - 1; i > 0; i--) {
                var j = random.Next(i + 1);
                (personalities[i], personalities[j]) = (personalities[j], personalities[i]);
            }

            var next = 0;
            foreach (var car in round.Cars) {
                car.Hit += OnHit;
                if (car != round.Player) {
                    bots.Add(new Bot(car, personalities[next++], tuning, difficulty, round.Player, random.Next()));
                }
            }
        }

        /// <summary>
        /// A car was hit: the bots quick to seize a chance look for a target at once, while it still slides outward.
        /// Only the hit car's side counts, so each hit is seen once.
        /// </summary>
        private void OnHit(CarHit hit)
        {
            if (hit.IsHitter || !round.IsRunning) {
                return;
            }

            foreach (var bot in bots) {
                if (bot.Traits.Opportunism > 1f && !bot.Car.IsOut) {
                    bot.Reconsider();
                }
            }
        }

        /// <summary>
        /// The best scoring car still in, other than the bot's own; null when there is none.
        /// </summary>
        private Car PickTarget(Bot bot, float radius)
        {
            var settings = tuning.Bots;
            var traits = bot.Traits;
            var player = round.Player;
            var position = bot.Car.Position;
            var diameter = Mathf.Max(1f, radius * 2f);
            var now = Time.time;
            var isPlayerSpared = round.Elapsed < difficulty.PlayerGrace
                || CountHunters(player, bot) >= difficulty.MaxPlayerHunters;
            var playerWeight = Mathf.Min(settings.MaxPlayerWeight, difficulty.PlayerWeight + traits.PlayerWeight);
            if (playerGrudge != null && (playerGrudge.Car.IsOut || playerGrudge.Target != player)) {
                playerGrudge = null;
            }

            var grudge = bot.Car.LastHitBy;
            if (grudge != null && (now - bot.Car.LastHitTime > settings.GrudgeTime
                || (grudge == player && playerGrudge != null && playerGrudge != bot))) {
                grudge = null;
            }

            Car best = null;
            var bestScore = float.NegativeInfinity;
            // By index: a foreach over the read-only list would box its enumerator on every pick.
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == bot.Car || car.IsOut || (isPlayerSpared && car == player)) {
                    continue;
                }

                var carPosition = new Vector2(car.Position.x, car.Position.z);
                var distance = Vector2.Distance(new Vector2(position.x, position.z), carPosition);
                var fromMiddle = carPosition.magnitude;
                var score = settings.EdgeWeight * traits.EdgeWeight * Mathf.Clamp01(fromMiddle / Mathf.Max(1f, radius))
                    - settings.DistanceWeight * distance / diameter
                    - settings.CrowdPenalty * CountHunters(car, bot)
                    + bot.Range(0f, settings.PickNoise);
                if (car == player) {
                    score += playerWeight;
                }

                if (car == bot.Target) {
                    score += settings.StickWeight;
                }

                if (car == grudge) {
                    score += settings.GrudgeWeight;
                }

                if (IsSlidingOff(car, now)) {
                    score += settings.OpportunityWeight * traits.Opportunism;
                }

                if (score > bestScore) {
                    bestScore = score;
                    best = car;
                }
            }

            if (best == player && grudge == player) {
                playerGrudge = bot;
            }
            else if (playerGrudge == bot) {
                playerGrudge = null;
            }

            return best;
        }

        /// <summary>
        /// Whether the car was hit a moment ago and is sliding away from the middle: the easiest car to finish.
        /// </summary>
        private bool IsSlidingOff(Car car, float now)
        {
            if (now - car.LastHitTime > tuning.Bots.OpportunityTime) {
                return false;
            }

            var position = car.Position;
            var velocity = car.Movement.Velocity;
            return position.x * velocity.x + position.z * velocity.z > 0f;
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
