using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the CROWN mode looks on the shared screens (<see cref="ModeLook"/>): purple, so the gold crown stands out on
    /// its card and tile; its HUD is the banner, the holder's seconds and the pointer to the crown (<see cref="HudCrown"/>),
    /// and its points (seconds held) carry the crown's icon on the bar, the scoreboard and the result's table, whose
    /// column says seconds. The win streak's crowns stay off its round: they would be taken for the mode's crown.
    /// </summary>
    internal static class CrownLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Crown,
            new Color32(0xd9, 0xb8, 0xff, 0xff), new Color32(0x8a, 0x4d, 0xff, 0xff),
            createHud: (layer, root, model) => HudCrown.Create(layer, root, model),
            paintPoints: RoundOverMatch.DrawCrown, pointsKey: "result.seconds_short", hidesStreakCrowns: true);
    }
}
