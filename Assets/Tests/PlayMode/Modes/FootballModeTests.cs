using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The FOOTBALL mode in the real Game scene: the round plays on the mode's own pitch with two goals and a ball; a goal
    /// is the scoring team's, and the point of the car of that team that touched the ball last, an own goal the team's
    /// alone; after a goal everyone goes back to kick-off; the ball and the cars never leave the pitch; the round ends by
    /// time with a win, a loss or a draw and the player's goals paid as knock-outs; the bots alone score; plain rounds and
    /// the rotation never see the pitch.
    /// </summary>
    public sealed class FootballModeTests : ModeFixture
    {
        private const float ShortRound = 4f;

        private readonly List<(object target, FieldInfo field, object value)> saved = new();

        [TearDown]
        public void RestoreTuning()
        {
            for (var i = saved.Count - 1; i >= 0; i--) {
                var (target, field, value) = saved[i];
                field.SetValue(target, value);
            }

            saved.Clear();
        }

        [UnityTest]
        public IEnumerator ItsRoundPlaysItsRules()
        {
            yield return OpenMode(SpecialRound.Football);
            ExpectRules<FootballMode>(SpecialRound.Football);
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent() => ExpectSilent<FootballMode>();

        [UnityTest]
        public IEnumerator ItPlaysOnItsOwnPitchWithTwoGoalsAndABall()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            Assert.That(Container.Resolve<ArenaMap>().Id, Is.EqualTo(GameTuning.FootballSettings.PitchId));
            Assert.That(Container.Resolve<ArenaMap>().Variant, Is.EqualTo(GameTuning.FootballSettings.PitchVariant));
            Assert.That(Round.Cars.Count, Is.EqualTo(6), "Three against three.");
            Assert.That(mode.Ball, Is.Not.Null, "No ball.");
            Assert.That(GameObject.Find("Red Goal"), Is.Not.Null);
            Assert.That(GameObject.Find("Blue Goal"), Is.Not.Null);
            Assert.That(Flat(mode.BallPosition).magnitude, Is.LessThan(0.5f), "The ball is not on the centre spot.");
            Assert.That(mode.State, Is.EqualTo(FootballMode.Phase.Play));
            // Each team on its own half, the player blue in front of its own goal, facing the far one.
            foreach (var car in Round.Cars) {
                var end = FootballPitch.EndOf(car.Team);
                Assert.That(car.Position.z * end, Is.GreaterThan(1f), $"Car {Round.IndexOf(car)} is not on its half.");
            }

            Assert.That(Round.Player.Team, Is.EqualTo(RoundTeams.Blue));
            Assert.That(Round.Player.Position.z, Is.LessThan(0f));
            Assert.That(Round.Player.transform.forward.z, Is.GreaterThan(0.9f), "The player does not face the red goal.");
        }

        [UnityTest]
        public IEnumerator AGoalIsThePointOfTheScoringTeamsLastToucher()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            var goals = new List<(int team, Car author)>();
            mode.Scored += (team, author) => goals.Add((team, author));

            yield return Shoot(mode, Round.Player, RoundTeams.Red);
            Assert.That(goals.Count, Is.EqualTo(1), "No goal.");
            Assert.That(goals[0], Is.EqualTo((RoundTeams.Blue, Round.Player)));
            Assert.That(Round.Score.PointsOf(0), Is.EqualTo(1), "The goal is not the player's.");
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(1));
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.Zero);
            Assert.That(mode.PlayerGoals, Is.EqualTo(1));
            Assert.That(mode.State, Is.EqualTo(FootballMode.Phase.Celebrating));

            // In the net it counts once.
            yield return Steps(20);
            Assert.That(goals.Count, Is.EqualTo(1), "The ball in the net scored again.");

            // A red car scores in blue's goal.
            yield return WaitForPlay(mode);
            var red = Round.Cars[1];
            yield return Shoot(mode, red, RoundTeams.Blue);
            Assert.That(goals.Count, Is.EqualTo(2));
            Assert.That(goals[1], Is.EqualTo((RoundTeams.Red, red)));
            Assert.That(Round.Score.PointsOf(1), Is.EqualTo(1));
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AnOwnGoalIsTheOtherTeamsAndNobodysOwn()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            var goals = new List<(int team, Car author)>();
            mode.Scored += (team, author) => goals.Add((team, author));
            var red = Round.Cars[1];

            yield return Shoot(mode, red, RoundTeams.Red);
            Assert.That(goals.Count, Is.EqualTo(1), "No goal.");
            Assert.That(goals[0], Is.EqualTo((RoundTeams.Blue, (Car)null)), "An own goal had an author.");
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(1));
            Assert.That(Round.Score.PointsOf(0), Is.Zero, "The player was paid for red's own goal.");
            Assert.That(Round.Score.PointsOf(1), Is.Zero, "The red car scored for itself.");
            Assert.That(mode.PlayerGoals, Is.Zero);

            // Nobody's ball is the team's as well.
            yield return WaitForPlay(mode);
            yield return Shoot(mode, null, RoundTeams.Blue);
            Assert.That(goals[1], Is.EqualTo((RoundTeams.Red, (Car)null)));
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AfterAGoalEveryoneGoesBackToKickOff()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            var places = Round.Cars.ToDictionary(car => car, car => Flat(car.Position));
            // Everyone somewhere else.
            foreach (var car in Round.Cars) {
                car.Movement.Place(new Vector3(Random.Range(-6f, 6f), 0.6f, Random.Range(-10f, 10f)),
                    car.transform.rotation);
            }

            yield return Shoot(mode, Round.Player, RoundTeams.Red);
            Assert.That(mode.State, Is.EqualTo(FootballMode.Phase.Celebrating));
            var timeout = Time.time + mode.Settings.CelebrateTime + 1f;
            while (mode.State == FootballMode.Phase.Celebrating && Time.time < timeout) {
                yield return null;
            }

            Assert.That(mode.State, Is.EqualTo(FootballMode.Phase.Kickoff), "The celebration never ended.");
            yield return new WaitForFixedUpdate();
            Assert.That(mode.Ball.Body.isKinematic, Is.True, "The ball is in play at the kick-off.");
            Assert.That(Flat(mode.BallPosition).magnitude, Is.LessThan(0.1f), "The ball is not on the spot.");
            Assert.That(mode.Ball.LastTouch, Is.Null, "The kick-off kept the last touch.");
            foreach (var car in Round.Cars) {
                Assert.That(Vector3.Distance(Flat(car.Position), places[car]), Is.LessThan(0.6f),
                    $"Car {Round.IndexOf(car)} is not in its place.");
            }

            // The player's input does nothing while the teams wait.
            Assert.That(Round.Player.Movement.IsStaggered, Is.True);
            yield return WaitForPlay(mode);
            Assert.That(mode.Ball.Body.isKinematic, Is.False, "The ball never came into play.");
        }

        [UnityTest]
        public IEnumerator TheBallStaysOnThePitch()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            var body = mode.Ball.Body;
            var settings = mode.Settings;
            var half = settings.PitchHalfSize;
            var shots = new[] {
                new Vector3(60f, 30f, 0f), new Vector3(-50f, 40f, 30f), new Vector3(0f, 50f, 60f),
                new Vector3(40f, 10f, -40f), new Vector3(-30f, 5f, -60f), new Vector3(25f, 60f, 45f),
            };
            foreach (var shot in shots) {
                body.position = new Vector3(0f, settings.BallRadius, 2f);
                body.linearVelocity = shot;
                for (var i = 0; i < 150; i++) {
                    yield return new WaitForFixedUpdate();
                    var position = body.position;
                    Assert.That(Mathf.Abs(position.x), Is.LessThan(half.x), $"The ball left the side at {position} ({shot}).");
                    Assert.That(Mathf.Abs(position.z), Is.LessThan(half.y), $"The ball left the end at {position} ({shot}).");
                    Assert.That(position.y, Is.LessThan(settings.BallMaxHeight + 0.3f), $"The ball flew at {position}.");
                    Assert.That(position.y, Is.GreaterThan(0f), $"The ball sank at {position}.");
                    Assert.That(Flat(body.linearVelocity).magnitude, Is.LessThanOrEqualTo(settings.BallMaxSpeed + 0.5f));
                }
            }
        }

        /// <summary>A ball still in a corner, nobody near, is nudged out into play.</summary>
        [UnityTest]
        public IEnumerator AStillBallInACornerComesOut()
        {
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            var body = mode.Ball.Body;
            var settings = mode.Settings;
            var corner = new Vector3(settings.PitchHalfSize.x - 1.3f, settings.BallRadius, 9f);
            body.position = corner;
            body.linearVelocity = Vector3.zero;
            // Every car far from it.
            foreach (var car in Round.Cars) {
                car.Movement.Place(new Vector3(-8f, 0.6f, -6f + Round.IndexOf(car) * 1.5f), car.transform.rotation);
            }

            var timeout = Time.time + settings.StuckTime + 2f;
            while (Vector3.Distance(Flat(body.position), Flat(corner)) < 1.5f && Time.time < timeout) {
                foreach (var car in Round.Cars) {
                    car.Movement.Drive(Vector3.zero, 0f);
                }

                yield return new WaitForFixedUpdate();
            }

            Assert.That(Vector3.Distance(Flat(body.position), Flat(corner)), Is.GreaterThan(1.5f), "The ball stayed stuck.");
        }

        /// <summary>The bots play alone (the player stands still): they score in a minute and nobody leaves the pitch.</summary>
        [UnityTest]
        public IEnumerator TheBotsAloneScoreAndNobodyLeavesThePitch()
        {
            SetField(LoadTuning().Football, "duration", 120f);
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            LetBotsDrive();
            var half = mode.Settings.PitchHalfSize;
            var until = Time.time + 75f;
            while (mode.Goals == 0 && Time.time < until) {
                yield return new WaitForFixedUpdate();
                foreach (var car in Round.Cars) {
                    var position = car.Position;
                    Assert.That(Mathf.Abs(position.x) < half.x && Mathf.Abs(position.z) < half.y && position.y > -1f,
                        Is.True, $"Car {Round.IndexOf(car)} left the pitch at {position}.");
                }

                var ball = mode.BallPosition;
                Assert.That(Mathf.Abs(ball.x) < half.x && Mathf.Abs(ball.z) < half.y, Is.True, $"The ball left at {ball}.");
            }

            Assert.That(mode.Goals, Is.GreaterThan(0), "The bots scored nothing in 75 seconds.");
            Debug.Log($"[Football] bots scored at {Round.Elapsed:0.0} s");
        }

        [UnityTest]
        public IEnumerator TheRoundEndsByTime()
        {
            SetField(LoadTuning().Football, "duration", ShortRound);
            yield return OpenMode(SpecialRound.Football);
            RoundResult? result = null;
            Round.Finished += r => result = r;
            yield return WaitSeconds(ShortRound - 1.5f);
            Assert.That(result.HasValue, Is.False, "The round ended before its time.");
            var timeout = Time.time + 3f;
            while (!result.HasValue && Time.time < timeout) {
                yield return null;
            }

            Assert.That(result.HasValue, Is.True, "The round did not end when its time ran out.");
            Assert.That(result.Value.IsTimed, Is.True);
            Assert.That(result.Value.Kind, Is.EqualTo(SpecialRound.Football));
            Assert.That(result.Value.Teams, Is.EqualTo(2));
        }

        /// <summary>A goal in the last frames of the match counts.</summary>
        [UnityTest]
        public IEnumerator AGoalOnTheBuzzerCounts()
        {
            SetField(LoadTuning().Football, "duration", ShortRound);
            yield return OpenMode(SpecialRound.Football);
            var mode = Container.Resolve<FootballMode>();
            RoundResult? result = null;
            Round.Finished += r => result = r;
            // A quarter of a second left: the ball needs two physics steps to cross, and the round's clock runs in frames,
            // so closer than that the test would race the clock rather than check the goal.
            while (Round.TimeLeft > 0.25f) {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(result.HasValue, Is.False);
            // Right on the line: in with the next steps, in the match's last frames.
            yield return Shoot(mode, Round.Player, RoundTeams.Red, -mode.Settings.BallRadius * 0.6f + 0.05f);
            var timeout = Time.time + 2f;
            while (!result.HasValue && Time.time < timeout) {
                yield return null;
            }

            Assert.That(result.HasValue, Is.True);
            Assert.That(result.Value.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(1), "The last-second goal was lost.");
            Assert.That(result.Value.Outcome, Is.EqualTo(RoundOutcome.Won));
        }

        /// <summary>
        /// More goals win, fewer lose, level draws; the player's goals pay as knock-outs, at most five, and a loss pays
        /// no win and breaks the streak.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTeamWithMoreGoalsWinsAndGoalsPayAsKnockOuts([Values("win", "loss", "draw")] string end)
        {
            SetField(LoadTuning().Football, "duration", ShortRound);
            yield return OpenMode(SpecialRound.Football);
            RoundResult? result = null;
            Round.Finished += r => result = r;
            Assert.That(Round.AddPoints(Round.Player, 7), Is.True, "Seven goals of the player's.");
            Assert.That(Round.AddTeamPoints(RoundTeams.Red, end == "win" ? 2 : end == "draw" ? 7 : 9), Is.True);
            var timeout = Time.time + ShortRound + 3f;
            while (!result.HasValue && Time.time < timeout) {
                yield return null;
            }

            Assert.That(result.HasValue, Is.True, "The round did not end.");
            var r = result.Value;
            Assert.That(r.Points, Is.EqualTo(7));
            Assert.That(r.Knockouts, Is.Zero, "Football knocks nobody out.");
            Assert.That(r.RewardKnockouts, Is.EqualTo(5), "The goals are not paid as knock-outs, capped at five.");
            Assert.That(r.HadNemesis, Is.False, "Football has no REVENGE.");
            switch (end) {
                case "win":
                    Assert.That(r.Outcome, Is.EqualTo(RoundOutcome.Won));
                    Assert.That(r.IsLoss, Is.False);
                    break;
                case "loss":
                    Assert.That(r.IsLoss, Is.True);
                    Assert.That(r.WinningTeam, Is.EqualTo(RoundTeams.Red));
                    break;
                default:
                    Assert.That(r.IsDraw, Is.True);
                    Assert.That(r.IsLoss, Is.False);
                    break;
            }

            var tuning = LoadTuning();
            var storage = new RoundOverScreenshots.MemoryStorage();
            var progress = new PlayerProgress(storage, tuning, LoadCarCatalog());
            var wallet = new PlayerWallet(storage, tuning);
            var xp = progress.Record(r);
            var coins = wallet.Record(r, false, 0);
            Assert.That(xp.KnockoutXp, Is.EqualTo(5 * tuning.Progress.KnockoutXp), "The goals' XP.");
            Assert.That(coins.KnockoutCoins, Is.EqualTo(5 * tuning.Coins.KnockoutCoins), "The goals' coins.");
            Assert.That(coins.WinCoins, end == "win" ? Is.GreaterThan(0) : Is.Zero);
        }

        [UnityTest]
        public IEnumerator APlainRoundPlaysTheRotationsArena()
        {
            yield return Open("tabletop", "board");
            Assert.That(Container.Resolve<ArenaMap>().Id, Is.EqualTo("tabletop"));
            Assert.That(GameObject.Find("FootballPitch"), Is.Null);
            Assert.That(Container.Resolve<FootballMode>().Ball, Is.Null);
        }

        /// <summary>The rotation never deals the pitch, and a round of football does not move it on.</summary>
        [Test]
        public void TheRotationNeverSeesThePitch()
        {
            var tuning = LoadTuning();
            var storage = new RoundOverScreenshots.MemoryStorage();
            var rotation = new ArenaRotation(tuning, storage);
            for (var level = 1; level < 30; level++) {
                rotation.Next(level);
                Assert.That(rotation.Current, Is.Not.EqualTo(GameTuning.FootballSettings.PitchId));
            }

            var progress = new SimulationProgress { Round = 5, Mode = EventMode.Football };
            var modes = new ModeArenaRotation(rotation, tuning, progress);
            var before = rotation.Current;
            Assert.That(modes.Current, Is.EqualTo(GameTuning.FootballSettings.PitchId));
            Assert.That(modes.CurrentVariant, Is.EqualTo(GameTuning.FootballSettings.PitchVariant));
            modes.MarkPlayed(modes.Current, modes.CurrentVariant);
            modes.Next(30);
            Assert.That(rotation.Current, Is.EqualTo(before), "A round of football moved the rotation on.");

            progress.Mode = EventMode.None;
            Assert.That(modes.Current, Is.EqualTo(before), "A plain round after football is not the rotation's.");
        }

        /// <summary>
        /// The mode's own analytics: the match's goals and the player's, one number each, beside the shared events of a
        /// mode and of a round in teams (<see cref="FootballMode.EndEvents"/>, silent outside its round).
        /// </summary>
        [Test]
        public void ItsAnalyticsCountTheGoals()
        {
            string[] Names(int goals, int playerGoals) =>
                FootballMode.EventsOf(goals, playerGoals).Select(e => $"special / {e.id} / {e.action}").ToArray();
            Assert.That(Names(5, 2), Is.EqualTo(new[] {
                "special / football-goals-5 / goals", "special / football-scored-2 / player-goals" }));
            Assert.That(Names(12, 7), Is.EqualTo(new[] {
                "special / football-goals-10plus / goals", "special / football-scored-5plus / player-goals" }));
            Assert.That(Names(0, 0), Is.EqualTo(new[] {
                "special / football-goals-0 / goals", "special / football-scored-0 / player-goals" }));
        }

        /// <summary>
        /// Puts the ball just short of the goal line of <paramref name="goal"/>'s goal, touched last by
        /// <paramref name="toucher"/>, rolling in, and waits for the goal.
        /// </summary>
        private IEnumerator Shoot(FootballMode mode, Car toucher, int goal, float shortOfLine = 0.5f)
        {
            var body = mode.Ball.Body;
            var end = FootballPitch.EndOf(goal);
            mode.Ball.Forget();
            if (toucher != null) {
                mode.Ball.Touch(toucher);
            }

            body.position = new Vector3(0f, mode.Settings.BallRadius, end * (mode.Settings.GoalLine - shortOfLine));
            body.linearVelocity = new Vector3(0f, 0f, end * 8f);
            var goals = mode.Goals;
            var timeout = Time.time + 1.5f;
            while (mode.Goals == goals && Time.time < timeout) {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitForPlay(FootballMode mode)
        {
            var timeout = Time.time + mode.Settings.CelebrateTime + mode.Settings.KickoffWait + 2f;
            while (mode.State != FootballMode.Phase.Play && Time.time < timeout) {
                yield return null;
            }

            Assert.That(mode.State, Is.EqualTo(FootballMode.Phase.Play), "Play never went on.");
        }

        private static IEnumerator Steps(int count)
        {
            for (var i = 0; i < count; i++) {
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            var until = Time.time + seconds;
            while (Time.time < until) {
                yield return null;
            }
        }

        // The fixture holds the bots still; football needs them to play.
        private void LetBotsDrive()
        {
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
        }

        private void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            saved.Add((target, field, field.GetValue(target)));
            field.SetValue(target, value);
        }
    }
}
