using System;
using System.Linq;
using Bugord.App.Platform;
using Bugord.Core.Storage;
using NUnit.Framework;
using ToyCarArena.App;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class VisitsTests
    {
        private static readonly DateTime Monday = new(2026, 11, 2, 23, 50, 0);

        [Test]
        public void AFirstVisitIsCountedWithoutAReturn()
        {
            var platform = new MatchTests.RecordingPlatform();
            var visits = new Visits(platform, new RoundOverScreenshots.MemoryStorage());

            visits.Record(Monday);
            visits.Record(Monday);

            Assert.That(visits.Count, Is.EqualTo(1));
            Assert.That(Names(platform), Is.EqualTo(new[] { "visit-1" }));
        }

        [Test]
        public void ReturnsAreCountedByCalendarDays()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            string[] Visit(DateTime day)
            {
                var platform = new MatchTests.RecordingPlatform();
                new Visits(platform, storage).Record(day);
                return Names(platform);
            }

            Visit(Monday);
            // Ten minutes later it is Tuesday: a calendar day, not 24 hours.
            Assert.That(Visit(Monday.AddMinutes(20)), Is.EqualTo(new[] { "visit-2", "back-d1" }));
            Assert.That(Visit(Monday.AddMinutes(30)), Is.EqualTo(new[] { "visit-3", "back-d0" }));
            Assert.That(Visit(Monday.AddDays(5)), Is.EqualTo(new[] { "visit-4plus", "back-d2-7" }));
            Assert.That(Visit(Monday.AddDays(30)), Is.EqualTo(new[] { "visit-4plus", "back-d8plus" }));
        }

        [Test]
        public void APlayerFromBeforeTheCountReturnsOnAnUnknownDay()
        {
            var storage = new RoundOverScreenshots.MemoryStorage();
            storage.SetInt("xp_total", 120);
            var platform = new MatchTests.RecordingPlatform();

            new Visits(platform, storage).Record(Monday);

            Assert.That(Names(platform), Is.EqualTo(new[] { "visit-2", "back-unknown" }));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(category, Is.EqualTo("session"));
                Assert.That(action, Is.EqualTo("visit"));
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        private static string[] Names(MatchTests.RecordingPlatform platform) =>
            platform.Events.Select(e => e.Item2).ToArray();
    }
}
