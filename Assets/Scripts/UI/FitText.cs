using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Shrinks an outlined label (both its labels, see <see cref="RoundOverProgress.Outlined"/>) to the width its button
    /// leaves it, never above the size the style sheet gives it: the longer words of other languages stay inside the
    /// button. Best-fit would size the outline and the face over it apart, so one size is set on both.
    /// </summary>
    internal static class FitText
    {
        // The smallest share of its own size the text goes down to; a little room for the outline round the letters.
        private const float MinShare = 0.5f;
        private const float Room = 0.96f;

        /// <summary>
        /// Keeps <paramref name="labels"/> inside <paramref name="stroke"/>, which must be free to shrink below its text
        /// (flex-shrink, a parent of limited width). One that fills its room (flex-grow) also grows back.
        /// </summary>
        public static void Fit(VisualElement stroke, Label[] labels)
        {
            var full = 0f;
            stroke.RegisterCallback<GeometryChangedEvent>(_ => {
                var label = labels[0];
                var size = label.resolvedStyle.fontSize;
                if (full <= 0f) {
                    full = size;
                }

                var width = stroke.contentRect.width;
                if (full <= 0f || float.IsNaN(width) || width <= 0f || string.IsNullOrEmpty(label.text)) {
                    return;
                }

                var text = label.MeasureTextSize(label.text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                    VisualElement.MeasureMode.Undefined).x;
                if (text <= 0f) {
                    return;
                }

                // Down when the text is wider than its room; back up (to its own size at most) when a label that fills
                // its room has more of it, as after an earlier, narrower layout. A label as wide as its text stays.
                var share = width * Room / text;
                var fit = Mathf.Clamp(text > width ? size * share : Mathf.Max(size, size * share), full * MinShare, full);
                if (Mathf.Abs(fit - size) < 0.5f) {
                    return;
                }

                foreach (var part in labels) {
                    part.style.fontSize = fit;
                }
            });
        }
    }
}
