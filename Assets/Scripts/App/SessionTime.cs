using System.Runtime.InteropServices;
using Bugord.App.Platform;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.App
{
    /// <summary>
    /// How long a visit stays, for the tests Poki's dashboard shows no session length for: it gives the average
    /// session and the share of players past three minutes (engaged) only for a Player Fit Test, not for a Web Fit
    /// Test or live traffic. So the game sends one milestone the first time the visit has been on the page so many
    /// minutes (<c>session / time-Nm / reached</c>) and one for minutes in rounds (<c>session / play-Nm /
    /// reached</c>); a report reads the visits that reached each, and the 3 minute one is the engaged share.
    /// </summary>
    /// <remarks>
    /// Time on the page starts where the browser's does (the page's <c>performance.now()</c> on the first frame), so it
    /// takes in the download and the engine's start, as a visit's session does. After that it adds the frames' real
    /// time, but at most <see cref="MaxFrame"/> of a frame: the player loop stops while the tab is hidden (Run In
    /// Background is off) and its next frame spans the whole pause, which is not time in the game. So this is time in
    /// front of the game and may come out shorter than Poki's own session length, which a Player Fit Test calibrates.
    /// Time in rounds is <see cref="AdBreaks.Played"/>, from GO to each round's end, so it moves at round ends and
    /// leaves out the result screen, the garage and the lobby. Lives in the app scope for the whole visit.
    /// </remarks>
    public sealed class SessionTime : ITickable
    {
        /// <summary>
        /// The minutes that send a milestone, each once a visit: finer early, where most visits end, and three for
        /// Poki's engaged threshold.
        /// </summary>
        public static readonly int[] Milestones = { 1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30 };

        /// <summary>
        /// The most real seconds one frame adds: a scene load or a weak phone's hitch stays in, a hidden tab does not.
        /// </summary>
        public const float MaxFrame = 4f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern double JS_PageTime_Seconds();
#endif

        private readonly IGamePlatform platform;
        private readonly AdBreaks adBreaks;

        private bool isStarted;
        private float onPage;
        private int nextOnPage;
        private int nextInRounds;

        public SessionTime(IGamePlatform platform, AdBreaks adBreaks)
        {
            this.platform = platform;
            this.adBreaks = adBreaks;
        }

        /// <summary>Seconds the visit has been on the page, as counted for the milestones.</summary>
        public float OnPage => onPage;

        public void Tick()
        {
            if (!isStarted) {
                isStarted = true;
                onPage = PageSeconds();
                Check();
                return;
            }

            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Adds a frame's real seconds, capped at <see cref="MaxFrame"/>, and sends the milestones they cross. The
        /// frames go through it; a test drives it alone.
        /// </summary>
        public void Advance(float seconds)
        {
            onPage += Mathf.Clamp(seconds, 0f, MaxFrame);
            Check();
        }

        private void Check()
        {
            while (nextOnPage < Milestones.Length && onPage >= Milestones[nextOnPage] * 60f) {
                platform.MeasureSessionTime(Milestones[nextOnPage]);
                nextOnPage++;
            }

            var inRounds = adBreaks.Played;
            while (nextInRounds < Milestones.Length && inRounds >= Milestones[nextInRounds] * 60f) {
                platform.MeasureSessionPlay(Milestones[nextInRounds]);
                nextInRounds++;
            }
        }

        // Seconds since the page began to load; outside the browser the count starts with the game.
        private static float PageSeconds()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Mathf.Max(0f, (float)JS_PageTime_Seconds());
#else
            return 0f;
#endif
        }
    }
}
