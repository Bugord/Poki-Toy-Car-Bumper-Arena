using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The special rounds of a visit: when they come, and that each one changes its rule in the real Game scene (the
    /// ice, the low gravity and its return, the giants, the boss and its reward, the hot potato passing on a bump and
    /// blowing its holder away to the credit of the car that passed it).
    /// </summary>
    public sealed class SpecialRoundTests : ObjectsBFixture
    {
        [Test]
        public void TheScheduleStartsAtTheFourthRoundAndRepeatsEveryThird()
        {
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            try {
                var specials = tuning.SpecialRounds;
                Assert.That(specials.For(1), Is.EqualTo(SpecialRound.None));
                Assert.That(specials.For(3), Is.EqualTo(SpecialRound.None));
                // Only low gravity is left in the rotation; the hot potato is a mode of the menu.
                Assert.That(specials.For(4), Is.EqualTo(SpecialRound.LowGravity));
                Assert.That(specials.For(5), Is.EqualTo(SpecialRound.None));
                Assert.That(specials.For(7), Is.EqualTo(SpecialRound.LowGravity));
                Assert.That(specials.For(10), Is.EqualTo(SpecialRound.LowGravity));
                specials.Force(SpecialRound.Boss);
                Assert.That(specials.For(1), Is.EqualTo(SpecialRound.Boss));
                specials.Force(null);
                Assert.That(specials.For(1), Is.EqualTo(SpecialRound.None));
            }
            finally {
                Object.DestroyImmediate(tuning);
            }
        }

        [UnityTest]
        public IEnumerator APlainRoundChangesNothing()
        {
            yield return Open("sandbox", "rake");
            Assert.That(Container.Resolve<RoundRules>().IsSpecial, Is.False);
            Assert.That(Round.Boss, Is.Null);
            Assert.That(Container.Resolve<HotPotato>().IsActive, Is.False);
            foreach (var car in Round.Cars) {
                Assert.That(car.Growth, Is.EqualTo(1f).Within(0.001f));
            }
        }

        [UnityTest]
        public IEnumerator TheIceRoundMakesTheFloorSlippery()
        {
            UseSpecial(SpecialRound.Ice);
            yield return Open("sandbox", "rake");
            var feel = Container.Resolve<IArenaSurface>().Sample(Vector3.zero);
            Assert.That(feel.Grip, Is.EqualTo(Tuning.SpecialRounds.IceGrip).Within(0.001f));
            Assert.That(feel.Acceleration, Is.EqualTo(Tuning.SpecialRounds.IceAcceleration).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator LowGravityLastsAsLongAsTheScene()
        {
            var shipped = Physics.gravity;
            UseSpecial(SpecialRound.LowGravity);
            yield return Open("sandbox", "rake");
            Assert.That(Physics.gravity.y, Is.EqualTo(shipped.y * Tuning.SpecialRounds.GravityScale).Within(0.01f));

            yield return UnloadGame();
            Assert.That(Physics.gravity.y, Is.EqualTo(shipped.y).Within(0.001f), "The scene left its gravity behind.");
        }

        [UnityTest]
        public IEnumerator AMegaRoundGrowsEveryCarAndAMegaItemKeepsThemGiant()
        {
            UseSpecial(SpecialRound.Mega);
            yield return Open("sandbox", "rake");
            var size = Tuning.SpecialRounds.MegaSize;
            foreach (var car in Round.Cars) {
                Assert.That(car.Growth, Is.EqualTo(size).Within(0.01f));
            }

            // A Mega item ending must not shrink a giant below the round's size.
            var player = Round.Player;
            player.StartAbility(ItemKind.Mega);
            yield return Wait(Tuning.Pickups.DurationOf(ItemKind.Mega) + 1f);
            Assert.That(player.Growth, Is.EqualTo(size).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator TheBossIsAGiantAndKnockingItOffPaysMore()
        {
            UseSpecial(SpecialRound.Boss);
            yield return Open("sandbox", "rake");
            var rules = Container.Resolve<RoundRules>();
            var boss = Round.Boss;
            Assert.That(boss, Is.Not.Null);
            Assert.That(boss, Is.SameAs(Round.Cars[rules.BossIndex]));
            Assert.That(boss.Growth, Is.EqualTo(Tuning.SpecialRounds.BossSize).Within(0.01f));

            RoundResult? result = null;
            Round.Finished += r => result = r;
            // The player's item hit the boss last; every bot then leaves the floor, the boss to the player's credit.
            boss.HitByItem(Round.Player);
            foreach (var car in Round.Cars) {
                if (car != Round.Player) {
                    car.Movement.Place(new Vector3(60f, -12f, 60f), Quaternion.identity);
                }
            }

            var timeout = Time.time + 5f;
            while (result == null && Time.time < timeout) {
                yield return Step(Round.Cars.ToArray());
            }

            Assert.That(result.HasValue, Is.True, "The round did not end with the player alone.");
            Assert.That(Round.BossTaken, Is.True);
            Assert.That(result.Value.Knockouts, Is.EqualTo(1));
            Assert.That(result.Value.RewardKnockouts, Is.EqualTo(Tuning.SpecialRounds.BossReward));
        }

        [UnityTest]
        public IEnumerator TheHotPotatoPassesOnABumpAndBlowsItsHolderToThePassersCredit()
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

            // The holder rams a car beside it.
            var other = Round.Cars[0] == holder ? Round.Cars[1] : Round.Cars[0];
            var toward = Flat(other.Position - holder.Position).normalized;
            holder.Movement.Place(other.Position - toward * 3f + Vector3.up * 0.05f, Quaternion.LookRotation(toward));
            holder.Movement.Push(toward * 8f);
            timeout = Time.time + 2f;
            while (potato.Holder == holder && Time.time < timeout) {
                yield return Step(holder, other);
            }

            Assert.That(potato.Holder, Is.SameAs(other), "The bump did not pass the bomb.");
            Assert.That(potato.Passer, Is.SameAs(holder));

            // A bot near the holder runs from it.
            var near = potato.Holder.Position + Vector3.right * 3f;
            if (Edge.DistanceToEdge(near, 1f) > 0f) {
                Assert.That(potato.TryAvoid(near, 0f, Edge, 1f, out var heading), Is.True);
                Assert.That(Vector3.Dot(heading, Flat(near - potato.Holder.Position)), Is.GreaterThan(0f));
            }

            Car blown = null;
            Car blownBy = null;
            var byItem = true;
            potato.Blown += car => {
                blown = car;
                blownBy = car.LastHitBy;
                byItem = car.LastHitByItem;
            };
            timeout = Time.time + settings.Fuse + 1f;
            while (blown == null && Time.time < timeout) {
                yield return Step();
            }

            Assert.That(blown, Is.SameAs(other), "The bomb did not go off on its holder.");
            Assert.That(blownBy, Is.SameAs(holder), "The blast is not to the credit of the car that passed the bomb.");
            Assert.That(byItem, Is.False, "The bomb counts towards the item missions.");
        }
    }
}
