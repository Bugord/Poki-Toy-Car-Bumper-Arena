using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
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
    /// In a long match a pickup lying on the floor (<see cref="ArenaPickups"/>) is weighed in the same pick: against the
    /// best car to hit, by how near it is, more the further the bot trails the leader in knock-outs, less for each bot
    /// already going for it. The bot's safety logic comes first all the same (<see cref="Bot.Steer"/>).
    /// </para>
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
        private readonly BallLane ballLane;
        private readonly ArenaHazards hazards;
        private readonly ArenaPickups pickups;
        private readonly RoundModes modes;
        private readonly List<Bot> bots = new();

        private int seed;
        private bool isDriving;

        // The score of the car the last pick chose, to weigh a pickup against; and the pickups seen so far.
        private float pickedScore;
        private int seenPickups;

        // The one bot whose grudge against the player is counted, while it is after the player.
        private Bot playerGrudge;

        public BotDriver(GameTuning tuning, ArenaRound round, RoundDifficulty difficulty, BallLane ballLane,
            ArenaHazards hazards, ArenaPickups pickups, RoundModes modes)
        {
            this.pickups = pickups;
            this.modes = modes;
            this.tuning = tuning;
            this.round = round;
            this.difficulty = difficulty;
            this.ballLane = ballLane;
            this.hazards = hazards;
            this.Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        }

        /// <summary>
        /// What the bots' choices are drawn from; set it before the round begins to replay a round's choices.
        /// </summary>
        public int Seed {
            get => seed;
            set {
                seed = value;
                // The Ball arena's chords come from the same seed, so a replayed round replays its balls.
                ballLane.Seed = value;
                hazards.Seed = value;
            }
        }

        public void Tick()
        {
            if (!round.IsBotsPlaying) {
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
            var edge = round.Edge;
            var isDuel = round.IsDuel;
            // A new pickup is worth a look at once, not at the next pick.
            var hasNewPickup = pickups.IsEnabled && pickups.Version != seenPickups;
            var steering = modes.BotSteering;
            seenPickups = pickups.Version;
            foreach (var bot in bots) {
                if (bot.Car.IsOut) {
                    continue;
                }

                if (hasNewPickup) {
                    bot.Reconsider();
                }

                // An event mode that plays its bots its own way (the hot potato's chase and flight) steers them here.
                if (steering != null && steering.SteerBot(bot, edge, deltaTime)) {
                    continue;
                }

                if (bot.WantsTarget) {
                    bot.SetTarget(PickTarget(bot, edge));
                    ChoosePickup(bot, edge);
                }

                bot.Steer(edge, deltaTime, isDuel);
                if (pickups.IsEnabled && round.IsRunning && bot.WantsToUseItem(edge, round.Cars, deltaTime)) {
                    pickups.Use(bot.Car);
                }
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
                    bots.Add(new Bot(car, personalities[next++], tuning, difficulty, round.Player, random.Next(), ballLane, hazards));
                }
            }
        }

        /// <summary>
        /// A car was hit: the bots quick to seize a chance look for a target at once, while it still slides outward.
        /// Only the hit car's side counts, so each hit is seen once.
        /// </summary>
        private void OnHit(CarHit hit)
        {
            if (hit.IsHitter || !round.IsBotsPlaying) {
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
        private Car PickTarget(Bot bot, IArenaEdge edge)
        {
            var settings = tuning.Bots;
            var traits = bot.Traits;
            var player = round.Player;
            var position = bot.Car.Position;
            var diameter = edge.Span;
            var now = Time.time;
            var isPlayerSpared = round.Elapsed < difficulty.PlayerGrace
                || CountHunters(player, bot) >= difficulty.MaxPlayerHunters;
            var playerWeight = Mathf.Min(settings.MaxPlayerWeight, difficulty.PlayerWeight + traits.PlayerWeight) +
                difficulty.StreakWeight;
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
                // A car just back in a match is safe and cannot be pushed: no target until it is in play again.
                if (car == bot.Car || !car.IsTargetable || (isPlayerSpared && car == player)) {
                    continue;
                }

                var carPosition = new Vector2(car.Position.x, car.Position.z);
                var distance = Vector2.Distance(new Vector2(position.x, position.z), carPosition);
                var score = settings.EdgeWeight * traits.EdgeWeight * edge.Nearness(car.Position)
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

                if (IsSlidingOff(car, edge, now)) {
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

            pickedScore = bestScore;
            return best;
        }

        /// <summary>
        /// Sends the bot to the pickup that beats the car it has just picked, or to none: its score is the pickup's weight
        /// and a bonus for trailing the leader, less the distance and the other bots going for it. A bot that is safe
        /// after coming back cannot take one yet; the edge-return of <see cref="Bot.Steer"/> still comes before it.
        /// </summary>
        private void ChoosePickup(Bot bot, IArenaEdge edge)
        {
            // A bot that holds an item gets nothing from a box, so it does not go for one.
            if (!pickups.IsEnabled || !bot.Car.IsTargetable || bot.Car.HasItem) {
                bot.SetPickup(null);
                return;
            }

            var settings = tuning.Pickups.Bots;
            var need = TrailingNeed(bot.Car);
            var position = bot.Car.Position;
            var diameter = edge.Span;
            FloorPickup best = null;
            var bestScore = pickedScore;
            var live = pickups.Live;
            for (var i = 0; i < live.Count; i++) {
                var pickup = live[i];
                var offset = pickup.Position - position;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance > settings.Range) {
                    continue;
                }

                var score = settings.Weight + settings.TrailingBonus * need
                    - tuning.Bots.DistanceWeight * distance / diameter
                    - settings.CrowdPenalty * CountChasers(pickup, bot);
                if (pickup == bot.Pickup) {
                    score += tuning.Bots.StickWeight;
                }

                if (score > bestScore) {
                    bestScore = score;
                    best = pickup;
                }
            }

            bot.SetPickup(best);
        }

        /// <summary>
        /// How far a car trails the match's leader in knock-outs, from 0 (level or leading) towards 1; a gap of one is a
        /// third, and it takes a wide gap to come near 1.
        /// </summary>
        private float TrailingNeed(Car car)
        {
            var score = round.Score;
            if (score == null) {
                return 0f;
            }

            var gap = score.KnockoutsOf(score.Leader) - score.KnockoutsOf(round.IndexOf(car));
            return gap <= 0 ? 0f : gap / (gap + 2f);
        }

        private int CountChasers(FloorPickup pickup, Bot except)
        {
            var count = 0;
            foreach (var bot in bots) {
                if (bot != except && !bot.Car.IsOut && bot.Pickup == pickup) {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Whether the car was hit a moment ago and is sliding towards the edge: the easiest car to finish.
        /// </summary>
        private bool IsSlidingOff(Car car, IArenaEdge edge, float now)
        {
            if (now - car.LastHitTime > tuning.Bots.OpportunityTime) {
                return false;
            }

            var position = car.Position;
            var velocity = car.Movement.Velocity;
            return Vector3.Dot(velocity, edge.Inward(position)) < 0f;
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
