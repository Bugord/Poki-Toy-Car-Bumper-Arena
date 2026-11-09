using System;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Today's daily gift as a screen offers it (<see cref="PlayerDaily"/> on the player's clock): the calendar the popup
    /// draws (<see cref="MenuDaily"/>), CLAIM, CLAIM ×2 after a rewarded ad, and the one offer a visit makes by itself.
    /// The main menu and the result hold one each, so the popup works the same over either; the offer is the visit's,
    /// whichever screen makes it: a player who closed it in the menu is not asked again over the result.
    /// </summary>
    public sealed class DailyGift
    {
        // An ad back without its reward sooner than this had no ad to show (as CLAIM ×2 on the result reads it).
        private const float AdPlayedSeconds = 2f;

        // The gift opens by itself once a visit, on the first screen that offers it with one to claim.
        private static bool isOffered;

        private readonly PlayerDaily daily;
        private readonly GameTuning tuning;
        private readonly IGamePlatform platform;
        private bool isAdTried;

        public DailyGift(PlayerDaily daily, GameTuning tuning, IGamePlatform platform)
        {
            this.daily = daily;
            this.tuning = tuning;
            this.platform = platform;
        }

        /// <summary>The clock the gift reads: the player's, or a test's.</summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        private DateTime Now => Clock();

        /// <summary>Whether there is a daily gift at all.</summary>
        public bool IsEnabled => daily.IsEnabled;

        /// <summary>Today's gift waits: DAILY reads READY and the popup offers CLAIM.</summary>
        public bool CanClaim => daily.CanClaim(Now);

        /// <summary>The days in a row the calendar shows, from 1, and today's slot of it, from 0.</summary>
        public int Streak => daily.Streak(Now);
        public int Slot => daily.Slot(Now);
        public int Days => tuning.Daily.Days;
        public int AdMultiplier => tuning.Daily.AdMultiplier;

        /// <summary>A slot's coins, or its hat (null once owned: the last day pays coins then).</summary>
        public int CoinsOf(int slot) => daily.CoinsOf(slot);
        public string HatOf(int slot) => daily.HatOf(slot);

        /// <summary>Tomorrow's slot, for COME BACK TOMORROW FOR.</summary>
        public int NextSlot => daily.NextSlot(Now);

        /// <summary>Time to the next gift, for the tile's countdown; zero while one waits.</summary>
        public TimeSpan TimeLeft => daily.TimeToNext(Now);

        /// <summary>
        /// CLAIM ×2 is offered: today's gift is coins (not the hat), ads can play, and no ad came back empty this time.
        /// </summary>
        public bool CanDouble => CanClaim && AdMultiplier > 1 && !platform.IsAdBlocked && HatOf(Slot) == null &&
            !isAdTried;

        /// <summary>The rewarded ad of CLAIM ×2 is playing: the screen takes no presses meanwhile.</summary>
        public bool IsAdPlaying { get; private set; }

        /// <summary>
        /// Whether the screen should open the gift by itself: the visit's first offer, with a gift to claim. Taking it
        /// marks the visit's offer made, so call it as the popup opens.
        /// </summary>
        public bool TakeOffer()
        {
            if (isOffered || !CanClaim) {
                return false;
            }

            isOffered = true;
            return true;
        }

        /// <summary>
        /// The popup is up: <c>daily / offer / visible</c> with a gift to claim (and <c>offer-result</c> over a result),
        /// and its CLAIM ×2 offer.
        /// </summary>
        public void Shown(bool isOnResult)
        {
            if (!CanClaim) {
                return;
            }

            platform.MeasureDailyShown();
            if (isOnResult) {
                platform.MeasureDailyResultShown();
            }

            if (CanDouble) {
                platform.MeasureDailyAdShown();
            }
        }

        /// <summary>CLAIM: today's gift, saved at once, times <paramref name="multiplier"/>. Null when it was taken.</summary>
        public DailyClaim Claim(int multiplier = 1)
        {
            var claim = daily.Claim(Now, multiplier);
            if (claim != null) {
                platform.MeasureDailyClaimed(claim);
            }

            return claim;
        }

        /// <summary>
        /// CLAIM ×2: a rewarded ad, the game held still under it; with its reward, today's coins times
        /// <see cref="AdMultiplier"/>. Null when the ad gave nothing: the gift still waits for CLAIM, and the popup stops
        /// offering the ad.
        /// </summary>
        public async UniTask<DailyClaim> Watch(CancellationToken token)
        {
            if (!CanDouble || IsAdPlaying) {
                return null;
            }

            platform.MeasureDailyAdPressed();
            var started = Time.realtimeSinceStartup;
            bool isRewarded;
            IsAdPlaying = true;
            try {
                isRewarded = await AdPause.Run(() => platform.RewardedBreak(token));
            }
            finally {
                IsAdPlaying = false;
            }

            if (!isRewarded) {
                isAdTried = true;
                platform.MeasureDailyAdEnded(Time.realtimeSinceStartup - started < AdPlayedSeconds
                    ? "unavailable" : "dismissed");
                return null;
            }

            platform.MeasureDailyAdEnded("granted");
            return Claim(AdMultiplier);
        }

        /// <summary>Tests: the visit's offer not made yet, as at the start of a visit.</summary>
        public static void ForgetOffer() => isOffered = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVisit() => isOffered = false;
    }
}
