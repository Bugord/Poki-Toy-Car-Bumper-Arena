using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where a point of the floor is on the floor's texture. Each look is one picture of the whole floor, painted from the
    /// floor's real outline, so rims, borders and rings land where the geometry is: the footprint (the bounding box of the
    /// floor, stretched over u 0..1 and v 0..<see cref="TopRange"/>) holds the top, and a strip across the top of the
    /// texture (v from <see cref="SideBottom"/> to <see cref="SideTop"/>) holds the sides, which wrap round the floor
    /// <see cref="SideRepeats"/> times. The UVs are baked into the meshes when they are built, so a tile keeps its look as
    /// it shakes and falls. The painter in the art pipeline uses the same numbers.
    /// </summary>
    public readonly struct FloorUv
    {
        /// <summary>Times the strip of the sides repeats in a full turn round the middle; whole, so the wrap is unseen.</summary>
        public const int SideRepeats = 16;

        /// <summary>The v the footprint ends at; the gap above it keeps the mip levels of the top out of the strip.</summary>
        public const float TopRange = 0.84f;

        /// <summary>The v of the top edge of a side and of its bottom edge.</summary>
        public const float SideTop = 0.99f;
        public const float SideBottom = 0.88f;

        private readonly float minX;
        private readonly float minZ;
        private readonly float sizeX;
        private readonly float sizeZ;

        public FloorUv(float minX, float minZ, float maxX, float maxZ)
        {
            this.minX = minX;
            this.minZ = minZ;
            this.sizeX = Mathf.Max(0.01f, maxX - minX);
            this.sizeZ = Mathf.Max(0.01f, maxZ - minZ);
        }

        /// <summary>The footprint of a circle of this radius.</summary>
        public static FloorUv ForCircle(float radius)
        {
            return new FloorUv(-radius, -radius, radius, radius);
        }

        /// <summary>The footprint of a shaped floor: the box round its rim.</summary>
        public static FloorUv For(ArenaShape shape)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var point in shape.Outline) {
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return new FloorUv(min.x, min.y, max.x, max.y);
        }

        /// <summary>The UV of a point of a top face.</summary>
        public Vector2 Top(float x, float z)
        {
            return new Vector2((x - minX) / sizeX, (z - minZ) / sizeZ * TopRange);
        }

        /// <summary>The u of a side at this point: its turn round the middle, in strip repeats.</summary>
        public static float Turn(float x, float z)
        {
            return Mathf.Atan2(z, x) / (2f * Mathf.PI) * SideRepeats;
        }

        /// <summary>
        /// Brings a turn next to a reference turn, so a face that straddles the place the angle wraps does not smear the
        /// whole strip across it.
        /// </summary>
        public static float NextTo(float turn, float reference)
        {
            while (turn - reference > SideRepeats * 0.5f) {
                turn -= SideRepeats;
            }

            while (turn - reference < -SideRepeats * 0.5f) {
                turn += SideRepeats;
            }

            return turn;
        }

        /// <summary>The UV of a point of a side: depth is 0 at the top edge and 1 at the bottom.</summary>
        public static Vector2 Side(float turn, float depth)
        {
            return new Vector2(turn, Mathf.Lerp(SideTop, SideBottom, depth));
        }
    }
}
