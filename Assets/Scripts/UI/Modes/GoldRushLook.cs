using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the GOLD RUSH mode looks on the shared screens (<see cref="ModeLook"/>): warm gold, pale to deep. Its HUD is the
    /// coins every car carries (<see cref="HudGoldRush"/>), and its points are coins: the coin icon on the bar, the score
    /// table and the result's table, whose column says coins.
    /// </summary>
    internal static class GoldRushLook
    {
        public static readonly ModeLook Look = new(SpecialRound.GoldRush,
            new Color32(0xff, 0xe2, 0x7a, 0xff), new Color32(0xf0, 0xa8, 0x00, 0xff),
            createHud: (layer, root, model) => HudGoldRush.Create(layer, root, model),
            paintPoints: HudGoldRush.DrawCoin, pointsKey: "result.reward_coins");
    }
}
