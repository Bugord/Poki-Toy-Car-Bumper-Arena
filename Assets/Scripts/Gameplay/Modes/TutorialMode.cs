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

        /// <summary>Drive to the point in front of the car.</summary>
        Drive,

        /// <summary>Push one bot off the edge.</summary>
        Push,

        /// <summary>Take the box lying in the middle.</summary>
        Box,

        /// <summary>Use its Shockwave next to the last two bots.</summary>
        Use,

        /// <summary>Every bot is off: the round ends as a win.</summary>
        Done,
    }

    /// <summary>
    /// The new player's first round as a tutorial (<see cref="GameTuning.TutorialSettings"/>): steps the player through
    /// driving, pushing a bot off and using an item, on a still floor with three bots that never attack. It is a mode
    /// in that it drives the bots its own way (<see cref="IBotSteering"/>) and gives the HUD its own layer; its kind is
    /// a plain round's, so nothing that names a special round or an event mode sees it.
    /// <para>
    /// The bots stand where they were placed until their step: the one to push first (the nearest at the start), then
    /// the last two walk side by side to the edge across the floor from the player's start, and a box with a Shockwave
    /// is laid between them and the middle. Each step is a gate on state: one bot off ends the push, a held item the box,
    /// no bot left the lesson; so a player who rams ahead skips the steps they have done. A step that drags on is helped:
    /// the drive is skipped, the bot to push comes to the player, a Shockwave spent too far away brings another box.
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

        private readonly GameTuning.TutorialSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaPickups pickups;
        private readonly Dictionary<Car, Vector3> places = new();
        private Car player;
        private float stepStart;
        private float boxAgainAt = float.PositiveInfinity;
        private bool isStarted;

        public TutorialMode(GameTuning tuning, RoundRules rules, ArenaRound round, ArenaPickups pickups)
        {
            this.settings = tuning.Tutorial;
            this.round = round;
            this.pickups = pickups;
            this.IsActive = rules.IsTutorial;
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

        /// <summary>Whether the lesson was done to the end: every bot off.</summary>
        public bool IsDone => Step == TutorialStep.Done;

        /// <summary>The point to drive to in the drive step, on the floor.</summary>
        public Vector3 DrivePoint { get; private set; }

        /// <summary>The bot to push off in the push step.</summary>
        public Car Target { get; private set; }

        /// <summary>The box of the box step, while it lies there.</summary>
        public FloorPickup Box { get; private set; }

        /// <summary>The bots the Shockwave is for, in the box and use steps: the ones still in.</summary>
        public IReadOnlyList<Car> Pair => pair;

        private readonly List<Car> pair = new();
        private Vector3 pairSpot;
        private Vector3 pairSide;

        public void Tick()
        {
            if (!IsActive || !round.IsPlaced) {
                return;
            }

            if (!isStarted) {
                Begin();
            }

            if (Step == TutorialStep.Done) {
                return;
            }

            pair.RemoveAll(car => car.IsOut);
            var botsLeft = round.CarsLeft - (player.IsOut ? 0 : 1);
            if (botsLeft <= 0) {
                Advance(TutorialStep.Done);
                return;
            }

            // Nothing moves on the clock until the round has begun: the first round waits for the player's first input.
            if (!round.HasBegun) {
                return;
            }

            // A bot off ends the push whenever it happens, the drive with it: the steps are gates on state, not an order.
            if (Step < TutorialStep.Box && botsLeft < round.Cars.Count - 1) {
                Advance(TutorialStep.Box);
                return;
            }

            switch (Step) {
                case TutorialStep.Drive:
                    if (Flat(player.Position - DrivePoint).magnitude <= settings.DriveReach
                        || round.Elapsed - stepStart >= settings.DriveSkip) {
                        Advance(TutorialStep.Push);
                    }

                    break;
                case TutorialStep.Box:
                    if (player.HasItem) {
                        Advance(TutorialStep.Use);
                    }

                    break;
                case TutorialStep.Use:
                    TickUse();
                    break;
            }
        }

        public void Dispose()
        {
            if (player != null) {
                player.ItemUsed -= OnPlayerUsed;
            }
        }

        /// <summary>
        /// The tutorial's bots: each walks to its place for the step and stands there, never attacking; the bot to push
        /// comes to the player once the push step has dragged on.
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
            if (car == Target && Step == TutorialStep.Push && round.Elapsed - stepStart >= settings.PushHelp) {
                place = player.Position;
                stop = settings.HelpStop;
            }

            var offset = Flat(place - car.Position);
            var distance = offset.magnitude;
            if (distance <= stop || car.Movement.IsStaggered) {
                car.Movement.Drive(Vector3.zero, 0f);
                return true;
            }

            var pace = settings.BotPace * Mathf.Clamp01((distance - stop) / SlowDown + 0.2f);
            car.Movement.Drive(offset / distance, pace);
            return true;
        }

        /// <summary>
        /// The lesson's layout, once the cars stand: the point ahead of the player, the bot to push (the nearest), the
        /// places the last two will walk to.
        /// </summary>
        private void Begin()
        {
            isStarted = true;
            player = round.Player;
            player.ItemUsed += OnPlayerUsed;
            var edge = round.Edge;
            var start = Flat(player.Position);
            var inward = Flat(-start);
            if (inward.sqrMagnitude < 0.01f) {
                inward = Flat(player.transform.forward);
            }

            inward.Normalize();
            // On the floor: short of a hole in the middle (candy) if one is nearer than the drive.
            DrivePoint = start + inward * settings.DriveDistance;
            for (var distance = settings.DriveDistance; distance > 1f; distance -= 0.25f) {
                DrivePoint = start + inward * distance;
                if (edge.DistanceToEdge(DrivePoint, settings.DriveReach) >= 0f) {
                    break;
                }
            }

            var bots = new List<Car>();
            foreach (var car in round.Cars) {
                if (car != player) {
                    bots.Add(car);
                    places[car] = Flat(car.Position);
                }
            }

            // The nearest bot is the one to push; the others wait for the Shockwave across the floor from the start.
            bots.Sort((a, b) => Flat(a.Position - start).sqrMagnitude.CompareTo(Flat(b.Position - start).sqrMagnitude));
            Target = bots.Count > 0 ? bots[0] : null;
            pairSpot = PairSpot(edge, inward);
            pairSide = Vector3.Cross(Vector3.up, inward);
            Advance(TutorialStep.Drive);
        }

        /// <summary>
        /// The middle of the pair's places: across the floor from the player's start, <c>PairInset</c> metres
        /// inside the safe edge.
        /// </summary>
        private Vector3 PairSpot(IArenaEdge edge, Vector3 inward)
        {
            // The farthest such point on the line: the middle may be a hole (candy), so the first unsafe one is no end.
            var spot = Vector3.zero;
            for (var distance = 0f; distance < edge.FullExtent; distance += 0.25f) {
                var point = inward * distance;
                if (edge.DistanceToEdge(point, settings.PairInset) >= 0f) {
                    spot = point;
                }
            }

            return spot;
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
                    // Every bot still in (the last two, or more if the player pushed another off first) walks to a place
                    // side by side by the edge; the box waits between them and the middle.
                    pair.Clear();
                    foreach (var car in round.Cars) {
                        if (car != player && !car.IsOut) {
                            pair.Add(car);
                        }
                    }

                    for (var i = 0; i < pair.Count; i++) {
                        places[pair[i]] = pairSpot + pairSide * ((i - (pair.Count - 1) * 0.5f) * settings.PairGap);
                    }

                    LayBox();
                    break;
                case TutorialStep.Done:
                    Box = null;
                    break;
            }
        }

        /// <summary>The use step: a Shockwave spent with bots left standing brings another box.</summary>
        private void TickUse()
        {
            if (!player.HasItem && (Box == null || !Box.IsLive) && round.Elapsed >= boxAgainAt) {
                boxAgainAt = float.PositiveInfinity;
                LayBox();
            }
        }

        private void OnPlayerUsed(ItemKind kind)
        {
            if (Step is TutorialStep.Use or TutorialStep.Box) {
                boxAgainAt = round.Elapsed + settings.BoxAgain;
            }
        }

        /// <summary>
        /// The box with a Shockwave, for the player alone, lying until taken, towards the pair from the middle: on the
        /// first floor out from <c>BoxDistance</c> that is well inside the edge, so a floor with a hole in the middle
        /// (candy) has it at the hole's rim.
        /// </summary>
        private void LayBox()
        {
            var towards = Flat(pairSpot);
            var way = towards.sqrMagnitude > 0.01f ? towards.normalized : Vector3.zero;
            var spot = way * settings.BoxDistance;
            for (var distance = settings.BoxDistance; distance < towards.magnitude; distance += 0.25f) {
                if (round.Edge.DistanceToEdge(way * distance, BoxClearance) >= 0f) {
                    spot = way * distance;
                    break;
                }
            }

            Box = pickups.Spawn(spot, player, ItemKind.Shockwave, true);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
