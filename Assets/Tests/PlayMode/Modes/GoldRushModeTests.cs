using System.Collections;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Modes;
using UnityEngine.TestTools;

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The GOLD RUSH mode in the real Game scene. A stub so far: its round makes it the active mode and plays its rules, a plain
    /// round leaves it silent. The mode's own play adds its tests here.
    /// </summary>
    public sealed class GoldRushModeTests : ModeFixture
    {
        [UnityTest]
        public IEnumerator ItsRoundPlaysItsRules()
        {
            yield return OpenMode(SpecialRound.GoldRush);
            ExpectRules<GoldRushMode>(SpecialRound.GoldRush);
        }

        [UnityTest]
        public IEnumerator APlainRoundLeavesItSilent() => ExpectSilent<GoldRushMode>();
    }
}
