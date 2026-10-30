using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// What the floor under a point does to a car. The arena's mechanic answers it; on an arena without a mechanic the
    /// answer is always <see cref="SurfaceFeel.Neutral"/>, so a car never needs to know which arena it is on.
    /// </summary>
    public interface IArenaSurface
    {
        /// <summary>The feel of the floor at a world position, only its horizontal place counts.</summary>
        SurfaceFeel Sample(Vector3 position);
    }

    /// <summary>
    /// Multipliers a floor puts on a car's driving and on how it takes a hit; 1 everywhere is plain floor.
    /// </summary>
    public readonly struct SurfaceFeel
    {
        public static readonly SurfaceFeel Neutral = new(1f, 1f, 1f, 1f, 1f);

        public SurfaceFeel(float speed, float acceleration, float grip, float push, float stagger)
        {
            Speed = speed;
            Acceleration = acceleration;
            Grip = grip;
            Push = push;
            Stagger = stagger;
        }

        /// <summary>Top speed, while the car is not boosting.</summary>
        public float Speed { get; }

        public float Acceleration { get; }

        public float Grip { get; }

        /// <summary>The extra push of a hit (<see cref="GameTuning.CarSettings.BumpForce"/>).</summary>
        public float Push { get; }

        /// <summary>The time a hit knocks the car off balance for.</summary>
        public float Stagger { get; }

        /// <summary>This feel moved towards <paramref name="other"/> by <paramref name="t"/>, 0 to 1.</summary>
        public SurfaceFeel Lerp(SurfaceFeel other, float t)
        {
            return new SurfaceFeel(
                Mathf.Lerp(Speed, other.Speed, t),
                Mathf.Lerp(Acceleration, other.Acceleration, t),
                Mathf.Lerp(Grip, other.Grip, t),
                Mathf.Lerp(Push, other.Push, t),
                Mathf.Lerp(Stagger, other.Stagger, t));
        }
    }
}
