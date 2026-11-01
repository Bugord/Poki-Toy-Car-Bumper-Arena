using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// One car's boxing glove of <see cref="ItemGloves"/>: how far into its punch it is and its look.
    /// </summary>
    internal sealed class GlovePunch
    {
        public Car User;
        public float Time;
        public bool IsActive;
        public bool HasHit;
        // How far out the fist was when it hit, so it goes back from there.
        public float HitReach;
        public float HitTime;

        // The look, built by PickupArt.
        public Transform Fist;
        public Transform Arm;
    }

    /// <summary>
    /// The boxing gloves of a long match. Used, a glove shoots out of the car's nose on a spring to
    /// <see cref="GameTuning.GloveSettings.Reach"/> in <see cref="GameTuning.GloveSettings.ExtendTime"/>, holds a moment
    /// and snaps back. The first car on its way (the nearest to the nose) is punched along the punch, credited to the
    /// user, and the glove goes straight back; a shield stops it the same way. Hit-tested by distance to the segment it
    /// covers each frame, so a fast punch cannot pass through a car. It follows its car's nose as it moves.
    /// </summary>
    internal sealed class ItemGloves
    {
        private const int PoolSize = 4;
        // Metres in front of the car's middle the arm starts, and above the floor it is held.
        private const float NoseAhead = 1.1f;
        private const float Height = 0.45f;
        private const float HitDepth = -1f;
        private const float HitHeight = 1.5f;

        private readonly GameTuning tuning;
        private readonly GameTuning.GloveSettings settings;
        private readonly ArenaRound round;
        private readonly ItemBlow blow;
        private readonly EffectPlayer effects;
        private readonly IAudioService audioService;
        private readonly ArenaCamera arenaCamera;
        private readonly GlovePunch[] gloves = new GlovePunch[PoolSize];

        public ItemGloves(GameTuning tuning, ArenaRound round, ItemBlow blow, EffectPlayer effects,
            IAudioService audioService, ArenaCamera arenaCamera, PickupArt art, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups.Glove;
            this.round = round;
            this.blow = blow;
            this.effects = effects;
            this.audioService = audioService;
            this.arenaCamera = arenaCamera;
            for (var i = 0; i < gloves.Length; i++) {
                gloves[i] = new GlovePunch();
                art.BuildGlove(parent, gloves[i]);
            }
        }

        /// <summary>Whether <paramref name="car"/>'s glove is out right now.</summary>
        public bool IsPunching(Car car)
        {
            foreach (var glove in gloves) {
                if (glove.IsActive && glove.User == car) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Punches from <paramref name="user"/>'s nose; false when every glove is out already.</summary>
        public bool Punch(Car user)
        {
            GlovePunch free = null;
            foreach (var glove in gloves) {
                if (glove.IsActive && glove.User == user) {
                    return false;
                }

                if (!glove.IsActive && free == null) {
                    free = glove;
                }
            }

            if (free == null) {
                return false;
            }

            free.User = user;
            free.Time = 0f;
            free.HasHit = false;
            free.IsActive = true;
            free.Fist.gameObject.SetActive(true);
            free.Arm.gameObject.SetActive(true);
            Pose(free, 0f);
            audioService?.Play(audioService.Library.Boost, user.IsPlayer ? 0.8f : 0.35f);
            return true;
        }

        public void Tick(float deltaTime)
        {
            foreach (var glove in gloves) {
                if (!glove.IsActive) {
                    continue;
                }

                if (glove.User == null || glove.User.IsOut) {
                    Free(glove);
                    continue;
                }

                var before = ReachAt(glove, glove.Time);
                glove.Time += deltaTime;
                if (glove.Time >= LengthOf(glove)) {
                    Free(glove);
                    continue;
                }

                var reach = ReachAt(glove, glove.Time);
                if (!glove.HasHit && glove.Time <= settings.ExtendTime + settings.HoldTime) {
                    TryHit(glove, Mathf.Max(before, reach));
                }

                Pose(glove, ReachAt(glove, glove.Time));
            }
        }

        /// <summary>Takes every glove back at once: the match is over.</summary>
        public void Clear()
        {
            foreach (var glove in gloves) {
                if (glove.IsActive) {
                    Free(glove);
                }
            }
        }

        // How far out the fist is, in metres from the nose: out fast with an ease, held, then back.
        private float ReachAt(GlovePunch glove, float time)
        {
            if (glove.HasHit) {
                var back = Mathf.Clamp01((time - glove.HitTime) / settings.RetractTime);
                return glove.HitReach * (1f - back * back);
            }

            if (time < settings.ExtendTime) {
                var share = time / settings.ExtendTime;
                return settings.Reach * (1f - (1f - share) * (1f - share));
            }

            time -= settings.ExtendTime + settings.HoldTime;
            if (time < 0f) {
                return settings.Reach;
            }

            var share2 = Mathf.Clamp01(time / settings.RetractTime);
            return settings.Reach * (1f - share2 * share2);
        }

        private float LengthOf(GlovePunch glove)
        {
            return glove.HasHit ? glove.HitTime + settings.RetractTime : settings.Length;
        }

        /// <summary>
        /// Finds the first car within <see cref="GameTuning.GloveSettings.HitRadius"/> of the arm out to
        /// <paramref name="reach"/> and punches it; a shield stops the glove too.
        /// </summary>
        private void TryHit(GlovePunch glove, float reach)
        {
            var user = glove.User;
            var forward = Forward(user);
            var nose = Flat(user.Position) + forward * NoseAhead;
            Car first = null;
            var firstAlong = float.MaxValue;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == user || car.IsOut || !car.IsTargetable) {
                    continue;
                }

                var dy = car.Position.y - user.Position.y;
                if (dy < HitDepth || dy > HitHeight) {
                    continue;
                }

                var offset = Flat(car.Position) - nose;
                var along = Vector3.Dot(offset, forward);
                if (along < -settings.HitRadius || along > reach + settings.HitRadius) {
                    continue;
                }

                var side = (offset - forward * Mathf.Clamp(along, 0f, reach)).magnitude;
                if (side > settings.HitRadius || along >= firstAlong) {
                    continue;
                }

                first = car;
                firstAlong = along;
            }

            if (first == null) {
                return;
            }

            glove.HasHit = true;
            glove.HitTime = glove.Time;
            glove.HitReach = Mathf.Clamp(firstAlong, 0f, reach);
            var point = first.Position;
            var cap = tuning.Car.MaxBumpSpeed * settings.MaxSpeedShare;
            if (!blow.Apply(first, user, forward, settings.Push, cap, settings.Lift, settings.Stagger)) {
                // The shield took it: a dull bounce, no stars.
                audioService?.Play(audioService.Library.HitLight, user.IsPlayer || first.IsPlayer ? 0.8f : 0.35f);
                return;
            }

            user.ReportItemHit(ItemKind.Glove, point);
            var isPlayer = user.IsPlayer || first.IsPlayer;
            if (effects != null) {
                effects.PlayHit(point, -forward, 1f, true, true);
            }

            audioService?.Play(audioService.Library.HitHeavy, isPlayer ? 1f : 0.45f);
            if (arenaCamera != null && isPlayer) {
                arenaCamera.Shake(0.5f);
            }
        }

        // The fist at the end of the arm, the arm from the nose to the fist's cuff.
        private static void Pose(GlovePunch glove, float reach)
        {
            var user = glove.User;
            var forward = Forward(user);
            var position = user.Position;
            var nose = new Vector3(position.x, position.y + Height, position.z) + forward * NoseAhead;
            var rotation = Quaternion.LookRotation(forward);
            glove.Fist.SetPositionAndRotation(nose + forward * (reach + 0.4f), rotation);
            glove.Arm.SetPositionAndRotation(nose, rotation);
            glove.Arm.localScale = new Vector3(1f, 1f, Mathf.Max(0.05f, reach));
        }

        private static void Free(GlovePunch glove)
        {
            glove.IsActive = false;
            glove.User = null;
            glove.Fist.gameObject.SetActive(false);
            glove.Arm.gameObject.SetActive(false);
        }

        private static Vector3 Forward(Car car)
        {
            var forward = Flat(car.transform.forward);
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
