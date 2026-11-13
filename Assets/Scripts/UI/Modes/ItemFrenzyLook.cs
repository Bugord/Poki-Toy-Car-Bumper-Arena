using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the ITEM FRENZY mode looks on the shared screens (<see cref="ModeLook"/>): pink, the colour of the boxes'
    /// mystery, and its HUD the item slot shown from the first second with its hint until the first item is used
    /// (<see cref="ItemFrenzyHud"/>). A round of last car standing, so it has no points and the score icon stays the burst.
    /// </summary>
    internal static class ItemFrenzyLook
    {
        public static readonly ModeLook Look = new(SpecialRound.ItemFrenzy,
            new Color32(0xff, 0xa6, 0xdc, 0xff), new Color32(0xff, 0x4f, 0xa8, 0xff),
            createHud: (layer, root, model) => ItemFrenzyHud.Create(layer, root, model));
    }
}
