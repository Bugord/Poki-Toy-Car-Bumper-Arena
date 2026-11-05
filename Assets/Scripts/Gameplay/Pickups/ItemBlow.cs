using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// What an item that hits a car does to it, the same for the shockwave, the bomb and the glove: a car just back from
    /// a fall is left alone, a shield takes the hit whole and is spent, and otherwise the car is pushed (never left going
    /// faster than the cap), thrown a little up, knocked off balance and marked as hit by the item's user, so a fall that
    /// follows is the user's knock-out. The player's share is eased on the easiest matches, as for the arena's hazards.
    /// </summary>
    internal sealed class ItemBlow
    {
        private readonly GameTuning.PickupSettings settings;
        private readonly RoundDifficulty difficulty;

        public ItemBlow(GameTuning.PickupSettings settings, RoundDifficulty difficulty)
        {
            this.settings = settings;
            this.difficulty = difficulty;
        }

        /// <summary>
        /// The share of a blast's push a car <paramref name="distance"/> metres from its middle gets: all of it in the
        /// middle, <paramref name="edgeShare"/> at <paramref name="radius"/>, falling faster near the middle the steeper
        /// the <paramref name="falloff"/>, so a car right by the blast flies and one at its rim is only nudged.
        /// </summary>
        public static float Share(float distance, float radius, float edgeShare, float falloff)
        {
            var near = 1f - Mathf.Clamp01(distance / radius);
            return Mathf.Lerp(edgeShare, 1f, Mathf.Pow(near, falloff));
        }

        /// <summary>
        /// Hits <paramref name="car"/> for <paramref name="user"/> along the flat <paramref name="direction"/>; false when
        /// nothing happened to it (safe after a fall, or its shield took it).
        /// </summary>
        /// <param name="push">Metres per second added along the direction.</param>
        /// <param name="cap">The most the push may leave the car going along the direction, in metres per second.</param>
        /// <param name="isItem">False for a blow that is not the user's item (the hot potato's bomb): the fall is still
        /// the user's knock-out, but not one by an item.</param>
        public bool Apply(Car car, Car user, Vector3 direction, float push, float cap, float lift, float stagger,
            bool isItem = true)
        {
            if (!car.IsTargetable) {
                return false;
            }

            if (car.TryAbsorbHit()) {
                return false;
            }

            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : car.transform.forward;
            var share = car.IsPlayer && difficulty != null ? Mathf.Lerp(settings.EasyPlayerShare, 1f, difficulty.Level) : 1f;
            var speedAway = Vector3.Dot(car.Movement.Velocity, direction);
            var added = Mathf.Min(push * share, Mathf.Max(0f, cap - speedAway));
            car.Movement.Push(direction * added + Vector3.up * (lift * share));
            car.Movement.Stagger(stagger * share);
            car.HitByItem(user, isItem);
            return true;
        }
    }
}
