using System.Collections;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using VContainer;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The shared checks of an event mode's tests: a round of the mode (<see cref="OpenMode"/>, by
    /// <see cref="SimulationSceneFixture.UseSpecial"/>, whether the build has the mode on or not) makes its entry point the
    /// scene's active mode and plays its rules (<see cref="ExpectRules"/>); a plain round leaves the mode silent
    /// (<see cref="ExpectSilent{T}"/>). A mode's own tests (<c>Modes/&lt;Mode&gt;ModeTests.cs</c>) start from these and
    /// add its play.
    /// </summary>
    public abstract class ModeFixture : ObjectsBFixture
    {
        /// <summary>A round of <paramref name="kind"/> in the real Game scene, on the sandbox's first variant.</summary>
        protected IEnumerator OpenMode(SpecialRound kind, string map = "sandbox", string variant = "rake")
        {
            UseSpecial(kind);
            yield return Open(map, variant);
        }

        /// <summary>The scene's entry point of the mode is the active mode and the round plays the mode's rules.</summary>
        protected T ExpectRules<T>(SpecialRound kind) where T : class, IRoundMode
        {
            var modes = Container.Resolve<RoundModes>();
            var mode = Container.Resolve<T>();
            Assert.That(mode.IsActive, Is.True, "The mode is not active in its own round.");
            Assert.That(modes.Active, Is.SameAs(mode), "Another mode is active in this one's round.");
            Assert.That(Container.Resolve<HotPotato>().IsActive, Is.EqualTo(kind == SpecialRound.HotPotato));

            var rules = Tuning.RulesOf(kind);
            Assert.That(Round.Mode.Fall, Is.EqualTo(rules.Fall));
            Assert.That(Round.IsTimed, Is.EqualTo(rules.IsTimed));
            Assert.That(Round.Score != null, Is.EqualTo(rules.IsTimed));
            Assert.That(Container.Resolve<ArenaFence>().Walls.Count > 0, Is.EqualTo(rules.HasFence), "The fence.");
            Assert.That(Container.Resolve<ArenaPickups>().IsEnabled, Is.EqualTo(rules.Pickups != PickupRule.None),
                "The boxes.");
            if (!rules.HasMechanics) {
                Assert.That(Container.Resolve<ArenaVariants>().Mechanic, Is.EqualTo(GameTuning.ArenaMechanic.None));
            }

            if (rules.Teams > 0) {
                Assert.That(Round.Cars.Count % rules.Teams, Is.Zero, "The teams are not even.");
            }

            if (rules.Bots > 0) {
                Assert.That(Round.Cars.Count, Is.EqualTo(rules.BotCountFor(rules.Bots, 6) + 1));
            }

            return mode;
        }

        /// <summary>A plain round: the mode builds nothing and is not the active one.</summary>
        protected IEnumerator ExpectSilent<T>() where T : class, IRoundMode
        {
            yield return Open("sandbox", "rake");
            Assert.That(Container.Resolve<T>().IsActive, Is.False, "The mode is active in a plain round.");
            Assert.That(Container.Resolve<RoundModes>().Active, Is.Null);
        }
    }
}
