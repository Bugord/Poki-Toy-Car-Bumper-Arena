using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// What a hazard of the arena does to a car it touches, the way the Ball's ball does it: a push that never leaves the
    /// car faster than a car's own hit may, a stagger, and the hit marked as nobody's (<see cref="Car.HitByHazard"/>), so a
    /// car it sends over the edge is no one's knockout. The player's share is eased on the easiest rounds.
    /// </summary>
    public sealed class HazardBlow
    {
        private readonly GameTuning tuning;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly EffectPlayer effects;

        public HazardBlow(GameTuning tuning, RoundDifficulty difficulty, ArenaCamera arenaCamera, IAudioService audioService)
        {
            this.tuning = tuning;
            this.difficulty = difficulty;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
            // The effects live in the scene, not the container; one look is enough.
            this.effects = Object.FindAnyObjectByType<EffectPlayer>();
        }

        /// <summary>
        /// Pushes <paramref name="car"/> along the flat <paramref name="direction"/> by <paramref name="push"/> (plus
        /// <paramref name="up"/> metres per second upwards) and staggers it. <paramref name="easyShare"/> is the share of it
        /// the player takes on the easiest round.
        /// </summary>
        public void Apply(Car car, Vector3 direction, float push, float up, float stagger, float easyShare, float power = 0.85f)
        {
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var share = car.IsPlayer ? Mathf.Lerp(easyShare, 1f, difficulty.Level) : 1f;
            push *= share;
            var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
            push = Mathf.Min(push, Mathf.Max(0f, tuning.Car.MaxBumpSpeed - speedAway));
            car.Movement.Push(direction * push + Vector3.up * (up * share));
            car.Movement.Stagger(stagger * share);
            car.HitByHazard();

            if (effects != null) {
                effects.PlayHit(car.Position, direction, power, true, false);
            }

            if (arenaCamera != null) {
                arenaCamera.Shake(car.IsPlayer ? 0.6f : 0.15f);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.HitHeavy, car.IsPlayer ? 0.9f : 0.45f);
            }
        }
    }
}
