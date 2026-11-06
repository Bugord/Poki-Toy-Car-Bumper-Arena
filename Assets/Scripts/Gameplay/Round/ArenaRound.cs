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
    /// <para>
    /// In a build that plays long matches (<see cref="GameTuning.IsMatch"/>) the same round runs as a match: it ends only
    /// when <see cref="GameTuning.MatchSettings.Duration"/> runs out, and a car that falls comes back
    /// <see cref="GameTuning.MatchSettings.RespawnDelay"/> later (<see cref="Respawned"/>), dropped onto the safe floor
    /// away from the others (<see cref="RespawnSpot"/>) and safe for a moment after it lands
    /// (<see cref="Car.IsInvulnerable"/>). Knock-outs score under the same credit rules (<see cref="Score"/>), and the
    /// player's rival lives within the match: the last car to knock the player off is marked until the player knocks
    /// it off (<see cref="NemesisChanged"/>). Still one match per scene.
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

        /// <summary>One complete elimination, after knockout credit is known; Nobody means a fall without credit.</summary>
        public event Action<int, int> Eliminated;

        /// <summary>
        /// Raised once, when the round is decided.
        /// </summary>
        public event Action<RoundResult> Finished;

        /// <summary>
        /// Raised in a match when a car that fell comes back onto the floor, safe for a moment.
        /// </summary>
        public event Action<Car> Respawned;

        /// <summary>
        /// Raised in a match when the player's marked rival changes: a car knocked the player off, or the player knocked
        /// the rival off. <see cref="Nemesis"/> is the new one, null for none.
        /// </summary>
        public event Action NemesisChanged;

        private readonly GameTuning tuning;
        private readonly IObjectResolver resolver;
        private readonly Car carPrefab;
        private readonly Transform carParent;
        private readonly IArenaEdge edge;
        private readonly GameTuning.ArenaSettings arena;
        private readonly RoundDifficulty difficulty;
        private readonly ISessionRoster roster;
        private readonly CarCatalog catalog;
        private readonly IPlayerCar playerCar;
        private readonly IPlayerTopper playerTopper;
        private readonly RoundRules rules;
        private readonly int nemesisIndex;
        private readonly bool isMatch;
        private readonly List<Car> cars = new();

        // A match's cars waiting to come back (Time.time, positive infinity for none), and the safe time of those that
        // came back: when they came back and when it ends (positive infinity until the car lands).
        private float[] respawnTimes;
        private float[] respawnedTimes;
        private float[] safeUntil;

        private float elapsed;
        private float lastPlayerKnockoutTime = float.NegativeInfinity;
        private FallCause playerFallCause;
        private IReadOnlyList<SessionPlayer> players;
        private RoundStandings standings;

        public IReadOnlyList<string> Names { get; private set; }

        /// <summary>The levels shown by the names, by index (0 the player's), fixed when the cars were placed.</summary>
        public IReadOnlyList<int> Levels { get; private set; }
        public IReadOnlyList<int> Colors { get; private set; }

        public ArenaRound(GameTuning tuning, IObjectResolver resolver, Car carPrefab, Transform carParent, IArenaEdge edge,
            GameTuning.ArenaSettings arena, RoundDifficulty difficulty, ISessionProgress progress, ISessionRoster roster,
            CarCatalog catalog, IPlayerCar playerCar, IPlayerTopper playerTopper, RoundRules rules)
        {
            this.rules = rules;
            this.tuning = tuning;
            this.resolver = resolver;
            this.carPrefab = carPrefab;
            this.carParent = carParent;
            this.edge = edge;
            this.arena = arena;
            this.difficulty = difficulty;
            this.roster = roster;
            this.catalog = catalog;
            this.playerCar = playerCar;
            this.playerTopper = playerTopper;
            this.isMatch = tuning.IsMatch;
            // A match's rival lives within the match: it starts with none.
            this.nemesisIndex = isMatch ? RoundResult.Nobody : progress.Nemesis;
        }

        public IReadOnlyList<Car> Cars => cars;

        /// <summary>
        /// The player's car, null before the cars are placed.
        /// </summary>
        public Car Player { get; private set; }

        /// <summary>
        /// This round's marked rival, null if its slot is absent. Kept after a knockout so the HUD can name it.
        /// </summary>
        public Car Nemesis { get; private set; }

        public bool RevengeTaken { get; private set; }

        /// <summary>A boss round's boss, null in any other round or if its slot is absent.</summary>
        public Car Boss { get; private set; }

        /// <summary>The player knocked the boss off this round.</summary>
        public bool BossTaken { get; private set; }

        /// <summary>
        /// Whether the round had a marked rival at any time: from the start in a round, once a car knocked the player
        /// off in a match.
        /// </summary>
        public bool HadNemesis { get; private set; }

        /// <summary>
        /// Whether this round is a long match with respawns (<see cref="GameTuning.IsMatch"/>), fixed as the scene is
        /// built.
        /// </summary>
        public bool IsMatch => isMatch;

        /// <summary>
        /// The match's score by car index, null in a last-car-standing round or before the cars are placed.
        /// </summary>
        public MatchScore Score { get; private set; }

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

        /// <summary>
        /// Whether the bots drive: while the round runs, and on after the player is out, so the arena behind the result
        /// screen goes on like a match of players rather than freezing. Nothing they do then counts: the result is out.
        /// </summary>
        public bool IsBotsPlaying => IsRunning || (!isMatch && IsFinished && Player != null && Player.IsOut);

        public float TimeLeft => Mathf.Max(0f, tuning.PlayDuration - elapsed);

        /// <summary>
        /// Seconds since the round began.
        /// </summary>
        public float Elapsed => elapsed;

        public int CarsLeft { get; private set; }

        /// <summary>
        /// Only the player and one bot remain: the final fight can be gentler without changing the earlier crowd.
        /// </summary>
        public bool IsDuel => !isMatch && Player != null && !Player.IsOut && CarsLeft == 2;

        /// <summary>
        /// Cars the player has knocked out this round.
        /// </summary>
        public int PlayerKnockouts { get; private set; }

        /// <summary>
        /// The player's knock-outs in a row, each within <see cref="GameTuning.RoundSettings.ComboWindow"/> of the one
        /// before: 1 for a lone one, 2 for a DOUBLE!, and on. Set before <see cref="KnockedOut"/> tells of the newest.
        /// </summary>
        public int PlayerCombo { get; private set; }

        /// <summary>
        /// The car that knocked the player out, null while the player is on the arena or after a fall with no one to
        /// credit.
        /// </summary>
        public Car PlayerKnockedOutBy { get; private set; }

        /// <summary>
        /// The arena's edge as it is now. The one place the bots and the player's help read the edge from: it moves in as
        /// the floor crumbles, and a wave of tiles counts as gone as soon as it starts to shake, so the bots get off it.
        /// </summary>
        public IArenaEdge Edge => edge;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public PlayerFallTelemetry FallTelemetry { get; private set; }
#endif

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
            players = roster.ForRound(difficulty.BotCount);
            var names = new string[1 + difficulty.BotCount];
            var colors = new int[names.Length];
            var levels = new int[names.Length];
            names[0] = "YOU";
            colors[0] = roster.ColorOf(0);
            levels[0] = roster.LevelOf(0);
            for (var index = 1; index < names.Length; index++) {
                names[index] = players[index - 1].Name;
                colors[index] = players[index - 1].ColorId;
                levels[index] = players[index - 1].Level;
            }
            Names = Array.AsReadOnly(names);
            Levels = Array.AsReadOnly(levels);
            Colors = Array.AsReadOnly(colors);
            standings = new RoundStandings(names.Length);
            Spawn(1 + difficulty.BotCount);
            Player = cars[0];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FallTelemetry = new PlayerFallTelemetry(this, edge, Player);
            Player.Movement.FallTelemetry = FallTelemetry;
#endif
            Nemesis = nemesisIndex > 0 && nemesisIndex < cars.Count ? cars[nemesisIndex] : null;
            Boss = rules.BossIndex > 0 && rules.BossIndex < cars.Count ? cars[rules.BossIndex] : null;
            HadNemesis = Nemesis != null;
            CarsLeft = cars.Count;
            if (isMatch) {
                Score = new MatchScore(cars.Count);
                respawnTimes = new float[cars.Count];
                respawnedTimes = new float[cars.Count];
                safeUntil = new float[cars.Count];
                for (var i = 0; i < cars.Count; i++) {
                    respawnTimes[i] = float.PositiveInfinity;
                    respawnedTimes[i] = float.NegativeInfinity;
                    safeUntil[i] = float.NegativeInfinity;
                }
            }
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
                if (IsBotsPlaying) {
                    TakeOutFallen();
                }

                return;
            }

            elapsed += Time.deltaTime;
            if (isMatch) {
                TickComebacks();
            }

            foreach (var car in cars) {
                if (!car.IsOut && car.Position.y < tuning.Round.FallHeight) {
                    var knockedOutBy = FindKnocker(car);
                    if (car == Player && knockedOutBy == null) {
                        playerFallCause = FindFallCause(car);
                    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (car == Player) {
                        FallTelemetry.Complete(playerFallCause, knockedOutBy);
                    }
#endif

                    car.TakeOut();
                    CarsLeft--;
                    var index = IndexOf(car);
                    var by = knockedOutBy != null ? IndexOf(knockedOutBy) : RoundResult.Nobody;
                    if (isMatch) {
                        // A car that misses the floor as it drops back is no longer safe while it falls again.
                        car.SetInvulnerable(false, cars);
                        Score.RecordFall(index, by, elapsed);
                        respawnTimes[index] = Time.time + tuning.Match.RespawnDelay;
                    }
                    else {
                        standings.RecordOut(index, by);
                    }

                    var rival = Nemesis;
                    CarOut?.Invoke(car);

                    if (knockedOutBy != null) {
                        if (knockedOutBy == Player) {
                            PlayerKnockouts++;
                            PlayerCombo = Time.time - lastPlayerKnockoutTime <= tuning.Round.ComboWindow
                                ? PlayerCombo + 1 : 1;
                            lastPlayerKnockoutTime = Time.time;
                            if (car == Nemesis) {
                                RevengeTaken = true;
                            }

                            if (car == Boss) {
                                BossTaken = true;
                            }
                        }

                        if (car == Player) {
                            PlayerKnockedOutBy = knockedOutBy;
                        }

                        KnockedOut?.Invoke(car, knockedOutBy);
                    }
                    Eliminated?.Invoke(index, by);
                    if (isMatch) {
                        // After the events, so the KNOCKOUT! sticker and the feed still see the rival it was.
                        UpdateRival(car, knockedOutBy, rival);
                    }
                }
            }

            if (isMatch) {
                if (elapsed >= tuning.Match.Duration) {
                    FinishMatch();
                }

                return;
            }

            standings.CompleteFrame();

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
        /// The car's identity in the round: 0 for the player, 1..N for the bots; -1 for a car not in the round.
        /// </summary>
        public int IndexOf(Car car)
        {
            return car != null ? cars.IndexOf(car) : -1;
        }

        /// <summary>The played occupant, even after the visit's roster has moved on.</summary>
        public string NameOf(int index) => Names[index];
        public int LevelOf(int index) => Levels[index];

        /// <summary>The win streak a bot of this round joined on (a look, crowned by its name), 0 for none.</summary>
        public int StreakOf(int index) => index == 0 || players == null ? 0 : players[index - 1].Streak;
        public int ColorOf(int index) => Colors[index];

        /// <summary>
        /// After the round, a bot that falls is only out of the bots' play: no standings, knockouts or events.
        /// </summary>
        private void TakeOutFallen()
        {
            foreach (var car in cars) {
                if (!car.IsOut && car.Position.y < tuning.Round.FallHeight) {
                    car.TakeOut();
                }
            }
        }

        /// <summary>
        /// Seconds until a match's fallen car comes back; 0 for a car in play, after the match, and in a round.
        /// </summary>
        public float RespawnTimeLeft(Car car)
        {
            var index = IndexOf(car);
            if (!isMatch || index < 0 || !car.IsOut || !IsRunning) {
                return 0f;
            }

            return Mathf.Max(0f, respawnTimes[index] - Time.time);
        }

        /// <summary>
        /// How much of a car's safe time after coming back is left, from 1 (still dropping, or just landed) to 0 once
        /// it is over.
        /// </summary>
        public float SafeShareLeft(Car car)
        {
            var index = IndexOf(car);
            if (!isMatch || index < 0 || !car.IsInvulnerable) {
                return 0f;
            }

            var until = safeUntil[index];
            if (float.IsPositiveInfinity(until)) {
                return 1f;
            }

            // Past its time while still in another car it reads as nearly over, not over.
            var length = tuning.Match.Invulnerability;
            return length > 0f ? Mathf.Clamp((until - Time.time) / length, 0.02f, 1f) : 0.02f;
        }

        /// <summary>
        /// The match's rival after a fall: a car that knocks the player off becomes it, and the player knocking it off
        /// (the revenge, already counted) leaves none until a car knocks the player off again.
        /// </summary>
        private void UpdateRival(Car car, Car knockedOutBy, Car rival)
        {
            if (knockedOutBy == null) {
                return;
            }

            if (car == Player && knockedOutBy != Player) {
                Nemesis = knockedOutBy;
                HadNemesis = true;
            }
            else if (knockedOutBy == Player && car == rival) {
                Nemesis = null;
            }

            if (Nemesis != rival) {
                NemesisChanged?.Invoke();
            }
        }

        /// <summary>
        /// Brings back the match's fallen cars whose time has come, and ends the safe time of those that came back once
        /// they have been on the floor long enough and are clear of every other car (the physics would throw the two
        /// apart at once otherwise).
        /// </summary>
        private void TickComebacks()
        {
            var now = Time.time;
            var settings = tuning.Match;
            for (var index = 0; index < cars.Count; index++) {
                var car = cars[index];
                if (car.IsOut) {
                    if (now >= respawnTimes[index]) {
                        Respawn(index);
                    }

                    continue;
                }

                if (!car.IsInvulnerable) {
                    continue;
                }

                if (float.IsPositiveInfinity(safeUntil[index])
                    && (car.Movement.IsGrounded || now - respawnedTimes[index] >= settings.MaxDropTime)) {
                    safeUntil[index] = now + settings.Invulnerability;
                }

                if (now >= safeUntil[index] && !car.OverlapsCar(cars)) {
                    car.SetInvulnerable(false, cars);
                }
            }
        }

        /// <summary>
        /// Drops a fallen car back onto the floor as it is now, away from the others, facing the middle.
        /// </summary>
        private void Respawn(int index)
        {
            var car = cars[index];
            var spot = RespawnSpot.Find(edge, tuning.Match, cars, car);
            var facing = spot.sqrMagnitude > 0.01f ? -spot.normalized : Vector3.forward;
            car.Respawn(spot + Vector3.up * tuning.Match.DropHeight, Quaternion.LookRotation(facing), cars);
            respawnTimes[index] = float.PositiveInfinity;
            respawnedTimes[index] = Time.time;
            safeUntil[index] = float.PositiveInfinity;
            CarsLeft++;
            Respawned?.Invoke(car);
        }

        /// <summary>
        /// The match is decided by its score once time runs out: first place wins, every other place ran out the clock.
        /// The player is never out at the end of a match, so nobody knocked the player out.
        /// </summary>
        private void FinishMatch()
        {
            IsRunning = false;
            IsFinished = true;
            var place = Score.PlaceOf(0);
            Finished?.Invoke(new RoundResult(place == 1 ? RoundOutcome.Won : RoundOutcome.Survived, place, cars.Count,
                elapsed, PlayerKnockouts, RoundResult.Nobody, FallCause.None, HadNemesis, RevengeTaken, Score.Snapshot(),
                Colors, true, Score.FallsOf(0), Score.KnockedOffOf(0), place > tuning.Match.TopPlaces));
        }

        private void Finish(RoundOutcome outcome, int place)
        {
            IsRunning = false;
            IsFinished = true;
            var knockedOutBy = PlayerKnockedOutBy != null ? IndexOf(PlayerKnockedOutBy) : RoundResult.Nobody;
            Finished?.Invoke(new RoundResult(outcome, place, cars.Count, elapsed, PlayerKnockouts, knockedOutBy,
                playerFallCause, HadNemesis, RevengeTaken, standings.Snapshot(), Colors, bonusKnockouts:
                BossTaken ? tuning.SpecialRounds.BossReward - 1 : 0));
        }

        /// <summary>
        /// Explains a fall without knockout credit from the last ground contact, so the time spent falling below
        /// FallHeight cannot age a boost or ring fall into Drive. Knockout credit still uses its existing clock.
        /// A ring giving way wins over a boost, which wins over sliding after an old hit.
        /// </summary>
        private FallCause FindFallCause(Car car)
        {
            var now = float.IsNegativeInfinity(car.Movement.LastGroundedTime) ? Time.time : car.Movement.LastGroundedTime;
            if (now - car.LastRingFallTime <= tuning.Round.RingFallWindow) {
                return FallCause.Ring;
            }

            if (now - car.Movement.LastBoostTime <= tuning.Round.BoostFallWindow) {
                return FallCause.Boost;
            }

            // A ball hit clears the hitter, so no car's push after it is blamed on the ball.
            if (car.LastHitBy == null && now - car.LastHazardTime <= tuning.Round.SlideWindow) {
                return FallCause.Ball;
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

            var window = (hitBy.IsPlayer ? difficulty.PlayerKnockoutWindow : tuning.Round.KnockoutCreditWindow)
                * rules.FallTimeScale;
            return Time.time - car.LastHitTime <= window ? hitBy : null;
        }

        /// <summary>
        /// Spreads the cars evenly round the spawn circle (or the contour of a floor that is not round), the first one nearest
        /// the camera, all facing the middle.
        /// </summary>
        private void Spawn(int count)
        {
            var radius = arena.Radius * arena.SpawnRadius;
            var height = arena.SpawnHeight;
            for (var i = 0; i < count; i++) {
                Vector3 position;
                Vector3 facing;
                if (edge.TryGetSpawn(i, count, out var spot)) {
                    // A floor that is not round has a contour of its own, clear of its pits.
                    position = spot + Vector3.up * height;
                    facing = spot.sqrMagnitude > 0.0001f ? -spot.normalized : Vector3.forward;
                }
                else {
                    var angle = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / count;
                    var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    // At the very middle (a radius of 0) a car faces away from the camera.
                    facing = radius > 0f ? -outward : Vector3.forward;
                    position = outward * radius + Vector3.up * height;
                }

                var rotation = Quaternion.LookRotation(facing);
                var car = resolver.InstantiateInjected(carPrefab, position, rotation, carParent);
                // The interpolated Rigidbody wakes up where the prefab was made, at the origin, and puts the transform
                // back there; placing the body itself keeps the cars apart.
                car.Movement.Place(position, rotation);
                car.name = NameOf(i);
                car.IsPlayer = i == 0;
                var entry = i == 0 ? catalog.Find(playerCar.SelectedCar) : players[i - 1].Car;
                var topper = i == 0 ? playerTopper.SelectedTopper : players[i - 1].Topper;
                if (i == rules.BossIndex) {
                    // The boss keeps its player's name and colour, in a monster truck with a crown.
                    var special = tuning.SpecialRounds;
                    entry = catalog.Find(special.BossCar) ?? entry;
                    topper = special.BossTopper;
                }

                car.GetComponent<CarLook>().Apply(i, entry, ColorOf(i), topper);
                if (i == 0) {
                    // The player's car drives by its stats; a bot's car is only its look.
                    var statSettings = tuning.CarStats;
                    var stats = statSettings.Of(entry.Id);
                    car.Movement.SetStats(statSettings.SpeedScale(stats), statSettings.CooldownScale(stats),
                        statSettings.WeightScale(stats));
                    car.PowerScale = statSettings.PowerScale(stats);
                }

                cars.Add(car);
            }
        }
    }
}
