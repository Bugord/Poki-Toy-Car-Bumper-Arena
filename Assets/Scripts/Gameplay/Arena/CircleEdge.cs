using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The edge of a round floor, from the exact formulas: the distance is the radius less the distance from the middle,
    /// and away from the edge is towards the middle. The floor keeps the two radii up to date as rings shake and fall.
    /// <para>
    /// A floor of any other shape needs a different <see cref="IArenaEdge"/>: a signed-distance grid of about half a metre
    /// a cell, recomputed when a wave of tiles warns or falls, which answers the same questions by sampling it. The round
    /// floor keeps the formulas, which are exact and free.
    /// </para>
    /// </summary>
    public sealed class CircleEdge : IArenaEdge
    {
        // Closer to the middle than this a car has no way out that is nearer than another: the middle itself has no
        // direction, and a bot or a player's help treats it as safe.
        private const float MiddleRadius = 0.01f;

        private readonly float full;
        private float safe;
        private float solid;

        public CircleEdge(float radius)
        {
            this.full = radius;
            this.safe = radius;
            this.solid = radius;
        }

        public event Action Changed;

        public float FullExtent => full;
        public float SafeExtent => safe;
        public float SolidExtent => solid;
        public float Span => Mathf.Max(1f, safe * 2f);

        /// <summary>
        /// Sets the radii of the safe and the solid floor, and raises <see cref="Changed"/> when either moved.
        /// </summary>
        public void Set(float safeRadius, float solidRadius)
        {
            if (safeRadius == safe && solidRadius == solid) {
                return;
            }

            safe = safeRadius;
            solid = solidRadius;
            Changed?.Invoke();
        }

        public float DistanceToEdge(Vector3 position, float inset = 0f)
        {
            return Mathf.Max(0f, safe - inset) - FromMiddle(position);
        }

        public float DistanceToSolidEdge(Vector3 position)
        {
            return solid - FromMiddle(position);
        }

        public bool IsSolid(Vector3 position)
        {
            return FromMiddle(position) <= solid;
        }

        public Vector3 Inward(Vector3 position)
        {
            position.y = 0f;
            return FromMiddle(position) < MiddleRadius ? Vector3.zero : -position;
        }

        public float Proximity(Vector3 position, float near, float far)
        {
            return Mathf.InverseLerp(safe - near, safe - far, FromMiddle(position));
        }

        public float Nearness(Vector3 position)
        {
            return Mathf.Clamp01(FromMiddle(position) / Mathf.Max(1f, safe));
        }

        public Vector3 ClampToFloor(Vector3 position)
        {
            return Vector3.ClampMagnitude(position, safe);
        }

        public float SolidHalfChord(Vector3 origin, Vector3 direction)
        {
            var offset = FromMiddle(origin);
            return Mathf.Sqrt(Mathf.Max(0f, solid * solid - offset * offset));
        }

        private static float FromMiddle(Vector3 position)
        {
            return new Vector2(position.x, position.z).magnitude;
        }
    }
}
