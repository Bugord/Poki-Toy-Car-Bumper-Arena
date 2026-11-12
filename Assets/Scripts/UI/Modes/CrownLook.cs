using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the CROWN mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class CrownLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Crown,
            new Color32(0xd9, 0xb8, 0xff, 0xff), new Color32(0x8a, 0x4d, 0xff, 0xff));
    }
}
