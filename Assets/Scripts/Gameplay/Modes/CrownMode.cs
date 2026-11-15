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
    /// The CROWN event mode: a crown drops from the sky onto the middle of a fenced floor in the first seconds after GO
    /// (<see cref="GameTuning.CrownSettings.FirstDelay"/>), the first car to reach it takes it, and every whole second a
    /// car holds it is a point (<see cref="ArenaRound.AddPoints"/>). A bump on its holder steals it (a hit at
    /// <see cref="GameTuning.CrownSettings.StealStrength"/> or a push held for <see cref="GameTuning.CrownSettings.PressTime"/>), though not in the
    /// holder's first moment with it and not straight back to the car it was just stolen from. The crown is heavy: its
    /// holder is slower (<see cref="GameTuning.CrownSettings.HolderSpeed"/>), so it changes hands. The round is timed;
    /// the car with the most seconds wins. Nobody falls: the floor is fenced, and a car past the fence after all is put
    /// back on (the rules, <see cref="GameTuning.CrownSettings.Rules"/>).
    /// <para>
    /// The bots play both sides (<see cref="SteerBot"/>): they race to the loose crown, and two or three of the nearest
    /// chase its holder (the nearest straight at it, the others where it is going) while the rest keep their distance on
    /// free floor round it, ready to cut in when it changes hands by them; with it they run where the floor is widest
    /// and the chasers farthest. The player's crown is hunted by one or two bots only, by the difficulty, not in its
    /// first seconds, and the player with it is as fast as they are, so driving away keeps it.
    /// </para>
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.Crown"/>), silent in every other: it builds nothing and draws no random numbers there.
    /// </para>
    /// </summary>
    public sealed class CrownMode : IRoundMode, IBotSteering, IModeAnalytics, ITickable, IDisposable
    {
        // Takes past this many share one bucket of the end event.
        private const int LastTakesBucket = 10;
        // Seconds since the holder's last contact credit by the same car that still count as one push held.
        private const float PressGap = 0.12f;
        // Metres between two cars' middles that still count as touching, for a push.
        private const float PressReach = 3.6f;
        // Metres above the floor a car's middle may be and still take the loose crown: one flying over it does not.
        private const float PickHeight = 2f;
        // Seconds between a bot's looks at what it chases, at the most: the difficulty's reaction time is shorter.
        private const float FleeThink = 0.25f;
        // Seconds between a waiting bot's looks for free floor round the holder.
        private const float ShadowThink = 0.6f;
        // Directions round the holder a waiting bot weighs for its place.
        private const int ShadowSlots = 12;

        /// <summary>The crown's state over the round.</summary>
        public enum CrownPhase
        {
            /// <summary>Before the round starts, nothing shows; then the crown drops to its spot.</summary>
            Dropping,

            /// <summary>On the floor, the first car to reach it takes it.</summary>
            Loose,

            /// <summary>Over its holder's roof.</summary>
            Held,
        }

        /// <summary>
        /// Raised when a car takes the crown: the new holder and the car it was stolen from (null for the loose crown).
        /// </summary>
        public event Action<Car, Car> Taken;

        private readonly GameTuning.CrownSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly ArenaHazards hazards;
        private readonly RoundDifficulty difficulty;
        private readonly MechanicArt mechanicArt;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly GameTuning.BotSettings botSettings;
        private readonly List<(Car, Action<CarHit>)> hitHandlers = new();
        private readonly Dictionary<Bot, BotPlay> botPlay = new();
        private readonly List<Car> nearHolder = new();

        private bool isBuilt;
        private CrownArt art;
        private EffectPlayer effects;
        private float[] held;
        private int[] takes;
        // Every car's velocity on the frame before, on the ground plane: how fast a thief drove at the holder before the
        // bump changed it.
        private Vector3[] runUp;
        private float dropLeft;
        private float protectedUntil;
        private float takenAt;
        private float stealBackUntil;
        private Car pressCar;
        private float pressSince;
        private readonly SurfaceFeel holderFeel;
        private readonly SurfaceFeel playerHolderFeel;

        public CrownMode(GameTuning tuning, RoundRules rules, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            RoundDifficulty difficulty, MechanicArt mechanicArt, ArenaCamera arenaCamera, IAudioService audioService)
        {
            this.settings = tuning.Crown;
            this.botSettings = tuning.Bots;
            this.round = round;
            this.edge = edge;
            this.hazards = hazards;
            this.difficulty = difficulty;
            this.mechanicArt = mechanicArt;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.IsActive = rules.Kind == SpecialRound.Crown;
            holderFeel = new SurfaceFeel(settings.HolderSpeed, settings.HolderAcceleration, 1f, settings.HolderPush, 1f);
            playerHolderFeel = new SurfaceFeel(settings.PlayerHolderSpeed, settings.PlayerHolderSpeed, 1f,
                settings.HolderPush, 1f);
        }

        public SpecialRound Kind => SpecialRound.Crown;

        /// <summary>A red racer with the round's gold crown riding over its roof.</summary>
        public ModeCard Card { get; } = new("racer", 1, CrownArt.PropName, 0.05f);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.CrownSettings Settings => settings;

        public CrownPhase Phase { get; private set; } = CrownPhase.Dropping;

        /// <summary>The car with the crown, null while it drops or lies loose.</summary>
        public Car Holder { get; private set; }

        /// <summary>The holder's index in the round (0 the player), or -1 while nobody holds the crown.</summary>
        public int HolderIndex => Holder != null ? round.IndexOf(Holder) : -1;

        /// <summary>The car the crown was last stolen from, null for none.</summary>
        public Car Robbed { get; private set; }

        /// <summary>Where the crown lands and lies loose: on the floor, near the middle.</summary>
        public Vector3 Spot { get; private set; }

        /// <summary>Where the crown is in the world now: over its holder, on its spot or dropping to it.</summary>
        public Vector3 CrownPosition => art != null ? art.CrownPosition : Spot;

        /// <summary>Whether the crown shows yet: it starts to drop at GO.</summary>
        public bool IsShown => isBuilt && round.HasBegun;

        /// <summary>Times the crown was taken, the loose one and every steal.</summary>
        public int Changes { get; private set; }

        /// <summary>Times the crown was stolen from one car by another.</summary>
        public int Steals { get; private set; }

        /// <summary>Of <see cref="Steals"/>, those by a push held on the holder rather than a bump.</summary>
        public int PressSteals { get; private set; }

        /// <summary>Grows on every change of the crown, for the bots and the HUD to notice it.</summary>
        public int Version { get; private set; }

        /// <summary>Whether the holder cannot lose the crown yet: its first moment with it.</summary>
        public bool IsProtected => Holder != null && Time.time < protectedUntil;

        /// <summary>Seconds the crown has been on <paramref name="car"/>'s roof this round, fractions included.</summary>
        /// <summary>
        /// The mode's own event at the end of its round, beside the shared ones of every mode:
        /// <c>special / crown-held-&lt;s&gt;-takes-&lt;n&gt; / crown</c>. <c>s</c> is how long the player kept the crown, by
        /// the lower end of its bucket (<see cref="HeldBucketOf"/>), <c>n</c> how many times the player took it
        /// (<see cref="TakesBucketOf"/>). It says whether the player gets the crown at all and keeps it, or takes it often
        /// and loses it straight away.
        /// </summary>
        /// <remarks>
        /// A bucket is named by its lower end rather than as a range: Poki drops an event with more than two numbers, and
        /// <c>held-1-9-takes-3</c> would have three.
        /// </remarks>
        public IEnumerable<(string id, string action)> EndEvents
        {
            get {
                if (!IsActive || round.Player == null) {
                    yield break;
                }

                yield return ($"crown-held-{HeldBucketOf(HeldOf(round.Player))}-takes-"
                    + TakesBucketOf(TakesOf(round.Player)), "crown");
            }
        }

        /// <summary>none, 1, 10 or 20plus: the player's whole seconds with the crown, by the bucket's lower end.</summary>
        public static string HeldBucketOf(float seconds)
        {
            var whole = (int)seconds;
            return whole <= 0 ? "none" : whole < 10 ? "1" : whole < 20 ? "10" : "20plus";
        }

        /// <summary>0..9, or 10plus: the times the player took the crown.</summary>
        public static string TakesBucketOf(int takes) =>
            takes >= LastTakesBucket ? $"{LastTakesBucket}plus" : Math.Max(0, takes).ToString();

        public float HeldOf(Car car)
        {
            var index = round.IndexOf(car);
            return held != null && index >= 0 ? held[index] : 0f;
        }

        /// <summary>Times <paramref name="car"/> took the crown this round.</summary>
        public int TakesOf(Car car)
        {
            var index = round.IndexOf(car);
            return takes != null && index >= 0 ? takes[index] : 0;
        }

        /// <summary>Whether <paramref name="car"/> just lost the crown and cannot steal it straight back.</summary>
        public bool IsStealBack(Car car) => car != null && car == Robbed && Time.time < stealBackUntil;

        public void Tick()
        {
            if (!IsActive) {
                return;
            }

            if (!isBuilt) {
                if (!round.IsPlaced) {
                    return;
                }

                Build();
            }

            var deltaTime = Time.deltaTime;
            if (round.IsRunning) {
                switch (Phase) {
                    case CrownPhase.Dropping:
                        dropLeft -= deltaTime;
                        if (dropLeft <= 0f) {
                            Phase = CrownPhase.Loose;
                            Version++;
                            effects?.PlayPickup(Spot);
                        }

                        break;
                    case CrownPhase.Loose:
                        TryPick();
                        break;
                    case CrownPhase.Held:
                        Hold(deltaTime);
                        break;
                }
            }

            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                runUp[i] = Flat(cars[i].Movement.Velocity);
            }

            var dropShare = settings.FirstDelay > 0f ? Mathf.Clamp01(1f - dropLeft / settings.FirstDelay) : 1f;
            art.Animate(Phase, round.HasBegun, Holder, Spot, dropShare, IsProtected);
        }

        /// <summary>
        /// The mode's own bot play, for every bot in play: with the crown it runs, without it it races to the loose crown
        /// or goes after the holder, the way its turn among the chasers says.
        /// </summary>
        public bool SteerBot(Bot bot, IArenaEdge floor, float deltaTime)
        {
            if (!IsActive) {
                return false;
            }

            bot.SetPickup(null);
            if (!botPlay.TryGetValue(bot, out var play)) {
                play = new BotPlay();
                botPlay[bot] = play;
            }

            play.PitHold -= deltaTime;
            if (!round.IsRunning) {
                bot.Car.Movement.Drive(Vector3.zero, 0f);
                return true;
            }

            var car = bot.Car;
            var position = Flat(car.Position);
            // A change of the crown is noticed a moment later, longer for the careless.
            if (play.SeenVersion != Version) {
                play.SeenVersion = Version;
                var reaction = difficulty.ReactionTime;
                play.ReactAt = Time.time + bot.Range(reaction.x, reaction.y) / Mathf.Max(0.1f, bot.Traits.Caution);
            }

            if (Holder == car) {
                Flee(bot, play, floor, position, deltaTime);
                return true;
            }

            if (Time.time < play.ReactAt) {
                // Still on its way to where it last meant to go.
                Drive(bot, play, floor, position, play.LastAim - position, play.LastPace);
                return true;
            }

            if (Phase != CrownPhase.Held) {
                Drive(bot, play, floor, position, Flat(Spot) - position, RacePace(car));
                return true;
            }

            Chase(bot, play, floor, position);
            return true;
        }

        public void Dispose()
        {
            foreach (var (car, handler) in hitHandlers) {
                if (car != null) {
                    car.Hit -= handler;
                }
            }

            hitHandlers.Clear();
            art?.Dispose();
        }

        private void Build()
        {
            isBuilt = true;
            var cars = round.Cars;
            held = new float[cars.Count];
            takes = new int[cars.Count];
            runUp = new Vector3[cars.Count];
            Spot = FindSpot();
            dropLeft = settings.FirstDelay;
            // The effects live in the scene, not the container.
            effects = Object.FindAnyObjectByType<EffectPlayer>();
            var look = cars.Count > 0 ? cars[0].GetComponent<CarLook>() : null;
            var parent = cars.Count > 0 ? cars[0].transform.parent : null;
            art = new CrownArt(mechanicArt, look, parent);
            foreach (var car in cars) {
                // The round's crown is the only one: a car's own crown hat would read as the holder.
                CrownArt.HideCrownHat(car);
                var self = car;
                Action<CarHit> handler = hit => OnHit(self, hit);
                car.Hit += handler;
                hitHandlers.Add((car, handler));
            }
        }

        /// <summary>
        /// The crown's spot: the floor nearest the middle with <see cref="GameTuning.CrownSettings.SpotMargin"/> of floor
        /// round it, which is the middle on the round and the square floors and the ring's inner side round the candy
        /// arena's hole, on a grid of half metres. The cars stand evenly round the rim, so it is as far from each.
        /// </summary>
        private Vector3 FindSpot()
        {
            if (edge.DistanceToEdge(Vector3.zero, settings.SpotMargin) >= 0f) {
                return Vector3.zero;
            }

            var best = Vector3.zero;
            var bestDistance = float.PositiveInfinity;
            var extent = edge.FullExtent;
            for (var x = -extent; x <= extent; x += 0.5f) {
                for (var z = -extent; z <= extent; z += 0.5f) {
                    var point = new Vector3(x, 0f, z);
                    // Away from the camera breaks the ring's tie: the crown lands where the player sees it.
                    var distance = point.magnitude - z * 0.001f;
                    if (distance < bestDistance && edge.DistanceToEdge(point, settings.SpotMargin) >= 0f) {
                        best = point;
                        bestDistance = distance;
                    }
                }
            }

            return best;
        }

        // The first car in play to reach the loose crown takes it; the nearest of those that reach it on one frame.
        private void TryPick()
        {
            Car nearest = null;
            var nearestDistance = settings.PickRadius;
            foreach (var car in round.Cars) {
                if (!car.IsTargetable || car.Position.y > PickHeight) {
                    continue;
                }

                var distance = Flat(car.Position - Spot).magnitude;
                if (distance <= nearestDistance) {
                    nearest = car;
                    nearestDistance = distance;
                }
            }

            if (nearest != null) {
                Take(nearest, null, nearest.Position);
            }
        }

        // The holder's seconds turn into points, its tyres feel the crown's weight, and a push held on it steals it.
        private void Hold(float deltaTime)
        {
            var holder = Holder;
            holder.Movement.Tyres = FeelOf(holder);
            var index = round.IndexOf(holder);
            var before = Mathf.FloorToInt(held[index]);
            held[index] += deltaTime;
            var after = Mathf.FloorToInt(held[index]);
            if (after > before) {
                round.AddPoints(holder, after - before);
            }

            // A press starts with a push on the holder, once the holder can lose the crown, or a car glued to it would
            // steal it the moment the holder's first moment is over. It goes on while the pusher stays at the holder's
            // side facing it: two cars stalled against the fence stop crediting the push, and the press must not end.
            var by = holder.LastHitBy;
            if (pressCar != null && IsPressing(pressCar, holder)) {
                by = pressCar;
            }
            else if (by == null || Time.time < ProtectedUntil(by) || holder.LastHitByItem
                || Time.time - holder.LastHitTime > PressGap || !IsPressing(by, holder)) {
                pressCar = null;
                return;
            }

            if (by != pressCar) {
                pressCar = by;
                pressSince = Time.time;
                return;
            }

            if (Time.time - pressSince >= settings.PressTime && CanSteal(by)) {
                PressSteals++;
                Steal(by, holder, (by.Position + holder.Position) * 0.5f);
            }
        }

        // Whether the car is at the holder's side and facing it: still pushing, if not crediting the push.
        private bool IsPressing(Car car, Car holder)
        {
            var toHolder = Flat(holder.Position - car.Position);
            var distance = toHolder.magnitude;
            return car.IsTargetable && distance <= PressReach
                && (distance < 0.01f || Vector3.Dot(Flat(car.transform.forward).normalized, toHolder / distance) > 0.5f);
        }

        // A ram on the holder steals the crown: a real bump by a car driving at it with a run-up, not a nudge in a crowd.
        // Both cars hear a hit, so only the holder's side counts.
        private void OnHit(Car car, CarHit hit)
        {
            if (car != Holder || !round.IsRunning || hit.Strength < settings.StealStrength || !CanSteal(hit.Other)
                || RunUpOf(hit.Other, car) < (hit.Other.IsPlayer ? settings.PlayerRunUp : settings.StealRunUp)) {
                return;
            }

            Steal(hit.Other, car, hit.Point);
        }

        // How fast the thief drove at the holder on the frame before the bump, in metres per second.
        private float RunUpOf(Car thief, Car holder)
        {
            var index = round.IndexOf(thief);
            var toHolder = Flat(holder.Position - thief.Position);
            return index >= 0 && toHolder.sqrMagnitude > 0.0001f ? Vector3.Dot(runUp[index], toHolder.normalized) : 0f;
        }

        private bool CanSteal(Car thief) => thief != null && thief != Holder && thief.IsTargetable
            && Time.time >= ProtectedUntil(thief) && !IsStealBack(thief) && round.IndexOf(thief) >= 0;

        // Until when the holder keeps the crown from this thief: a bot's crown is the player's to take sooner.
        private float ProtectedUntil(Car thief) => thief.IsPlayer && Holder != null && !Holder.IsPlayer
            ? Mathf.Min(protectedUntil, takenAt + settings.PlayerStealAfter)
            : protectedUntil;

        /// <summary>
        /// The crown goes from <paramref name="from"/> to <paramref name="thief"/>: the robbed car is knocked away from
        /// the thief and off balance for a moment, so the thief gets a start and the two do not trade it back and forth.
        /// </summary>
        private void Steal(Car thief, Car from, Vector3 point)
        {
            var away = Flat(from.Position - thief.Position);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Flat(from.transform.forward);
            from.Movement.Push(away * settings.StealPush);
            from.Movement.Stagger(settings.StealStagger);
            Steals++;
            Robbed = from;
            // The player may go after the crown again sooner than a bot: losing it should feel like a chase, not a wait.
            stealBackUntil = Time.time + (from.IsPlayer ? settings.PlayerStealBack : settings.StealBackTime);
            if (effects != null) {
                effects.PlayHit(point, away, 1f, true, false);
            }

            Take(thief, from, point);
        }

        private void Take(Car car, Car from, Vector3 point)
        {
            if (Holder != null) {
                Holder.Movement.Tyres = SurfaceFeel.Neutral;
            }

            Holder = car;
            Phase = CrownPhase.Held;
            // The player's crown is safe a moment longer, longer on Easy: time to see it and turn away.
            takenAt = Time.time;
            protectedUntil = Time.time + (car.IsPlayer
                ? Mathf.Lerp(settings.PlayerKeep.x, settings.PlayerKeep.y, difficulty.Level)
                : settings.StealCooldown);
            pressCar = null;
            Changes++;
            Version++;
            takes[round.IndexOf(car)]++;
            car.Movement.Tyres = FeelOf(car);

            var isPlayer = car.IsPlayer || from != null && from.IsPlayer;
            if (effects != null) {
                effects.PlayPickup(car.Position);
                effects.PlayKnockoutCredit(car.Position + Vector3.up * 0.6f);
            }

            if (audioService != null) {
                var near = isPlayer || IsNearPlayer(point);
                audioService.Play(audioService.Library.KnockoutCredit, near ? 1f : 0.45f);
                if (from != null) {
                    audioService.Play(audioService.Library.HitHeavy, near ? 0.9f : 0.4f);
                }
            }

            if (arenaCamera != null && isPlayer) {
                arenaCamera.Shake(from != null ? 0.4f : 0.2f);
            }

            Taken?.Invoke(car, from);
        }

        /// <summary>
        /// The pace a bot races to the loose crown at: the difficulty's, and no faster than it would take the player to
        /// drive there, so a bot that starts nearer the spot than the player (the stadium's sides against its end) does
        /// not take the first crown every round. Slower while it is still dropping.
        /// </summary>
        private float RacePace(Car car)
        {
            var pace = Mathf.Lerp(0.8f, 1f, difficulty.Level);
            var player = round.Player;
            if (player != null && player.IsTargetable) {
                var own = Flat(car.Position - Spot).magnitude;
                var players = Flat(player.Position - Spot).magnitude;
                if (players > own && players > 0.01f) {
                    pace *= Mathf.Max(0.5f, own / players);
                }
            }

            return Phase == CrownPhase.Loose ? pace : pace * 0.8f;
        }

        // Whether the bot has held the crown longest of all this round: one more bot goes after it (never more than
        // the most hunters), so one bot does not run away with the round while the others wait.
        private bool IsLeading(Car car)
        {
            var own = HeldOf(car);
            foreach (var other in round.Cars) {
                if (other != car && HeldOf(other) >= own) {
                    return false;
                }
            }

            return true;
        }

        // The holder's tyres: the crown's weight on a bot, the player as fast as its hunters.
        private SurfaceFeel FeelOf(Car car) => car.IsPlayer ? playerHolderFeel : holderFeel;

        /// <summary>
        /// A bot without the crown after its holder. While the player holds it, only the difficulty's one or two hunters
        /// go for it, after its first seconds, at the difficulty's pace. A bot's holder is chased by two or three: the
        /// nearest straight at it, the others at where it is going, every other one further ahead, so they close in from
        /// more than one side. While the holder cannot lose the crown yet they close in slowly instead of ramming it
        /// round the floor. The others keep their distance on free floor round it (<see cref="Wait"/>).
        /// </summary>
        private void Chase(Bot bot, BotPlay play, IArenaEdge floor, Vector3 position)
        {
            var holder = Holder;
            var holderPosition = Flat(holder.Position);
            var rank = RankNear(bot.Car, holder, out var chasers);
            var isPlayer = holder == round.Player;
            var hunters = isPlayer
                ? round.Elapsed < difficulty.PlayerGrace
                    ? 0
                    : Mathf.RoundToInt(Mathf.Lerp(settings.PlayerHunters.x, settings.PlayerHunters.y, difficulty.Level))
                : Mathf.Min(chasers, Mathf.Min(settings.BotHunters.y,
                    Mathf.RoundToInt(Mathf.Lerp(settings.BotHunters.x, settings.BotHunters.y, difficulty.Level))
                    + (IsLeading(holder) ? 1 : 0)));
            if (IsStealBack(bot.Car) || rank >= hunters) {
                Wait(bot, play, floor, position, holderPosition);
                return;
            }

            // Where the holder was at the bot's last look, carried on along its way, and a little off.
            if (Time.time >= play.NextLook) {
                var reaction = difficulty.ReactionTime;
                play.NextLook = Time.time + bot.Range(reaction.x, reaction.y);
                play.SeenPosition = holderPosition;
                play.SeenVelocity = Flat(holder.Movement.Velocity);
                play.SeenAt = Time.time;
                var error = difficulty.AimError * bot.Traits.AimError;
                play.AimError = bot.Range(-error, error);
            }

            var lead = rank == 0 ? settings.Lead.x : settings.Lead.y * (rank % 2 == 1 ? 1f : 0.6f);
            var seen = play.SeenPosition + play.SeenVelocity * (Time.time - play.SeenAt);
            var distance = (seen - position).magnitude;
            // Never further ahead than it takes to get there: a close chaser drives at the holder itself.
            var ahead = Mathf.Min(lead, distance / 11f);
            var aim = seen + play.SeenVelocity * ahead;
            if (floor.DistanceToEdge(aim, 1f) < 0f) {
                aim = seen;
            }

            var heading = Quaternion.AngleAxis(play.AimError, Vector3.up) * (aim - position);
            var pace = isPlayer
                ? Mathf.Lerp(settings.HunterPlayerPace.x, settings.HunterPlayerPace.y, difficulty.Level)
                : 1f;
            Drive(bot, play, floor, position, heading, IsProtected ? pace * settings.ProtectedPace : pace, aim);
        }

        /// <summary>
        /// A bot that does not hunt the holder keeps <see cref="GameTuning.CrownSettings.ShadowDistance"/> off it, on the
        /// free floor round it: of the directions round the holder that stand on the floor, the one farthest from the
        /// other cars, its own side breaking ties. So the waiting bots spread round the holder instead of piling up at
        /// it, and the one by the car that takes the crown next is near enough to cut in.
        /// </summary>
        private void Wait(Bot bot, BotPlay play, IArenaEdge floor, Vector3 position, Vector3 holderPosition)
        {
            if (Time.time >= play.ShadowAt || play.ShadowVersion != Version) {
                play.ShadowAt = Time.time + ShadowThink;
                play.ShadowVersion = Version;
                play.ShadowDirection = ShadowDirection(bot.Car, play, floor, position, holderPosition);
            }

            var spot = holderPosition + play.ShadowDirection * play.ShadowReach;
            if (floor.DistanceToEdge(spot, 1.5f) < 0f) {
                spot = floor.ClampToFloor(spot);
            }

            var toSpot = spot - position;
            var distance = toSpot.magnitude;
            Drive(bot, play, floor, position, toSpot, distance > 2f ? Mathf.Clamp(distance / 8f, 0.4f, 1f) : 0f);
        }

        private Vector3 ShadowDirection(Car self, BotPlay play, IArenaEdge floor, Vector3 position,
            Vector3 holderPosition)
        {
            var last = play.ShadowDirection;
            var own = position - holderPosition;
            own = own.sqrMagnitude > 0.01f ? own.normalized : Flat(self.transform.forward).normalized;
            var best = own;
            var bestReach = settings.ShadowDistance * 0.6f;
            var bestScore = float.NegativeInfinity;
            for (var slot = 0; slot < ShadowSlots; slot++) {
                var direction = Quaternion.AngleAxis(slot * (360f / ShadowSlots), Vector3.up) * Vector3.forward;
                var reach = settings.ShadowDistance;
                var point = holderPosition + direction * reach;
                // Short of the fence, a nearer place on the same side will do.
                if (floor.DistanceToEdge(point, 1.5f) < 0f) {
                    reach *= 0.6f;
                    point = holderPosition + direction * reach;
                    if (floor.DistanceToEdge(point, 1.5f) < 0f) {
                        continue;
                    }
                }

                var room = settings.ShadowDistance;
                foreach (var car in round.Cars) {
                    if (car == self || car == Holder || !car.IsTargetable) {
                        continue;
                    }

                    room = Mathf.Min(room, Flat(car.Position - point).magnitude);
                }

                var score = room + Vector3.Dot(direction, own) * 2f + Vector3.Dot(direction, last) * 1.5f
                    + reach / settings.ShadowDistance;
                if (score > bestScore) {
                    bestScore = score;
                    best = direction;
                    bestReach = reach;
                }
            }

            play.ShadowReach = bestReach;
            return best;
        }

        /// <summary>
        /// The bot with the crown runs: away from the cars near it (the nearer, the more), toward the floor that is
        /// widest ahead, sliding along the fence rather than into it. A fresh look a few times a second. From the player
        /// it runs at the difficulty's pace, so the player can catch it; with nobody near it drives on to open floor.
        /// </summary>
        private void Flee(Bot bot, BotPlay play, IArenaEdge floor, Vector3 position, float deltaTime)
        {
            if (Time.time >= play.NextLook || play.FleeHeading.sqrMagnitude < 0.01f) {
                play.NextLook = Time.time + Mathf.Min(FleeThink, bot.Range(difficulty.ReactionTime.x, difficulty.ReactionTime.y));
                play.FleeHeading = FleeHeading(bot.Car, floor, position, out play.FleePace);
            }

            Drive(bot, play, floor, position, play.FleeHeading * 6f, play.FleePace);
        }

        private Vector3 FleeHeading(Car self, IArenaEdge floor, Vector3 position, out float pace)
        {
            var away = Vector3.zero;
            Car nearest = null;
            var nearestDistance = float.PositiveInfinity;
            nearHolder.Clear();
            foreach (var car in round.Cars) {
                if (car == self || !car.IsTargetable) {
                    continue;
                }

                var offset = position - Flat(car.Position);
                var distance = offset.magnitude;
                if (distance > settings.FleeRadius || distance < 0.01f) {
                    continue;
                }

                nearHolder.Add(car);
                away += offset / (distance * distance);
                if (distance < nearestDistance) {
                    nearestDistance = distance;
                    nearest = car;
                }
            }

            var margin = settings.FenceMargin;
            if (nearest == null) {
                // Nobody near: on to the floor's widest part, slowly.
                pace = 0.5f;
                var inward = Flat(floor.Inward(position));
                return inward.sqrMagnitude > 0.01f && floor.DistanceToEdge(position, margin * 2f) < 0f
                    ? inward.normalized
                    : Flat(self.transform.forward).normalized;
            }

            pace = nearest == round.Player
                ? Mathf.Lerp(settings.FleePlayerPace.x, settings.FleePlayerPace.y, difficulty.Level)
                : 1f;
            away = away.normalized;
            var best = away;
            var bestScore = float.NegativeInfinity;
            for (var step = 0; step < 16; step++) {
                var option = Quaternion.AngleAxis(step * 22.5f, Vector3.up) * Vector3.forward;
                var room = floor.DistanceToEdge(position + option * margin, 0f);
                var far = floor.DistanceToEdge(position + option * margin * 2.5f, 0f);
                var score = Vector3.Dot(option, away) * 1.2f
                    + Mathf.Clamp(room, -margin, margin) / margin * 1.5f
                    + Mathf.Clamp(far, -margin, margin) / margin * 0.6f;
                // Not through a chaser: each one in the way counts against the way, the nearer the more.
                foreach (var car in nearHolder) {
                    var toCar = Flat(car.Position) - position;
                    var distance = toCar.magnitude;
                    var facing = Vector3.Dot(option, toCar / Mathf.Max(0.01f, distance));
                    if (facing > 0.5f) {
                        score -= (facing - 0.5f) * 2f * Mathf.Clamp01(1.2f - distance / settings.FleeRadius);
                    }
                }

                if (score > bestScore) {
                    bestScore = score;
                    best = option;
                }
            }

            return best;
        }

        // The bot's place among the cars in play by distance to the holder, robbed car and holder aside, and how many.
        // The player after a bot's crown counts too: a bot leaves its turn to the player nearer the holder than it is,
        // so the player is not beaten to every steal by the bots around it.
        private int RankNear(Car self, Car holder, out int count)
        {
            var own = Flat(self.Position - holder.Position).sqrMagnitude;
            var rank = 0;
            count = 0;
            foreach (var car in round.Cars) {
                if (car == holder || !car.IsTargetable || IsStealBack(car)) {
                    continue;
                }

                count++;
                if (car != self && Flat(car.Position - holder.Position).sqrMagnitude < own) {
                    rank++;
                }
            }

            return rank;
        }

        private void Drive(Bot bot, BotPlay play, IArenaEdge floor, Vector3 position, Vector3 heading, float pace,
            Vector3? aim = null)
        {
            play.LastAim = aim ?? position + heading;
            play.LastPace = pace;
            if (heading.sqrMagnitude < 0.04f) {
                bot.Car.Movement.Drive(Vector3.zero, 0f);
                return;
            }

            heading = PitDetour.Bend(floor, position, heading, botSettings.EdgeMargin * 0.5f, botSettings.PitProbe,
                botSettings.PitSamples, botSettings.PitInward, botSettings.PitSideHold, ref play.PitSide, ref play.PitHold);
            bot.Car.Movement.Drive(heading, pace);
        }

        private bool IsNearPlayer(Vector3 point)
        {
            var player = round.Player;
            return player != null && Flat(player.Position - point).sqrMagnitude < 15f * 15f;
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);

        /// <summary>A bot's own crown play: what it last saw and where it was going.</summary>
        private sealed class BotPlay
        {
            public int SeenVersion = -1;
            public float ReactAt;
            public float NextLook;
            public Vector3 SeenPosition;
            public Vector3 SeenVelocity;
            public float SeenAt;
            public float AimError;
            public Vector3 FleeHeading;
            public float FleePace = 1f;
            public Vector3 LastAim;
            public float LastPace;
            public float PitSide = 1f;
            public float PitHold;
            public Vector3 ShadowDirection;
            public float ShadowReach;
            public float ShadowAt;
            public int ShadowVersion = -1;
        }
    }
}
