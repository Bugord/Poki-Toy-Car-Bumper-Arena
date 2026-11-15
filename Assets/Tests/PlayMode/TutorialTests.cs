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
    /// to a win, the bots never attack, only the bot across starts on the floor and the last two drop in for the smash,
    /// a grown hit throws them far, and a round of a player who has played is no tutorial.
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
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Push), "the push is shown as soon as the cars stand");
            Assert.That(round.CarsLeft, Is.EqualTo(2), "only the player and the bot across start on the floor");

            var steps = new List<TutorialStep>();
            var praises = new List<TutorialPraise>();
            var times = new List<string>();
            var fastest = 0f;
            tutorial.StepStarted += step => {
                steps.Add(step);
                times.Add($"{step} {round.Elapsed:0.0}s");
            };
            var hitByBot = false;
            round.Player.Hit += hit => hitByBot |= !hit.IsHitter && hit.Other != null && hit.Other.Movement.Velocity.magnitude > 2f
                && Vector3.Dot(hit.Other.Movement.Velocity, round.Player.Position - hit.Other.Position) > 0f
                && tutorial.Step != TutorialStep.Push && tutorial.Step != TutorialStep.Smash;

            var selfFalls = 0;
            round.Eliminated += (index, by) => {
                selfFalls += by == RoundResult.Nobody ? 1 : 0;
                times.Add($"out {index} by {by} at {tutorial.Step} {round.Elapsed:0.0}s");
            };
            RoundResult? result = null;
            round.Finished += r => result = r;
            round.Begin();
            var used = 0f;
            while (!round.IsFinished && round.Elapsed < Limit) {
                Follow(ref used);
                if (tutorial.Praise != TutorialPraise.None && (praises.Count == 0 || praises[^1] != tutorial.Praise)) {
                    praises.Add(tutorial.Praise);
                }

                if (tutorial.Step == TutorialStep.Smash) {
                    foreach (var car in tutorial.Pair) {
                        if (!car.IsOut) {
                            fastest = Mathf.Max(fastest, Flat(car.Movement.Velocity).magnitude);
                        }
                    }
                }

                yield return null;
            }

            input.SetMove(Vector2.zero);
            Debug.Log($"Tutorial on {map}: {string.Join(", ", times)}; fastest throw {fastest:0.0} m/s");
            if (!round.IsFinished) {
                foreach (var car in round.Cars) {
                    Debug.Log($"Stuck on {map}: {car.name} out {car.IsOut} at {car.Position} edge "
                        + $"{round.Edge.DistanceToEdge(car.Position, 0f):0.0} mega {car.IsRunning(ItemKind.Mega)} "
                        + $"item {car.HeldItem} staggered {car.Movement.IsStaggered}");
                }
            }
            Assert.That(round.IsFinished, Is.True, $"finished within {Limit} s; at {tutorial.Step}");
            Assert.That(tutorial.IsDone, Is.True);
            Assert.That(result?.Outcome, Is.EqualTo(RoundOutcome.Won));
            Assert.That(steps, Is.EqualTo(new[] {
                TutorialStep.Box, TutorialStep.Use, TutorialStep.Smash, TutorialStep.Done,
            }));
            Assert.That(praises, Is.EqualTo(new[] { TutorialPraise.Pushed, TutorialPraise.Grown }));
            Assert.That(fastest, Is.GreaterThan(12f), "a grown hit throws the last two far");
            Assert.That(hitByBot, Is.False, "the bots never drive into the player");
            Assert.That(selfFalls, Is.Zero, "every bot off was knocked off (by the player, or by a bot thrown into it): none drives off by itself");
        }

        [UnityTest]
        public IEnumerator TheLastTwoWaitOffTheArenaUntilTheSmash()
        {
            yield return Open("sandbox", true);
            var player = round.Player;
            Assert.That(tutorial.Target, Is.Not.Null);
            Assert.That(tutorial.Pair, Has.Count.EqualTo(2));
            foreach (var car in tutorial.Pair) {
                Assert.That(round.IsHeld(car), Is.True);
                Assert.That(car.gameObject.activeSelf, Is.False, "held off the arena, out of sight");
            }

            round.Begin();
            yield return null;
            Assert.That(round.KnockOut(tutorial.Target, player), Is.True);
            yield return null;
            Assert.That(tutorial.Praise, Is.EqualTo(TutorialPraise.Pushed), "the push is praised");
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Push), "the step waits for the praise");
            yield return WaitFor(TutorialStep.Box);
            Assert.That(round.IsFinished, Is.False, "the player alone on the floor has not won: two are still to come");
            Assert.That(tutorial.Box, Is.Not.Null, "the box is laid");
            Assert.That(tutorial.Box.Item, Is.EqualTo(ItemKind.Mega));
            Assert.That(Flat(tutorial.Box.Position).magnitude, Is.LessThan(1f), "in the middle");
            Assert.That(round.IsHeld(tutorial.Pair[0]), Is.True, "still held");

            var pickups = GameScope().Container.Resolve<ToyCarArena.Gameplay.Pickups.ArenaPickups>();
            Assert.That(pickups.Give(player, ItemKind.Mega), Is.True);
            yield return WaitFor(TutorialStep.Use);
            Assert.That(pickups.Use(player), Is.True);
            yield return null;
            Assert.That(tutorial.Praise, Is.EqualTo(TutorialPraise.Grown), "the giant is praised");
            yield return WaitFor(TutorialStep.Smash);
            Assert.That(tutorial.IsPairIn, Is.True);
            Assert.That(round.CarsLeft, Is.EqualTo(3), "the last two are in");
            var first = tutorial.Pair[0].Position;
            var second = tutorial.Pair[1].Position;
            Assert.That(Flat(first - second).magnitude, Is.LessThan(5f), "side by side");
            Assert.That(Flat(first - player.Position).magnitude, Is.GreaterThan(4f), "away from the player");
            for (var i = 0; i < 60; i++) {
                yield return null;
            }

            foreach (var car in tutorial.Pair) {
                Assert.That(car.IsOut, Is.False);
                Assert.That(car.Movement.IsGrounded, Is.True, "dropped onto the floor");
                Assert.That(car.IsInvulnerable, Is.False, "there to be pushed, not a ghost");
            }
        }

        [UnityTest]
        public IEnumerator TheTutorialsMegaHasNoTimer()
        {
            yield return Open("sandbox", true);
            round.Begin();
            yield return null;
            Assert.That(round.KnockOut(tutorial.Target, round.Player), Is.True);
            yield return WaitFor(TutorialStep.Box);

            var pickups = GameScope().Container.Resolve<ToyCarArena.Gameplay.Pickups.ArenaPickups>();
            Assert.That(pickups.Give(round.Player, ItemKind.Mega), Is.True);
            Assert.That(pickups.Use(round.Player), Is.True);
            Assert.That(float.IsPositiveInfinity(round.Player.TimeLeft(ItemKind.Mega)), Is.True, "runs with no end");
            yield return WaitFor(TutorialStep.Smash);
            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.Smash), "a running Mega passes the box and the use");
        }

        [UnityTest]
        public IEnumerator APlayerWhoHasPlayedGetsAPlainRound()
        {
            yield return Open("sandbox", false);
            Assert.That(tutorial.IsActive, Is.False);
            Assert.That(GameScope().Container.Resolve<RoundRules>().IsTutorial, Is.False);
            Assert.That(round.CarsLeft, Is.EqualTo(round.Cars.Count), "every car on the floor");
            round.Begin();
            for (var i = 0; i < 30; i++) {
                yield return null;
            }

            Assert.That(tutorial.Step, Is.EqualTo(TutorialStep.None), "builds and does nothing");
        }

        private IEnumerator WaitFor(TutorialStep step)
        {
            var until = Time.time + 5f;
            while (tutorial.Step < step && Time.time < until) {
                yield return null;
            }

            Assert.That(tutorial.Step, Is.EqualTo(step));
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
            if (tutorial.Praise != TutorialPraise.None) {
                Steer(Vector3.zero);
                return;
            }

            switch (tutorial.Step) {
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
                case TutorialStep.Use:
                case TutorialStep.Smash: {
                    if (player.HasItem) {
                        Steer(Vector3.zero);
                        if (player.IsItemReady && Time.time - usedAt > 0.5f) {
                            input.PressItem();
                            usedAt = Time.time;
                        }
                    }
                    else if (!player.IsRunning(ItemKind.Mega)) {
                        Steer(tutorial.Box != null && tutorial.Box.IsLive ? tutorial.Box.Position - player.Position
                            : Vector3.zero);
                    }
                    else {
                        // Grown: ram the nearer of the last two straight out, as the push step.
                        Car nearest = null;
                        foreach (var car in tutorial.Pair) {
                            if (!car.IsOut && (nearest == null || Flat(car.Position - player.Position).sqrMagnitude
                                    < Flat(nearest.Position - player.Position).sqrMagnitude)) {
                                nearest = car;
                            }
                        }

                        if (nearest == null) {
                            Steer(Vector3.zero);
                            break;
                        }

                        var target = nearest.Position;
                        var outward = Flat(target).normalized;
                        var behind = target - outward * 3.5f;
                        // Outside it (between it and the edge), come round to the middle's side first.
                        var isOutside = Flat(player.Position).magnitude > Flat(target).magnitude - 1f;
                        var side = Vector3.Cross(Vector3.up, outward);
                        var round_ = behind + side * (Vector3.Dot(player.Position - target, side) >= 0f ? 3f : -3f);
                        Steer(isOutside ? round_ - player.Position
                            : Flat(player.Position - target).magnitude > 4.2f ? behind - player.Position : outward);
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
