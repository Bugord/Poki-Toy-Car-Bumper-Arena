using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
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
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The TEAM PUSH mode in the real Game scene: six cars in two teams, the player blue and every car in its team's colour
    /// for the round only; a fall is a point for the other team, a knock-out and a fall of its own alike, and the car comes
    /// back safe; a teammate's bump gives no push and no knock-out, the bots leave their teammates alone; the round ends by
    /// time, the team ahead wins, level is a draw, and the reward, the losses and the win streak follow; the peak of the
    /// floor's cycle leaves room to come back on every arena; a plain round knows nothing of teams.
    /// </summary>
    public sealed class TeamPushModeTests : ModeFixture
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
            yield return OpenMode(SpecialRound.TeamPush);
            ExpectRules<TeamPushMode>(SpecialRound.TeamPush);
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent() => ExpectSilent<TeamPushMode>();

        [UnityTest]
        public IEnumerator SixCarsPlayInTwoTeamsInTheirTeamColoursThePlayerBlue()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            Assert.That(Round.Cars.Count, Is.EqualTo(6), "Three against three.");
            Assert.That(Round.Mode.Teams, Is.EqualTo(2));
            Assert.That(Round.Score.Teams, Is.EqualTo(2));
            Assert.That(Round.Player.Team, Is.EqualTo(RoundTeams.Blue), "The player is not blue.");
            for (var index = 0; index < Round.Cars.Count; index++) {
                var team = index % 2 == 0 ? RoundTeams.Blue : RoundTeams.Red;
                Assert.That(Round.Cars[index].Team, Is.EqualTo(team), $"Car {index}'s team.");
                Assert.That(Round.TeamOf(index), Is.EqualTo(team));
                Assert.That(Round.ColorOf(index), Is.EqualTo(RoundTeams.PaintOf(team)), $"Car {index}'s colour.");
            }

            Assert.That(Round.Cars.Count(car => car.Team == RoundTeams.Blue), Is.EqualTo(3));
            Assert.That(Round.Cars[2].IsTeammateOf(Round.Player), Is.True);
            Assert.That(Round.Cars[1].IsTeammateOf(Round.Player), Is.False);
            Assert.That(Round.Player.IsTeammateOf(Round.Player), Is.False, "A car is not its own teammate.");

            // The teams start facing each other across the floor.
            var blue = Middle(Round.Cars.Where(car => car.Team == RoundTeams.Blue));
            var red = Middle(Round.Cars.Where(car => car.Team == RoundTeams.Red));
            Assert.That(Vector3.Dot(blue, red), Is.LessThan(0f), "The teams do not start on opposite sides.");
        }

        [UnityTest]
        public IEnumerator TheTeamColoursAreForTheRoundOnlyAndOwnColoursComeBack()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            var roster = Container.Resolve<ISessionRoster>();
            var playerCar = Container.Resolve<IPlayerCar>();
            var own = Enumerable.Range(0, Round.Cars.Count).Select(roster.ColorOf).ToArray();
            Assert.That(own[0], Is.EqualTo(playerCar.SelectedColor));
            Assert.That(Round.ColorOf(0), Is.EqualTo(RoundTeams.PaintOf(RoundTeams.Blue)));
            Assert.That(own[0], Is.Not.EqualTo(Round.ColorOf(0)), "The test needs a player who is not blue.");
            // The round played its team colours without touching the visit's.
            yield return Wait(0.5f);
            Assert.That(Enumerable.Range(0, Round.Cars.Count).Select(roster.ColorOf), Is.EqualTo(own),
                "The team round changed the visit's colours.");
            Assert.That(playerCar.SelectedColor, Is.EqualTo(own[0]));
            yield return UnloadGame();
            Object.DestroyImmediate(parentScope.gameObject);
            parentScope = null;

            // The visit's colours never changed, and the next plain round paints them.
            UseSpecial(SpecialRound.None);
            yield return Open("sandbox", "rake");
            Assert.That(Round.Mode.Teams, Is.Zero);
            for (var index = 0; index < Round.Cars.Count; index++) {
                Assert.That(Round.ColorOf(index), Is.EqualTo(Container.Resolve<ISessionRoster>().ColorOf(index)),
                    $"Car {index} kept its team colour.");
                Assert.That(Round.Cars[index].Team, Is.EqualTo(RoundTeams.None));
            }

            Assert.That(Round.ColorOf(0), Is.EqualTo(own[0]), "The player's own colour did not come back.");
        }

        [UnityTest]
        public IEnumerator AKnockOutAndAFallOfItsOwnAreBothAPointForTheOtherTeam()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            var mode = Container.Resolve<TeamPushMode>();
            var changes = new List<(int team, int change)>();
            Round.TeamScoreChanged += (team, change) => changes.Add((team, change));

            // The player knocks a red car out: the player's knock-out, blue's point.
            Assert.That(Round.KnockOut(Round.Cars[1], Round.Player), Is.True);
            Assert.That(Round.Score.KnockoutsOf(0), Is.EqualTo(1));
            Assert.That(Round.PlayerKnockouts, Is.EqualTo(1));
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(1));
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.Zero);

            // A blue car drives off on its own: red's point, to no car.
            var ally = Round.Cars[2];
            ally.Movement.Place(new Vector3(0f, Tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(ally);
            yield return Step(ally);
            Assert.That(ally.IsOut, Is.True);
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.EqualTo(1), "A fall of its own gave no point.");
            Assert.That(Round.Score.TeamOwnPointsOf(RoundTeams.Red), Is.EqualTo(1));
            Assert.That(mode.TeamFallPoints, Is.EqualTo(1));

            // A red car off on its own: blue's second.
            var enemy = Round.Cars[3];
            enemy.Movement.Place(new Vector3(0f, Tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(enemy);
            yield return Step(enemy);
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(new[] { (RoundTeams.Blue, 1), (RoundTeams.Red, 1), (RoundTeams.Blue, 1) }));

            // A red car knocks the player out: red's point and the rival of the round, from the other team.
            Assert.That(Round.KnockOut(Round.Player, Round.Cars[5]), Is.True);
            Assert.That(Round.Score.TeamPointsOf(RoundTeams.Red), Is.EqualTo(2));
            Assert.That(Round.Nemesis, Is.SameAs(Round.Cars[5]));
            Assert.That(Round.IsFinished, Is.False, "A timed round goes on after the player falls.");
        }

        [UnityTest]
        public IEnumerator AFallenCarComesBackSafe()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            var car = Round.Cars[1];
            car.Movement.Place(new Vector3(0f, Tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(car);
            yield return Step(car);
            Assert.That(car.IsOut, Is.True);
            Assert.That(Round.RespawnTimeLeft(car), Is.GreaterThan(0f));
            var timeout = Time.time + Tuning.Match.RespawnDelay + 2f;
            while (car.IsOut && Time.time < timeout) {
                yield return Step(car);
            }

            Assert.That(car.IsOut, Is.False, "The car that fell did not come back.");
            Assert.That(car.IsInvulnerable, Is.True, "A car that comes back is safe at first.");
            Assert.That(car.IsTargetable, Is.False);
            Assert.That(car.Team, Is.EqualTo(RoundTeams.Red), "It came back on another team.");
            Assert.That(Round.IsFinished, Is.False);
        }

        [UnityTest]
        public IEnumerator ATeammatesBumpGivesNoPushAndNoKnockOut()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            var player = Round.Player;
            var ally = Round.Cars[2];
            var enemy = Round.Cars[1];

            // The same ram on an enemy is a hit: the shared push and the loss of balance, and the credit.
            yield return Ram(player, enemy);
            Assert.That(enemy.LastHitBy, Is.SameAs(player), "The ram on an enemy was no hit (the test's ram is too weak).");
            Assert.That(enemy.Movement.IsStaggered, Is.True);

            yield return Ram(player, ally);
            Assert.That(ally.LastHitBy, Is.Null, "A teammate's bump counted as a hit.");
            Assert.That(ally.Movement.IsStaggered, Is.False, "A teammate's bump took the car's balance.");

            // Over the edge right after it: a fall of its own, the other team's point and nobody's knock-out.
            var knockouts = Round.Score.KnockoutsOf(0);
            ally.Movement.Place(new Vector3(0f, Tuning.Round.FallHeight - 1f, 0f), Quaternion.identity);
            yield return Step(ally);
            yield return Step(ally);
            Assert.That(ally.IsOut, Is.True);
            Assert.That(Round.Score.KnockoutsOf(0), Is.EqualTo(knockouts), "The player got a knock-out of a teammate.");
            Assert.That(Round.Score.TeamOwnPointsOf(RoundTeams.Red), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheBotsLeaveTheirTeammatesAlone()
        {
            yield return OpenMode(SpecialRound.TeamPush);
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // The fixture stills the bots; let them drive.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);
            yield return null;
            var bots = (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
            Assert.That(bots.Count, Is.EqualTo(5), "The bots did not start.");
            var picks = 0;
            var until = Time.time + 6f;
            while (Time.time < until) {
                yield return new WaitForFixedUpdate();
                foreach (var bot in bots) {
                    if (bot.Target == null) {
                        continue;
                    }

                    picks++;
                    Assert.That(bot.Car.IsTeammateOf(bot.Target), Is.False,
                        $"Bot {Round.IndexOf(bot.Car)} went for its teammate {Round.IndexOf(bot.Target)}.");
                }
            }

            Assert.That(picks, Is.GreaterThan(0), "No bot picked anyone.");
        }

        [UnityTest]
        public IEnumerator TheRoundEndsByTime()
        {
            SetField(LoadTuning().TeamPush, "duration", ShortRound);
            yield return OpenMode(SpecialRound.TeamPush);
            RoundResult? result = null;
            Round.Finished += r => result = r;
            // Every red car out for a moment ends nothing: the round is timed.
            for (var index = 1; index < Round.Cars.Count; index += 2) {
                Round.KnockOut(Round.Cars[index], null);
            }

            yield return Wait(ShortRound - 1.5f);
            Assert.That(result.HasValue, Is.False, "The round ended before its time.");
            var timeout = Time.time + 3f;
            while (!result.HasValue && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(result.HasValue, Is.True, "The round did not end when its time ran out.");
            Assert.That(Round.Elapsed, Is.GreaterThanOrEqualTo(ShortRound - 0.05f));
            Assert.That(result.Value.IsTimed, Is.True);
            Assert.That(result.Value.Kind, Is.EqualTo(SpecialRound.TeamPush));
            Assert.That(result.Value.TeamPoints, Is.EqualTo(new[] { 3, 0 }));
        }

        /// <summary>
        /// The team ahead at the end wins, behind loses, level draws; the place, the loss, the podium, the reward and the
        /// win streak follow the team's result, not the player's own points.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTeamAheadWinsAndTheRewardFollows([Values("win", "loss", "draw")] string end)
        {
            SetField(LoadTuning().TeamPush, "duration", ShortRound);
            yield return OpenMode(SpecialRound.TeamPush);
            RoundResult? result = null;
            Round.Finished += r => result = r;
            // The player scores most of all, so the team and not the player decides: a red car's two points against it.
            Assert.That(Round.AddPoints(Round.Player, 3), Is.True);
            Assert.That(Round.AddPoints(Round.Cars[3], 2), Is.True);
            Assert.That(Round.AddTeamPoints(RoundTeams.Red, end == "win" ? 0 : end == "draw" ? 1 : 3), Is.True);
            var timeout = Time.time + ShortRound + 3f;
            while (!result.HasValue && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(result.HasValue, Is.True, "The round did not end.");
            var r = result.Value;
            Assert.That(r.Teams, Is.EqualTo(2));
            Assert.That(r.TeamPointsOf(RoundTeams.Blue), Is.EqualTo(3));
            Assert.That(r.Points, Is.EqualTo(3));
            switch (end) {
                case "win":
                    Assert.That(r.Outcome, Is.EqualTo(RoundOutcome.Won));
                    Assert.That(r.Place, Is.EqualTo(1));
                    Assert.That(r.IsLoss, Is.False);
                    Assert.That(r.IsDraw, Is.False);
                    Assert.That(r.WinningTeam, Is.EqualTo(RoundTeams.Blue));
                    Assert.That(r.PodiumCar, Is.Zero, "The podium is not the winning team's best.");
                    break;
                case "loss":
                    Assert.That(r.Outcome, Is.EqualTo(RoundOutcome.Survived));
                    Assert.That(r.Place, Is.EqualTo(6), "A lost team round pays no place.");
                    Assert.That(r.IsLoss, Is.True);
                    Assert.That(r.WinningTeam, Is.EqualTo(RoundTeams.Red));
                    Assert.That(r.PodiumCar, Is.EqualTo(3), "The podium is not the winning team's best.");
                    break;
                default:
                    Assert.That(r.IsDraw, Is.True);
                    Assert.That(r.Outcome, Is.EqualTo(RoundOutcome.Survived));
                    Assert.That(r.Place, Is.EqualTo(3));
                    Assert.That(r.IsLoss, Is.False, "A draw is no loss.");
                    break;
            }

            // The reward, the loss and the streak on the real result.
            var tuning = LoadTuning();
            var storage = new RoundOverScreenshots.MemoryStorage();
            var progress = new PlayerProgress(storage, tuning, LoadCarCatalog());
            var wallet = new PlayerWallet(storage, tuning);
            var session = new SessionProgress();
            session.Record(Won());
            Assert.That(session.WinStreak, Is.EqualTo(1));
            var xp = progress.Record(r);
            var coins = wallet.Record(r, false, 0);
            session.Record(r);
            Assert.That(xp.Total, Is.GreaterThan(0), "A team round paid no XP.");
            Assert.That(coins.WinCoins, end == "win" ? Is.GreaterThan(0) : Is.Zero, "The win coins.");
            Assert.That(coins.KnockoutCoins, Is.Zero, "The test's player knocked nobody out.");
            Assert.That(session.WinStreak, Is.EqualTo(end == "win" ? 2 : end == "loss" ? 0 : 1), "The win streak.");
            Assert.That(session.LosingStreak, Is.EqualTo(end == "loss" ? 1 : 0), "The losing streak.");
            Assert.That(session.Nemesis, Is.EqualTo(RoundResult.Nobody), "A team round's rival outlived it.");

            RoundResult Won() => new(RoundOutcome.Won, 1, 6, 30f, 0, RoundResult.Nobody, FallCause.None, false, false);
        }

        [Test]
        public void TheTeamRoundPaysAtMostFiveKnockOuts()
        {
            var tuning = LoadTuning();
            var rules = tuning.RulesOf(SpecialRound.TeamPush);
            var many = new RoundResult(RoundOutcome.Won, 1, 6, 60f, 9, RoundResult.Nobody, FallCause.None, false, false,
                isTimed: true, kind: SpecialRound.TeamPush, knockoutRewardCap: rules.KnockoutRewardCap, teams: 2,
                teamPoints: new[] { 12, 4 });
            Assert.That(rules.KnockoutRewardCap, Is.EqualTo(5));
            Assert.That(many.RewardKnockouts, Is.EqualTo(5));
            Assert.That(many.Knockouts, Is.EqualTo(9), "The missions and the analytics see every knock-out.");
        }

        [UnityTest]
        public IEnumerator ThePeakLeavesRoomToComeBack([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            // The mode's floor goes in cycles; short ones, so the peak comes soon.
            var cycles = LoadTuning().FloorCycles;
            SetField(cycles, "open", 1f);
            SetField(cycles, "shrink", 4f);
            SetField(cycles, "peak", 3f);
            SetField(cycles, "returnTime", 2f);
            SetField(cycles, "returnWarning", 0.8f);
            UseSpecial(SpecialRound.TeamPush);
            yield return Open(map, null);
            var crumble = Container.Resolve<ArenaCrumble>();
            var floor = Container.Resolve<ArenaFloor>();
            Assert.That(crumble.IsCycling, Is.True, $"{map}: the floor does not go in cycles.");
            var timeout = Time.time + 10f;
            while (crumble.Phase != FloorPhase.Peak && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(crumble.Phase, Is.EqualTo(FloorPhase.Peak), $"{map}: no peak.");
            var fallen = Enumerable.Range(0, floor.WaveCount).Count(w => floor.StateOf(w) != WaveState.Standing);
            Assert.That(fallen, Is.GreaterThan(0).And.LessThan(floor.WaveCount), $"{map}: the floor shrank, in part.");

            // Every car at rest on what is left, well apart, then one more place to come back to.
            var spots = new List<Vector3>();
            for (var x = -25f; x <= 25f; x += 0.5f) {
                for (var z = -25f; z <= 25f; z += 0.5f) {
                    var spot = new Vector3(x, 0f, z);
                    if (floor.DistanceToEdge(spot) >= 2f && spots.All(other => (other - spot).magnitude >= 3.2f)) {
                        spots.Add(spot);
                    }
                }
            }

            Assert.That(spots.Count, Is.GreaterThanOrEqualTo(Round.Cars.Count), $"{map}: no room for every car.");
            for (var i = 0; i < Round.Cars.Count; i++) {
                Round.Cars[i].Movement.Place(spots[i] + Vector3.up * 0.3f, Quaternion.identity);
            }

            yield return null;
            var back = RespawnSpot.Find(floor, Tuning.Match, Round.Cars, null);
            var nearest = Round.Cars.Min(car => (Flat(car.Position) - back).magnitude);
            Debug.Log($"TEAM PUSH {map}: {fallen} of {floor.WaveCount} waves fall, comes back " +
                $"{floor.DistanceToEdge(back):0.0} m from the edge, {nearest:0.0} m from a car");
            Assert.That(floor.DistanceToEdge(back), Is.GreaterThanOrEqualTo(Tuning.Match.SpawnMargin - 0.01f),
                $"{map}: no safe floor to come back on.");
            Assert.That(nearest, Is.GreaterThanOrEqualTo(3f), $"{map}: it comes back on top of a car.");
        }

        // The car rams the other from three metres off at the speed of a hard bump, for a second.
        private IEnumerator Ram(Car car, Car other)
        {
            var toward = Flat(other.Position - car.Position).normalized;
            car.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * 9f);
            var until = Time.time + 0.5f;
            while (Time.time < until && other.LastHitBy == null && !car.IsOut) {
                yield return Step(car, other);
            }
        }

        private static Vector3 Middle(IEnumerable<Car> cars)
        {
            var list = cars.ToList();
            var sum = Vector3.zero;
            foreach (var car in list) {
                sum += Flat(car.Position);
            }

            return sum / list.Count;
        }

        private void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name} has no field {name}");
            saved.Add((target, field, field.GetValue(target)));
            field.SetValue(target, value);
        }
    }
}
