using NUnit.Framework;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class SessionProgressTests
    {
        [Test]
        public void KnockoutCarriesRivalIntoNextRoundAndRevengeClearsIt()
        {
            var progress = new SessionProgress();
            Assert.That(progress.Nemesis, Is.EqualTo(RoundResult.Nobody));

            progress.Record(Result(RoundOutcome.Out, 2));
            Assert.That(progress.Round, Is.EqualTo(2));
            Assert.That(progress.Nemesis, Is.EqualTo(2));

            progress.Record(Result(RoundOutcome.Survived, revengeTaken: true));
            Assert.That(progress.Round, Is.EqualTo(3));
            Assert.That(progress.Nemesis, Is.EqualTo(RoundResult.Nobody));
        }

        [TestCase(RoundOutcome.Out, false, 2)]
        [TestCase(RoundOutcome.Survived, false, 2)]
        [TestCase(RoundOutcome.Won, false, RoundResult.Nobody)]
        [TestCase(RoundOutcome.Out, true, RoundResult.Nobody)]
        [TestCase(RoundOutcome.Survived, true, RoundResult.Nobody)]
        public void OnlyAWinOrCreditedRevengeClearsTheRival(RoundOutcome outcome, bool revengeTaken, int expected)
        {
            var progress = new SessionProgress();
            progress.Record(Result(RoundOutcome.Out, 2));
            progress.Record(Result(outcome, revengeTaken: revengeTaken));
            Assert.That(progress.Nemesis, Is.EqualTo(expected));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ANewKnockoutSetsTheNextRivalEvenAfterRevenge(bool revengeTaken)
        {
            var progress = new SessionProgress();
            progress.Record(Result(RoundOutcome.Out, 2));
            progress.Record(Result(RoundOutcome.Out, 3, revengeTaken));
            Assert.That(progress.Nemesis, Is.EqualTo(3));
        }

        [Test]
        public void AnAbsentRivalSurvivesASmallerRound()
        {
            var progress = new SessionProgress();
            progress.Record(Result(RoundOutcome.Out, 6));
            progress.Record(new RoundResult(RoundOutcome.Out, 4, 4, 20f, 0, RoundResult.Nobody,
                FallCause.Drive, false, false));
            Assert.That(progress.Nemesis, Is.EqualTo(6));
        }

        private static RoundResult Result(RoundOutcome outcome, int knockedOutBy = RoundResult.Nobody,
            bool revengeTaken = false)
        {
            return new RoundResult(outcome, outcome == RoundOutcome.Won ? 1 : 2, 7, 30f,
                revengeTaken ? 1 : 0, knockedOutBy, FallCause.None, true, revengeTaken);
        }
    }
}
