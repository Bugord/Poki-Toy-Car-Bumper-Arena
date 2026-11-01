using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The match's HUD scoreboard on the real Game scene, without a panel: its rows follow <see cref="MatchScore"/>
    /// through a knock-out, the player's fall and comeback and a revenge, the narrow form keeps the leaders and the
    /// player's own row, and a round of the last car standing has no board at all.
    /// </summary>
    public sealed class ScoreboardTests : SimulationSceneFixture
    {
        private const int FramesPerSecond = 60;

        private static readonly PropertyInfo LastHitByProperty = typeof(Car).GetProperty(nameof(Car.LastHitBy));
        private static readonly PropertyInfo LastHitTimeProperty = typeof(Car).GetProperty(nameof(Car.LastHitTime));

        [UnityTest]
        public IEnumerator TheScoreboardFollowsTheMatchScoreThroughKnockoutsAndAComeback()
        {
            var tuning = UseMode(GameTuning.GameMode.Match, 40f);
            // Round 7 plays Normal, with six bots: more cars than the narrow form shows.
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 7 }, "sandbox");
            yield return LoadGame();
            var container = GameScope().Container;
            var round = container.Resolve<ArenaRound>();
            FreezeBots(container);
            round.Place();
            round.Begin();
            Assert.That(round.Cars.Count, Is.GreaterThan(4), "The narrow form needs more cars than it shows.");

            var model = new HudViewModel(null, new HudControls());
            model.SetPayload(new HudRound(round, null, tuning.Controls, false, 7, false));
            var board = HudScoreboard.Create(new VisualElement(), model);
            Assert.That(board, Is.Not.Null, "A match has a scoreboard.");
            try {
                board.SetWide(true);
                AssertFollowsScore(round, board, true);

                // A bot knocks the player off: it leads, the player waits dimmed, and the bot is the player's rival.
                var player = round.Player;
                var rival = round.Cars[3];
                yield return KnockOff(player, rival);
                AssertFollowsScore(round, board, true);
                Assert.That(board.PlaceShownOf(3), Is.EqualTo(1));
                Assert.That(board.KnockoutsShownOf(3), Is.EqualTo(1));
                Assert.That(board.IsRowWaiting(0), Is.True, "The fallen player's row must be dimmed.");
                Assert.That(board.IsRowRevenge(3), Is.True, "The car that knocked the player off is the REVENGE target.");

                // The narrow form: the three leaders, and the player's own row apart below them at its real place.
                board.SetWide(false);
                AssertFollowsScore(round, board, false);
                Assert.That(round.Score.PlaceOf(0), Is.GreaterThan(3), "The player who fell should be below the podium.");
                Assert.That(board.IsRowShown(0), Is.True);
                Assert.That(board.SlotOf(0), Is.EqualTo(3));

                yield return Until(() => !player.IsOut, tuning.Match.RespawnDelay + 1f, "The player never came back.");
                Assert.That(board.IsRowWaiting(0), Is.False, "The row must light up as the car comes back.");

                // The player takes revenge: both have a knock-out and a fall, and the rival scored first.
                yield return Until(() => !rival.IsInvulnerable && !player.IsInvulnerable,
                    tuning.Match.MaxDropTime + tuning.Match.Invulnerability + 2f, "The cars never became solid.");
                yield return KnockOff(rival, player);
                AssertFollowsScore(round, board, false);
                Assert.That(board.KnockoutsShownOf(0), Is.EqualTo(1));
                Assert.That(board.PlaceShownOf(0), Is.EqualTo(2));
                Assert.That(board.SlotOf(0), Is.EqualTo(1), "In the top three the player's row is at its place.");
                Assert.That(board.IsRowRevenge(3), Is.False, "Revenge taken clears the mark.");
                Assert.That(board.IsRowWaiting(3), Is.True);

                board.SetWide(true);
                AssertFollowsScore(round, board, true);
            }
            finally {
                board.Dispose();
                model.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ARoundOfTheLastCarStandingHasNoScoreboard()
        {
            var tuning = UseMode(GameTuning.GameMode.LastStanding);
            CreateParent(new SimulationInput(), new SimulationProgress { Round = 1 }, "sandbox");
            yield return LoadGame();
            var round = GameScope().Container.Resolve<ArenaRound>();
            round.Place();
            var model = new HudViewModel(null, new HudControls());
            model.SetPayload(new HudRound(round, null, tuning.Controls, true, 1, false));
            Assert.That(HudScoreboard.Create(new VisualElement(), model), Is.Null);
            model.Dispose();
        }

        /// <summary>
        /// Every row shows its car's place and knock-outs from the score; in the wide form each is at its place, in the
        /// narrow one the top three are and only the player's is shown below them.
        /// </summary>
        private static void AssertFollowsScore(ArenaRound round, HudScoreboard board, bool wide)
        {
            for (var place = 0; place < round.Cars.Count; place++) {
                var index = round.Score.IndexAt(place);
                Assert.That(board.PlaceShownOf(index), Is.EqualTo(place + 1), $"car {index}");
                Assert.That(board.KnockoutsShownOf(index), Is.EqualTo(round.Score.KnockoutsOf(index)), $"car {index}");
                if (wide || place < 3) {
                    Assert.That(board.SlotOf(index), Is.EqualTo(place), $"car {index}");
                    Assert.That(board.IsRowShown(index), Is.True, $"car {index}");
                }
                else {
                    Assert.That(board.IsRowShown(index), Is.EqualTo(index == 0), $"car {index}");
                }
            }
        }

        private static void FreezeBots(IObjectResolver container)
        {
            var botDriver = container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // Marked as driving before the round begins, it never makes its bots: the cars stand still.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, true);
        }

        private static IEnumerator KnockOff(Car car, Car by)
        {
            car.Movement.Place(new Vector3(40f, 0.5f, 0f), Quaternion.identity);
            LastHitByProperty.SetValue(car, by);
            LastHitTimeProperty.SetValue(car, Time.time);
            yield return Until(() => car.IsOut, 5f, "The pushed car never fell.");
        }

        private static IEnumerator Until(System.Func<bool> condition, float seconds, string message)
        {
            var frames = Mathf.CeilToInt(seconds * FramesPerSecond);
            for (var frame = 0; frame < frames && !condition(); frame++) {
                yield return null;
            }

            Assert.That(condition(), Is.True, message);
        }
    }
}
