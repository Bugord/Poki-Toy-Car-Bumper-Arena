using System;
using Bugord.App.Platform;
using Bugord.Core.Storage;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// Counts the player's visits, so the content meant for returning players can be measured at all: a Player Fit
    /// Test only sees one session, and Poki does not link a player's visits in its exports. On the first frame the
    /// platform is up it reads the saved count and the day of the last visit, saves this one and sends
    /// <c>session / visit-1, 2, 3, 4plus / visit</c> and, from the second visit, <c>session / back-d0, d1, d2-7,
    /// d8plus, unknown / visit</c>, the days since the last one (<c>unknown</c> for a player from before v0.16, who
    /// has progress but no count).
    /// </summary>
    /// <remarks>
    /// Days are calendar days on the player's clock, as a daily reward would count them. The action is not Poki's
    /// <c>start</c>: a progress event without its <c>complete</c> would read as a visit that left. Lives in the app
    /// scope; the events go once a visit.
    /// </remarks>
    public sealed class Visits : ITickable
    {
        private const string CountKey = "visit_count";
        private const string DayKey = "visit_day";
        private const string Action = "visit";

        // Saved by PlayerProgress: a player who has it but no visit count played before visits were counted.
        private const string XpKey = "xp_total";

        // Visits past this one share one bucket.
        private const int LastVisitBucket = 4;

        private readonly IGamePlatform platform;
        private readonly IKeyValueStorage storage;

        private bool isCounted;

        public Visits(IGamePlatform platform, IKeyValueStorage storage)
        {
            this.platform = platform;
            this.storage = storage;
        }

        /// <summary>This visit's number, from 1; 0 until it is counted.</summary>
        public int Count { get; private set; }

        public void Tick()
        {
            // Poki drops an event sent before it is up.
            if (!isCounted && platform.IsInitialized) {
                Record(DateTime.Now);
            }
        }

        /// <summary>
        /// Counts this visit on <paramref name="today"/> and sends its events, once; the frames call it as soon as the
        /// platform is up, a test with its own date.
        /// </summary>
        public void Record(DateTime today)
        {
            if (isCounted) {
                return;
            }

            isCounted = true;
            var day = DayNumber(today);
            var saved = Math.Max(0, storage.GetInt(CountKey));
            var isOld = saved == 0 && storage.HasKey(XpKey);
            var previous = isOld ? 1 : saved;

            this.Count = previous + 1;
            platform.Measure("session", Count >= LastVisitBucket ? $"visit-{LastVisitBucket}plus" : $"visit-{Count}",
                Action);
            if (previous > 0) {
                platform.Measure("session", isOld || !storage.HasKey(DayKey) ? "back-unknown" :
                    BackBucket(day - storage.GetInt(DayKey)), Action);
            }

            storage.SetInt(CountKey, Count);
            storage.SetInt(DayKey, day);
        }

        /// <summary>The bucket of days since the last visit: d0, d1, d2-7 or d8plus.</summary>
        public static string BackBucket(int days)
        {
            if (days <= 0) {
                return "back-d0";
            }

            if (days == 1) {
                return "back-d1";
            }

            return days <= 7 ? "back-d2-7" : "back-d8plus";
        }

        private static int DayNumber(DateTime date) => (int)(date.Date - DateTime.MinValue.Date).TotalDays;
    }
}
