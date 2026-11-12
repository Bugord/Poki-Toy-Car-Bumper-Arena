using ToyCarArena.Gameplay;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// How the TEAM PUSH mode looks on the shared screens (<see cref="ModeLook"/>). A stub so far: no HUD of its own and the knock-out burst for its points; the mode's work fills them in here.
    /// </summary>
    internal static class TeamPushLook
    {
        public static readonly ModeLook Look = new(SpecialRound.TeamPush,
            new Color32(0x9c, 0xcc, 0xff, 0xff), new Color32(0x3d, 0x8b, 0xff, 0xff));
    }
}
