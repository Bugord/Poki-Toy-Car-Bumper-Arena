using System.Linq;
using Bugord.App.Platform;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Rendering;
using ToyCarArena.UI;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class ScreenFrameRateTests
    {
        [SetUp]
        public void SetUp() => ShownScreens.Clear();

        [TearDown]
        public void TearDown() => ShownScreens.Clear();

        [Test]
        public void TheScreenIsAnAdThenTheCurtainThenTheNewest()
        {
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.None));
            var result = ShownScreens.Add(ShownScreen.Result, false);
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.Result));
            Assert.That(ShownScreens.IsArenaCovered, Is.False);

            // The curtain comes down over the result, and the next round's HUD fades in under it.
            var curtain = ShownScreens.Add(ShownScreen.Curtain, true);
            ShownScreens.Remove(result);
            var hud = ShownScreens.Add(ShownScreen.Round, false);
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.Curtain));
            Assert.That(ShownScreens.IsArenaCovered, Is.True);

            var ad = ShownScreens.Add(ShownScreen.Ad, false);
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.Ad));
            ShownScreens.Remove(ad);

            ShownScreens.Remove(curtain);
            ShownScreens.Remove(curtain);
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.Round));
            Assert.That(ShownScreens.IsArenaCovered, Is.False, "removing an entry twice does not uncover twice");

            ShownScreens.Remove(hud);
            Assert.That(ShownScreens.Current, Is.EqualTo(ShownScreen.None));
        }

        [Test]
        public void EachScreenSendsItsFrameRateOnceWithinTwoNumbers()
        {
            var platform = new MatchTests.RecordingPlatform();
            var rate = new ScreenFrameRate(platform, new HudControls());

            // Twenty seconds of a round at 50 frames a second, then more of it.
            for (var i = 0; i < 50 * 30; i++) {
                rate.Advance(1f / 50f, ShownScreen.Round);
            }

            // Three seconds of the curtain at 30, split by a round.
            for (var i = 0; i < 45; i++) {
                rate.Advance(1f / 30f, ShownScreen.Curtain);
            }

            rate.Advance(1f / 50f, ShownScreen.Round);
            for (var i = 0; i < 46; i++) {
                rate.Advance(1f / 30f, ShownScreen.Curtain);
            }

            var screens = platform.Events.Where(e => e.Item3 == "screen").Select(e => e.Item2).ToArray();
            Assert.That(screens, Is.EqualTo(new[] { "round-mouse-50", "curtain-mouse-30" }));
            foreach (var (category, what, action) in platform.Events) {
                Assert.That(category, Is.EqualTo("fps"));
                Assert.That(GameEvent.CountNumbers(category, what, action), Is.LessThanOrEqualTo(GameEvent.MaxNumbers));
            }
        }

        [Test]
        public void TheVisitSendsTheMedianOfItsFirstMinuteOfSeconds()
        {
            var platform = new MatchTests.RecordingPlatform();
            var rate = new ScreenFrameRate(platform, new HudControls());

            // Forty seconds at 60 and twenty at 40, with a hidden tab between them that counts for nothing.
            for (var i = 0; i < 60 * 40; i++) {
                rate.Advance(1f / 60f, ShownScreen.None);
            }

            rate.Advance(30f, ShownScreen.None);
            for (var i = 0; i < 40 * 19; i++) {
                rate.Advance(1f / 40f, ShownScreen.None);
            }

            Assert.That(platform.Events, Is.Empty, "a minute of seconds has not been counted yet");
            for (var i = 0; i < 40 * 5; i++) {
                rate.Advance(1f / 40f, ShownScreen.None);
            }

            Assert.That(platform.Events, Is.EqualTo(new[] { ("fps", "visit-mouse-60", "visit") }));
        }

        [TestCase(0f, "0")]
        [TestCase(57.4f, "55")]
        [TestCase(59.8f, "60")]
        [TestCase(66f, "65")]
        [TestCase(120f, "70plus")]
        public void FramesPerSecondGoInBucketsOfFive(float fps, string bucket)
        {
            Assert.That(RoundEvents.FpsBucket(fps), Is.EqualTo(bucket));
        }
    }
}
