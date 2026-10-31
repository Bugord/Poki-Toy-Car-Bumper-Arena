using System;
using ToyCarArena.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// Keeps the 3D scene from rendering at a phone's full pixel count. A browser canvas is as big as the screen times the
    /// device pixel ratio (about 3 megapixels on a ratio-3 phone), and fill rate, not the CPU, is what makes weak phones
    /// lag. So the URP asset's render scale is set so the shorter side of the 3D image is at most a cap in pixels (lower
    /// for a touch device, which has the smaller GPU). UI Toolkit draws after the camera at the screen's own resolution,
    /// so text and stickers stay sharp; only the arena is a little softer. The outline shader reads the size of its
    /// render target, so its line keeps its share of the screen at any scale.
    /// </summary>
    /// <remarks>
    /// Lives in the app scope (Boot). The Game scene opened alone, without Boot, keeps the asset's scale of 1. The asset is
    /// shared and survives Play mode in the editor, so the scale it had is put back on dispose.
    /// </remarks>
    public sealed class RenderScaler : ITickable, IDisposable
    {
        // A scale this small is only reachable through the dev override; the automatic rule never gets close.
        private const float MinScale = 0.1f;

        // Resizing a window drags the scale through many nearly equal values; each change reallocates the render targets.
        private const float Hysteresis = 0.005f;

        private readonly HudControls controls;
        private readonly float touchMaxShortSide;
        private readonly float desktopMaxShortSide;
        private readonly UniversalRenderPipelineAsset asset;
        private readonly float originalScale;

        private int lastWidth = -1;
        private int lastHeight = -1;
        private bool lastTouch;
        private float scaleOverride;
        private bool isDirty = true;

        public RenderScaler(HudControls controls, float touchMaxShortSide, float desktopMaxShortSide)
        {
            this.controls = controls;
            this.touchMaxShortSide = touchMaxShortSide;
            this.desktopMaxShortSide = desktopMaxShortSide;
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            originalScale = asset != null ? asset.renderScale : 1f;
        }

        /// <summary>
        /// The scale the 3D camera renders with now (1 without a URP asset).
        /// </summary>
        public float Scale => asset != null ? asset.renderScale : 1f;

        /// <summary>
        /// The size in pixels of the 3D image, the screen's size times <see cref="Scale"/>.
        /// </summary>
        public Vector2Int RenderSize => new(Mathf.RoundToInt(Screen.width * Scale), Mathf.RoundToInt(Screen.height * Scale));

        /// <summary>
        /// A scale forced by a developer to compare speeds; 0 hands the choice back to the rule.
        /// </summary>
        public float Override
        {
            get => scaleOverride;
            set
            {
                scaleOverride = Mathf.Max(0f, value);
                isDirty = true;
            }
        }

        public void Tick()
        {
            if (asset == null) {
                return;
            }

            var width = Screen.width;
            var height = Screen.height;
            var isTouch = controls.CheckTouchScreen();
            if (!isDirty && width == lastWidth && height == lastHeight && isTouch == lastTouch) {
                return;
            }

            isDirty = false;
            lastWidth = width;
            lastHeight = height;
            lastTouch = isTouch;
            if (width <= 0 || height <= 0) {
                return;
            }

            var scale = scaleOverride > 0f
                ? scaleOverride
                : ScaleFor(Mathf.Min(width, height), isTouch ? touchMaxShortSide : desktopMaxShortSide);
            scale = Mathf.Clamp(scale, MinScale, 2f);

            // Tiny steps are skipped, except back to exactly 1 (no intermediate target at all) and a forced value.
            if (Mathf.Abs(scale - asset.renderScale) > Hysteresis || scaleOverride > 0f || scale == 1f) {
                asset.renderScale = scale;
            }
        }

        public void Dispose()
        {
            if (asset != null) {
                asset.renderScale = originalScale;
            }
        }

        /// <summary>
        /// The render scale that brings the shorter screen side down to <paramref name="maxShortSide"/> pixels, never
        /// above 1: a small screen is not enlarged and a cap of 0 means no limit.
        /// </summary>
        public static float ScaleFor(float shortSide, float maxShortSide)
        {
            if (maxShortSide <= 0f || shortSide <= maxShortSide) {
                return 1f;
            }

            return maxShortSide / shortSide;
        }
    }
}
