using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// One ball of <see cref="ItemBowls"/>: where it is in its life and its look.
    /// </summary>
    internal sealed class RolledBall
    {
        public enum Stage
        {
            Free,
            Rolling,
            Falling,
        }

        public Stage State;
        public Car Roller;
        public Vector3 Position;
        public Vector3 Direction;
        public Vector3 Velocity;
        public float Travelled;
        // The cars it has hit on this roll, by their index in the round.
        public int HitMask;

        // The look, built by PickupArt.
        public Transform Root;
        public Transform Spin;
    }

    /// <summary>
    /// The bowling balls of a match. A ball leaves the car's nose and rolls straight along its heading at
    /// <see cref="GameTuning.BowlingSettings.Speed"/>, over shaking tiles too; every other car near its line is knocked
    /// aside once, harder away from the line than along it, credited to the roller (<see cref="ItemBlow"/>), and the
    /// ball rolls on, so a line of cars goes down like pins. Off the floor (past the rim or over a hole) it falls away;
    /// it fades once it has rolled its range. No physics: it is moved here, and hits by the distance from each car to the
    /// stretch it rolled this frame, so a fast ball does not jump a car. The bots do not dodge it, as they do not dodge a
    /// bomb: a strike is the roller's to earn.
    /// </summary>
    internal sealed class ItemBowls
    {
        private const int PoolSize = 4;
        private const float Gravity = 22f;
        private const float GoneHeight = -14f;
        // Metres in front of the car's middle the ball leaves from.
        private const float LaunchAhead = 1.8f;
        // Cars this far below or above the floor are not hit: one falling, or one flying over it.
        private const float HitDepth = -1f;
        private const float HitHeight = 2.5f;
        private const float FadeTime = 0.25f;

        private readonly GameTuning tuning;
        private readonly GameTuning.BowlingSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly ItemBlow blow;
        private readonly EffectPlayer effects;
        private readonly IAudioService audioService;
        private readonly ArenaCamera arenaCamera;
        private readonly RolledBall[] balls = new RolledBall[PoolSize];

        public ItemBowls(GameTuning tuning, ArenaRound round, IArenaEdge edge, ItemBlow blow, EffectPlayer effects,
            IAudioService audioService, ArenaCamera arenaCamera, PickupArt art, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups.Bowling;
            this.round = round;
            this.edge = edge;
            this.blow = blow;
            this.effects = effects;
            this.audioService = audioService;
            this.arenaCamera = arenaCamera;
            for (var i = 0; i < balls.Length; i++) {
                balls[i] = new RolledBall();
                art.BuildBowlingBall(parent, balls[i], settings.Radius);
            }
        }

        /// <summary>Rolls a ball from <paramref name="roller"/>'s nose; false when every ball is rolling already.</summary>
        public bool Roll(Car roller)
        {
            foreach (var ball in balls) {
                if (ball.State != RolledBall.Stage.Free) {
                    continue;
                }

                var forward = Flat(roller.transform.forward);
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                var from = roller.Position;
                ball.Roller = roller;
                ball.Direction = forward;
                ball.Position = new Vector3(from.x, settings.Radius, from.z) + forward * (LaunchAhead * roller.Growth);
                ball.Travelled = 0f;
                ball.HitMask = 0;
                ball.State = RolledBall.Stage.Rolling;
                ball.Root.gameObject.SetActive(true);
                ball.Root.SetPositionAndRotation(ball.Position, Quaternion.LookRotation(forward));
                ball.Root.localScale = Vector3.one;
                audioService?.Play(audioService.Library.HitLight, roller.IsPlayer ? 0.7f : 0.3f);
                return true;
            }

            return false;
        }

        public void Tick(float deltaTime)
        {
            foreach (var ball in balls) {
                switch (ball.State) {
                    case RolledBall.Stage.Rolling:
                        RollOn(ball, deltaTime);
                        break;
                    case RolledBall.Stage.Falling:
                        Fall(ball, deltaTime);
                        break;
                }
            }
        }

        /// <summary>Takes every ball away at once: the match is over.</summary>
        public void Clear()
        {
            foreach (var ball in balls) {
                if (ball.State != RolledBall.Stage.Free) {
                    Free(ball);
                }
            }
        }

        private void RollOn(RolledBall ball, float deltaTime)
        {
            var step = settings.Speed * deltaTime;
            var from = ball.Position;
            ball.Position += ball.Direction * step;
            ball.Travelled += step;
            ball.Root.position = ball.Position;
            // Rolling, not sliding: the turn the distance gives a ball of its radius.
            ball.Spin.localRotation *= Quaternion.Euler(step / settings.Radius * Mathf.Rad2Deg, 0f, 0f);
            HitAlong(ball, from, ball.Position);

            if (!edge.IsSolid(ball.Position)) {
                ball.Velocity = ball.Direction * settings.Speed;
                ball.State = RolledBall.Stage.Falling;
                return;
            }

            var left = settings.Range - ball.Travelled;
            if (left <= 0f) {
                Free(ball);
                return;
            }

            if (left < settings.Speed * FadeTime) {
                ball.Root.localScale = Vector3.one * Mathf.Clamp01(left / (settings.Speed * FadeTime));
            }
        }

        private void Fall(RolledBall ball, float deltaTime)
        {
            ball.Velocity += Vector3.down * (Gravity * deltaTime);
            ball.Position += ball.Velocity * deltaTime;
            ball.Root.position = ball.Position;
            ball.Spin.localRotation *= Quaternion.Euler(settings.Speed * deltaTime / settings.Radius * Mathf.Rad2Deg, 0f, 0f);
            if (ball.Position.y < GoneHeight) {
                Free(ball);
            }
        }

        /// <summary>
        /// Hits every car in play, but the roller, whose middle is within reach of the stretch the ball rolled from
        /// <paramref name="from"/> to <paramref name="to"/>, once a roll each: out to its side of the line and on along it.
        /// </summary>
        private void HitAlong(RolledBall ball, Vector3 from, Vector3 to)
        {
            var cars = round.Cars;
            var across = Vector3.Cross(Vector3.up, ball.Direction);
            for (var i = 0; i < cars.Count && i < 31; i++) {
                var car = cars[i];
                if (car == ball.Roller || (ball.HitMask & (1 << i)) != 0 || car.Position.y < HitDepth
                    || car.Position.y > HitHeight) {
                    continue;
                }

                var point = Flat(car.Position);
                var reach = settings.Radius + settings.HitReach * car.Growth;
                if (DistanceToSegment(point, Flat(from), Flat(to)) > reach) {
                    continue;
                }

                ball.HitMask |= 1 << i;
                var side = Vector3.Dot(point - Flat(to), across);
                // A car dead on the line goes the way the middle of the arena is not, so it is bowled off rather than in.
                var sign = Mathf.Abs(side) > 0.2f ? Mathf.Sign(side) : Vector3.Dot(point, across) >= 0f ? 1f : -1f;
                var direction = ball.Direction * (1f - settings.SideShare) + across * (sign * settings.SideShare);
                var cap = tuning.Car.MaxBumpSpeed * settings.MaxSpeedShare;
                if (!blow.Apply(car, ball.Roller, direction, settings.Push, cap, settings.Lift, settings.Stagger)) {
                    continue;
                }

                if (ball.Roller != null) {
                    ball.Roller.ReportItemHit(ItemKind.Bowling, car.Position);
                }

                if (effects != null) {
                    effects.PlayHit(car.Position, direction.normalized, 1f, true, false);
                }

                var isPlayer = car.IsPlayer || (ball.Roller != null && ball.Roller.IsPlayer);
                audioService?.Play(audioService.Library.HitHeavy, isPlayer ? 1f : 0.45f);
                if (arenaCamera != null && isPlayer) {
                    arenaCamera.Shake(car.IsPlayer ? 0.6f : 0.35f);
                }
            }
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            var ab = b - a;
            var length = ab.sqrMagnitude;
            var t = length > 0.0001f ? Mathf.Clamp01(Vector3.Dot(point - a, ab) / length) : 0f;
            return Vector3.Distance(point, a + ab * t);
        }

        private static void Free(RolledBall ball)
        {
            ball.State = RolledBall.Stage.Free;
            ball.Roller = null;
            ball.Root.gameObject.SetActive(false);
        }

        private static Vector3 Flat(Vector3 vector)
        {
            return new Vector3(vector.x, 0f, vector.z);
        }
    }
}
