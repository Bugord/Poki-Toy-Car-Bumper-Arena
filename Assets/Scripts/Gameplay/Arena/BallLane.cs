using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where the Ball arena's ball is about to roll, or is rolling, for the bots to keep out of: a plain state the
    /// <see cref="ArenaBall"/> writes and the bots read. It sits between the two so that neither needs the other (the ball
    /// draws its chords from the bots' seed, the bots dodge the ball). On other arenas it stays inactive.
    /// </summary>
    public sealed class BallLane
    {
        // A bot this far past the ball along the lane has been passed and has nothing to dodge.
        private const float PassedSlack = 0.5f;

        // Extra room a dodging bot aims for beyond the lane's side and the margin, so it does not stop on the line.
        private const float ClearRoom = 0.8f;

        /// <summary>
        /// A ball is announced or rolling on the floor; false once it has fallen off or before the first.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Counts the balls of the round, so a bot can tell a new one from the one it already saw.
        /// </summary>
        public int Pass { get; set; }

        /// <summary>
        /// The seed the chords are drawn from; <see cref="Bots.BotDriver.Seed"/> sets it, so a headless simulation
        /// repeats the balls with the bots' choices.
        /// </summary>
        public int Seed { get; set; }

        /// <summary>
        /// The way the ball rolls, flat and of length 1.
        /// </summary>
        public Vector3 Direction { get; set; } = Vector3.forward;

        /// <summary>
        /// Across the lane, flat and of length 1.
        /// </summary>
        public Vector3 Normal { get; set; } = Vector3.right;

        /// <summary>
        /// The point of the lane's middle line nearest the middle of the arena.
        /// </summary>
        public Vector3 Origin { get; set; }

        /// <summary>
        /// Half the lane's width, in metres.
        /// </summary>
        public float HalfWidth { get; set; }

        /// <summary>
        /// Where the ball is now, flat.
        /// </summary>
        public Vector3 BallPosition { get; set; }

        /// <summary>
        /// Whether a car at <paramref name="position"/> is in the ball's way (in the lane, or within
        /// <paramref name="margin"/> of its side, and not yet passed by the ball), and the flat heading out of it: across
        /// the lane, to the side the car is on, or to the middle's side when it is on the line itself. A side that
        /// leads past <paramref name="safeRadius"/> is not taken; if neither is safe the car is left to stay.
        /// </summary>
        public bool TryAvoid(Vector3 position, float margin, float safeRadius, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (!IsActive) {
                return false;
            }

            var sideOffset = Vector3.Dot(position - Origin, Normal);
            var reach = HalfWidth + margin;
            if (Mathf.Abs(sideOffset) > reach) {
                return false;
            }

            var ahead = Vector3.Dot(position - BallPosition, Direction);
            if (ahead < -(HalfWidth + PassedSlack)) {
                return false;
            }

            // The nearer side out of the lane; on the line itself, the side the middle of the arena is on.
            float side;
            if (Mathf.Abs(sideOffset) > 0.3f) {
                side = Mathf.Sign(sideOffset);
            }
            else {
                var middle = -Vector3.Dot(Origin, Normal);
                side = Mathf.Abs(middle) > 0.3f ? Mathf.Sign(middle) : 1f;
            }

            for (var attempt = 0; attempt < 2; attempt++) {
                // Where the car would be once clear of the lane on this side.
                var travel = Mathf.Max(0f, (reach + ClearRoom) - side * sideOffset);
                var target = position + Normal * (side * travel);
                if (target.sqrMagnitude <= safeRadius * safeRadius) {
                    heading = Normal * side;
                    return true;
                }

                side = -side;
            }

            return false;
        }
    }
}
