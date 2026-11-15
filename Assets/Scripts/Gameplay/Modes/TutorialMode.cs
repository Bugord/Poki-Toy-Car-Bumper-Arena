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
    /// <summary>The tutorial's steps, in order (<see cref="TutorialMode"/>).</summary>
    public enum TutorialStep
    {
        /// <summary>Not begun: the cars are not placed yet.</summary>
        None,

        /// <summary>Push the one bot on the floor off the edge (the sticker says how to drive).</summary>
        Push,

        /// <summary>Take the box lying in the middle.</summary>
        Box,

        /// <summary>Use its Mega: the car grows, with no end until the lesson is over.</summary>
        Use,

        /// <summary>Two bots drop in: push them off, grown.</summary>
        Smash,

        /// <summary>Every bot is off: the round ends as a win.</summary>
        Done,
    }

    /// <summary>The praise between two steps of the tutorial (<see cref="TutorialMode.Praise"/>).</summary>
    public enum TutorialPraise
    {
        None,

        /// <summary>The first bot is off.</summary>
        Pushed,

        /// <summary>The Mega is used: the car is giant.</summary>
        Grown,
    }

    /// <summary>
    /// The new player's first round as a tutorial (<see cref="GameTuning.TutorialSettings"/>): steps the player through
    /// pushing a bot off and using an item, on a still floor with bots that never attack. It is a mode in that it drives
    /// the bots its own way (<see cref="IBotSteering"/>) and gives the HUD its own layer; its kind is a plain round's, so
    /// nothing that names a special round or an event mode sees it.
    /// <para>
    /// The cars do not all start on the floor: only the bot across from the player does, the other two are held off the
    /// arena (<see cref="ArenaRound.Hold"/>). Push it off, and the lesson praises the player; then a box with a Mega lies
    /// in the middle; taken and used (the Mega has no timer here, <see cref="Car.UntimedAbility"/>), the lesson praises
    /// the giant; then the last two drop in side by side, away from the player, and a grown hit throws them far
    /// (<see cref="GameTuning.TutorialSettings.SmashPush"/>). A step that drags on is helped: the bot to push (and later
    /// the last ones) comes to the player, a Mega lost to a fall brings another box.
    /// </para>
    /// </summary>
    public sealed class TutorialMode : IRoundMode, IBotSteering, ITickable, IDisposable
    {
        // How close a walking bot must come to its place to stand still there, in metres.
        private const float Arrive = 0.6f;

        // The bot slows over this many metres before its place.
        private const float SlowDown = 2.5f;

        // Metres of floor the box keeps around it to the edge or a hole.
        private const float BoxClearance = 2f;

        // Seconds a bot thrown by the grown player is not thrown again: one hit, one throw.
        private const float ThrowRest = 0.6f;

        // How much of the grown player's own way of driving the throw follows, against the hit's push.
        private const float ThrowFollow = 0.6f;

        private readonly GameTuning.TutorialSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaPickups pickups;
        private readonly Dictionary<Car, Vector3> places = new();
        private readonly Dictionary<Car, float> thrownAt = new();
        private readonly List<(Car Car, Action<CarHit> OnHit)> hits = new();
        private readonly List<Car> pair = new();
        private Car player;
        private Vector3 inward;
        private float stepStart;
        private float praiseEnd;
        private float boxAgainAt = float.PositiveInfinity;
        private bool isStarted;

        public TutorialMode(GameTuning tuning, RoundRules rules, ArenaRound round, ArenaPickups pickups)
        {
            this.settings = tuning.Tutorial;
            this.round = round;
            this.pickups = pickups;
            this.IsActive = rules.IsTutorial;
            if (IsActive) {
                // As the cars stand, before the HUD or the bots see them: the last two must not show first.
                round.Placed += Begin;
            }
        }

        /// <summary>A step began (<see cref="Step"/> is it already).</summary>
        public event Action<TutorialStep> StepStarted;

        /// <summary>A step was done, just before the next one begins.</summary>
        public event Action<TutorialStep> StepDone;

        /// <summary>A plain round's: nothing that names a special round or an event mode sees the tutorial.</summary>
        public SpecialRound Kind => SpecialRound.None;

        public bool IsActive { get; }

        public ModeCard Card { get; } = new("racer", 0);

        public TutorialStep Step { get; private set; }

        /// <summary>
        /// The praise shown now, between a step done and the next: the step stays the one done until it ends.
        /// </summary>
        public TutorialPraise Praise { get; private set; }

        /// <summary>Whether the lesson was done to the end: every bot off.</summary>
        public bool IsDone => Step == TutorialStep.Done;

        /// <summary>The bot to push off in the push step: the one across from the player.</summary>
        public Car Target { get; private set; }

        /// <summary>The box of the box step, while it lies there.</summary>
        public FloorPickup Box { get; private set; }

        /// <summary>
        /// The last two bots, to smash off grown: held off the arena until the smash step, then in until they fall.
        /// </summary>
        public IReadOnlyList<Car> Pair => pair;

        /// <summary>Whether the last two have dropped in.</summary>
        public bool IsPairIn { get; private set; }

        public void Tick()
        {
            if (!IsActive || !isStarted || Step == TutorialStep.Done) {
                return;
            }

            // Nothing moves on the clock until the round has begun: the first round waits for the player's first input.
            if (!round.HasBegun) {
                return;
            }

            if (Praise != TutorialPraise.None) {
                if (round.Elapsed >= praiseEnd) {
                    var next = Praise == TutorialPraise.Pushed ? TutorialStep.Box : TutorialStep.Smash;
                    Praise = TutorialPraise.None;
                    Advance(next);
                }

                return;
            }

            switch (Step) {
                case TutorialStep.Push:
                    if (Target == null || Target.IsOut) {
                        StartPraise(TutorialPraise.Pushed);
                    }

                    break;
                case TutorialStep.Box:
                    if (player.HasItem || player.IsRunning(ItemKind.Mega)) {
                        Advance(TutorialStep.Use);
                    }

                    break;
                case TutorialStep.Use:
                    if (player.IsRunning(ItemKind.Mega)) {
                        StartPraise(TutorialPraise.Grown);
                    }

                    break;
                case TutorialStep.Smash:
                    if (IsPairIn && pair.TrueForAll(car => car.IsOut)) {
                        Advance(TutorialStep.Done);
                        return;
                    }

                    TickSmash();
                    break;
            }
        }

        public void Dispose()
        {
            round.Placed -= Begin;
            foreach (var (car, onHit) in hits) {
                if (car != null) {
                    car.Hit -= onHit;
                }
            }

            hits.Clear();
        }

        /// <summary>
        /// The tutorial's bots: each stands at its place for the step, never attacking; the bot to push comes to the
        /// player once the push step has dragged on, and the last two once the smash has.
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime)
        {
            if (!IsActive) {
                return false;
            }

            var car = bot.Car;
            if (!isStarted || !round.HasBegun) {
                car.Movement.Drive(Vector3.zero, 0f);
                return true;
            }

            var place = places.TryGetValue(car, out var spot) ? spot : car.Position;
            var stop = Arrive;
            var isHelping = Praise == TutorialPraise.None && round.Elapsed - stepStart >= settings.PushHelp
                && (car == Target && Step == TutorialStep.Push
                    || Step == TutorialStep.Smash && pair.Contains(car) && player.IsRunning(ItemKind.Mega));
            if (isHelping) {
                place = player.Position;
                stop = settings.HelpStop;
            }

            var offset = Flat(place - car.Position);
            var distance = offset.magnitude;
            // A bot the player just rammed lets itself be pushed: walking back to its place it would push against them.
            var isRammed = car.LastHitBy == player && Time.time - car.LastHitTime < settings.RammedRest;
            if (distance <= stop || car.Movement.IsStaggered || isRammed || !car.Movement.IsGrounded) {
                car.Movement.Drive(Vector3.zero, 0f);
                return true;
            }

            var pace = settings.BotPace * Mathf.Clamp01((distance - stop) / SlowDown + 0.2f);
            car.Movement.Drive(offset / distance, pace);
            return true;
        }

        /// <summary>
        /// The lesson's layout, as the cars stand: the bot across from the player stays to be pushed, the other two are
        /// held off the arena for the smash.
        /// </summary>
        private void Begin()
        {
            if (isStarted) {
                return;
            }

            isStarted = true;
            player = round.Player;
            player.UntimedAbility = ItemKind.Mega;
            var start = Flat(player.Position);
            inward = Flat(-start);
            if (inward.sqrMagnitude < 0.01f) {
                inward = Flat(player.transform.forward);
            }

            inward.Normalize();
            var bots = new List<Car>();
            foreach (var car in round.Cars) {
                if (car != player) {
                    bots.Add(car);
                    places[car] = Flat(car.Position);
                    Action<CarHit> onHit = hit => OnBotHit(car, hit);
                    car.Hit += onHit;
                    hits.Add((car, onHit));
                }
            }

            // The farthest bot is the one to push: across the floor, straight ahead of the player's car.
            bots.Sort((a, b) => Flat(b.Position - start).sqrMagnitude.CompareTo(Flat(a.Position - start).sqrMagnitude));
            Target = bots.Count > 0 ? bots[0] : null;
            for (var i = 1; i < bots.Count; i++) {
                pair.Add(bots[i]);
                round.Hold(bots[i]);
            }

            Advance(TutorialStep.Push);
        }

        private void StartPraise(TutorialPraise praise)
        {
            Praise = praise;
            praiseEnd = round.Elapsed + settings.PraiseTime;
        }

        private void Advance(TutorialStep next)
        {
            while (Step != next) {
                var done = Step;
                Step = Step + 1;
                if (done != TutorialStep.None) {
                    StepDone?.Invoke(done);
                }

                stepStart = round.Elapsed;
                Enter(Step);
                StepStarted?.Invoke(Step);
            }
        }

        private void Enter(TutorialStep step)
        {
            switch (step) {
                case TutorialStep.Box:
                    LayBox();
                    break;
                case TutorialStep.Smash:
                    JoinPair();
                    break;
                case TutorialStep.Done:
                    Box = null;
                    break;
            }
        }

        /// <summary>
        /// The last two drop in side by side, facing the player, <c>PairInset</c> inside the edge on whichever side of the
        /// middle (across from the player's start, or by it) is farther from where the player is now.
        /// </summary>
        private void JoinPair()
        {
            var edge = round.Edge;
            var at = Flat(player.Position);
            var far = PairSpot(edge, inward);
            var near = PairSpot(edge, -inward);
            var spot = (far - at).sqrMagnitude >= (near - at).sqrMagnitude ? far : near;
            var side = Vector3.Cross(Vector3.up, spot.sqrMagnitude > 0.01f ? spot.normalized : inward);
            for (var i = 0; i < pair.Count; i++) {
                var place = spot + side * ((i - (pair.Count - 1) * 0.5f) * settings.PairGap);
                places[pair[i]] = place;
                round.Join(pair[i], place, at - place);
            }

            IsPairIn = true;
        }

        /// <summary>
        /// A point <c>PairInset</c> metres inside the safe edge along <paramref name="way"/> from the middle: the
        /// farthest such on the line, since the middle may be a hole (candy).
        /// </summary>
        private Vector3 PairSpot(IArenaEdge edge, Vector3 way)
        {
            var spot = Vector3.zero;
            for (var distance = 0f; distance < edge.FullExtent; distance += 0.25f) {
                var point = way * distance;
                if (edge.DistanceToEdge(point, settings.PairInset) >= 0f) {
                    spot = point;
                }
            }

            return spot;
        }

        /// <summary>
        /// A grown hit on one of the last two throws it far and high, spinning, the way the hit went: a giant toy car
        /// sends the small ones flying. The hit itself already marked the player as who knocked it.
        /// </summary>
        private void OnBotHit(Car car, CarHit hit)
        {
            if (Step != TutorialStep.Smash || hit.Other != player || !pair.Contains(car) || car.IsOut
                || !player.IsRunning(ItemKind.Mega) || hit.Strength < settings.SmashFrom
                || thrownAt.TryGetValue(car, out var at) && Time.time - at < ThrowRest) {
                return;
            }

            thrownAt[car] = Time.time;
            var away = Flat(hit.Normal);
            if (away.sqrMagnitude < 0.01f) {
                away = Flat(car.Position - player.Position);
            }

            away.Normalize();
            var drive = Flat(player.Movement.Velocity);
            if (drive.sqrMagnitude > 1f) {
                away = (away + drive.normalized * ThrowFollow).normalized;
            }

            car.Movement.Push(away * settings.SmashPush - Flat(car.Movement.Velocity));
            var turn = Vector3.Cross(away, drive).y >= 0f ? 1f : -1f;
            car.Movement.Launch(settings.SmashLift, settings.SmashSpin * turn);
            car.Movement.Stagger(settings.RammedRest);
        }

        /// <summary>
        /// The smash step: a Mega lost (a fall ends it) with bots left standing brings another box a moment later.
        /// </summary>
        private void TickSmash()
        {
            if (player.IsRunning(ItemKind.Mega) || player.HasItem || Box != null && Box.IsLive) {
                boxAgainAt = float.PositiveInfinity;
                return;
            }

            if (float.IsPositiveInfinity(boxAgainAt)) {
                boxAgainAt = round.Elapsed + settings.BoxAgain;
            }
            else if (round.Elapsed >= boxAgainAt) {
                boxAgainAt = float.PositiveInfinity;
                LayBox();
            }
        }

        /// <summary>
        /// The box with a Mega, for the player alone, lying until taken, in the middle: on the first floor out from it
        /// towards the player that is well inside the edge, so a floor with a hole in the middle (candy) has it at the
        /// hole's rim.
        /// </summary>
        private void LayBox()
        {
            var towards = Flat(player.Position);
            var way = towards.sqrMagnitude > 0.01f ? towards.normalized : -inward;
            var spot = Vector3.zero;
            for (var distance = 0f; distance < round.Edge.FullExtent; distance += 0.25f) {
                if (round.Edge.DistanceToEdge(way * distance, BoxClearance) >= 0f) {
                    spot = way * distance;
                    break;
                }
            }

            Box = pickups.Spawn(spot, player, ItemKind.Mega, true);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
