using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ToyCarArena.UI
{
    /// <summary>
    /// Where Poki's pill (the round Poki button it shows over the game on a phone) covers the screen, in a panel's
    /// reference pixels. The game leaves it at Poki's default place, 24 CSS px from the top at the left edge, above the
    /// HUD. The HUD moves below it rather than moving Poki's button into the steering area. The rect comes from
    /// <c>PokiPill.jslib</c>; the Editor and desktop browsers have none. Ported from SMUP; a candidate for bugord-kit.
    /// </summary>
    public static class PokiPill
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private static readonly float[] webRect = new float[4];

        [DllImport("__Internal")]
        private static extern int JS_PokiPill_GetRect(float[] rect);
#endif

        /// <summary>
        /// The pill's rect in the panel's coordinates, false where Poki shows no pill.
        /// </summary>
        public static bool TryGetRect(IPanel panel, out Rect rect)
        {
            rect = default;
            var panelWidth = panel?.visualTree.layout.width ?? float.NaN;
            if (float.IsNaN(panelWidth) || panelWidth <= 0f || Screen.width <= 0) {
                return false;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            if (JS_PokiPill_GetRect(webRect) == 0) {
                return false;
            }

            // Canvas pixels to reference pixels, as SafeArea does.
            var scale = panelWidth / Screen.width;
            rect = Rect.MinMaxRect(webRect[0] * scale, webRect[1] * scale, webRect[2] * scale, webRect[3] * scale);
            return rect.width > 0f && rect.height > 0f;
#else
            return false;
#endif
        }

        /// <summary>
        /// Repositions a view if the SDK becomes available after its first layout, even when safe insets stay the same.
        /// </summary>
        public static void Watch(VisualElement element, Action changed)
        {
            var isKnown = false;
            var last = default(Rect);
            var hadPill = false;
            element.schedule.Execute(() => {
                if (element.panel == null) {
                    return;
                }

                var hasPill = TryGetRect(element.panel, out var rect);
                if (isKnown && hasPill == hadPill && rect == last) {
                    return;
                }

                isKnown = true;
                hadPill = hasPill;
                last = rect;
                changed();
            }).Every(250);
        }
    }
}
