using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace ToyCarArena.UI
{
    /// <summary>
    /// How far the screen's safe area (clear of a phone's notch, camera cutout, rounded corners and home indicator)
    /// lies inside each edge, in a panel's reference pixels. Unity's web player does not fill in
    /// <see cref="Screen.safeArea"/>, so on WebGL the insets come from the page's CSS <c>env(safe-area-inset-*)</c>
    /// through <c>SafeArea.jslib</c>, less the part of them outside the canvas. Other targets, including the Editor,
    /// keep the existing layout. Ported from SMUP; a candidate for bugord-kit.
    /// </summary>
    public static class SafeArea
    {
        // Milliseconds between checks: the insets change without a resize when a phone turns or goes full screen.
        private const long WatchInterval = 250;

#if UNITY_WEBGL && !UNITY_EDITOR
        private static readonly float[] webInsets = new float[4];

        [DllImport("__Internal")]
        private static extern void JS_SafeArea_GetInsets(float[] insets);
#endif

        public static SafeInsets GetInsets(IPanel panel)
        {
            var panelWidth = panel?.visualTree.layout.width ?? float.NaN;
            if (float.IsNaN(panelWidth) || panelWidth <= 0f || Screen.width <= 0) {
                return default;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            var scale = panelWidth / Screen.width;
            JS_SafeArea_GetInsets(webInsets);
            return new SafeInsets(webInsets[0] * scale, webInsets[1] * scale, webInsets[2] * scale,
                webInsets[3] * scale);
#else
            return default;
#endif
        }

        /// <summary>
        /// Calls <paramref name="changed"/> with the insets as soon as the element is on a panel and whenever they
        /// change after that.
        /// </summary>
        public static void Watch(VisualElement element, Action<SafeInsets> changed)
        {
            var isKnown = false;
            var last = default(SafeInsets);
            void Check()
            {
                if (element.panel == null) {
                    return;
                }

                var insets = GetInsets(element.panel);
                if (isKnown && insets.Equals(last)) {
                    return;
                }

                isKnown = true;
                last = insets;
                changed(insets);
            }

            element.RegisterCallback<GeometryChangedEvent>(_ => Check());
            element.schedule.Execute(Check).Every(WatchInterval);
        }

        /// <summary>
        /// Keeps the element's padding at the insets, so a screen that covers the panel lays its content out inside
        /// the safe area while its background still covers everything.
        /// </summary>
        public static void Pad(VisualElement element)
        {
            Watch(element, insets => {
                element.style.paddingLeft = insets.Left;
                element.style.paddingTop = insets.Top;
                element.style.paddingRight = insets.Right;
                element.style.paddingBottom = insets.Bottom;
            });
        }
    }

    /// <summary>
    /// Reference pixels from each edge to the safe area, rounded up to whole pixels.
    /// </summary>
    public readonly struct SafeInsets : IEquatable<SafeInsets>
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public SafeInsets(float left, float top, float right, float bottom)
        {
            this.Left = Mathf.Ceil(Mathf.Max(0f, left));
            this.Top = Mathf.Ceil(Mathf.Max(0f, top));
            this.Right = Mathf.Ceil(Mathf.Max(0f, right));
            this.Bottom = Mathf.Ceil(Mathf.Max(0f, bottom));
        }

        /// <summary>
        /// The insets as seen from a column of <paramref name="columnWidth"/> centered on a panel
        /// <paramref name="panelWidth"/> wide: the sides only as far as they reach into it.
        /// </summary>
        public SafeInsets ForColumn(float panelWidth, float columnWidth)
        {
            var columnMargin = (panelWidth - columnWidth) / 2f;
            return new SafeInsets(Left - columnMargin, Top, Right - columnMargin, Bottom);
        }

        public bool Equals(SafeInsets other)
        {
            return Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
        }

        public override bool Equals(object obj)
        {
            return obj is SafeInsets other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }
    }
}
