using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The GOLD RUSH event mode: every car for itself for a minute on a still floor, a car that falls comes back a moment
    /// later as in the long match (<see cref="GameTuning.GoldRushSettings.Rules"/>). Gold coins drop on safe floor (one in
    /// front of every car and a heap in the middle at GO!, then in bursts, leaning to the player on the easy rounds); a
    /// car drives over one to take it, a point each (<see cref="ArenaRound.AddPoints"/>), the player's car pulling the
    /// near ones in. A rival's bump hard enough shakes a share of a car's coins out: they fly a few metres onto the floor
    /// and the car that lost them cannot take them back for a moment. A fall loses a bigger share, spilled where the car
    /// went off. The points are the coins: the car with the most at the end wins.
    /// <para>
    /// The coins a car carries are its points, kept nowhere else: a loss takes the points first, then throws as many
    /// coins as the pool has room for. The bots go for the coins nearest to them, leaving one to a bot much nearer, at a
    /// throttle that grows with the difficulty; one or two of them go after a car rich enough to rob, never more than
    /// <see cref="GameTuning.GoldRushSettings.MaxPlayerHunters"/> after the player, and the usual picks favour rich cars
    /// (<see cref="IBotTargeting"/>). The player loses a smaller share on Easy.
    /// </para>
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.GoldRush"/>), silent in every other: nothing subscribed, nothing built, no random numbers
    /// drawn.
    /// </para>
    /// </summary>
    public sealed class GoldRushMode : IRoundMode, IBotSteering, IBotTargeting, IModeAnalytics, ITickable,
        IInitializable, IDisposable
    {
        // Coins the pool holds: the bursts' cap plus room for what bumps and falls throw out.
        private const int PoolSize = 48;
        // Metres between the spots a burst may land on: the floor is still, so they are found once.
        private const float SpotStep = 1f;
        // Metres a car's middle may be over the floor and still take a coin: not one flying over the arena.
        private const float TakeHeight = 2.2f;
        // Tries at a free spot for a coin before it settles for any safe one.
        private const int SpotTries = 8;
        // A coin's spin and bob start somewhere of its own.
        private const float PhaseStep = 0.618f;
        private const float DropTime = 0.45f;
        private const float DropHeight = 8f;

        private sealed class BotPlay
        {
            public GoldCoin Coin;
            public Car Hunt;
            public float ThinkAt;
            public float PitSide = 1f;
            public float PitHold;
        }

        private readonly GameTuning.GoldRushSettings settings;
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly RoundDifficulty difficulty;
        private readonly IArenaEdge edge;
        private readonly ArenaHazards hazards;
        private readonly MechanicArt art;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly List<GoldCoin> coins = new();
        private readonly List<Vector3> spots = new();
        private readonly List<Car> cars = new();
        private readonly Dictionary<Bot, BotPlay> plays = new();
        private Action<CarHit>[] hitHandlers = Array.Empty<Action<CarHit>>();
        private Vector3[] grounds = Array.Empty<Vector3>();
        private float[] shakenUntil = Array.Empty<float>();
        private float[] lastTake = Array.Empty<float>();
        private int[] runs = Array.Empty<int>();
        private GoldRushArt look;
        private EffectPlayer effects;
        private Random random;
        private bool isBuilt;
        private bool hasOpened;
        private float nextBurst;
        private int phase;

        public GoldRushMode(GameTuning tuning, RoundRules rules, ArenaRound round, RoundDifficulty difficulty,
            IArenaEdge edge, ArenaHazards hazards, MechanicArt art, ArenaCamera arenaCamera, IAudioService audioService,
            Transform parent)
        {
            this.settings = tuning.GoldRush;
            this.tuning = tuning;
            this.round = round;
            this.difficulty = difficulty;
            this.edge = edge;
            this.hazards = hazards;
            this.art = art;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            this.IsActive = rules.Kind == SpecialRound.GoldRush;
        }

        /// <summary>A car took a coin: its index and where the coin lay. The HUD counts it up.</summary>
        public event Action<int, Vector3> CoinTaken;

        /// <summary>
        /// A car lost coins: its index, how many and the index of the car whose bump shook them out
        /// (<see cref="RoundResult.Nobody"/> for a fall). The HUD shows the loss over it.
        /// </summary>
        public event Action<int, int, int> CoinsLost;

        public SpecialRound Kind => SpecialRound.GoldRush;

        /// <summary>The card's picture: a purple truck with the leader's pile of coins on its roof, shrunk so the
        /// pile stays clear of the card's SELECTED tag.</summary>
        public ModeCard Card { get; } = new("truck", 4, GoldRushArt.PileName, propScale: 0.7f);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.GoldRushSettings Settings => settings;

        /// <summary>Every coin of the pool, free ones too: the HUD's pointer and the tests read them.</summary>
        public IReadOnlyList<GoldCoin> Coins => coins;

        /// <summary>The car with the most coins (the one wearing the pile), null while nobody has one.</summary>
        public Car Leader { get; private set; }

        /// <summary>The index of <see cref="Leader"/>, -1 while nobody has a coin.</summary>
        public int LeaderIndex => Leader != null ? round.IndexOf(Leader) : -1;

        /// <summary>Coins dropped by the bursts and the start this round.</summary>
        public int Dropped { get; private set; }

        /// <summary>Coins every car took this round.</summary>
        public int Taken { get; private set; }

        /// <summary>Coins bumps shook out of the cars this round.</summary>
        public int Shaken { get; private set; }

        /// <summary>Coins the cars lost to falls this round.</summary>
        public int FallLost { get; private set; }

        /// <summary>Coins the player took this round.</summary>
        public int PlayerTaken { get; private set; }

        /// <summary>Coins the player lost to bumps and falls this round.</summary>
        public int PlayerLost { get; private set; }

        /// <summary>Coins the player's bumps shook out of the other cars this round.</summary>
        public int PlayerShook { get; private set; }

        /// <summary>The coins the player carries now: its points.</summary>
        public int PlayerCoins => CoinsOf(round.Player);

        public IEnumerable<(string id, string action)> EndEvents
        {
            get {
                if (!IsActive) {
                    yield break;
                }

                yield return ($"gold-rush-coins-{CoinBucket(PlayerCoins)}", "gold-rush-coins");
                yield return ($"gold-rush-lost-{LossBucket(PlayerLost)}", "gold-rush-lost");
                yield return ($"gold-rush-shook-{LossBucket(PlayerShook)}", "gold-rush-shook");
            }
        }

        public void Initialize()
        {
            if (!IsActive) {
                return;
            }

            round.Eliminated += OnEliminated;
        }

        public void Dispose()
        {
            round.Eliminated -= OnEliminated;
            for (var i = 0; i < cars.Count && i < hitHandlers.Length; i++) {
                if (cars[i] != null) {
                    cars[i].Hit -= hitHandlers[i];
                }
            }

            cars.Clear();
            plays.Clear();
            look?.Dispose();
            look = null;
        }

        public void Tick()
        {
            if (!IsActive || !round.IsPlaced) {
                return;
            }

            if (!isBuilt) {
                Build();
            }

            var deltaTime = Time.deltaTime;
            if (round.IsRunning) {
                TrackGround();
                Drop();
                Collect(deltaTime);
                Age(deltaTime);
            }
            else {
                Leave(deltaTime);
            }

            Leader = FindLeader();
            look.Tick(coins, Leader, deltaTime, settings.BlinkTime);
        }

        /// <summary>The coins <paramref name="car"/> carries: its points, 0 outside the mode.</summary>
        public int CoinsOf(Car car)
        {
            var index = round.IndexOf(car);
            return IsActive && round.Score != null && index >= 0 ? round.Score.PointsOf(index) : 0;
        }

        /// <summary>Coins lying on the floor to be taken now.</summary>
        public int LiveCount
        {
            get {
                var count = 0;
                foreach (var coin in coins) {
                    if (coin.IsLive) {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Drops a coin on the floor at <paramref name="point"/> at once, ready to take: for the tests.</summary>
        public GoldCoin PlaceCoin(Vector3 point)
        {
            if (!IsActive || !isBuilt) {
                return null;
            }

            var coin = FreeCoin();
            if (coin == null) {
                return null;
            }

            Land(coin, Flat(point), settings.Lifetime, false);
            return coin;
        }

        /// <summary>Gives <paramref name="car"/> coins at once, as if it had taken them: for the tests and the dev console.</summary>
        public void GiveCoins(Car car, int amount)
        {
            if (IsActive && amount > 0) {
                round.AddPoints(car, amount);
            }
        }

        /// <summary>
        /// A car carrying coins is worth more to a bot that picks a target the usual way; never more than the mode allows
        /// after the player.
        /// </summary>
        public float TargetBonus(Bot bot, Car car, int hunters)
        {
            if (!IsActive) {
                return 0f;
            }

            if (car == round.Player && hunters >= settings.MaxPlayerHunters) {
                return -1000f;
            }

            return Mathf.Min(settings.TargetCap, settings.TargetWeight * CoinsOf(car));
        }

        /// <summary>
        /// A bot goes after a rich car when it is one of the nearest few to it, or drives to its coin; near the edge, off
        /// balance, or with nothing to go for, it plays the usual round (which drives it back in first).
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge arenaEdge, float deltaTime)
        {
            var car = bot.Car;
            // The bots may tick before this mode on the frame the round begins: nothing to go for until it is built.
            if (!IsActive || !isBuilt || !round.IsRunning || car.IsOut) {
                return false;
            }

            if (!plays.TryGetValue(bot, out var play)) {
                play = new BotPlay { ThinkAt = Time.time + (float)random.NextDouble() * settings.BotThink };
                plays[bot] = play;
            }

            if (Time.time >= play.ThinkAt) {
                Think(bot, play);
            }

            if (play.Hunt != null) {
                if (!play.Hunt.IsTargetable) {
                    play.Hunt = null;
                    return false;
                }

                if (bot.Target != play.Hunt) {
                    bot.SetTarget(play.Hunt);
                }

                // The usual chase and push, with its edge return first: robbing a car is bumping it.
                bot.Steer(arenaEdge, deltaTime, round.IsDuel);
                return true;
            }

            var coin = play.Coin;
            if (coin == null || !coin.IsLive || IsLockedFor(coin, car) || car.Movement.IsStaggered) {
                return false;
            }

            var bots = tuning.Bots;
            var position = Flat(car.Position);
            var ahead = position + Flat(car.Movement.Velocity) * (bots.LookAhead * bot.Traits.Caution);
            if (arenaEdge.DistanceToEdge(position, bots.EdgeMargin) < 0f || arenaEdge.DistanceToEdge(ahead, bots.EdgeMargin) < 0f) {
                return false;
            }

            play.PitHold -= deltaTime;
            var heading = PitDetour.Bend(arenaEdge, position, Flat(coin.Position) - position, bots.EdgeMargin,
                bots.PitProbe, bots.PitSamples, bots.PitInward, bots.PitSideHold, ref play.PitSide, ref play.PitHold);
            car.Movement.Drive(heading, settings.BotPaceAt(difficulty.Level) * bot.Pace);
            return true;
        }

        private void Think(Bot bot, BotPlay play)
        {
            play.ThinkAt = Time.time + settings.BotThink * (0.8f + 0.4f * (float)random.NextDouble());
            var car = bot.Car;
            var rich = RichTarget(car);
            var coin = ChooseCoin(bot, car);
            // A coin right by it is still taken first: a few metres there beat a chase across the floor.
            if (rich != null && IsHunter(car, rich)
                && (coin == null || FlatDistance(car.Position, coin.Position) > FlatDistance(car.Position, rich.Position) * 0.5f)) {
                play.Hunt = rich;
                play.Coin = null;
                return;
            }

            play.Hunt = null;
            play.Coin = coin;
            // A bot off to a coin is not after the player any more: its old pick would count against the cap.
            if (bot.Target == round.Player) {
                bot.SetTarget(null);
            }
        }

        /// <summary>The richest car other than <paramref name="car"/>'s own worth robbing: rich enough and well ahead of it.</summary>
        private Car RichTarget(Car car)
        {
            var own = CoinsOf(car);
            Car best = null;
            var most = 0;
            foreach (var other in cars) {
                if (other == car || !other.IsTargetable || car.IsTeammateOf(other)) {
                    continue;
                }

                var count = CoinsOf(other);
                if (count >= settings.HuntFrom && count >= own + settings.HuntLead && count > most) {
                    best = other;
                    most = count;
                }
            }

            return best;
        }

        /// <summary>
        /// Whether <paramref name="car"/> is one of the bots nearest the rich car, as many as the difficulty sends; the
        /// player is spared at the round's start and is never after more than the mode allows.
        /// </summary>
        private bool IsHunter(Car car, Car rich)
        {
            var count = settings.BotHuntersAt(difficulty.Level);
            if (rich == round.Player) {
                if (round.Elapsed < difficulty.PlayerGrace) {
                    return false;
                }

                // Every bot after the player counts, whether this mode sent it or the usual pick did.
                var after = 0;
                foreach (var other in plays.Keys) {
                    if (other.Car != car && !other.Car.IsOut && other.Target == round.Player) {
                        after++;
                    }
                }

                if (after >= settings.MaxPlayerHunters) {
                    return false;
                }

                count = Mathf.Min(count, settings.MaxPlayerHunters);
            }

            if (count <= 0) {
                return false;
            }

            var distance = FlatDistance(car.Position, rich.Position);
            var nearer = 0;
            foreach (var other in cars) {
                if (other == car || other == rich || other == round.Player || other.IsOut) {
                    continue;
                }

                if (FlatDistance(other.Position, rich.Position) < distance) {
                    nearer++;
                }
            }

            return nearer < count;
        }

        /// <summary>
        /// The nearest coin the bot has noticed (one lies there for a reaction time before it is seen) and may take, in
        /// its reach, that no other bot much nearer is already going for.
        /// </summary>
        private GoldCoin ChooseCoin(Bot bot, Car car)
        {
            var reaction = Mathf.Lerp(difficulty.ReactionTime.x, difficulty.ReactionTime.y, (float)random.NextDouble())
                / Mathf.Max(0.1f, bot.Traits.Caution);
            GoldCoin best = null;
            var bestDistance = settings.BotRange;
            foreach (var coin in coins) {
                if (!coin.IsLive || coin.Age < reaction || IsLockedFor(coin, car)) {
                    continue;
                }

                var distance = FlatDistance(car.Position, coin.Position);
                if (distance >= bestDistance || IsYielded(bot, coin, distance)) {
                    continue;
                }

                best = coin;
                bestDistance = distance;
            }

            return best;
        }

        /// <summary>Whether another bot going for <paramref name="coin"/> is enough nearer it to leave it to.</summary>
        private bool IsYielded(Bot bot, GoldCoin coin, float distance)
        {
            foreach (var (other, play) in plays) {
                if (other == bot || play.Coin != coin || other.Car.IsOut) {
                    continue;
                }

                if (FlatDistance(other.Car.Position, coin.Position) * settings.BotYield < distance) {
                    return true;
                }
            }

            return false;
        }

        private void Build()
        {
            isBuilt = true;
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            random = new Random(hazards.Seed ^ 0x601D);
            cars.AddRange(round.Cars);
            hitHandlers = new Action<CarHit>[cars.Count];
            grounds = new Vector3[cars.Count];
            shakenUntil = new float[cars.Count];
            lastTake = new float[cars.Count];
            runs = new int[cars.Count];
            for (var i = 0; i < cars.Count; i++) {
                var index = i;
                hitHandlers[i] = hit => OnHit(index, hit);
                cars[i].Hit += hitHandlers[i];
                grounds[i] = Flat(cars[i].Position);
                lastTake[i] = -10f;
            }

            for (var i = 0; i < PoolSize; i++) {
                coins.Add(new GoldCoin());
            }

            FindSpots();
            look = new GoldRushArt(art, parent);
            look.Build(coins);
            nextBurst = settings.FirstDelay + settings.BurstInterval;
        }

        /// <summary>Every spot of a grid over the floor a coin may land on: on the floor, the margin off any edge or pit.</summary>
        private void FindSpots()
        {
            var extent = edge.FullExtent;
            for (var x = -extent; x <= extent; x += SpotStep) {
                for (var z = -extent; z <= extent; z += SpotStep) {
                    var point = new Vector3(x, 0f, z);
                    if (IsSafe(point)) {
                        spots.Add(point);
                    }
                }
            }
        }

        private void TrackGround()
        {
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (!car.IsOut && car.Movement.IsGrounded) {
                    grounds[i] = Flat(car.Position);
                }
            }
        }

        /// <summary>The start's coins once, then a burst whenever its time comes and the floor has room.</summary>
        private void Drop()
        {
            var elapsed = round.Elapsed;
            if (!hasOpened && elapsed >= settings.FirstDelay) {
                hasOpened = true;
                Open();
            }

            if (!hasOpened || elapsed < nextBurst) {
                return;
            }

            var jitter = settings.BurstJitter;
            nextBurst = elapsed + settings.BurstInterval + Range(-jitter, jitter);
            var room = settings.MaxCoins - BurstCoins();
            if (room <= 0 || spots.Count == 0) {
                return;
            }

            var size = settings.BurstSize;
            var count = Mathf.Min(room, random.Next(size.x, size.y + 1));
            DropAround(BurstMiddle(), count, settings.BurstSpread);
        }

        /// <summary>A coin in front of every car, toward the middle, and a heap in the middle: something to drive to at once.</summary>
        private void Open()
        {
            foreach (var car in cars) {
                var position = Flat(car.Position);
                var inward = edge.Inward(position);
                inward.y = 0f;
                if (inward.sqrMagnitude < 0.0001f) {
                    inward = -position;
                }

                var point = position + inward.normalized * settings.OpeningAhead;
                DropAt(IsSafe(point) ? point : NearestSpot(point));
            }

            if (settings.OpeningMiddle > 0 && spots.Count > 0) {
                DropAround(NearestSpot(Vector3.zero), settings.OpeningMiddle, settings.BurstSpread * 0.7f);
            }
        }

        /// <summary>
        /// Where a burst lands: on Easy often near the player (the lean), else anywhere on the floor; never right on a car.
        /// </summary>
        private Vector3 BurstMiddle()
        {
            var leans = random.NextDouble() < settings.PlayerLeanAt(difficulty.Level) && !round.Player.IsOut;
            var player = Flat(round.Player.Position);
            var best = spots[random.Next(spots.Count)];
            var bestScore = float.NegativeInfinity;
            for (var i = 0; i < 12; i++) {
                var spot = spots[random.Next(spots.Count)];
                var clearance = Clearance(spot);
                var score = Mathf.Min(clearance, settings.CarClearance);
                if (leans) {
                    var distance = FlatDistance(spot, player);
                    var range = settings.LeanRange;
                    if (distance < range.x || distance > range.y) {
                        score -= 10f;
                    }
                }

                if (score > bestScore) {
                    best = spot;
                    bestScore = score;
                }
            }

            return best;
        }

        private void DropAround(Vector3 middle, int count, float spread)
        {
            for (var i = 0; i < count; i++) {
                var placed = false;
                for (var t = 0; t < SpotTries && !placed; t++) {
                    var angle = (float)(random.NextDouble() * Math.PI * 2.0);
                    var distance = i == 0 && t == 0 ? 0f : spread * Mathf.Sqrt((float)random.NextDouble());
                    var point = middle + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    if (IsSafe(point) && IsClearOfCoins(point, settings.CoinSpacing)) {
                        placed = DropAt(point);
                    }
                }

                if (!placed && IsSafe(middle)) {
                    DropAt(middle);
                }
            }
        }

        /// <summary>A coin falls from the sky onto <paramref name="point"/>; false when the pool is full.</summary>
        private bool DropAt(Vector3 point)
        {
            var coin = FreeCoin();
            if (coin == null) {
                return false;
            }

            Dropped++;
            Fly(coin, point + Vector3.up * DropHeight, Flat(point), DropTime, 0f, settings.Lifetime, false);
            return true;
        }

        /// <summary>
        /// Takes up to <paramref name="amount"/> of a car's coins (its points) and throws as many as the pool has room for
        /// round <paramref name="origin"/>, most of them toward <paramref name="toward"/> (zero for all round); the car
        /// cannot take them back for a moment. The number of points taken.
        /// </summary>
        private int Spill(int index, int amount, Vector3 origin, Vector3 toward, int throwMax)
        {
            var car = cars[index];
            var points = CoinsOf(car);
            var take = Mathf.Min(amount, points);
            if (take <= 0 || !round.AddPoints(car, -take)) {
                return 0;
            }

            var thrown = Mathf.Min(take, throwMax);
            var lockUntil = Time.time + settings.SpillTime + settings.ShakeCooldown;
            var range = settings.SpillDistance;
            var hasToward = toward.sqrMagnitude > 0.0001f;
            var baseAngle = hasToward ? Mathf.Atan2(toward.z, toward.x) : 0f;
            for (var i = 0; i < thrown; i++) {
                var coin = FreeCoin();
                if (coin == null) {
                    break;
                }

                var angle = hasToward && i % 3 != 2
                    ? baseAngle + Range(-1.6f, 1.6f)
                    : (float)(random.NextDouble() * Math.PI * 2.0);
                var target = origin;
                var found = false;
                for (var t = 0; t < SpotTries && !found; t++) {
                    var distance = Range(range.x, range.y);
                    target = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                    found = IsSafe(target);
                    angle += 0.8f;
                }

                if (!found) {
                    target = NearestSpot(target);
                }

                Fly(coin, origin + Vector3.up * 0.6f, Flat(target), settings.SpillTime, settings.SpillHeight,
                    settings.SpillLifetime, true);
                coin.Locked = car;
                coin.LockedUntil = lockUntil;
            }

            return take;
        }

        private void OnHit(int index, CarHit hit)
        {
            if (!round.IsRunning || hit.IsHitter || hit.Strength < settings.ShakeStrength) {
                return;
            }

            var car = cars[index];
            var other = hit.Other;
            if (other == null || other.IsOut || car.IsOut || car.IsTeammateOf(other) || Time.time < shakenUntil[index]) {
                return;
            }

            var points = CoinsOf(car);
            if (points <= 0) {
                return;
            }

            var amount = points * settings.ShakeShareAt(hit.Strength, tuning.Car.MaxSpeed);
            if (other.IsPlayer) {
                amount *= settings.FromPlayer;
            }

            if (car.IsPlayer) {
                amount *= settings.PlayerShakeAt(difficulty.Level);
            }

            var wanted = Mathf.Clamp(Mathf.RoundToInt(amount), 1, settings.ShakeMax);
            // Most of them fly toward the car that bumped: the robber is right there to scoop them up.
            var toward = Flat(other.Position - car.Position);
            var lost = Spill(index, wanted, Flat(car.Position), toward, wanted);
            if (lost <= 0) {
                return;
            }

            shakenUntil[index] = Time.time + settings.ShakeCooldown;
            Shaken += lost;
            if (car.IsPlayer) {
                PlayerLost += lost;
            }

            if (other.IsPlayer) {
                PlayerShook += lost;
            }

            var isPlayers = car.IsPlayer || other.IsPlayer;
            audioService?.Play(audioService.Library.KnockoutCredit, isPlayers ? 0.85f : 0.3f, 0.95f);
            if (isPlayers) {
                effects?.PlayKnockoutCredit(car.Position);
                arenaCamera?.Shake(0.35f);
            }

            CoinsLost?.Invoke(index, lost, round.IndexOf(other));
        }

        private void OnEliminated(int index, int by)
        {
            if (!isBuilt || index < 0 || index >= cars.Count) {
                return;
            }

            var car = cars[index];
            var points = CoinsOf(car);
            if (points <= 0) {
                return;
            }

            var share = settings.FallShare * (car.IsPlayer ? settings.PlayerFallAt(difficulty.Level) : 1f);
            var wanted = Mathf.Max(1, Mathf.RoundToInt(points * share));
            // Spilled a little in from where the car went off: there for the car that pushed it to scoop up.
            var origin = grounds[index];
            var inward = edge.Inward(origin);
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.0001f) {
                origin += inward.normalized * 2f;
            }

            if (!IsSafe(origin)) {
                origin = NearestSpot(origin);
            }

            var lost = Spill(index, wanted, origin, Vector3.zero, settings.FallSpillMax);
            if (lost <= 0) {
                return;
            }

            FallLost += lost;
            if (car.IsPlayer) {
                PlayerLost += lost;
            }

            CoinsLost?.Invoke(index, lost, RoundResult.Nobody);
        }

        /// <summary>
        /// Every coin lying there is taken by the nearest car in reach; the player's car pulls the near ones in first.
        /// </summary>
        private void Collect(float deltaTime)
        {
            var player = round.Player;
            var now = Time.time;
            foreach (var coin in coins) {
                if (!coin.IsLive) {
                    continue;
                }

                if (!player.IsOut && player.Position.y < TakeHeight && !IsLockedFor(coin, player)) {
                    var toPlayer = Flat(player.Position) - coin.Position;
                    var distance = toPlayer.magnitude;
                    if (distance < settings.PlayerMagnet && distance > 0.01f) {
                        // Faster the nearer it gets, so a coin by the car snaps in.
                        var pull = settings.MagnetSpeed * deltaTime * (1f + (settings.PlayerMagnet - distance));
                        coin.Position += toPlayer * Mathf.Min(1f, pull / distance);
                    }
                }

                Car taker = null;
                var nearest = float.PositiveInfinity;
                for (var i = 0; i < cars.Count; i++) {
                    var car = cars[i];
                    if (car.IsOut || car.Position.y > TakeHeight || (coin.Locked == car && now < coin.LockedUntil)) {
                        continue;
                    }

                    var distance = FlatDistance(car.Position, coin.Position);
                    if (distance < settings.PickRadius * car.Growth && distance < nearest) {
                        taker = car;
                        nearest = distance;
                    }
                }

                if (taker != null) {
                    Take(coin, taker);
                }
            }
        }

        private void Take(GoldCoin coin, Car car)
        {
            var index = round.IndexOf(car);
            if (index < 0 || !round.AddPoints(car, 1)) {
                return;
            }

            coin.State = GoldCoin.Stage.Leaving;
            coin.WasTaken = true;
            coin.TakenBy = car;
            coin.StageTime = 0f;
            Taken++;
            var now = Time.unscaledTime;
            runs[index] = now - lastTake[index] <= settings.RunWindow ? runs[index] + 1 : 0;
            lastTake[index] = now;
            if (car.IsPlayer) {
                PlayerTaken++;
            }

            // A ring that climbs with every coin of a quick run; loud for the player, soft for the bots.
            var pitch = Mathf.Min(1.85f, 1.35f + runs[index] * 0.07f);
            audioService?.Play(audioService.Library.KnockoutCredit, car.IsPlayer ? 0.55f : 0.12f, pitch);
            effects?.PlayPickup(coin.Position);
            CoinTaken?.Invoke(index, coin.Position);
        }

        /// <summary>Flights land, lying coins age and fade, leaving ones finish.</summary>
        private void Age(float deltaTime)
        {
            foreach (var coin in coins) {
                switch (coin.State) {
                    case GoldCoin.Stage.Flying:
                        coin.StageTime += deltaTime;
                        if (coin.StageTime >= coin.FlightTime) {
                            coin.State = GoldCoin.Stage.Live;
                            coin.Age = 0f;
                        }

                        break;
                    case GoldCoin.Stage.Live:
                        coin.Age += deltaTime;
                        if (coin.Age >= coin.Lifetime) {
                            coin.State = GoldCoin.Stage.Leaving;
                            coin.WasTaken = false;
                            coin.StageTime = 0f;
                            effects?.PlayPickupFade(coin.Position);
                        }

                        break;
                    case GoldCoin.Stage.Leaving:
                        coin.StageTime += deltaTime;
                        if (coin.StageTime >= 0.3f) {
                            Free(coin);
                        }

                        break;
                }
            }
        }

        /// <summary>After the round only the coins already leaving finish; the rest stay where they are on the result.</summary>
        private void Leave(float deltaTime)
        {
            foreach (var coin in coins) {
                if (coin.State == GoldCoin.Stage.Leaving) {
                    coin.StageTime += deltaTime;
                    if (coin.StageTime >= 0.3f) {
                        Free(coin);
                    }
                }
                else if (coin.State == GoldCoin.Stage.Flying) {
                    coin.StageTime = Mathf.Min(coin.FlightTime, coin.StageTime + deltaTime);
                }
            }
        }

        private Car FindLeader()
        {
            if (round.Score == null || cars.Count == 0) {
                return null;
            }

            var index = round.Score.IndexAt(0);
            return index >= 0 && index < cars.Count && round.Score.PointsOf(index) > 0 ? cars[index] : null;
        }

        private void Fly(GoldCoin coin, Vector3 from, Vector3 to, float time, float height, float lifetime, bool isSpilled)
        {
            coin.State = GoldCoin.Stage.Flying;
            coin.From = from;
            coin.Position = to;
            coin.FlightTime = time;
            coin.FlightHeight = height;
            coin.StageTime = 0f;
            coin.Age = 0f;
            coin.Lifetime = lifetime;
            coin.IsSpilled = isSpilled;
            coin.Locked = null;
            coin.TakenBy = null;
            coin.WasTaken = false;
            coin.Phase = (phase++ * PhaseStep) % 1f;
        }

        private void Land(GoldCoin coin, Vector3 point, float lifetime, bool isSpilled)
        {
            Fly(coin, point, point, 0f, 0f, lifetime, isSpilled);
            coin.State = GoldCoin.Stage.Live;
        }

        private static void Free(GoldCoin coin)
        {
            coin.State = GoldCoin.Stage.Free;
            coin.TakenBy = null;
            coin.Locked = null;
        }

        private GoldCoin FreeCoin()
        {
            foreach (var coin in coins) {
                if (coin.State == GoldCoin.Stage.Free) {
                    return coin;
                }
            }

            return null;
        }

        /// <summary>Coins of the bursts and the start on the floor or on their way down: the cap counts these only.</summary>
        private int BurstCoins()
        {
            var count = 0;
            foreach (var coin in coins) {
                if (!coin.IsSpilled && (coin.State == GoldCoin.Stage.Live || coin.State == GoldCoin.Stage.Flying)) {
                    count++;
                }
            }

            return count;
        }

        private bool IsSafe(Vector3 point) => edge.DistanceToEdge(Flat(point), settings.EdgeMargin) >= 0f;

        private bool IsClearOfCoins(Vector3 point, float spacing)
        {
            foreach (var coin in coins) {
                if ((coin.IsLive || coin.IsFlying) && FlatDistance(coin.Position, point) < spacing) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Metres from <paramref name="point"/> to the nearest car in play.</summary>
        private float Clearance(Vector3 point)
        {
            var clearance = float.PositiveInfinity;
            foreach (var car in cars) {
                if (!car.IsOut) {
                    clearance = Mathf.Min(clearance, FlatDistance(car.Position, point));
                }
            }

            return clearance;
        }

        private Vector3 NearestSpot(Vector3 point)
        {
            var best = Flat(point);
            var bestDistance = float.PositiveInfinity;
            foreach (var spot in spots) {
                var distance = (spot - Flat(point)).sqrMagnitude;
                if (distance < bestDistance) {
                    best = spot;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static bool IsLockedFor(GoldCoin coin, Car car) => coin.Locked == car && Time.time < coin.LockedUntil;

        private float Range(float min, float max) => min + (max - min) * (float)random.NextDouble();

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            var x = a.x - b.x;
            var z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        // By the lower end of each range, so an event's name carries one number: none, 1-4, 5-9, 10-14, 15-19, 20-29, 30+.
        private static string CoinBucket(int count) => count switch {
            <= 0 => "none",
            < 5 => "1",
            < 10 => "5",
            < 15 => "10",
            < 20 => "15",
            < 30 => "20",
            _ => "30plus",
        };

        // None, 1-4, 5-9, 10-19, 20+.
        private static string LossBucket(int count) => count switch {
            <= 0 => "none",
            < 5 => "1",
            < 10 => "5",
            < 20 => "10",
            _ => "20plus",
        };
    }
}
