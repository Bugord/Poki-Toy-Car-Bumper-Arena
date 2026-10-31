using ToyCarArena.Gameplay.Arena;
using UnityEngine;

namespace ToyCarArena.Gameplay.Bots
{
    /// <summary>
    /// Bends a bot's line to its target round a pit or a hole in the way. The floor is sampled along the line at a few
    /// points: where the distance to the nearest void drops below both ends of the line (a convex floor never does, so a bot
    /// still drives its target off the rim), the bot goes along the void's rim instead, on the side that leads on towards
    /// the target, leaning inward. Kept apart from <see cref="Bot"/> so it is a plain function of the floor, cheap enough
    /// for every bot every frame: a few samples of the edge field and one gradient.
    /// </summary>
    public static class PitDetour
    {
        // How much lower than both ends a sample has to read, in metres, to count as a dip and not as noise.
        private const float Dip = 0.25f;

        /// <summary>
        /// The heading to drive instead of <paramref name="heading"/>: the same when the line is clear. The side taken
        /// (+1 or -1 along the rim) is kept in <paramref name="side"/> until <paramref name="hold"/> runs out, so a bot
        /// facing the middle of a pit does not dither.
        /// </summary>
        public static Vector3 Bend(IArenaEdge edge, Vector3 position, Vector3 heading, float margin, float probe,
            int samples, float inward, float sideHold, ref float side, ref float hold)
        {
            var length = Mathf.Min(probe, heading.magnitude);
            if (probe <= 0f || length < 0.5f) {
                return heading;
            }

            var direction = heading / heading.magnitude;
            var start = edge.DistanceToEdge(position, margin);
            var end = edge.DistanceToEdge(position + direction * length, margin);
            var floor = Mathf.Min(start, end) - Dip;
            var worst = 0f;
            var at = 0f;
            for (var i = 1; i < samples; i++) {
                var along = length * i / samples;
                var value = edge.DistanceToEdge(position + direction * along, margin);
                if (value < floor && value < worst) {
                    worst = value;
                    at = along;
                }
            }

            if (at <= 0f) {
                return heading;
            }

            var away = edge.Inward(position + direction * at);
            if (away == Vector3.zero) {
                return heading;
            }

            var rim = new Vector3(-away.z, 0f, away.x);
            if (hold <= 0f) {
                // The way along the rim that still leads towards the target.
                side = Vector3.Dot(rim, direction) >= 0f ? 1f : -1f;
                hold = sideHold;
            }

            return (rim * side + away * inward).normalized * heading.magnitude;
        }
    }
}
