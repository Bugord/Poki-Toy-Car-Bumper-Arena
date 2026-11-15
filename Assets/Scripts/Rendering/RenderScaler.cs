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
    /// <para>
    /// A touch device that still runs slowly at its cap during a round (the v0.18.0 playtest's phones played at 10 to 25 frames a
    /// second) steps the cap down: after each <see cref="SampleSeconds"/> of frames averaging under
    /// <see cref="SlowFps"/>, by <see cref="StepDown"/>, to no lower than <see cref="LowestShortSide"/>, and stops for good
    /// once a step gained nothing (a phone held back by its CPU). It never steps back up, so the picture does not pump
    /// between two sizes.
    /// </para>
    /// <para>
    /// A desktop also gets MSAA and a thicker outline (<see cref="DesktopMsaa"/>, <see cref="DesktopOutline"/>): its game
    /// frame on Poki is a window with few pixels on its short side, and the comic line, drawn without anti-aliasing at a
    /// share of that side, came out about 2 pixels wide and stair-stepped. A touch device keeps MSAA off: phones are fill
    /// rate bound, and their dense screens hide the steps anyway.
    /// </para>
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

        private const float SampleSeconds = 3f;
        // Under a 30 Hz cap (a phone's low-power mode): such a browser is not slow, and a smaller picture would gain nothing.
        private const float SlowFps = 25f;

        // A step that did not raise the frame rate by this share says the GPU is not what holds it back: stepping stops.
        private const float StepGain = 1.1f;
        private const float StepDown = 0.8f;
        private const float LowestShortSide = 540f;

        // A frame this long is a hidden tab or a load, not the GPU.
        private const float LongestFrame = 0.5f;

        /// <summary>MSAA samples of the 3D image on a desktop; a touch device keeps 1.</summary>
        public const int DesktopMsaa = 4;

        /// <summary>How much thicker the outline is drawn on a desktop than its share of the short side gives.</summary>
        public const float DesktopOutline = 1.3f;

        private static readonly int OutlineDeviceScaleId = Shader.PropertyToID("_OutlineDeviceScale");

        private readonly HudControls controls;
        private readonly float touchMaxShortSide;
        private readonly float desktopMaxShortSide;
        private readonly UniversalRenderPipelineAsset asset;
        private readonly float originalScale;
        private readonly int originalMsaa;

        private int lastWidth = -1;
        private int lastHeight = -1;
        private bool lastTouch;
        private bool isDeviceSet;
        private float scaleOverride;
        private bool isDirty = true;
        private float touchCap;
        private float sampleTime;
        private int sampleFrames;
        private float fpsBeforeStep;
        private bool isStepping = true;

        public RenderScaler(HudControls controls, float touchMaxShortSide, float desktopMaxShortSide)
        {
            this.controls = controls;
            this.touchMaxShortSide = touchMaxShortSide;
            this.desktopMaxShortSide = desktopMaxShortSide;
            touchCap = touchMaxShortSide;
            asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            originalScale = asset != null ? asset.renderScale : 1f;
            originalMsaa = asset != null ? asset.msaaSampleCount : 1;
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
            if (isTouch && scaleOverride <= 0f) {
                SampleSpeed();
            }

            if (!isDirty && width == lastWidth && height == lastHeight && isTouch == lastTouch) {
                return;
            }

            // Only when the kind of device changes, so a developer's perf_msaa stays until then. A touch laptop turns
            // into a touch device on its first touch and loses MSAA there.
            if (!isDeviceSet || isTouch != lastTouch) {
                isDeviceSet = true;
                asset.msaaSampleCount = isTouch ? 1 : DesktopMsaa;
                Shader.SetGlobalFloat(OutlineDeviceScaleId, isTouch ? 1f : DesktopOutline);
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
                : ScaleFor(Mathf.Min(width, height), isTouch ? touchCap : desktopMaxShortSide);
            scale = Mathf.Clamp(scale, MinScale, 2f);

            // Tiny steps are skipped, except back to exactly 1 (no intermediate target at all) and a forced value.
            if (Mathf.Abs(scale - asset.renderScale) > Hysteresis || scaleOverride > 0f || scale == 1f) {
                asset.renderScale = scale;
            }
        }

        /// <summary>The cap on the 3D image's shorter side on a touch device now, after any steps down.</summary>
        public float TouchCap => touchCap;

        /// <summary>Steps the touch cap down after a sample of slow frames; a sample ends either way.</summary>
        private void SampleSpeed()
        {
            // Only the arena in play counts: loads under the curtain and the menus say nothing about the 3D image.
            var frame = Time.unscaledDeltaTime;
            if (ShownScreens.Current != ShownScreen.Round || ShownScreens.IsArenaCovered) {
                return;
            }

            if (!isStepping || frame <= 0f || frame > LongestFrame || touchCap <= LowestShortSide) {
                return;
            }

            sampleTime += frame;
            sampleFrames++;
            if (sampleTime < SampleSeconds) {
                return;
            }

            var fps = sampleFrames / sampleTime;
            sampleTime = 0f;
            sampleFrames = 0;
            if (fpsBeforeStep > 0f && fps < fpsBeforeStep * StepGain) {
                isStepping = false;
                return;
            }

            fpsBeforeStep = 0f;
            if (fps < SlowFps) {
                fpsBeforeStep = fps;
                touchCap = Mathf.Max(LowestShortSide, touchCap * StepDown);
                isDirty = true;
            }
        }

        public void Dispose()
        {
            if (asset != null) {
                asset.renderScale = originalScale;
                asset.msaaSampleCount = originalMsaa;
            }

            Shader.SetGlobalFloat(OutlineDeviceScaleId, 1f);
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
