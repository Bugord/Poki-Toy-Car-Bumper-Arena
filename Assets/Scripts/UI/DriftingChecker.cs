using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A quiet background for the full screens over navy (the garage, the curtain): a checker of slightly lighter
    /// squares drifting slowly sideways, as party games put behind their menus. The squares are drawn once for the
    /// element's size; only its translation changes, by a whole period at most, so a frame costs no new geometry.
    /// It sits under everything, catches no input and stands still while its screen is hidden.
    /// </summary>
    internal sealed class DriftingChecker : VisualElement
    {
        private const float Tile = 180f;
        private const float Speed = 36f;
        private static readonly Color Square = new(1f, 1f, 1f, 0.025f);

        private readonly VisualElement screen;

        private DriftingChecker(VisualElement screen)
        {
            this.screen = screen;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            // One period wider than the screen on the left, so the drift always has squares to bring in.
            style.left = -2f * Tile;
            style.top = 0f;
            style.right = 0f;
            style.bottom = 0f;
            generateVisualContent += Draw;
            schedule.Execute(Step).Every(0);
        }

        /// <summary>Puts the checker under everything else of <paramref name="screen"/>.</summary>
        public static void AddTo(VisualElement screen) => screen.Insert(0, new DriftingChecker(screen));

        private void Step()
        {
            if (screen.resolvedStyle.display == DisplayStyle.None || screen.resolvedStyle.opacity <= 0f) {
                return;
            }

            style.translate = new Translate(Time.unscaledTime * Speed % (2f * Tile), 0f);
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f) {
                return;
            }

            var painter = context.painter2D;
            painter.fillColor = Square;
            painter.BeginPath();
            for (var row = 0; row * Tile < rect.height; row++) {
                for (var column = row % 2; column * Tile < rect.width; column += 2) {
                    var x = column * Tile;
                    var y = row * Tile;
                    painter.MoveTo(new Vector2(x, y));
                    painter.LineTo(new Vector2(x + Tile, y));
                    painter.LineTo(new Vector2(x + Tile, y + Tile));
                    painter.LineTo(new Vector2(x, y + Tile));
                    painter.ClosePath();
                }
            }

            painter.Fill();
        }
    }
}
