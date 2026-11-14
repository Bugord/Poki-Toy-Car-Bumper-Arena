using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Modes;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The looks of every event mode (<see cref="ModeLook"/>), for the shared screens to draw a mode by its kind instead of
    /// naming it. Every mode of <see cref="EventModes.Circle"/> has its entry already: a new mode fills in its own
    /// <c>&lt;Mode&gt;Look.cs</c> and leaves this list alone.
    /// </summary>
    internal static class ModeLooks
    {
        private static readonly ModeLook[] All = {
            HotPotatoLook.Look,
            CrownLook.Look,
            GoldRushLook.Look,
            ItemFrenzyLook.Look,
            OverheatLook.Look,
            TeamPushLook.Look,
            FootballLook.Look,
        };

        /// <summary>The look of a mode, null for a round of no mode (or a special round of the rotation).</summary>
        public static ModeLook Of(SpecialRound kind)
        {
            foreach (var look in All) {
                if (look.Kind == kind) {
                    return look;
                }
            }

            return null;
        }

        public static ModeLook Of(EventMode mode) => Of(EventModes.KindOf(mode));

        public static ModeLook Of(IRoundMode mode) => mode != null ? Of(mode.Kind) : null;

        /// <summary>
        /// Draws the icon of a round's points on <paramref name="icon"/>: the mode's own, or the knock-out burst of the
        /// match and of a mode whose points are knock-outs.
        /// </summary>
        public static void DrawPoints(VisualElement icon, SpecialRound kind)
        {
            var paint = Of(kind)?.PaintPoints;
            if (paint == null) {
                HudView.DrawAsBurst(icon);
                return;
            }

            icon.Clear();
            icon.generateVisualContent -= paint;
            icon.generateVisualContent += paint;
            icon.MarkDirtyRepaint();
        }
    }
}
