using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// One bomb of <see cref="ItemBombs"/>: where it is in its life and its look.
    /// </summary>
    internal sealed class ThrownBomb
    {
        public enum Stage
        {
            Free,
            Flying,
            Fused,
            Falling,
        }

        public Stage State;
        public Car Thrower;
        public Vector3 From;
        public Vector3 To;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Time;
        public float Blink;

        // The look, built by PickupArt.
        public Transform Root;
        public GameObject Flash;
        public Transform Spark;
        public Transform Mark;
    }

    /// <summary>
    /// The bombs of a long match. A bomb is thrown from the car's nose in an arc to a spot
    /// <see cref="GameTuning.BombSettings.ThrowDistance"/> ahead along its heading; landing on floor (a shaking tile
    /// too) it sits and blinks faster and faster over a red ring that grows to its reach, then goes off and blows every
    /// other car in the ring outward, credited to the thrower (<see cref="ItemBlow"/>), so a car it sends off is the
    /// thrower's knock-out. The thrower itself is left out of its own blast. Landing past the edge, or losing its floor
    /// as it waits, it falls away without going off. No physics: it is moved here, as the Ball's ball is.
    /// </summary>
    internal sealed class ItemBombs
    {
        private const int PoolSize = 6;
        private const float Gravity = 22f;
        private const float GoneHeight = -14f;
        // Metres in front of the car's middle, and above the floor, the bomb leaves from.
        private const float LaunchAhead = 1.3f;
        private const float LaunchHeight = 1.1f;
        // Cars this far below or above the floor are not in the blast: one falling, or one flying over it.
        private const float BlastDepth = -1f;
        private const float BlastHeight = 3f;

        private readonly GameTuning tuning;
        private readonly GameTuning.BombSettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly ItemBlow blow;
        private readonly EffectPlayer effects;
        private readonly IAudioService audioService;
        private readonly ArenaCamera arenaCamera;
        private readonly ThrownBomb[] bombs = new ThrownBomb[PoolSize];

        public ItemBombs(GameTuning tuning, ArenaRound round, IArenaEdge edge, ItemBlow blow, EffectPlayer effects,
            IAudioService audioService, ArenaCamera arenaCamera, PickupArt art, Transform parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pickups.Bomb;
            this.round = round;
            this.edge = edge;
            this.blow = blow;
            this.effects = effects;
            this.audioService = audioService;
            this.arenaCamera = arenaCamera;
            for (var i = 0; i < bombs.Length; i++) {
                bombs[i] = new ThrownBomb();
                art.BuildBomb(parent, bombs[i]);
            }
        }

        /// <summary>
        /// The bombs thrown and not yet gone, for the bots to keep clear of; a free one is skipped by its stage.
        /// </summary>
        public IReadOnlyList<ThrownBomb> All => bombs;

        /// <summary>Throws a bomb from <paramref name="thrower"/>'s nose; false when every bomb is in the air already.</summary>
        public bool Throw(Car thrower)
        {
            foreach (var bomb in bombs) {
                if (bomb.State != ThrownBomb.Stage.Free) {
                    continue;
                }

                var forward = Forward(thrower);
                var from = thrower.Position;
                bomb.Thrower = thrower;
                bomb.From = new Vector3(from.x, Mathf.Max(0f, from.y) + LaunchHeight, from.z) + forward * LaunchAhead;
                bomb.To = new Vector3(from.x, 0f, from.z) + forward * settings.ThrowDistance;
                bomb.Position = bomb.From;
                bomb.Time = 0f;
                bomb.Blink = 0f;
                bomb.State = ThrownBomb.Stage.Flying;
                bomb.Root.gameObject.SetActive(true);
                bomb.Root.position = bomb.From;
                bomb.Root.localScale = Vector3.one;
                bomb.Flash.SetActive(false);
                bomb.Mark.gameObject.SetActive(false);
                return true;
            }

            return false;
        }

        public void Tick(float deltaTime)
        {
            foreach (var bomb in bombs) {
                switch (bomb.State) {
                    case ThrownBomb.Stage.Flying:
                        Fly(bomb, deltaTime);
                        break;
                    case ThrownBomb.Stage.Fused:
                        Wait(bomb, deltaTime);
                        break;
                    case ThrownBomb.Stage.Falling:
                        Fall(bomb, deltaTime);
                        break;
                }
            }
        }

        /// <summary>Takes every bomb away at once, without a blast: the match is over.</summary>
        public void Clear()
        {
            foreach (var bomb in bombs) {
                if (bomb.State != ThrownBomb.Stage.Free) {
                    Free(bomb);
                }
            }
        }

        private void Fly(ThrownBomb bomb, float deltaTime)
        {
            bomb.Time += deltaTime;
            var share = Mathf.Clamp01(bomb.Time / settings.FlightTime);
            var rest = bomb.To + Vector3.up * PickupArt.BombRadius;
            bomb.Position = Vector3.Lerp(bomb.From, rest, share) + Vector3.up * (settings.ArcHeight * 4f * share * (1f - share));
            bomb.Root.position = bomb.Position;
            bomb.Root.rotation = Quaternion.Euler(share * 360f, 0f, 0f) * Quaternion.LookRotation(Flat(bomb.To - bomb.From));
            if (share < 1f) {
                return;
            }

            if (!edge.IsSolid(bomb.To)) {
                // Past the edge or over a hole: it carries on down and away.
                bomb.Velocity = (rest - bomb.From) / settings.FlightTime;
                bomb.State = ThrownBomb.Stage.Falling;
                return;
            }

            bomb.State = ThrownBomb.Stage.Fused;
            bomb.Time = 0f;
            if (IsNearTarget(bomb)) {
                Explode(bomb);
                return;
            }

            bomb.Root.rotation = Quaternion.LookRotation(Flat(bomb.To - bomb.From));
            bomb.Mark.gameObject.SetActive(true);
            bomb.Mark.position = bomb.To;
            if (effects != null) {
                effects.PlayPickupFade(bomb.To);
            }

            audioService?.Play(audioService.Library.HitLight, IsNearPlayer(bomb.To, settings.Radius * 2f) ? 0.6f : 0.3f);
        }

        // A bomb landing on or next to another car in play goes off at once.
        private bool IsNearTarget(ThrownBomb bomb)
        {
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car != bomb.Thrower && car.IsTargetable && car.Position.y > BlastDepth && car.Position.y < BlastHeight
                    && Flat(car.Position - bomb.To).magnitude <= settings.ImpactRadius) {
                    return true;
                }
            }

            return false;
        }

        // On the floor, blinking faster as the fuse burns, the ring growing to the blast's reach; gone with its floor.
        private void Wait(ThrownBomb bomb, float deltaTime)
        {
            if (!edge.IsSolid(bomb.To)) {
                bomb.Velocity = Vector3.zero;
                bomb.Mark.gameObject.SetActive(false);
                bomb.Flash.SetActive(false);
                bomb.State = ThrownBomb.Stage.Falling;
                return;
            }

            bomb.Time += deltaTime;
            var share = Mathf.Clamp01(bomb.Time / settings.Fuse);
            bomb.Blink += deltaTime * Mathf.Lerp(3f, 12f, share);
            var isLit = Mathf.Repeat(bomb.Blink, 1f) < 0.5f;
            bomb.Flash.SetActive(isLit);
            bomb.Root.localScale = Vector3.one * (isLit ? 1.12f : 1f);
            bomb.Spark.localScale = Vector3.one * (0.14f * (isLit ? 1.4f : 0.9f));
            var radius = settings.Radius * Mathf.Lerp(0.3f, 1f, share);
            bomb.Mark.localScale = new Vector3(radius, 1f, radius);
            if (share >= 1f) {
                Explode(bomb);
            }
        }

        private void Fall(ThrownBomb bomb, float deltaTime)
        {
            bomb.Velocity += Vector3.down * (Gravity * deltaTime);
            bomb.Position += bomb.Velocity * deltaTime;
            bomb.Root.position = bomb.Position;
            if (bomb.Position.y < GoneHeight) {
                Free(bomb);
            }
        }

        /// <summary>
        /// The blast: every other car in play within reach is pushed straight away from the bomb, harder the nearer, and
        /// counts as hit by the thrower; a shield takes it.
        /// </summary>
        private void Explode(ThrownBomb bomb)
        {
            var origin = bomb.To;
            var thrower = bomb.Thrower;
            var isPlayerHit = false;
            var cars = round.Cars;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == thrower || car.Position.y < BlastDepth || car.Position.y > BlastHeight) {
                    continue;
                }

                var offset = Flat(car.Position - origin);
                var distance = offset.magnitude;
                if (distance > settings.Radius) {
                    continue;
                }

                var direction = distance > 0.05f ? offset / distance : Flat(car.transform.forward);
                var share = ItemBlow.Share(distance, settings.Radius, settings.EdgeShare, settings.Falloff);
                var cap = tuning.Car.MaxBumpSpeed * settings.MaxSpeedShare;
                if (!blow.Apply(car, thrower, direction, settings.Push * share, cap, settings.Lift * share,
                        settings.Stagger)) {
                    continue;
                }

                isPlayerHit |= car.IsPlayer;
                if (effects != null) {
                    effects.PlayHit(car.Position, direction, 1f, true, false);
                }

                if (thrower != null) {
                    thrower.ReportItemHit(ItemKind.Bomb, car.Position);
                }
            }

            if (effects != null) {
                effects.PlayShockwave(origin, settings.Radius);
                effects.PlayKnockout(origin + Vector3.up * 0.4f, false);
            }

            var isNear = isPlayerHit || IsNearPlayer(origin, settings.Radius * 1.6f);
            audioService?.Play(audioService.Library.HitHeavy, isNear ? 1f : 0.5f);
            if (arenaCamera != null) {
                arenaCamera.Shake(isPlayerHit ? 0.7f : isNear ? 0.4f : 0.15f);
            }

            Free(bomb);
        }

        private bool IsNearPlayer(Vector3 point, float distance)
        {
            var player = round.Player;
            return player != null && !player.IsOut && Flat(player.Position - point).sqrMagnitude < distance * distance;
        }

        private static void Free(ThrownBomb bomb)
        {
            bomb.State = ThrownBomb.Stage.Free;
            bomb.Thrower = null;
            bomb.Root.gameObject.SetActive(false);
            bomb.Mark.gameObject.SetActive(false);
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
