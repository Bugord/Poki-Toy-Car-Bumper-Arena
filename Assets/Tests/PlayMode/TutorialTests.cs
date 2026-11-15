using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The new player's tutorial on the real Game scene: a player driven by the steps' own hints gets through every step
    /// to a win, the bots never attack, a bot pushed off out of order still moves the lesson on, and a round of a
    /// player who has played is no tutorial.
    /// </summary>
    public sealed class TutorialTests : SimulationSceneFixture
    {
        private const float Limit = 90f;

        private SimulationInput input;
        private ArenaRound round;
        private TutorialMode tutorial;
        private Transform view;

        [UnityTest]
        public IEnumerator APlayerFollowingTheStepsWinsTheTutorial([Values("sandbox", "tabletop", "candy", "playroom")] string map)
        {
            yield return Open(map, true);
            Assert.That(tutorial.IsActive, Is.True);
            Assert.That(round.Cars.Count, Is.EqualTo(4), "the player and three bots");
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Drive), "the drive is shown as soon as the cars stand");

            var steps = new List<TutorialStep>();
            var times = new List<string>();
            tutorial.StepStarted += step => {
                steps.Add(step);
                times.Add($"{step} {round.Elapsed:0.0}s");
            };
            var hitByBot = false;
            round.Player.Hit += hit => hitByBot |= !hit.IsHitter && hit.Other != null && hit.Other.Movement.Velocity.magnitude > 2f
                && Vector3.Dot(hit.Other.Movement.Velocity, round.Player.Position - hit.Other.Position) > 0f
                && tutorial.Step != TutorialStep.Push;

            RoundResult? result = null;
            round.Finished += r => result = r;
            round.Begin();
            var used = 0f;
            while (!round.IsFinished && round.Elapsed < Limit) {
                Follow(ref used);
                yield return null;
            }

            input.SetMove(Vector2.zero);
            Debug.Log($"Tutorial on {map}: {string.Join(", ", times)}");
            Assert.That(round.IsFinished, Is.True, $"finished within {Limit} s; at {tutorial.Step}");
            Assert.That(tutorial.IsDone, Is.True);
            Assert.That(result?.Outcome, Is.EqualTo(RoundOutcome.Won));
            Assert.That(steps, Is.EqualTo(new[] {
                TutorialStep.Push, TutorialStep.Box, TutorialStep.Use, TutorialStep.Done,
            }));
            Assert.That(hitByBot, Is.False, "the bots never drive into the player");
        }

        [UnityTest]
        public IEnumerator ABotPushedOffFirstMovesTheLessonOn()
        {
            yield return Open("sandbox", true);
            round.Begin();
            yield return null;
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Drive));

            // Any bot off ends the push, whichever and whenever: the drive and the push are passed together.
            var bot = round.Cars[3] == tutorial.Target ? round.Cars[2] : round.Cars[3];
            Assert.That(round.KnockOut(bot, null), Is.True);
            for (var i = 0; i < 10 && tutorial.Step < TutorialStep.Box; i++) {
                yield return null;
            }

            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Box));
            Assert.That(tutorial.Box, Is.Not.Null, "the box is laid");
            Assert.That(tutorial.Box.Item, Is.EqualTo(ItemKind.Shockwave));
            Assert.That(tutorial.Pair, Has.Member(tutorial.Target), "the bot that was to be pushed waits with the other");
        }

        [UnityTest]
        public IEnumerator APlayerWhoHasPlayedGetsAPlainRound()
        {
            yield return Open("sandbox", false);
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(GameScope().Container.Resolve<RoundRules>().IsTutorial, Is.False);
            round.Begin();
            for (var i = 0; i < 30; i++) {
                yield return null;
            }

            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.None), "builds and does nothing");
        }

        private IEnumerator Open(string map, bool isNew)
        {
            UseSafety(null);
            input = new SimulationInput();
            CreateParent(input, new SimulationProgress { Round = 1, IsNewPlayer = isNew }, map);
            yield return LoadGame();
            var container = GameScope().Container;
            round = container.Resolve<ArenaRound>();
            tutorial = container.Resolve<TutorialMode>();
            view = Object.FindAnyObjectByType<ArenaCamera>().transform;
            round.Place();
            for (var i = 0; i < 5; i++) {
                yield return null;
            }
        }

        /// <summary>A player doing what each step asks, as a new player would read it.</summary>
        private void Follow(ref float usedAt)
        {
            var player = round.Player;
            switch (tutorial.Step) {
                case TutorialStep.Drive:
                    Steer(tutorial.DrivePoint - player.Position);
                    break;
                case TutorialStep.Push: {
                    var target = tutorial.Target.Position;
                    var outward = Flat(target).normalized;
                    // Come at it from the middle's side, then drive it straight out.
                    var behind = target - outward * 2.5f;
                    Steer(Flat(player.Position - target).magnitude > 3.2f ? behind - player.Position : outward);
                    break;
                }
                case TutorialStep.Box:
                    if (tutorial.Box != null) {
                        Steer(tutorial.Box.Position - player.Position);
                    }

                    break;
                case TutorialStep.Use: {
                    var middle = Vector3.zero;
                    foreach (var car in tutorial.Pair) {
                        middle += car.Position;
                    }

                    middle /= Mathf.Max(1, tutorial.Pair.Count);
                    if (player.HasItem) {
                        Steer(middle - player.Position);
                        if (player.IsItemReady && Flat(middle - player.Position).magnitude < 4.5f
                            && Time.time - usedAt > 0.5f) {
                            input.PressItem();
                            usedAt = Time.time;
                        }
                    }
                    else if (tutorial.Box != null && tutorial.Box.IsLive) {
                        Steer(tutorial.Box.Position - player.Position);
                    }
                    else {
                        Steer(Vector3.zero);
                    }

                    break;
                }
                default:
                    Steer(Vector3.zero);
                    break;
            }
        }

        private void Steer(Vector3 way)
        {
            way = Flat(way);
            if (way.sqrMagnitude < 0.01f) {
                input.SetMove(Vector2.zero);
                return;
            }

            way.Normalize();
            var forward = Flat(view.forward).normalized;
            var right = Flat(view.right).normalized;
            input.SetMove(new Vector2(Vector3.Dot(way, right), Vector3.Dot(way, forward)));
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
