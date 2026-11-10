using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using VContainer.Unity;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The HOT POTATO mode in the real Game scene: the bomb passes on a bump and not straight back, its blast knocks the
    /// holder out to the credit of the car that passed it, the last car left wins; a fence round every rim, hole and pit
    /// keeps the cars on, a car that got past it after all is put back; the floor does not crumble, the variant's
    /// mechanic and the boxes are off; the bots chase with the bomb and run from its holder.
    /// </summary>
    public sealed class HotPotatoModeTests : ObjectsBFixture
    {
        [UnityTest]
        public IEnumerator TheBombPassesOnABumpNotStraightBackAndItsBlastKnocksTheHolderOutToThePassersCredit()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("sandbox", "rake");
            var potato = Container.Resolve<HotPotato>();
            var settings = Tuning.SpecialRounds.HotPotato;
            Assert.That(potato.IsActive, Is.True);
            Assert.That(potato.Holder, Is.Null, "A bomb came before its delay.");

            var timeout = Time.time + settings.FirstDelay + 2f;
            while (potato.Holder == null && Time.time < timeout) {
                yield return Step();
            }

            var holder = potato.Holder;
            Assert.That(holder, Is.Not.Null, "No bomb came.");
            yield return Wait(settings.PassCooldown + 0.1f);

            var other = Round.Cars[0] == holder ? Round.Cars[1] : Round.Cars[0];
            yield return Ram(holder, other, () => potato.Holder == holder, 2f);
            Assert.That(potato.Holder, Is.SameAs(other), "The bump did not pass the bomb.");
            Assert.That(potato.Passer, Is.SameAs(holder));
            Assert.That(potato.IsPassBack(holder), Is.True);

            // Straight back, as soon as the new holder may pass it: the bomb stays.
            yield return Wait(settings.PassCooldown + 0.1f);
            Assert.That(settings.PassCooldown + 0.1f, Is.LessThan(settings.PassBackTime));
            yield return Ram(other, holder, () => true, 0.6f);
            Assert.That(potato.Holder, Is.SameAs(other), "The bomb went straight back to the car that passed it.");

            Car knockedOut = null;
            Car knockedOutBy = null;
            Round.KnockedOut += (car, by) => {
                knockedOut = car;
                knockedOutBy = by;
            };
            var carsLeft = Round.CarsLeft;
            timeout = Time.time + settings.Fuse + 1f;
            while (knockedOut == null && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(knockedOut, Is.SameAs(other), "The blast did not knock its holder out.");
            Assert.That(knockedOutBy, Is.SameAs(holder), "The knock-out is not to the credit of the car that passed it.");
            Assert.That(other.IsOut, Is.True);
            Assert.That(Round.CarsLeft, Is.EqualTo(carsLeft - 1));

            // Blown up and away, it stays out: it is not put back as a fall is.
            yield return Wait(1.5f);
            Assert.That(other.IsOut, Is.True, "The car the blast knocked out came back.");
        }

        [UnityTest]
        public IEnumerator TheLastCarLeftWins()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("sandbox", "rake");
            RoundResult? result = null;
            Round.Finished += r => result = r;
            foreach (var car in Round.Cars.ToArray()) {
                if (car != Round.Player) {
                    Assert.That(Round.KnockOut(car, null), Is.True);
                }
            }

            yield return Step();
            yield return Step();
            Assert.That(result.HasValue, Is.True, "The round did not end with one car left.");
            Assert.That(result.Value.Outcome, Is.EqualTo(RoundOutcome.Won));
        }

        [UnityTest]
        public IEnumerator TheFenceKeepsACarOnAtTheRimOfTheSandbox() => KeepsOn("sandbox", "rake", false);

        [UnityTest]
        public IEnumerator TheFenceKeepsACarOnAtTheRimOfTheTabletop() => KeepsOn("tabletop", "board", false);

        [UnityTest]
        public IEnumerator TheFenceKeepsACarOutOfTheCandyHole() => KeepsOn("candy", "frosting", true);

        [UnityTest]
        public IEnumerator TheFenceKeepsACarOutOfThePlayroomPits() => KeepsOn("playroom", "puzzle", true);

        /// <summary>
        /// A car rammed at the nearest void (the rim, or a hole or pit with <paramref name="hasVoids"/>) stops at the fence:
        /// it never drops below the floor, so it is the fence and not the safety net that keeps it on.
        /// </summary>
        private IEnumerator KeepsOn(string map, string variant, bool hasVoids)
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open(map, variant);
            var fence = Container.Resolve<ArenaFence>();
            Assert.That(fence.Walls.Count, Is.GreaterThan(0), "No fence.");
            var shape = Container.Resolve<ArenaMap>().Shape;
            if (hasVoids) {
                Assert.That(shape, Is.Not.Null);
                Assert.That(shape.Voids.Count, Is.GreaterThan(0));
                Assert.That(fence.Walls.Count, Is.GreaterThan(shape.Outline.Count), "The holes are not fenced.");
            }

            Assert.That(FindNearVoid(out var start), Is.True, "No floor near a void.");
            var toward = -Flat(Edge.Inward(start)).normalized;
            var car = Round.Player;
            car.Movement.Place(start + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * 16f);
            var lowest = car.Position.y;
            var until = Time.time + 2.5f;
            while (Time.time < until) {
                yield return Step(car);
                lowest = Mathf.Min(lowest, car.Position.y);
            }

            Assert.That(car.IsOut, Is.False);
            Assert.That(lowest, Is.GreaterThan(-0.5f), $"The car dropped to {lowest:0.0} m: it went past the fence.");
            Assert.That(Edge.DistanceToEdge(car.Position), Is.GreaterThan(-0.3f), "The car is over the void.");
        }

        [UnityTest]
        public IEnumerator ACarThatGotPastTheFenceIsPutBackOnTheFloor()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("sandbox", "rake");
            var car = Round.Cars[1];
            car.Movement.Place(new Vector3(40f, Tuning.Round.FallHeight - 2f, 40f), Quaternion.identity);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return null;

            Assert.That(car.IsOut, Is.False, "A fall counted in the mode nobody falls in.");
            Assert.That(car.Position.y, Is.GreaterThan(Tuning.Round.FallHeight));
            Assert.That(Edge.DistanceToEdge(car.Position), Is.GreaterThan(0f), "The car was not put back on the floor.");
        }

        [UnityTest]
        public IEnumerator TheFloorStandsAndTheMechanicAndTheBoxesAreOff()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("playroom", "puzzle");
            Assert.That(Container.Resolve<ArenaPickups>().IsEnabled, Is.False, "Boxes in the mode.");
            Assert.That(Container.Resolve<ArenaVariants>().Mechanic, Is.EqualTo(GameTuning.ArenaMechanic.None));

            var floor = Container.Resolve<ArenaFloor>();
            var arena = Container.Resolve<GameTuning.ArenaSettings>();
            var platform = Container.Resolve<IArenaPlatform>();
            var until = Time.time + arena.CrumbleDelay + arena.CrumbleWarning + 1f;
            var turned = false;
            while (Time.time < until) {
                yield return Step();
                turned |= platform.Sample(new Vector3(3f, 0f, 0f)).IsMoving;
            }

            Assert.That(turned, Is.False, "The playroom's turntable turns in the mode.");
            for (var wave = 0; wave < floor.WaveCount; wave++) {
                Assert.That(floor.StateOf(wave), Is.EqualTo(WaveState.Standing), $"Wave {wave} crumbled.");
            }
        }

        [UnityTest]
        public IEnumerator TheHolderChasesACarAndTheOthersRunFromIt()
        {
            UseSpecial(SpecialRound.HotPotato);
            yield return Open("sandbox", "rake");
            var potato = Container.Resolve<HotPotato>();
            // The bomb comes with its fuse lit; it is then handed to the bot this test watches, as a pass would.
            var timeout = Time.time + Tuning.SpecialRounds.HotPotato.FirstDelay + 2f;
            while (potato.Holder == null && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(potato.Holder, Is.Not.Null, "No bomb came.");
            var botDriver = Container.Resolve<IEnumerable<ITickable>>().OfType<BotDriver>().Single();
            // The fixture stills the bots; let them drive.
            typeof(BotDriver).GetField("isDriving", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(botDriver, false);

            var holder = Round.Cars[1];
            var runner = Round.Cars[2];
            var hold = typeof(HotPotato).GetMethod("Hold", BindingFlags.Instance | BindingFlags.NonPublic);
            // The runner a few metres off the holder, inside the reach it runs from.
            var spot = holder.Position + Flat(-holder.Position).normalized * 4f;
            runner.Movement.Place(spot + Vector3.up * 0.05f, runner.transform.rotation);
            yield return null;
            var bots = (IList<Bot>)typeof(BotDriver).GetField("bots", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(botDriver);
            Assert.That(bots.Count, Is.GreaterThan(0), "The bots did not start.");
            var holderBot = bots.Single(bot => bot.Car == holder);
            hold.Invoke(potato, new object[] { holder });

            var chased = false;
            var ran = false;
            var until = Time.time + 2f;
            while (Time.time < until && !(chased && ran)) {
                yield return new WaitForFixedUpdate();
                if (potato.Holder != holder) {
                    break;
                }

                var target = holderBot.Target;
                if (target != null && Vector3.Dot(Flat(holder.Movement.DiagnosticDirection).normalized,
                        Flat(target.Position - holder.Position).normalized) > 0.5f) {
                    chased = true;
                }

                var away = Flat(runner.Position - holder.Position);
                if (away.magnitude < Tuning.SpecialRounds.HotPotato.FleeRadius
                    && Vector3.Dot(Flat(runner.Movement.DiagnosticDirection).normalized, away.normalized) > 0.3f) {
                    ran = true;
                }
            }

            Assert.That(chased, Is.True, "The holder did not drive at a car.");
            Assert.That(ran, Is.True, "The car near the holder did not run from it.");
        }

        // The car rams the other from three metres off, for the seconds given or until keepGoing says no.
        private IEnumerator Ram(Car car, Car other, System.Func<bool> keepGoing, float seconds)
        {
            var toward = Flat(other.Position - car.Position).normalized;
            car.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            car.Movement.Push(toward * 8f);
            var until = Time.time + seconds;
            while (Time.time < until && keepGoing()) {
                yield return Step(car, other);
            }
        }

        /// <summary>
        /// A spot of floor two and a half to four and a half metres from the nearest void, out from the middle, whose run
        /// to that void passes no other car: the first such along any of 24 headings.
        /// </summary>
        private bool FindNearVoid(out Vector3 spot)
        {
            for (var i = 0; i < 24; i++) {
                var angle = i * 15f * Mathf.Deg2Rad;
                var heading = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                for (var t = 0f; t < 40f; t += 0.25f) {
                    spot = heading * t;
                    var distance = Edge.DistanceToEdge(spot);
                    if (distance < 2.5f || distance > 4.5f) {
                        continue;
                    }

                    var end = spot - Flat(Edge.Inward(spot)).normalized * distance;
                    if (IsClear(spot, end)) {
                        return true;
                    }

                    break;
                }
            }

            spot = Vector3.zero;
            return false;
        }

        // No car but the player within three and a half metres of the run from a to b.
        private bool IsClear(Vector3 a, Vector3 b)
        {
            foreach (var car in Round.Cars) {
                if (car == Round.Player) {
                    continue;
                }

                var point = Flat(car.Position);
                var along = Mathf.Clamp01(Vector3.Dot(point - a, b - a) / Mathf.Max(0.01f, (b - a).sqrMagnitude));
                if (Vector3.Distance(point, Vector3.Lerp(a, b, along)) < 3.5f) {
                    return false;
                }
            }

            return true;
        }
    }
}
