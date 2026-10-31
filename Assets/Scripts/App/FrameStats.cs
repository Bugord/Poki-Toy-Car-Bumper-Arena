#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// The last second or so of frame times in a ring buffer, for the performance readouts of development builds.
    /// Adding a frame and measuring a window allocate nothing.
    /// </summary>
    public sealed class FrameStats
    {
        // Enough for one second at 500 frames a second.
        private const int Capacity = 512;

        private readonly float[] frames = new float[Capacity];
        private int count;
        private int head;

        public void Add(float seconds)
        {
            frames[head] = seconds;
            head = (head + 1) % Capacity;
            if (count < Capacity) {
                count++;
            }
        }

        /// <summary>
        /// The average and the worst frame time, in seconds, of the newest frames that fit in
        /// <paramref name="window"/> seconds (at least one). False before the first frame.
        /// </summary>
        public bool Measure(float window, out float average, out float worst)
        {
            var total = 0f;
            worst = 0f;
            var used = 0;
            for (var i = 0; i < count && (total < window || used == 0); i++) {
                var seconds = frames[(head - 1 - i + Capacity) % Capacity];
                total += seconds;
                worst = Mathf.Max(worst, seconds);
                used++;
            }

            average = used > 0 ? total / used : 0f;
            return used > 0 && total > 0f;
        }
    }
}
#endif
