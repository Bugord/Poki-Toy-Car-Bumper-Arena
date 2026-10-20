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
        [Tooltip("Real seconds the game nearly stops on a heavy hit with the player in it, and on a boosted one")]
        [SerializeField]
        private Vector2 hitStopDuration = new(0.06f, 0.08f);

        [Tooltip("How slow the game runs during a hit-stop, as a share of normal speed")]
        [Range(0f, 1f)]
        [SerializeField]
        private float hitStopTimeScale = 0.05f;

        [Tooltip("Real seconds after a hit-stop before the next one can come, so a pile-up does not stutter")]
        [Min(0f)]
        [SerializeField]
        private float hitStopGap = 0.25f;

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

        [Header("Sound")]
        [Tooltip("Volume of a bot's boost, against the player's")]
        [Range(0f, 1f)]
        [SerializeField]
        private float botBoostVolume = 0.35f;

        [Tooltip("The last this many seconds of a round tick, as the HUD's clock pulses")]
        [Min(0)]
        [SerializeField]
        private int countdownSeconds = 10;

        [Tooltip("Real seconds from the end of a round to its jingle, so the sound of the last fall is heard first")]
        [Min(0f)]
        [SerializeField]
        private float jingleDelay = 0.6f;

        private readonly HitStop hitStop = new();
        private readonly List<Car> cars = new();
        private readonly List<Action<CarHit>> hitHandlers = new();
        private readonly List<Action> boostHandlers = new();

        private ArenaRound round;
        private ArenaCrumble crumble;
        private ArenaCamera arenaCamera;
        private IAudioService audioService;
        private GameTuning.ArenaSettings arenaSettings;
        private EffectPlayer effects;
        private int shownSeconds = -1;
        private Sound pendingJingle;
        private float jingleTime;

        [Inject]
        private void Construct(ArenaRound round, ArenaCrumble crumble, ArenaCamera arenaCamera,
            IAudioService audioService, GameTuning tuning)
        {
            this.round = round;
            this.crumble = crumble;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            this.arenaSettings = tuning.Arena;
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
            crumble.RingWarning += OnRingWarning;
            crumble.RingFell += OnRingFell;
            audioService.PlayMusic();
        }

        private void Update()
        {
            hitStop.Tick();
            WatchNewCars();
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
                crumble.RingWarning -= OnRingWarning;
                crumble.RingFell -= OnRingFell;
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

            var isTick = shownSeconds >= 0 && seconds > 0 && seconds <= countdownSeconds;
            shownSeconds = seconds;
            if (isTick) {
                audioService.Play(audioService.Library.CountdownTick);
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
            var isHeavy = hit.Strength >= heavyStrength;
            var player = round.Player;
            var hasPlayer = player != null && (car == player || hit.Other == player);

            // The normal points from the hit car to the hitter; the effect sprays the way the hit car is pushed.
            effects.PlayHit(hit.Point, -hit.Normal, power, isHeavy, hit.IsBoosted);

            var library = audioService.Library;
            var volume = Mathf.Lerp(quietestHit, 1f, power) * (hasPlayer ? 1f : botHitVolume);
            audioService.Play(isHeavy ? library.HitHeavy : library.HitLight, volume);

            if (!isHeavy) {
                return;
            }

            var shake = hitShake * Mathf.Lerp(0.6f, 1f, power) * (hasPlayer ? 1f : botHitShake);
            arenaCamera.Shake(shake * (hit.IsBoosted ? boostedHitShake : 1f));

            // Only the player's hits stop the game: eight cars bump all the time, and the bots' hits would stutter it.
            if (hasPlayer && round.IsRunning) {
                hitStop.Stop(hit.IsBoosted ? hitStopDuration.y : hitStopDuration.x, hitStopTimeScale, hitStopGap);
            }
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
        /// sparkles over the splash and the knock-out sound.
        /// </summary>
        private void OnKnockedOut(Car car, Car knockedOutBy)
        {
            if (knockedOutBy == null || knockedOutBy != round.Player) {
                return;
            }

            effects.PlayKnockoutCredit(SplashPoint(car));
            audioService.Play(audioService.Library.KnockoutCredit);
        }

        /// <summary>
        /// Where a falling car's splash goes: over the spot it fell at, at the floor's height, and no further out than
        /// a little past the full arena, so a car flung far still splashes in view.
        /// </summary>
        private Vector3 SplashPoint(Car car)
        {
            var flat = car.Position;
            flat.y = 0f;
            flat = Vector3.ClampMagnitude(flat, arenaSettings.Radius + SplashReach);
            return flat + Vector3.up * SplashHeight;
        }

        /// <summary>
        /// The jingle of the round's end, a moment later. Running out the clock still on the arena is no loss, so it
        /// gets the win's.
        /// </summary>
        private void OnFinished(RoundResult result)
        {
            var library = audioService.Library;
            pendingJingle = result.Outcome == RoundOutcome.Out ? library.Lose : library.Win;
            jingleTime = Time.unscaledTime + jingleDelay;
        }

        private void OnRingWarning(float innerRadius, float outerRadius)
        {
            effects.PlayRingWarning(innerRadius, outerRadius, arenaSettings.CrumbleWarning);
            audioService.Play(audioService.Library.CrumbleWarning);
        }

        private void OnRingFell(float innerRadius, float outerRadius)
        {
            effects.PlayRingFall(innerRadius, outerRadius);
            audioService.Play(audioService.Library.CrumbleFall);
            arenaCamera.Shake(ringFallShake);
        }
    }
}
