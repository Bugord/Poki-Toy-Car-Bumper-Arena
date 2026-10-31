using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The way out of a strip of floor that a hazard is about to sweep (the crab's path, the pencil's band), for
    /// <see cref="IArenaHazard.TryAvoid"/>: a car in the strip steers across it to the nearer side, as with the Ball's lane,
    /// and to the other side if the first would take it too near the edge.
    /// </summary>
    public static class HazardBand
    {
        // Room beyond the strip's side and the margin a dodging car aims for, so it does not stop on the line.
        private const float ClearRoom = 0.8f;

        /// <summary>
        /// Whether <paramref name="position"/> lies in the strip of half width <paramref name="halfWidth"/> (plus
        /// <paramref name="margin"/>) round the line from <paramref name="center"/> along <paramref name="along"/>, between
        /// <paramref name="minAlong"/> and <paramref name="maxAlong"/> metres along it; and the flat heading across it,
        /// out of the nearer side, that keeps <paramref name="edgeMargin"/> from the edge.
        /// </summary>
        public static bool TryAvoid(Vector3 position, Vector3 center, Vector3 along, Vector3 across, float halfWidth,
            float margin, float minAlong, float maxAlong, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            var offset = position - center;
            offset.y = 0f;
            var distance = Vector3.Dot(offset, along);
            if (distance < minAlong || distance > maxAlong) {
                return false;
            }

            var sideOffset = Vector3.Dot(offset, across);
            var reach = halfWidth + margin;
            if (Mathf.Abs(sideOffset) > reach) {
                return false;
            }

            // The nearer side; on the line itself, the side the middle of the arena is on.
            float side;
            if (Mathf.Abs(sideOffset) > 0.3f) {
                side = Mathf.Sign(sideOffset);
            }
            else {
                var middle = -Vector3.Dot(center, across);
                side = Mathf.Abs(middle) > 0.3f ? Mathf.Sign(middle) : 1f;
            }

            for (var attempt = 0; attempt < 2; attempt++) {
                var travel = Mathf.Max(0f, reach + ClearRoom - side * sideOffset);
                var target = position + across * (side * travel);
                if (edge.DistanceToEdge(target, edgeMargin) >= 0f) {
                    heading = across * side;
                    return true;
                }

                side = -side;
            }

            return false;
        }
    }
}
