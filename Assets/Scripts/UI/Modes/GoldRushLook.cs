using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the GOLD RUSH mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class GoldRushLook
    {
        public static readonly ModeLook Look = new(SpecialRound.GoldRush,
            new Color32(0xff, 0xe2, 0x7a, 0xff), new Color32(0xf0, 0xa8, 0x00, 0xff));
    }
}
