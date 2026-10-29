using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay.Round;

namespace ToyCarArena.Tests.PlayMode
{
    public sealed class RoundStandingsTests
    {
        [Test]
        public void EarlierFallsRankBehindLaterFallsAndEachKnockerGetsCredit()
        {
            var round = new RoundStandings(4);
            round.RecordOut(3, 1);
            round.CompleteFrame();
            round.RecordOut(2, 0);
            round.CompleteFrame();
            round.RecordOut(1, 0);
            round.CompleteFrame();
            var scores = round.Snapshot();
            Assert.That(scores.Select(score => score.Index), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(scores.Select(score => score.Place), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(scores.Select(score => score.Knockouts), Is.EqualTo(new[] { 2, 1, 0, 0 }));
            Assert.That(scores.Select(score => score.OutOrder), Is.EqualTo(new[] { 0, 3, 2, 1 }));
        }

        [Test]
        public void SameFrameFallsShareTheExistingPlayerPlace()
        {
            var round = new RoundStandings(5);
            round.RecordOut(0, 1);
            round.RecordOut(3, RoundResult.Nobody);
            round.RecordOut(4, 2);
            round.CompleteFrame();
            Assert.That(round.Snapshot().Where(score => score.OutOrder > 0).Select(score => score.Place),
                Is.All.EqualTo(3), "Two survivors: the player's existing result is #3, including simultaneous falls.");
        }

        [Test]
        public void AllCarsFallingTogetherNeverAwardsAnOutPlayerFirstPlace()
        {
            var round = new RoundStandings(2);
            round.RecordOut(0, 1);
            round.RecordOut(1, 0);
            round.CompleteFrame();
            Assert.That(round.Snapshot().Select(score => score.Place), Is.All.EqualTo(2));
        }

        [Test]
        public void SurvivorsShareFirstAndDisplayInKnockoutOrder()
        {
            var round = new RoundStandings(4);
            round.RecordOut(3, 2);
            round.CompleteFrame();
            var scores = round.Snapshot();
            Assert.That(scores.Take(3).Select(score => score.Place), Is.All.EqualTo(1));
            Assert.That(scores.Take(3).Select(score => score.Index), Is.EqualTo(new[] { 2, 0, 1 }));
        }

        [Test]
        public void SnapshotDoesNotChangeAfterLaterRecordsAndDuplicateFallsDoNotCountTwice()
        {
            var round = new RoundStandings(3);
            round.RecordOut(2, 1);
            round.RecordOut(2, 1);
            round.CompleteFrame();
            var snapshot = round.Snapshot();
            round.RecordOut(1, 0);
            round.CompleteFrame();
            Assert.That(snapshot.Single(score => score.Index == 1).Place, Is.EqualTo(1));
            Assert.That(snapshot.Single(score => score.Index == 1).Knockouts, Is.EqualTo(1));
            Assert.That(round.Snapshot().Single(score => score.Index == 1).Place, Is.EqualTo(2));
        }

        [Test]
        public void SingleCarKeepsItsExistingPlace()
        {
            var round = new RoundStandings(1);
            round.RecordOut(0, RoundResult.Nobody);
            round.CompleteFrame();
            Assert.That(round.Snapshot()[0].Place, Is.EqualTo(1));
        }
    }
}
