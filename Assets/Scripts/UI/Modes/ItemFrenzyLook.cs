using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the ITEM FRENZY mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class ItemFrenzyLook
    {
        public static readonly ModeLook Look = new(SpecialRound.ItemFrenzy,
            new Color32(0xff, 0xa6, 0xdc, 0xff), new Color32(0xff, 0x4f, 0xa8, 0xff));
    }
}
