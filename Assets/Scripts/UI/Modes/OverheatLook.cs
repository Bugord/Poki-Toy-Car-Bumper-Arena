using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the OVERHEAT mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class OverheatLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Overheat,
            new Color32(0xff, 0x9e, 0x8c, 0xff), new Color32(0xff, 0x3b, 0x30, 0xff));
    }
}
