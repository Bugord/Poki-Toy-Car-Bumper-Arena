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
    /// ice, the low gravity and its return, the giants, the boss and its reward). Low gravity and the hot potato are in
    /// the rotation; the others stay for <see cref="GameTuning.SpecialRoundSettings.Force"/>. The hot potato, a mode of
    /// the menu too, has <see cref="HotPotatoModeTests"/>.
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
                // Low gravity, then the hot potato (a mode of the menu as well), then again.
                Assert.That(specials.For(4), Is.EqualTo(SpecialRound.LowGravity));
                Assert.That(specials.For(5), Is.EqualTo(SpecialRound.None));
                Assert.That(specials.For(7), Is.EqualTo(SpecialRound.HotPotato));
                Assert.That(specials.For(10), Is.EqualTo(SpecialRound.LowGravity));
                Assert.That(specials.For(13), Is.EqualTo(SpecialRound.HotPotato));
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
    }
}
