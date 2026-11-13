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

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The OVERHEAT event mode: every car for itself for a minute, a point for each knock-out, a car that falls comes
    /// back a moment later as in the long match (<see cref="GameTuning.OverheatSettings.Rules"/>). Every hit a car takes
    /// heats it up, by how hard the other car drove in (<see cref="GameTuning.OverheatSettings.HeatPerHit"/>); the
    /// hotter it is, the more the next hit pushes it, the longer it is off balance, the faster a hit may leave it going
    /// and the higher it flies (<see cref="Car.PushTakenScale"/>, <see cref="Car.StaggerTakenScale"/>,
    /// <see cref="Car.BumpSpeedScale"/>), through a curve, so the first hits feel like plain ones and a red-hot car flies
    /// off from almost anywhere. A fall cools it: it comes back cold.
    /// <para>
    /// The bots want hot cars (<see cref="IBotTargeting"/>: more the hotter, less with every bot already after it, and
    /// never more than <see cref="GameTuning.OverheatSettings.MaxPlayerHunters"/> after the player) and a hot bot keeps
    /// further from the edge (<see cref="SteerBot"/>). The player takes a share of the heat on Easy.
    /// </para>
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.Overheat"/>), silent in every other: nothing subscribed, nothing drawn, no scale
    /// changed, no random numbers drawn.
    /// </para>
    /// </summary>
    public sealed class OverheatMode : IRoundMode, IBotSteering, IBotTargeting, IModeAnalytics, ITickable,
        IInitializable, IDisposable
    {
        /// <summary>The name of the flame crown a red-hot car wears: the card's prop.</summary>
        public const string FlameName = "Heat Flame";

        // Metres ahead a hot bot looks inward, and how much deeper the floor must be there for it to drive back in.
        private const float RidgeProbe = 1.5f;
        private const float RidgeGain = 0.4f;

        private readonly GameTuning.OverheatSettings settings;
        private readonly GameTuning tuning;
        private readonly ArenaRound round;
        private readonly RoundDifficulty difficulty;
        private readonly IArenaEdge edge;
        private readonly MechanicArt art;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly List<Car> cars = new();
        private float[] heats = Array.Empty<float>();
        private Action<CarHit>[] hitHandlers = Array.Empty<Action<CarHit>>();
        private HeatLook look;
        private EffectPlayer effects;
        private bool isBuilt;
        private float fallHeatSum;
        private float playerFallHeatSum;

        public OverheatMode(GameTuning tuning, RoundRules rules, ArenaRound round, RoundDifficulty difficulty,
            IArenaEdge edge, MechanicArt art, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
        {
            this.settings = tuning.Overheat;
            this.tuning = tuning;
            this.round = round;
            this.difficulty = difficulty;
            this.edge = edge;
            this.art = art;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.parent = parent;
            this.IsActive = rules.Kind == SpecialRound.Overheat;
        }

        /// <summary>A car of the heat went red-hot: its index. The HUD calls it out over the car.</summary>
        public event Action<int> WentRedHot;

        /// <summary>The player knocked a red-hot car off: its index. Celebrated more than a plain knock-out.</summary>
        public event Action<int> Meltdown;

        public SpecialRound Kind => SpecialRound.Overheat;

        /// <summary>The card's picture: a blue racer with the red-hot flame crown on its roof.</summary>
        public ModeCard Card { get; } = new("racer", 2, FlameName, 0.05f);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.OverheatSettings Settings => settings;

        /// <summary>Falls of every car this round, and the heat they fell with in all: for the balance runs.</summary>
        public int Falls { get; private set; }

        /// <summary>The average heat a car fell with this round, 0 without a fall.</summary>
        public float AverageFallHeat => Falls > 0 ? fallHeatSum / Falls : 0f;

        /// <summary>The player's falls this round.</summary>
        public int PlayerFalls { get; private set; }

        /// <summary>The average heat the player fell with this round, 0 without a fall.</summary>
        public float AveragePlayerFallHeat => PlayerFalls > 0 ? playerFallHeatSum / PlayerFalls : 0f;

        /// <summary>Cars the player knocked off while they were red-hot, this round.</summary>
        public int PlayerHotKnockouts { get; private set; }

        public IEnumerable<(string id, string action)> EndEvents
        {
            get {
                if (!IsActive) {
                    yield break;
                }

                yield return ($"overheat-hotko-{Bucket(PlayerHotKnockouts)}", "overheat-knockouts");
                yield return ($"overheat-fall-{FallBucket(PlayerFalls, AveragePlayerFallHeat)}", "overheat-falls");
            }
        }

        public void Initialize()
        {
            if (!IsActive) {
                return;
            }

            round.KnockedOut += OnKnockedOut;
            round.Eliminated += OnEliminated;
        }

        public void Dispose()
        {
            round.KnockedOut -= OnKnockedOut;
            round.Eliminated -= OnEliminated;
            for (var i = 0; i < cars.Count && i < hitHandlers.Length; i++) {
                if (cars[i] != null) {
                    cars[i].Hit -= hitHandlers[i];
                }
            }

            cars.Clear();
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

            look.Tick(cars, heats, Time.deltaTime);
        }

        /// <summary>The heat of car <paramref name="index"/>, 0 cold to 1; 0 outside the mode.</summary>
        public float HeatOf(int index) => index >= 0 && index < heats.Length ? heats[index] : 0f;

        /// <summary>The heat of <paramref name="car"/>, 0 outside the mode or for a car not in the round.</summary>
        public float HeatOf(Car car) => HeatOf(round.IndexOf(car));

        /// <summary>Whether car <paramref name="index"/> is red-hot.</summary>
        public bool IsRedHot(int index) => IsActive && HeatOf(index) >= settings.Hot;

        /// <summary>Sets a car's heat at once, with what it does: for the tests and the dev console.</summary>
        public void SetHeat(Car car, float heat)
        {
            var index = round.IndexOf(car);
            if (!IsActive || index < 0 || index >= heats.Length) {
                return;
            }

            ChangeHeat(index, heat);
        }

        /// <summary>
        /// A hot car is worth more to a bot, and less with every bot already after it, so they finish it off without all
        /// piling onto it; the player is never hunted by more than the mode allows, however hot.
        /// </summary>
        public float TargetBonus(Bot bot, Car car, int hunters)
        {
            if (!IsActive) {
                return 0f;
            }

            if (car == round.Player && hunters >= settings.MaxPlayerHunters) {
                return -1000f;
            }

            return settings.TargetWeight * settings.EffectOf(HeatOf(car)) / (1f + hunters);
        }

        /// <summary>
        /// A warm bot near the edge drives back in before anything else: the hotter, the wider the margin it keeps.
        /// Everything else is the usual play.
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge arenaEdge, float deltaTime)
        {
            var car = bot.Car;
            var heat = HeatOf(car);
            if (!IsActive || heat < settings.Warm || car.Movement.IsStaggered) {
                return false;
            }

            var share = Mathf.InverseLerp(settings.Warm, 1f, heat);
            var margin = tuning.Bots.EdgeMargin + settings.HotEdgeMargin * share;
            var position = car.Position;
            if (arenaEdge.DistanceToEdge(position, margin) >= 0f) {
                return false;
            }

            var inward = arenaEdge.Inward(position);
            inward.y = 0f;
            if (inward.sqrMagnitude < 0.0001f) {
                return false;
            }

            // Only where driving in gets it deeper: on a narrow band, or on what is left at the peak, no floor is that far
            // from every edge, and the bot would only shuffle along the ridge instead of playing.
            inward.Normalize();
            if (arenaEdge.DistanceToEdge(position + inward * RidgeProbe) < arenaEdge.DistanceToEdge(position) + RidgeGain) {
                return false;
            }

            car.Movement.Drive(inward, 1f);
            return true;
        }

        private void Build()
        {
            isBuilt = true;
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            cars.AddRange(round.Cars);
            heats = new float[cars.Count];
            hitHandlers = new Action<CarHit>[cars.Count];
            for (var i = 0; i < cars.Count; i++) {
                var index = i;
                hitHandlers[i] = hit => OnHit(index, hit);
                cars[i].Hit += hitHandlers[i];
            }

            look = new HeatLook(art, parent, effects, settings);
            look.Build(cars);
        }

        private void OnHit(int index, CarHit hit)
        {
            if (!round.IsRunning || hit.Strength < settings.MinStrength) {
                return;
            }

            var car = cars[index];
            var share = Mathf.Clamp01(hit.Strength / Mathf.Max(0.01f, tuning.Car.MaxSpeed));
            // The push has gone already, by the heat before this hit; a hot car thrown hard also leaves the floor.
            var effect = settings.EffectOf(heats[index]);
            if (!hit.IsHitter && effect > 0.02f && share >= settings.LiftFrom && car.Movement.IsGrounded) {
                var side = Vector3.Cross(hit.Normal, hit.Other.Movement.Velocity).y >= 0f ? 1f : -1f;
                car.Movement.Launch(settings.LiftAtMax * effect * share, settings.SpinAtMax * effect * share * side);
            }

            var gain = settings.HeatFloor + settings.HeatPerHit * share;
            if (hit.IsHitter) {
                gain *= settings.HitterShare;
            }
            else if (hit.Other.IsPlayer) {
                gain *= settings.FromPlayer;
            }

            if (car.IsPlayer) {
                gain *= Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level);
            }

            var wasHot = heats[index] >= settings.Hot;
            ChangeHeat(index, heats[index] + gain);
            if (!wasHot && heats[index] >= settings.Hot) {
                look?.Flare(index);
                var isPlayers = car.IsPlayer || hit.Other.IsPlayer;
                audioService?.Play(audioService.Library.CrumbleWarning, isPlayers ? 0.9f : 0.35f, 1.4f);
                WentRedHot?.Invoke(index);
            }
        }

        private void ChangeHeat(int index, float heat)
        {
            heat = Mathf.Clamp01(heat);
            heats[index] = heat;
            var effect = settings.EffectOf(heat);
            var car = cars[index];
            car.PushTakenScale = 1f + (settings.PushAtMax - 1f) * effect;
            car.StaggerTakenScale = 1f + (settings.StaggerAtMax - 1f) * effect;
            car.BumpSpeedScale = 1f + (settings.SpeedCapAtMax - 1f) * effect;
        }

        private void OnKnockedOut(Car car, Car by)
        {
            var index = round.IndexOf(car);
            if (by == null || by != round.Player || !IsRedHot(index)) {
                return;
            }

            PlayerHotKnockouts++;
            var point = car.Position;
            if (effects != null) {
                effects.PlayKnockoutCredit(point);
                effects.PlayShockwave(point, 3.5f);
            }

            arenaCamera?.Shake(0.7f);
            audioService?.Play(audioService.Library.KnockoutCredit, 1f, 1.25f);
            Meltdown?.Invoke(index);
        }

        private void OnEliminated(int index, int by)
        {
            if (index < 0 || index >= heats.Length) {
                return;
            }

            // The heat it fell with, then it cools down: it comes back cold.
            Falls++;
            fallHeatSum += heats[index];
            if (cars[index] == round.Player) {
                PlayerFalls++;
                playerFallHeatSum += heats[index];
            }

            ChangeHeat(index, 0f);
        }

        private static string Bucket(int count) => count >= 10 ? "10plus" : count.ToString();

        private string FallBucket(int falls, float heat)
        {
            if (falls == 0) {
                return "none";
            }

            return heat >= settings.Hot ? "hot" : heat >= settings.Warm ? "warm" : "cold";
        }
    }
}
