using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Effects
{
    /// <summary>
    /// Makes what happens on the arena felt: for every hit, boost, car out and crumble it picks the effect
    /// (<see cref="EffectPlayer"/>), the sound (<see cref="IAudioService"/>), the camera shake and, for the player's
    /// hard hits, the hit-stop, all scaled by how strong the moment is. It also starts the music, ticks the last seconds
    /// of a round in time with the HUD, and plays the win or lose jingle. The gameplay knows nothing of it: it only
    /// listens to the round's, the cars' and the crumble's events.
    /// <para>
    /// The numbers here are feel, not rules: they change no outcome, so they live on this component in the Game scene
    /// with the looks, not in <see cref="GameTuning"/>. The hit-stop slows the game clock for a few hundredths of a
    /// second, which the round's time and the bots barely notice.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(EffectPlayer))]
    public sealed class ArenaFeedback : MonoBehaviour
    {
        // Height above the floor of the splash where a car goes over, and how far past the full arena's edge it may be,
        // in metres.
        private const float SplashHeight = 0.3f;
        private const float SplashReach = 1.5f;

        [Header("Hits")]
        [Tooltip("Hits closing slower than this, in metres per second, are touches: no effect, no sound")]
        [Min(0f)]
        [SerializeField]
        private float minStrength = 1.5f;

        [Tooltip("From this closing speed on, in metres per second, a hit is heavy: the heavy sound, the flash, the " +
            "camera shake and, with the player in it, the hit-stop. A car at top speed into a still one closes at 10")]
        [Min(0f)]
        [SerializeField]
        private float heavyStrength = 9f;

        [Tooltip("Hits closing this fast or faster, in metres per second, get the biggest effect and the loudest sound")]
        [Min(0.1f)]
        [SerializeField]
        private float fullStrength = 18f;

        [Tooltip("Volume of the softest hit that makes a sound, the strongest being at full volume")]
        [Range(0f, 1f)]
        [SerializeField]
        private float quietestHit = 0.35f;

        [Tooltip("Volume of a hit between two bots, against one with the player in it")]
        [Range(0f, 1f)]
        [SerializeField]
        private float botHitVolume = 0.6f;

        [Header("Hit-stop")]
        [Tooltip("Hits with the player in them, given or taken, from this closing speed on, in metres per second, " +
            "freeze the game for a moment: only the strong ones, so it reads as a hit and not as a stutter. A car at " +
            "top speed into a still one closes at 11")]
        [Min(0f)]
        [SerializeField]
        private float playerStopStrength = 8f;

        [Tooltip("Real seconds the game freezes on such a hit, from the weakest that freezes to a full one: a few " +
            "frames, longer would feel like the game lagging")]
        [SerializeField]
        private Vector2 playerStopDuration = new(0.04f, 0.06f);

        [Tooltip("How slow the game runs during a hit's freeze, as a share of normal speed")]
        [Range(0f, 1f)]
        [SerializeField]
        private float hitStopTimeScale = 0.02f;

        [Tooltip("Real seconds after the player's freeze before the next one can come, so a pile-up does not stutter")]
        [Min(0f)]
        [SerializeField]
        private float hitStopGap = 0.25f;

        [Tooltip("Shake of a hit that freezes the game, from 0 (none) to 1 (the camera's most), at full strength")]
        [Range(0f, 1f)]
        [SerializeField]
        private float stopShake = 0.75f;

        [Tooltip("Shake of any other hit with the player in it, at full strength; a weak one gets a quarter")]
        [Range(0f, 1f)]
        [SerializeField]
        private float playerHitShake = 0.35f;

        [Tooltip("How flat the hit car goes for a moment, as a share of its height: the weakest real hit, a full one")]
        [SerializeField]
        private Vector2 hitSquash = new(0.12f, 0.3f);

        [Tooltip("Pitch of the player's hit sound, from the weakest hit to a full one: harder sounds deeper")]
        [SerializeField]
        private Vector2 bonkPitch = new(1.15f, 0.85f);

        [Header("Player's knock-out")]
        [Tooltip("Real seconds the game runs slow when a car the player pushed goes over, so the player sees it fall")]
        [Min(0f)]
        [SerializeField]
        private float knockoutSlowDuration = 0.35f;

        [Tooltip("How slow the game runs as a car the player pushed goes over, as a share of normal speed")]
        [Range(0f, 1f)]
        [SerializeField]
        private float knockoutSlowTimeScale = 0.3f;

        [Tooltip("Shake when a car the player pushed goes over, on top of the shake of any car going over")]
        [Range(0f, 1f)]
        [SerializeField]
        private float knockoutCreditShake = 0.5f;

        [Tooltip("How much higher each knock-out of a combo sounds than the one before, as a share of the normal pitch")]
        [Min(0f)]
        [SerializeField]
        private float comboPitchStep = 0.12f;

        [Tooltip("Highest pitch a combo's knock-out climbs to")]
        [Min(1f)]
        [SerializeField]
        private float comboMaxPitch = 1.5f;

        [Header("Camera shake")]
        [Tooltip("Shake of a heavy hit with the player in it, from 0 (none) to 1 (the camera's most)")]
        [Range(0f, 1f)]
        [SerializeField]
        private float hitShake = 0.45f;

        [Tooltip("Share of that shake a heavy hit between two bots gets")]
        [Range(0f, 1f)]
        [SerializeField]
        private float botHitShake = 0.35f;

        [Tooltip("How much more a boosted hit shakes")]
        [Min(1f)]
        [SerializeField]
        private float boostedHitShake = 1.4f;

        [Tooltip("Shake when a bot goes over the edge, and when the player's car does")]
        [SerializeField]
        private Vector2 knockoutShake = new(0.3f, 0.7f);

        [Tooltip("Shake when a ring of the edge falls")]
        [Range(0f, 1f)]
        [SerializeField]
        private float ringFallShake = 0.3f;

        [Tooltip("Shake when a wave of the floor lands back in place; the waves land one after another, so it is light")]
        [Range(0f, 1f)]
        [SerializeField]
        private float landShake = 0.12f;

        [Header("Wheel smoke")]
        [Tooltip("Sideways slide, in metres per second, at which smoke starts to come from under the back wheels: a "
            + "sharp turn at speed, or a car shoved sideways")]
        [Min(0f)]
        [SerializeField]
        private float skidFrom = 3.5f;

        [Tooltip("Sideways slide, in metres per second, at which the smoke is at its thickest")]
        [Min(0.1f)]
        [SerializeField]
        private float skidFull = 8f;

        [Tooltip("Below this speed forward, in metres per second, a car driven hard spins its wheels and smokes: the "
            + "start at GO!, a car pushing against another")]
        [Min(0f)]
        [SerializeField]
        private float spinSpeed = 3.5f;

        [Tooltip("Least throttle, 0 to 1, that spins the wheels of a slow car")]
        [Range(0f, 1f)]
        [SerializeField]
        private float spinThrottle = 0.6f;

        [Tooltip("Seconds between puffs from a light skid and from a full one")]
        [SerializeField]
        private Vector2 smokeInterval = new(0.14f, 0.06f);

        [Tooltip("How far out to each side of the middle, and behind it, the back wheels are, in metres")]
        [SerializeField]
        private Vector2 rearWheels = new(0.55f, 0.95f);

        [Tooltip("A car off the floor for longer than this, in seconds, throws up dust as it lands")]
        [Min(0f)]
        [SerializeField]
        private float landingAir = 0.3f;

        [Header("Sound")]
        [Tooltip("Volume of a bot's boost, against the player's")]
        [Range(0f, 1f)]
        [SerializeField]
        private float botBoostVolume = 0.35f;

        [Tooltip("Volume of the thud of a wave of the floor landing back in place, against a wave falling; a few land in " +
            "a row")]
        [Range(0f, 1f)]
        [SerializeField]
        private float landVolume = 0.6f;

        [Tooltip("The last this many seconds of a round tick, as the HUD's clock pulses")]
        [Min(0)]
        [SerializeField]
        private int countdownSeconds = 10;

        [Tooltip("The last this many seconds of a match tick louder, as the HUD's bar beats bigger")]
        [Min(0)]
        [SerializeField]
        private int finalSeconds = 3;

        [Tooltip("Volume of the tick of the last seconds of a match, against the usual tick (the source caps it at full)")]
        [Min(1f)]
        [SerializeField]
        private float finalTickVolume = 1.6f;

        [Tooltip("A match whistles once with this many seconds left, as the HUD shows its note")]
        [Min(0)]
        [SerializeField]
        private int warningSeconds = 30;

        [Header("Time up")]
        [Tooltip("Real seconds the cars freeze as a match's clock runs out, under the TIME UP! stamp")]
        [Min(0f)]
        [SerializeField]
        private float timeUpStop = 0.3f;

        [Tooltip("Time scale while the cars freeze at the end of a match")]
        [Range(0f, 1f)]
        [SerializeField]
        private float timeUpTimeScale = 0.02f;

        [Tooltip("Real seconds from the end of a round to its jingle, so the sound of the last fall is heard first")]
        [Min(0f)]
        [SerializeField]
        private float jingleDelay = 0.6f;

        private readonly HitStop hitStop = new();
        private readonly List<Car> cars = new();
        private readonly List<Action<CarHit>> hitHandlers = new();
        private readonly List<Action> boostHandlers = new();
        private readonly List<float> nextSmoke = new();
        private readonly List<float> airSince = new();

        private ArenaRound round;
        private ArenaCrumble crumble;
        private ArenaCamera arenaCamera;
        private IAudioService audioService;
        private IArenaEdge edge;
        private EffectPlayer effects;
        private int shownSeconds = -1;
        private Sound pendingJingle;
        private float jingleTime;

        [Inject]
        private void Construct(ArenaRound round, ArenaCrumble crumble, ArenaCamera arenaCamera,
            IAudioService audioService, IArenaEdge edge)
        {
            this.round = round;
            this.crumble = crumble;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.edge = edge;
        }

        private void Awake()
        {
            effects = GetComponent<EffectPlayer>();
        }

        private void Start()
        {
            round.CarOut += OnCarOut;
            round.KnockedOut += OnKnockedOut;
            round.Finished += OnFinished;
            crumble.WaveWarning += OnWaveWarning;
            crumble.WaveFell += OnWaveFell;
            crumble.FloorReturning += OnFloorReturning;
            crumble.WaveReturning += OnWaveReturning;
            crumble.WaveLanded += OnWaveLanded;
            audioService.PlayMusic();
        }

        private void Update()
        {
            hitStop.Tick();
            WatchNewCars();
            TickWheels();
            TickCountdown();

            if (pendingJingle != null && Time.unscaledTime >= jingleTime) {
                audioService.PlayJingle(pendingJingle);
                pendingJingle = null;
            }
        }

        private void OnDisable()
        {
            // Never leave the game slowed, whatever takes this away mid-stop.
            hitStop.Release();
        }

        private void OnDestroy()
        {
            if (round != null) {
                round.CarOut -= OnCarOut;
                round.KnockedOut -= OnKnockedOut;
                round.Finished -= OnFinished;
            }

            if (crumble != null) {
                crumble.WaveWarning -= OnWaveWarning;
                crumble.WaveFell -= OnWaveFell;
                crumble.FloorReturning -= OnFloorReturning;
                crumble.WaveReturning -= OnWaveReturning;
                crumble.WaveLanded -= OnWaveLanded;
            }

            for (var i = 0; i < cars.Count; i++) {
                if (cars[i] != null) {
                    cars[i].Hit -= hitHandlers[i];
                    cars[i].Movement.Boosted -= boostHandlers[i];
                }
            }
        }

        /// <summary>
        /// Listens to the cars the round has placed since the last frame; the round makes them when it begins.
        /// </summary>
        private void WatchNewCars()
        {
            var roundCars = round.Cars;
            for (var i = cars.Count; i < roundCars.Count; i++) {
                var car = roundCars[i];
                Action<CarHit> onHit = hit => OnHit(car, hit);
                Action onBoosted = () => OnBoosted(car);
                car.Hit += onHit;
                car.Movement.Boosted += onBoosted;
                cars.Add(car);
                hitHandlers.Add(onHit);
                boostHandlers.Add(onBoosted);
                nextSmoke.Add(0f);
                airSince.Add(float.NaN);
            }
        }

        /// <summary>
        /// Smoke from under the wheels, where it tells the player something: a car sliding sideways (a sharp turn at
        /// speed, or shoved by a hit) leaves a trail from its back wheels, a slow car driven hard spins them (the start
        /// at GO!, a car pushing against another), and a car landing after a jump throws up a ring of dust.
        /// </summary>
        private void TickWheels()
        {
            var now = Time.time;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == null || car.IsOut || !car.isActiveAndEnabled || car.Movement.IsCarried) {
                    airSince[i] = float.NaN;
                    continue;
                }

                var movement = car.Movement;
                if (!movement.IsGrounded) {
                    if (float.IsNaN(airSince[i])) {
                        airSince[i] = now;
                    }

                    continue;
                }

                var position = car.Position;
                if (!float.IsNaN(airSince[i])) {
                    var air = now - airSince[i];
                    airSince[i] = float.NaN;
                    if (air > landingAir) {
                        effects.PlayLanding(position + Vector3.up * 0.2f, Mathf.InverseLerp(landingAir, 1f, air));
                    }
                }

                if (!round.HasBegun || now < nextSmoke[i]) {
                    continue;
                }

                var forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                var velocity = movement.Velocity;
                var slide = Mathf.InverseLerp(skidFrom, skidFull, Mathf.Abs(Vector3.Dot(velocity, right)));
                var ahead = Vector3.Dot(velocity, forward);
                var spin = movement.Throttle >= spinThrottle && ahead < spinSpeed
                    ? 0.6f * (1f - Mathf.Max(0f, ahead) / Mathf.Max(spinSpeed, 0.01f))
                    : 0f;
                var share = Mathf.Max(slide, spin);
                if (share <= 0f) {
                    continue;
                }

                nextSmoke[i] = now + Mathf.Lerp(smokeInterval.x, smokeInterval.y, share);
                var back = position - forward * rearWheels.y + Vector3.up * 0.2f;
                effects.PlayWheelSmoke(back + right * rearWheels.x, velocity, share);
                effects.PlayWheelSmoke(back - right * rearWheels.x, velocity, share);
            }
        }

        /// <summary>
        /// Ticks each of the last seconds as the HUD's clock shows it, rounded up, so the tick and the clock's pulse
        /// come together.
        /// </summary>
        private void TickCountdown()
        {
            if (!round.IsRunning) {
                return;
            }

            var seconds = Mathf.CeilToInt(round.TimeLeft);
            if (seconds == shownSeconds) {
                return;
            }

            var isCrossing = shownSeconds >= 0;
            var isWarning = isCrossing && round.IsTimed && seconds == warningSeconds && shownSeconds > warningSeconds;
            var isTick = isCrossing && seconds > 0 && seconds <= countdownSeconds;
            shownSeconds = seconds;
            if (isWarning) {
                audioService.Play(audioService.Library.TimeWarning);
            }

            if (isTick) {
                var isFinal = round.IsTimed && seconds <= finalSeconds;
                audioService.Play(audioService.Library.CountdownTick, isFinal ? finalTickVolume : 1f);
            }
        }

        /// <summary>
        /// A hit, once: both cars report it, and only the hitter's report is taken.
        /// </summary>
        private void OnHit(Car car, CarHit hit)
        {
            if (!hit.IsHitter || hit.Strength < minStrength) {
                return;
            }

            var power = Mathf.InverseLerp(minStrength, fullStrength, hit.Strength);
            var player = round.Player;
            var hasPlayer = player != null && (car == player || hit.Other == player);

            // Only a strong hit with the player in it freezes the game, for a few frames; bots bump all the time, and
            // their hits or a soft one freezing read as the game lagging.
            var isStop = hasPlayer && hit.Strength >= playerStopStrength && round.IsRunning;
            var isHeavy = hit.Strength >= heavyStrength || isStop;

            // The normal points from the hit car to the hitter; the effect sprays the way the hit car is pushed. A hit
            // that freezes the game gets the bigger, boosted burst.
            effects.PlayHit(hit.Point, -hit.Normal, power, isHeavy, hit.IsBoosted || isStop);
            if (hit.Other != null && hit.Other.TryGetComponent<CarLook>(out var look)) {
                look.Squash(Mathf.Lerp(hitSquash.x, hitSquash.y, power) * (hasPlayer ? 1f : 0.6f));
            }

            // The player's hits are a toy's bonk, deeper the harder; two bots bumping only tap, so the player's own
            // hits stand out.
            var library = audioService.Library;
            var volume = Mathf.Lerp(quietestHit, 1f, power);
            if (hasPlayer) {
                audioService.Play(library.CarBonk, volume, Mathf.Lerp(bonkPitch.x, bonkPitch.y, power));
            }
            else {
                audioService.Play(library.CarTap, volume * botHitVolume);
            }

            if (isStop) {
                arenaCamera.Shake(stopShake * Mathf.Lerp(0.6f, 1f, power));
                hitStop.Stop(Mathf.Lerp(playerStopDuration.x, playerStopDuration.y, power), hitStopTimeScale,
                    hitStopGap);
                return;
            }

            if (hasPlayer) {
                arenaCamera.Shake(playerHitShake * Mathf.Lerp(0.25f, 1f, power) * (hit.IsBoosted ? boostedHitShake : 1f));
                return;
            }

            if (!isHeavy) {
                return;
            }

            var shake = hitShake * Mathf.Lerp(0.6f, 1f, power) * botHitShake;
            arenaCamera.Shake(shake * (hit.IsBoosted ? boostedHitShake : 1f));
        }

        private void OnBoosted(Car car)
        {
            effects.PlayBoost(car);
            audioService.Play(audioService.Library.Boost, car == round.Player ? 1f : botBoostVolume);
        }

        /// <summary>
        /// A car fell: the splash where it went over and its sound.
        /// </summary>
        private void OnCarOut(Car car)
        {
            var isPlayer = car == round.Player;
            effects.PlayKnockout(SplashPoint(car), isPlayer);

            var library = audioService.Library;
            audioService.Play(isPlayer ? library.KnockoutPlayer : library.Knockout);
            arenaCamera.Shake(isPlayer ? knockoutShake.y : knockoutShake.x);
        }

        /// <summary>
        /// A car fell soon after another drove into it (the round's knock-out credit): when the player did it, gold
        /// sparkles over the splash, the knock-out sound (higher for each of a combo), a harder shake and a moment of slow motion, so the player's
        /// own win is the best felt thing on the arena. The slow motion takes over any hit-stop still on: this is the
        /// moment that counts.
        /// </summary>
        private void OnKnockedOut(Car car, Car knockedOutBy)
        {
            if (knockedOutBy == null || knockedOutBy != round.Player) {
                return;
            }

            effects.PlayKnockoutCredit(SplashPoint(car));
            var pitch = Mathf.Min(comboMaxPitch, 1f + comboPitchStep * Mathf.Max(0, round.PlayerCombo - 1));
            audioService.Play(audioService.Library.KnockoutCredit, 1f, pitch);
            arenaCamera.Shake(knockoutCreditShake);
            if (round.IsRunning) {
                hitStop.Hold(knockoutSlowDuration, knockoutSlowTimeScale);
            }
        }

        /// <summary>
        /// Where a falling car's splash goes: over the spot it fell at, at the floor's height, and no further out than
        /// a little past the full arena, so a car flung far still splashes in view.
        /// </summary>
        private Vector3 SplashPoint(Car car)
        {
            var flat = car.Position;
            flat.y = 0f;
            flat = Vector3.ClampMagnitude(flat, edge.FullExtent + SplashReach);
            return flat + Vector3.up * SplashHeight;
        }

        /// <summary>
        /// The jingle of the round's end, a moment later. Running out the clock still on the arena is no loss, so it
        /// gets the win's; a match placed low is one (<see cref="RoundResult.IsLoss"/>).
        /// </summary>
        private void OnFinished(RoundResult result)
        {
            var library = audioService.Library;
            if (result.IsTimed) {
                // The buzzer: the whistle, and every car frozen a moment under the HUD's TIME UP! stamp.
                audioService.Play(library.TimeUp);
                hitStop.Hold(timeUpStop, timeUpTimeScale);
            }
            else {
                // The round's last moments play out at full speed, and nothing slowed is left under the result.
                hitStop.Release();
            }

            pendingJingle = result.IsLoss ? library.Lose : library.Win;
            jingleTime = Time.unscaledTime + jingleDelay;
        }

        private void OnWaveWarning(CrumbleWave wave)
        {
            if (wave.Spots != null) {
                effects.PlayTileWarning(wave.Spots, crumble.Warning);
            }
            else {
                effects.PlayRingWarning(wave.Inner, wave.Outer, crumble.Warning);
            }

            audioService.Play(audioService.Library.CrumbleWarning);
        }

        private void OnWaveFell(CrumbleWave wave)
        {
            if (wave.Spots != null) {
                effects.PlayTileFall(wave.Spots);
            }
            else {
                effects.PlayRingFall(wave.Inner, wave.Outer);
            }

            audioService.Play(audioService.Library.CrumbleFall);
            arenaCamera.Shake(ringFallShake);
        }

        /// <summary>
        /// The floor of a cycle about to come back: one rumble for all its waves, which appear together.
        /// </summary>
        private void OnFloorReturning(int cycle)
        {
            audioService.Play(audioService.Library.CrumbleWarning);
        }

        /// <summary>
        /// A wave hanging over its place: gold rings close on the empty spots it will land on.
        /// </summary>
        private void OnWaveReturning(CrumbleWave wave, float landIn)
        {
            if (wave.Spots != null) {
                effects.PlayTileReturn(wave.Spots, landIn);
            }
            else {
                effects.PlayRingReturn(wave.Inner, wave.Outer, landIn);
            }
        }

        private void OnWaveLanded(CrumbleWave wave)
        {
            if (wave.Spots != null) {
                effects.PlayTileLand(wave.Spots);
            }
            else {
                effects.PlayRingLand(wave.Inner, wave.Outer);
            }

            audioService.Play(audioService.Library.CrumbleFall, landVolume);
            arenaCamera.Shake(landShake);
        }
    }
}
