using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the FOOTBALL mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class FootballLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Football,
            new Color32(0xa8, 0xe8, 0x8c, 0xff), new Color32(0x3f, 0xb9, 0x3f, 0xff));
    }
}
