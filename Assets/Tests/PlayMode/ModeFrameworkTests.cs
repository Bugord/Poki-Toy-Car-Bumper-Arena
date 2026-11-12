using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The common framework of the event modes: every round kind's <see cref="ModeRules"/>, the score of a timed round
    /// (points, ties, teams) and a timed scored round played on a stub mode in the real Game scene: it ends by time, the
    /// places go by points, the result carries them and the reward caps the knock-outs.
    /// </summary>
    public sealed class ModeFrameworkTests : ObjectsBFixture
    {
        private const float ShortRound = 10f;

        private static readonly FieldInfo CrownDuration =
            typeof(GameTuning.CrownSettings).GetField("duration", BindingFlags.NonPublic | BindingFlags.Instance);

        private float savedCrownDuration = -1f;

        [TearDown]
        public void RestoreCrown()
        {
            if (savedCrownDuration >= 0f) {
                CrownDuration.SetValue(LoadTuning().Crown, savedCrownDuration);
                savedCrownDuration = -1f;
            }
        }

        [Test]
        public void EveryKindOfRoundHasItsRules()
        {
            var tuning = LoadTuning();
            var plain = tuning.RulesOf(SpecialRound.None);
            Assert.That(plain.Fall, Is.EqualTo(FallRule.Out));
            Assert.That(plain.Floor, Is.EqualTo(FloorRule.Crumble));
            Assert.That(plain.HasMechanics && plain.HasEdgeAssist && !plain.HasFence && !plain.IsTimed, Is.True);
            Assert.That(plain.Pickups, Is.EqualTo(PickupRule.Usual));

            // A special round of the rotation changes one rule of its own, not the round's.
            Assert.That(tuning.RulesOf(SpecialRound.LowGravity), Is.SameAs(ModeRules.Plain));

            var potato = tuning.RulesOf(SpecialRound.HotPotato);
            Assert.That(potato.Fall, Is.EqualTo(FallRule.PutBack));
            Assert.That(potato.HasFence, Is.True);
            Assert.That(potato.Floor, Is.EqualTo(FloorRule.Still));
            Assert.That(potato.HasMechanics || potato.HasEdgeAssist || potato.IsTimed, Is.False);
            Assert.That(potato.Pickups, Is.EqualTo(PickupRule.None));

            var match = ModeRules.Match(tuning);
            Assert.That(match.Fall, Is.EqualTo(FallRule.Respawn));
            Assert.That(match.Floor, Is.EqualTo(FloorRule.Cycles));
            Assert.That(match.Pickups, Is.EqualTo(PickupRule.Always));
            Assert.That(match.Duration, Is.EqualTo(tuning.Match.Duration));
            Assert.That(match.PointsPerKnockout, Is.EqualTo(1));
            Assert.That(match.TopPlacesOf(7), Is.EqualTo(tuning.Match.TopPlaces));

            foreach (var mode in EventModes.Circle) {
                var kind = EventModes.KindOf(mode);
                Assert.That(EventModes.ModeOf(kind), Is.EqualTo(mode), mode.ToString());
                Assert.That(EventModes.Parse(EventModes.IdOf(mode)), Is.EqualTo(mode), mode.ToString());
                Assert.That(RoundRules.IdOf(kind), Is.EqualTo(EventModes.IdOf(mode)));
                Assert.That(tuning.RulesOf(kind), Is.Not.SameAs(ModeRules.Plain), mode + " has no rules of its own.");
            }

            var frenzy = tuning.RulesOf(SpecialRound.ItemFrenzy);
            Assert.That(frenzy.Pickups, Is.EqualTo(PickupRule.Always));
            Assert.That(frenzy.PickupPace, Is.GreaterThan(1f));
            Assert.That(frenzy.PickupMax, Is.GreaterThan(tuning.Pickups.MaxCount));
            Assert.That(tuning.RulesOf(SpecialRound.TeamPush).Teams, Is.EqualTo(2));
            Assert.That(tuning.RulesOf(SpecialRound.Crown).IsTimed, Is.True);
            Assert.That(tuning.RulesOf(SpecialRound.Crown).KnockoutRewardCap, Is.GreaterThan(0));
        }

        [Test]
        public void OnlyTheHotPotatoIsOnAndOnlyInABuildOfRounds()
        {
            var tuning = LoadTuning();
            Assert.That(tuning.IsModeEnabled(EventMode.HotPotato), Is.EqualTo(tuning.HasEvents));
            foreach (var mode in EventModes.Circle.Where(mode => mode != EventMode.HotPotato)) {
                Assert.That(tuning.IsModeEnabled(mode), Is.False, mode + " is a stub and must stay off.");
            }

            Assert.That(tuning.IsModeEnabled(EventMode.None), Is.False);
        }

        [Test]
        public void TeamsMakeTheCarCountEven()
        {
            var tuning = LoadTuning();
            var teams = tuning.RulesOf(SpecialRound.TeamPush);
            for (var bots = 1; bots <= 6; bots++) {
                var count = teams.BotCountFor(bots, 6) + 1;
                Assert.That(count % 2, Is.Zero, $"{bots} bots made {count} cars.");
            }

            // A mode with a number of its own keeps it, whatever the difficulty brings.
            var football = tuning.RulesOf(SpecialRound.Football);
            Assert.That(football.BotCountFor(6, 6), Is.EqualTo(football.BotCountFor(1, 6)));
            Assert.That((football.BotCountFor(6, 6) + 1) % 2, Is.Zero);
            Assert.That(ModeRules.Plain.BotCountFor(5, 6), Is.EqualTo(5));
        }

        [Test]
        public void PointsDecideAndTiesGoToWhoScoredFirst()
        {
            var score = new MatchScore(4, pointsPerKnockout: 0);
            score.AddPoints(2, 3, 1f);
            score.AddPoints(1, 3, 2f);
            score.AddPoints(3, 1, 0.5f);
            Assert.That(score.IndexAt(0), Is.EqualTo(2), "The car that reached 3 first is not first.");
            Assert.That(score.IndexAt(1), Is.EqualTo(1));
            Assert.That(score.IndexAt(2), Is.EqualTo(3));
            Assert.That(score.IndexAt(3), Is.EqualTo(0));

            // A knock-out scores nothing in a mode that counts something else, but a fall still costs the tie.
            score.RecordFall(2, 1, 3f);
            Assert.That(score.PointsOf(1), Is.EqualTo(3));
            Assert.That(score.KnockoutsOf(1), Is.EqualTo(1));
            Assert.That(score.IndexAt(0), Is.EqualTo(1), "The fall did not lose the tie.");

            score.AddPoints(1, -10, 4f);
            Assert.That(score.PointsOf(1), Is.Zero, "Points went below zero.");

            var standings = score.Snapshot();
            Assert.That(standings.First(s => s.Index == 2).Points, Is.EqualTo(3));
            Assert.That(standings.First(s => s.Index == 1).Knockouts, Is.EqualTo(1));
        }

        [Test]
        public void ATeamRoundIsDecidedByTheTeamsSum()
        {
            var score = new MatchScore(4, pointsPerKnockout: 0, teams: 2);
            Assert.That(score.TeamOf(0), Is.EqualTo(score.TeamOf(2)));
            Assert.That(score.TeamOf(1), Is.Not.EqualTo(score.TeamOf(0)));
            Assert.That(score.LeadingTeam, Is.EqualTo(-1), "A level score is not a draw.");

            score.AddPoints(1, 2, 1f);
            score.AddPoints(0, 1, 2f);
            score.AddPoints(2, 2, 3f);
            Assert.That(score.TeamPointsOf(score.TeamOf(0)), Is.EqualTo(3));
            Assert.That(score.LeadingTeam, Is.EqualTo(score.TeamOf(0)));
            Assert.That(score.Snapshot().First(s => s.Index == 3).Team, Is.EqualTo(score.TeamOf(1)));
        }

        [Test]
        public void TheRewardOfATimedModeCapsItsKnockouts()
        {
            var capped = new RoundResult(RoundOutcome.Won, 1, 7, 60f, 12, RoundResult.Nobody, FallCause.None, false,
                false, isTimed: true, kind: SpecialRound.Crown, knockoutRewardCap: 5);
            Assert.That(capped.RewardKnockouts, Is.EqualTo(5));
            Assert.That(capped.Knockouts, Is.EqualTo(12), "The stats must see every knock-out.");

            var match = new RoundResult(RoundOutcome.Won, 1, 7, 60f, 12, RoundResult.Nobody, FallCause.None, false,
                false, isMatch: true);
            Assert.That(match.RewardKnockouts, Is.EqualTo(12), "The match pays every knock-out, as it did.");
            Assert.That(match.IsTimed, Is.True);
        }

        [UnityTest]
        public IEnumerator ATimedModeEndsByTimeAndPlacesByPoints()
        {
            var tuning = LoadTuning();
            savedCrownDuration = tuning.Crown.Duration;
            CrownDuration.SetValue(tuning.Crown, ShortRound);
            UseSpecial(SpecialRound.Crown);
            yield return Open("sandbox", "rake");
            Assert.That(Round.IsTimed, Is.True);
            Assert.That(Round.Mode.Fall, Is.EqualTo(FallRule.Respawn));
            Assert.That(Round.Score, Is.Not.Null);
            Assert.That(Round.Cars.Count, Is.GreaterThanOrEqualTo(3));

            RoundResult? result = null;
            Round.Finished += r => result = r;
            var changes = 0;
            Round.ScoreChanged += _ => changes++;
            var leader = Round.Cars[2];
            var second = Round.Cars[1];
            Assert.That(Round.AddPoints(leader, 5), Is.True);
            Assert.That(Round.AddPoints(Round.Player, 3), Is.True);
            Assert.That(Round.AddPoints(second, 3), Is.True);
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(Round.Score.IndexAt(0), Is.EqualTo(2));
            Assert.That(Round.Score.PlaceOf(0), Is.EqualTo(2), "The player scored 3 first and is not second.");

            // Time running out ends it, not the cars left.
            yield return Wait(ShortRound - 2f);
            Assert.That(result.HasValue, Is.False, "The timed round ended before its time.");
            var timeout = Time.time + 4f;
            while (!result.HasValue && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(result.HasValue, Is.True, "The timed round did not end when its time ran out.");
            var end = result.Value;
            Assert.That(end.IsTimed, Is.True);
            Assert.That(end.IsMatch, Is.False, "A mode in a build of rounds is not the build's match.");
            Assert.That(end.Kind, Is.EqualTo(SpecialRound.Crown));
            Assert.That(end.Place, Is.EqualTo(2));
            Assert.That(end.Outcome, Is.EqualTo(RoundOutcome.Survived));
            Assert.That(end.Points, Is.EqualTo(3));
            Assert.That(end.IsLoss, Is.EqualTo(2 > end.CarCount / 2));
            Assert.That(end.Standings[0].Index, Is.EqualTo(2));
            Assert.That(end.Standings[0].Points, Is.EqualTo(5));
            Assert.That(end.PodiumCar, Is.EqualTo(2));
            Assert.That(end.KnockoutRewardCap, Is.EqualTo(tuning.Crown.KnockoutRewardCap));
        }

        [UnityTest]
        public IEnumerator ACarThatFallsInARespawnModeComesBack()
        {
            var tuning = LoadTuning();
            savedCrownDuration = tuning.Crown.Duration;
            CrownDuration.SetValue(tuning.Crown, ShortRound * 3f);
            UseSpecial(SpecialRound.Crown);
            yield return Open("sandbox", "rake");
            var car = Round.Cars[1];
            car.Movement.Place(new Vector3(0f, tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(car);
            yield return Step(car);
            Assert.That(car.IsOut, Is.True, "The car below the fall height is not out.");
            var timeout = Time.time + tuning.Match.RespawnDelay + 2f;
            while (car.IsOut && Time.time < timeout) {
                yield return Step(car);
            }

            Assert.That(car.IsOut, Is.False, "The car that fell did not come back.");
            Assert.That(Round.IsFinished, Is.False);
        }
    }
}
