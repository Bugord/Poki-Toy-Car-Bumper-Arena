using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the FOOTBALL mode looks on the shared screens (<see cref="ModeLook"/>): a clear sky blue, the one hue no
    /// other mode has (gold, orange, pink, red, teal and purple are taken), lighter and greener than the blue team, so
    /// the card's orange car and white ball stand out on it and the green frame of a selected card reads around it. Its HUD is the GOAL! banner, the arrow to the red goal and the
    /// ball's pointer (<see cref="HudFootball"/>) over the shared team score; a car's points are its goals, so the score
    /// icon is a football.
    /// </summary>
    internal static class FootballLook
    {
        public static readonly ModeLook Look = new(SpecialRound.Football,
            new Color32(0x9b, 0xe0, 0xff, 0xff), new Color32(0x1f, 0x95, 0xe6, 0xff),
            createHud: (layer, root, model) => HudFootball.Create(layer, root, model),
            paintPoints: HudFootball.PaintPoints);
    }
}
