using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// A part of the floor that moves and carries the cars standing on it, as the Carousel arena's turntable does. The
    /// arena's mechanic answers it; elsewhere the answer is always <see cref="PlatformMotion.None"/>, so a car never
    /// needs to know which arena it is on.
    /// </summary>
    public interface IArenaPlatform
    {
        /// <summary>How the floor under a point moves; only its horizontal place counts.</summary>
        PlatformMotion Sample(Vector3 position);
    }

    /// <summary>
    /// How a bit of floor moves: the velocity of the point itself and how fast it turns, already faded out towards the
    /// platform's rim so a car is not snatched on or off it.
    /// </summary>
    public readonly struct PlatformMotion
    {
        public static readonly PlatformMotion None = new(Vector3.zero, 0f);

        public PlatformMotion(Vector3 velocity, float angularVelocity)
        {
            Velocity = velocity;
            AngularVelocity = angularVelocity;
        }

        /// <summary>Velocity of the floor at the point, in metres per second.</summary>
        public Vector3 Velocity { get; }

        /// <summary>Turn of the floor about the vertical axis, in radians per second, positive as Unity's Y rotation.</summary>
        public float AngularVelocity { get; }

        public bool IsMoving => Velocity.sqrMagnitude > 0f || AngularVelocity != 0f;
    }
}
