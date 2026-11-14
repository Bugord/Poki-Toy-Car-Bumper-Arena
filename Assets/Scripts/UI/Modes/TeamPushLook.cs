using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the TEAM PUSH mode looks on the shared screens (<see cref="ModeLook"/>): teal, so the blue and the red car of
    /// its card both stand out on it and it is no team's colour. Its HUD is the shared team score (<see cref="HudTeams"/>,
    /// made for any round in teams), so it has none of its own; a car's points are its knock-outs, so the score icon
    /// stays the burst.
    /// </summary>
    internal static class TeamPushLook
    {
        public static readonly ModeLook Look = new(SpecialRound.TeamPush,
            new Color32(0x8e, 0xf0, 0xe0, 0xff), new Color32(0x14, 0xa8, 0x96, 0xff));
    }
}
