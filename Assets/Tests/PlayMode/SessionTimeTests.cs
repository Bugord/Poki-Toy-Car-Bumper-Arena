using System.Linq;
using Bugord.App.Platform;
using NUnit.Framework;
using ToyCarArena.App;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class SessionTimeTests
    {
        [Test]
        public void EachMinuteOnThePageIsSentOnceWithinTwoNumbers()
        {
            var platform = new MatchTests.RecordingPlatform();
            var time = new SessionTime(platform, new AdBreaks());

            // A minute and a half in frames of a second, then a long stretch.
            for (var i = 0; i < 90; i++) {
                time.Advance(1f);
            }

            Assert.That(Names(platform, "time"), Is.EqualTo(new[] { "time-1m" }));

            for (var i = 0; i < 60 * 31; i++) {
                time.Advance(1f);
            }

            Assert.That(Names(platform, "time"),
                Is.EqualTo(SessionTime.Milestones.Select(minutes => $"time-{minutes}m").ToArray()));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(category, Is.EqualTo("session"));
                Assert.That(action, Is.EqualTo("reached"));
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        [Test]
        public void AHiddenTabDoesNotCountAsTimeInTheGame()
        {
            var platform = new MatchTests.RecordingPlatform();
            var time = new SessionTime(platform, new AdBreaks());

            // The frame after ten minutes in the background spans them; only a few seconds of it count.
            time.Advance(600f);

            Assert.That(time.OnPage, Is.EqualTo(SessionTime.MaxFrame));
            Assert.That(platform.Events, Is.Empty);
        }

        [Test]
        public void MinutesInRoundsFollowThePlayedTime()
        {
            var platform = new MatchTests.RecordingPlatform();
            var rounds = new AdBreaks();
            var time = new SessionTime(platform, rounds);

            rounds.AddPlay(130f);
            time.Advance(0f);

            Assert.That(Names(platform, "play"), Is.EqualTo(new[] { "play-1m", "play-2m" }));
        }

        private static string[] Names(MatchTests.RecordingPlatform platform, string prefix) =>
            platform.Events.Where(e => e.Item2.StartsWith(prefix + "-")).Select(e => e.Item2).ToArray();
    }
}
