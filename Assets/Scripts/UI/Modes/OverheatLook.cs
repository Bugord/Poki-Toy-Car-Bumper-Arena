using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the OVERHEAT mode looks on the shared screens (<see cref="ModeLook"/>): a fire gradient, golden orange to a
    /// deep red, which reads against the green SELECTED frame of the menu. Its HUD is the heat of every car
    /// (<see cref="HudOverheat"/>); its points are knock-outs, so the score icon stays the burst.
    /// </summary>
    internal static class OverheatLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Overheat,
            new Color32(0xff, 0xb2, 0x4a, 0xff), new Color32(0xe0, 0x2f, 0x24, 0xff),
            createHud: (layer, root, model) => HudOverheat.Create(layer, root, model));
    }
}
