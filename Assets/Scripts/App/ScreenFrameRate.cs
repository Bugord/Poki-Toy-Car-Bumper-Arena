using Bugord.App.Platform;
using ToyCarArena.Rendering;
using ToyCarArena.UI;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// The game's frame rate as players get it, since Poki's Player Fit Test only gives one median for a whole visit:
    /// once a visit, the frames per second of each screen (<see cref="ShownScreens.Current"/>) over its first
    /// <see cref="SampleOf"/> seconds, summed over every time it was shown (<c>fps / round-touch-55 / screen</c>), and
    /// the median of the frames counted in each of the visit's first <see cref="VisitSeconds"/> seconds
    /// (<c>fps / visit-touch-55 / visit</c>), as Poki counts its own. A report reads the visit one against Poki's
    /// number, and the screens for where a phone loses its frames.
    /// </summary>
    /// <remarks>
    /// These are the game's frames, which the browser's animation frames drive, so they match Poki's count while the game
    /// keeps up. A frame longer than <see cref="MaxFrame"/> (a hidden tab: the player loop stops) is left out, and the
    /// second it fell in is dropped for the visit's median. Touch or mouse is read when the event goes. Lives in the app
    /// scope for the whole visit; a tick allocates nothing.
    /// </remarks>
    public sealed class ScreenFrameRate : ITickable
    {
        /// <summary>Seconds of the visit whose frame counts make its median, from the game's first frame.</summary>
        public const int VisitSeconds = 60;

        /// <summary>The longest frame counted, in seconds; a longer one is a pause, not a slow frame.</summary>
        public const float MaxFrame = 1f;

        private readonly IGamePlatform platform;
        private readonly HudControls controls;

        // Indexed by ShownScreen: seconds and frames so far, and whether the screen's event went.
        private readonly float[] seconds = new float[(int)ShownScreen.Ad + 1];
        private readonly int[] frames = new int[(int)ShownScreen.Ad + 1];
        private readonly bool[] isSent = new bool[(int)ShownScreen.Ad + 1];

        private readonly int[] perSecond = new int[VisitSeconds];
        private int secondsCounted;
        private float secondTime;
        private int secondFrames;

        public ScreenFrameRate(IGamePlatform platform, HudControls controls)
        {
            this.platform = platform;
            this.controls = controls;
        }

        /// <summary>
        /// Seconds on <paramref name="screen"/> its frame rate is measured over: enough frames to settle, yet short
        /// enough that most visits get there. The lobby lasts under two seconds a time, so it adds up over a few.
        /// </summary>
        public static float SampleOf(ShownScreen screen)
        {
            switch (screen) {
                case ShownScreen.Round:
                    return 20f;
                case ShownScreen.Result:
                    return 8f;
                case ShownScreen.Curtain:
                    return 3f;
                case ShownScreen.Menu:
                case ShownScreen.Garage:
                    return 6f;
                case ShownScreen.Ad:
                    return 4f;
                default:
                    return 0f;
            }
        }

        public void Tick() => Advance(Time.unscaledDeltaTime, ShownScreens.Current);

        /// <summary>
        /// Counts a frame of <paramref name="frame"/> real seconds on <paramref name="screen"/>, and sends what it
        /// completes. The frames go through it; a test drives it alone.
        /// </summary>
        public void Advance(float frame, ShownScreen screen)
        {
            if (frame <= 0f || frame > MaxFrame) {
                // The second a pause fell in says nothing of the frame rate.
                secondTime = 0f;
                secondFrames = 0;
                return;
            }

            CountVisit(frame);
            var index = (int)screen;
            if (screen == ShownScreen.None || isSent[index]) {
                return;
            }

            seconds[index] += frame;
            frames[index]++;
            if (seconds[index] >= SampleOf(screen)) {
                isSent[index] = true;
                platform.MeasureScreenFps(screen, controls.CheckTouchScreen(), frames[index] / seconds[index]);
            }
        }

        private void CountVisit(float frame)
        {
            if (secondsCounted >= VisitSeconds) {
                return;
            }

            secondTime += frame;
            secondFrames++;
            if (secondTime < 1f) {
                return;
            }

            // A long frame across the second's end counts towards the second it ends, as a browser's count would.
            perSecond[secondsCounted++] = Mathf.RoundToInt(secondFrames / secondTime);
            secondTime = 0f;
            secondFrames = 0;
            if (secondsCounted == VisitSeconds) {
                System.Array.Sort(perSecond);
                platform.MeasureVisitFps(controls.CheckTouchScreen(), perSecond[VisitSeconds / 2]);
            }
        }
    }
}
