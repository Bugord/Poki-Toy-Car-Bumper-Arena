using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The shared team layer on its own, without any mode: who is on which team, their paint and starting slots, a car's
    /// teammates, the team score and its events, and the end of a team round (the team ahead, a draw, the team's best car
    /// for the podium). The modes in teams (TEAM PUSH, FOOTBALL) build on these.
    /// </summary>
    public sealed class TeamLayerTests
    {
        [Test]
        public void ThePlayerIsBlueAndTheBotsAlternate()
        {
            Assert.That(RoundTeams.TeamOf(0, 2), Is.EqualTo(RoundTeams.Blue));
            Assert.That(Enumerable.Range(0, 6).Select(index => RoundTeams.TeamOf(index, 2)),
                Is.EqualTo(new[] { 0, 1, 0, 1, 0, 1 }));
            Assert.That(RoundTeams.TeamOf(3, 0), Is.EqualTo(RoundTeams.None), "No teams, no team.");
            Assert.That(RoundTeams.TeamOf(-1, 2), Is.EqualTo(RoundTeams.None));
            Assert.That(CarPaint.NameOf(RoundTeams.PaintOf(RoundTeams.Blue)), Is.EqualTo("blue"));
            Assert.That(CarPaint.NameOf(RoundTeams.PaintOf(RoundTeams.Red)), Is.EqualTo("red"));
            Assert.That(RoundTeams.IdOf(RoundTeams.Blue), Is.EqualTo("blue"));
            Assert.That(RoundTeams.IdOf(RoundTeams.Red), Is.EqualTo("red"));
        }

        [Test]
        public void EachTeamStartsOnItsOwnSideInSlotsOfItsOwn([Values(2, 4, 6)] int count)
        {
            var slots = Enumerable.Range(0, count).Select(index => RoundTeams.SpawnSlotOf(index, count, 2)).ToList();
            Assert.That(slots.Distinct().Count(), Is.EqualTo(count), "Two cars share a slot.");
            Assert.That(slots.All(slot => slot >= 0 && slot < count), Is.True);
            // A team's slots are a run round the circle, the other team's the run opposite.
            var blue = slots.Where((_, index) => index % 2 == 0).OrderBy(slot => slot).ToList();
            var red = slots.Where((_, index) => index % 2 == 1).OrderBy(slot => slot).ToList();
            Assert.That(Spread(blue, count), Is.LessThan(count / 2f), "Blue's cars are not side by side.");
            Assert.That(Spread(red, count), Is.LessThan(count / 2f), "Red's cars are not side by side.");
            Assert.That(Enumerable.Range(0, count).Select(index => RoundTeams.SpawnSlotOf(index, count, 0)),
                Is.EqualTo(Enumerable.Range(0, count)), "Without teams the slots are the indices.");
        }

        [Test]
        public void ATeamsPointsAreItsCarsAndItsOwnAndEveryChangeIsRaised()
        {
            var score = new MatchScore(6, 1, 2);
            var changes = new List<(int team, int change)>();
            score.TeamChanged += (team, change) => changes.Add((team, change));

            score.RecordFall(1, 0, 1f);
            score.AddTeamPoints(RoundTeams.Red, 1);
            score.AddPoints(4, 2, 2f);
            score.RecordFall(3, RoundResult.Nobody, 3f);

            Assert.That(score.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(3));
            Assert.That(score.TeamPointsOf(RoundTeams.Red), Is.EqualTo(1));
            Assert.That(score.TeamOwnPointsOf(RoundTeams.Red), Is.EqualTo(1));
            Assert.That(score.TeamOwnPointsOf(RoundTeams.Blue), Is.Zero);
            Assert.That(score.TeamScores(), Is.EqualTo(new[] { 3, 1 }));
            Assert.That(score.LeadingTeam, Is.EqualTo(RoundTeams.Blue));
            Assert.That(changes, Is.EqualTo(new[] { (RoundTeams.Blue, 1), (RoundTeams.Red, 1), (RoundTeams.Blue, 2) }),
                "A fall of its own is no point by itself; the mode decides.");

            score.AddTeamPoints(RoundTeams.Red, 2);
            Assert.That(score.LeadingTeam, Is.EqualTo(RoundTeams.None), "Level is a draw.");
            score.AddTeamPoints(RoundTeams.Red, -10);
            Assert.That(score.TeamOwnPointsOf(RoundTeams.Red), Is.Zero, "A team's own points stay at 0 or more.");
            Assert.That(score.Snapshot().Select(standing => standing.Team), Is.EquivalentTo(new[] { 0, 1, 0, 1, 0, 1 }));
        }

        [Test]
        public void WithoutTeamsThereIsNoTeamScore()
        {
            var score = new MatchScore(4);
            var raised = false;
            score.TeamChanged += (_, _) => raised = true;
            score.RecordFall(1, 0, 1f);
            score.AddTeamPoints(0, 3);
            Assert.That(raised, Is.False);
            Assert.That(score.Teams, Is.Zero);
            Assert.That(score.TeamScores(), Is.Empty);
            Assert.That(score.LeadingTeam, Is.EqualTo(RoundTeams.None));
            Assert.That(score.TeamOf(1), Is.EqualTo(RoundTeams.None));
        }

        [Test]
        public void TheEndOfATeamRoundGoesByTheTeams()
        {
            var standings = new[] {
                new RoundStanding(3, 1, 2, 0, 4, RoundTeams.Red), new RoundStanding(0, 2, 1, 0, 2, RoundTeams.Blue),
                new RoundStanding(1, 3, 0, 0, 1, RoundTeams.Red), new RoundStanding(2, 4, 0, 0, 0, RoundTeams.Blue),
            };
            RoundResult End(params int[] points) => new(RoundOutcome.Survived, 4, 4, 60f, 1, RoundResult.Nobody,
                FallCause.None, false, false, standings, isTimed: true, teams: 2, teamPoints: points);

            var redAhead = End(2, 7);
            Assert.That(redAhead.WinningTeam, Is.EqualTo(RoundTeams.Red));
            Assert.That(redAhead.IsDraw, Is.False);
            Assert.That(redAhead.BestOf(RoundTeams.Red), Is.EqualTo(3));
            Assert.That(redAhead.PodiumCar, Is.EqualTo(3), "The podium shows the winning team's best car.");

            var blueAhead = End(9, 7);
            Assert.That(blueAhead.WinningTeam, Is.EqualTo(RoundTeams.Blue));
            Assert.That(blueAhead.PodiumCar, Is.Zero, "The player is blue's best car.");

            var level = End(5, 5);
            Assert.That(level.IsDraw, Is.True);
            Assert.That(level.PodiumCar, Is.Zero, "A draw shows the player's team.");
            Assert.That(level.TeamPointsOf(RoundTeams.Red), Is.EqualTo(5));

            var plain = new RoundResult(RoundOutcome.Won, 1, 4, 60f, 0, RoundResult.Nobody, FallCause.None, false, false);
            Assert.That(plain.IsDraw, Is.False);
            Assert.That(plain.WinningTeam, Is.EqualTo(RoundTeams.None));
            Assert.That(plain.TeamPoints, Is.Empty);
        }

        // How far round the circle a team's slots spread: the gap left by the biggest jump between them.
        private static float Spread(List<int> slots, int count)
        {
            if (slots.Count < 2) {
                return 0f;
            }

            var biggest = 0;
            for (var i = 0; i < slots.Count; i++) {
                var next = slots[(i + 1) % slots.Count] + (i + 1 == slots.Count ? count : 0);
                biggest = Mathf.Max(biggest, next - slots[i]);
            }

            return count - biggest;
        }
    }
}
