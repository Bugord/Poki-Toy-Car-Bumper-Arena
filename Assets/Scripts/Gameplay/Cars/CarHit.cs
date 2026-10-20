using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// One car running into another, as the car that raised <see cref="Car.Hit"/> felt it. Both cars of a hit raise it,
    /// each from its own side; <see cref="IsHitter"/> is true on exactly one of them, so an effect or a sound that wants
    /// each hit once listens for that side only.
    /// </summary>
    public readonly struct CarHit
    {
        /// <summary>
        /// The car this one ran into.
        /// </summary>
        public readonly Car Other;

        /// <summary>
        /// Where the cars touched, in the world.
        /// </summary>
        public readonly Vector3 Point;

        /// <summary>
        /// On the ground plane, pointing from the other car to this one: the way this car was pushed.
        /// </summary>
        public readonly Vector3 Normal;

        /// <summary>
        /// How fast the cars closed in on each other along <see cref="Normal"/>, in metres per second: the same on
        /// both sides of a hit.
        /// </summary>
        public readonly float Strength;

        /// <summary>
        /// Whether this car did the hitting: it drove into the other harder than the other drove into it.
        /// </summary>
        public readonly bool IsHitter;

        /// <summary>
        /// Whether this car was boosting when it hit.
        /// </summary>
        public readonly bool IsBoosting;

        /// <summary>
        /// Whether the other car was boosting when it hit, which made this car's push stronger.
        /// </summary>
        public readonly bool IsOtherBoosting;

        public CarHit(Car other, Vector3 point, Vector3 normal, float strength, bool isHitter, bool isBoosting,
            bool isOtherBoosting)
        {
            this.Other = other;
            this.Point = point;
            this.Normal = normal;
            this.Strength = strength;
            this.IsHitter = isHitter;
            this.IsBoosting = isBoosting;
            this.IsOtherBoosting = isOtherBoosting;
        }

        /// <summary>
        /// Whether either car was boosting: a boosted hit, which deserves the bigger effect.
        /// </summary>
        public bool IsBoosted => IsBoosting || IsOtherBoosting;
    }
}
