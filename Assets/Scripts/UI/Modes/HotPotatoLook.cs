using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the HOT POTATO mode looks on the shared screens (<see cref="ModeLook"/>). Its HUD is the fuse over the bomb and PASS IT ON! (<see cref="HudPotato"/>); its points are not counted, so the score icon stays the burst.
    /// </summary>
    internal static class HotPotatoLook
    {
        public static readonly ModeLook Look = new(SpecialRound.HotPotato,
            new Color32(0xff, 0xc0, 0x6a, 0xff), new Color32(0xff, 0x8f, 0x1f, 0xff),
            createHud: (layer, root, model) => HudPotato.Create(layer, root, model));
    }
}
