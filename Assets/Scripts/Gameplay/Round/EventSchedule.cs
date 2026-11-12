using System;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The mode of the day: one event mode a calendar day (<see cref="CalendarDay"/>, the player's clock), going round
    /// the fixed circle of <see cref="EventModes.Circle"/> and skipping the modes the build has off (their
    /// <c>enabled</c>). The menu offers it beside QUICK MATCH and the rotation's event slot plays it, so every player sees
    /// the same mode on the same day and a new one the next. Pure: the day comes in, the mode comes out.
    /// </summary>
    public static class EventSchedule
    {
        /// <summary>
        /// The mode every day plays instead, on or off in the build: for the dev console and the tests. Null for the
        /// schedule. Kept in memory only, and dropped when Play Mode starts.
        /// </summary>
        public static EventMode? Forced { get; set; }

        /// <summary>
        /// The mode of calendar day <paramref name="day"/>: the enabled modes in the circle's order, one a day, round
        /// and round. <see cref="EventMode.None"/> when the build has no events (the match build) or every mode is off.
        /// </summary>
        public static EventMode Of(GameTuning tuning, int day)
        {
            if (!tuning.HasEvents) {
                return EventMode.None;
            }

            if (Forced.HasValue) {
                return Forced.Value;
            }

            var count = 0;
            foreach (var mode in EventModes.Circle) {
                if (tuning.IsModeEnabled(mode)) {
                    count++;
                }
            }

            if (count == 0) {
                return EventMode.None;
            }

            // Days before the epoch (a clock far back) still land on the circle.
            var index = (day % count + count) % count;
            foreach (var mode in EventModes.Circle) {
                if (!tuning.IsModeEnabled(mode)) {
                    continue;
                }

                if (index-- == 0) {
                    return mode;
                }
            }

            return EventMode.None;
        }

        /// <summary>The mode of the day the player's clock shows now.</summary>
        public static EventMode Today(GameTuning tuning) => Of(tuning, CalendarDay.Of(DateTime.Now));

        /// <summary>How long until the mode of the day changes: until the player's midnight.</summary>
        public static TimeSpan LeftToday(DateTime now) => now.Date.AddDays(1) - now;

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlayMode() => Forced = null;
#endif
    }
}
