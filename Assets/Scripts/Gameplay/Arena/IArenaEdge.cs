using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where the arena's floor ends, whatever its shape: how far a point is from the nearest void (the rim or a pit) and
    /// which way is away from it. Everything that cares about "the edge" asks this instead of comparing a radius with
    /// <c>position.magnitude</c>, so the same bots, edge assist and camera work on a round floor, a square one or one with
    /// a hole in it.
    /// <para>
    /// Two views of one floor. The safe view leaves out the tiles that have started to shake (they are about to fall, so
    /// the bots and the player's help treat them as gone); the solid view keeps them (they still hold a car up, and the
    /// camera frames them). Positions are read flat: the height is ignored.
    /// </para>
    /// </summary>
    public interface IArenaEdge
    {
        /// <summary>
        /// Raised when the safe or the solid floor changes: a wave of tiles starts to shake or falls.
        /// </summary>
        event Action Changed;

        /// <summary>
        /// Radius, in metres, of the smallest circle round the middle that holds the whole floor as the round starts. The
        /// scale of the arena, and a bound for anything flung out of it.
        /// </summary>
        float FullExtent { get; }

        /// <summary>
        /// Radius of the smallest circle round the middle that holds the safe floor, in metres. The camera and the
        /// telemetry read it; the rules use <see cref="DistanceToEdge"/>.
        /// </summary>
        float SafeExtent { get; }

        /// <summary>
        /// Radius of the smallest circle round the middle that holds the solid floor, in metres. The camera frames it.
        /// </summary>
        float SolidExtent { get; }

        /// <summary>
        /// A width of the safe floor, in metres, to scale distances by; never below 1.
        /// </summary>
        float Span { get; }

        /// <summary>
        /// Metres from <paramref name="position"/> to the nearest void of the safe floor: positive on it, negative past
        /// the edge. <paramref name="inset"/> pulls the edge in by that many metres, for a margin to keep.
        /// </summary>
        float DistanceToEdge(Vector3 position, float inset = 0f);

        /// <summary>
        /// The same for the solid floor, shaking tiles included.
        /// </summary>
        float DistanceToSolidEdge(Vector3 position);

        /// <summary>
        /// Whether there is floor under <paramref name="position"/>, a shaking tile too.
        /// </summary>
        bool IsSolid(Vector3 position);

        /// <summary>
        /// The flat direction away from the nearest void, zero where there is no preferred way (the middle of a round
        /// floor). Only the direction counts: the length is not defined, so normalise it where a unit vector is needed.
        /// A round floor returns the reverse of the position, so a bot heading home drives the heading it always did.
        /// </summary>
        Vector3 Inward(Vector3 position);

        /// <summary>
        /// How near the edge a point is on a scale: 0 when it is <paramref name="near"/> metres from the edge or more, 1
        /// when it is <paramref name="far"/> or fewer (<paramref name="far"/> is the smaller distance), linear between.
        /// </summary>
        float Proximity(Vector3 position, float near, float far);

        /// <summary>
        /// How near the edge a point is relative to the floor, from 0 (deep inside) to 1 (at the edge or past it).
        /// </summary>
        float Nearness(Vector3 position);

        /// <summary>
        /// The point of the safe floor nearest <paramref name="position"/>: the point itself when it is on the floor.
        /// </summary>
        Vector3 ClampToFloor(Vector3 position);

        /// <summary>
        /// Half the length of the solid floor along the line through <paramref name="origin"/> in
        /// <paramref name="direction"/>, measured from <paramref name="origin"/> each way. The line is taken to run across
        /// the floor with <paramref name="origin"/> at its middle (the foot of the perpendicular from the arena's middle).
        /// </summary>
        float SolidHalfChord(Vector3 origin, Vector3 direction);
    }
}
