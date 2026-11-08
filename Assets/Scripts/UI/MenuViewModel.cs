using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using UnityEngine;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The main menu a returning visit opens on (<see cref="MenuState"/>): the player's level, XP and coins, their car on
    /// its plinth, the missions, the mode and PLAY. The result is set once, by PLAY (in the menu or the garage's NEXT
    /// MATCH); the garage, the settings, the missions and the modes open over the menu and come back to it. XP BOOST
    /// plays a rewarded ad for double XP over the next matches. DAILY holds the daily gift (<see cref="PlayerDaily"/>): the
    /// first menu of a visit with a gift to claim opens it by itself. Its TASKS tab shows the day's three tasks
    /// (<see cref="DailyTasks"/>) and the day's first win.
    /// </summary>
    public sealed class MenuViewModel : ResultViewModel<bool>
    {
        private readonly PlayerProgress progress;
        private readonly PlayerWallet wallet;
        private readonly MissionTracker missions;
        private readonly CarCatalog catalog;
        private readonly ArenaCatalog arenaCatalog;
        private readonly GameTuning tuning;
        private readonly IScreenNavigator navigator;
        private readonly IGamePlatform platform;
        private readonly LevelScene levelScene;
        private readonly PlayerDaily daily;
        private readonly DailyTasks dailyTasks;
        private GarageStage stage;

        // An ad back without its reward sooner than this had no ad to show (as CLAIM ×2 on the result reads it).
        private const float AdPlayedSeconds = 2f;
        private bool isBoostTried;
        private bool isDailyAdTried;

        // The daily gift opens by itself once a visit, on the first menu with one to claim.
        private static bool isDailyOffered;

        public MenuViewModel(PlayerProgress progress, PlayerWallet wallet, MissionTracker missions, CarCatalog catalog,
            ArenaCatalog arenaCatalog, GameTuning tuning, IScreenNavigator navigator, IGamePlatform platform,
            LevelScene levelScene, PlayerDaily daily, DailyTasks dailyTasks)
        {
            this.daily = daily;
            this.dailyTasks = dailyTasks;
            this.progress = progress;
            this.wallet = wallet;
            this.missions = missions;
            this.catalog = catalog;
            this.arenaCatalog = arenaCatalog;
            this.tuning = tuning;
            this.navigator = navigator;
            this.platform = platform;
            this.levelScene = levelScene;
        }

        /// <summary>The player's level, the XP into it and its price.</summary>
        public XpLevel Level => progress.At(progress.TotalXp);

        /// <summary>The player's league, the rank of their level: 0 rookie .. 4 legend.</summary>
        public int League => PlayerRank.TierOf(Level.Level);

        /// <summary>
        /// Whether the menu should greet the player's league with NEW LEAGUE!: the first menu in a league above rookie
        /// they have not been greeted in. Taking it marks the league seen and counts the popup, so call it as it opens.
        /// </summary>
        public bool TakeNewLeague()
        {
            var league = League;
            if (league <= 0 || league <= progress.SeenLeague) {
                return false;
            }

            progress.MarkLeagueSeen(league);
            platform.MeasureLeagueShown(LeagueLook.IdOf(league));
            return true;
        }

        public int Coins => wallet.Coins;

        /// <summary>The car the player drives next, as the garage shows it: its colour and its topper.</summary>
        public CarCatalog.Entry Car => catalog.Find(progress.SelectedCar);
        public int CarColor => progress.SelectedColor;
        public string Topper => wallet.SelectedTopper;

        /// <summary>
        /// The 3D car on its turntable (<see cref="GarageStage"/>), from the arena loaded behind the menu (or the one a
        /// test gives); null with no arena, when the menu shows the catalog's portrait instead.
        /// </summary>
        public GarageStage Stage
        {
            get => stage ?? (levelScene is { IsLoaded: true } ? levelScene.Resolve<GarageStage>() : null);
            set => stage = value;
        }

        /// <summary>The missions in play, by slot, and the one closest to done for the menu's card.</summary>
        public IReadOnlyList<MissionState> Missions => missions.Slots;
        public MissionState NearestMission => missions.Nearest();

        /// <summary>A mission's task in the language shown, its arena named.</summary>
        public string MissionText(GameTuning.MissionDefinition mission) =>
            GameText.Mission(mission, mission.Map.Length > 0 ? GameText.Arena(arenaCatalog.Find(mission.Map)) : null);

        public int MissionCoins(GameTuning.MissionDefinition mission) => tuning.Missions.CoinsOf(mission.Tier);

        /// <summary>A level reached since the garage was last opened has opened a car: GARAGE carries NEW.</summary>
        public bool HasNewCar => progress.HasUnseenCar;

        /// <summary>The coins buy a topper or an open car: GARAGE carries the red dot, as CAR does on the result.</summary>
        public bool CanShop => GarageViewModel.CanBuyAnyTopper(wallet, tuning) ||
            GarageViewModel.CanBuyAnyCar(progress, wallet, catalog);

        /// <summary>The XP boost is on: the matches it still runs for and what it multiplies their XP by.</summary>
        public bool IsBoosted => progress.IsBoosted;
        public int BoostMatchesLeft => progress.BoostMatchesLeft;
        public int BoostMatches => tuning.Ads.BoostMatches;
        public int BoostMultiplier => tuning.Ads.BoostMultiplier;

        /// <summary>
        /// The menu offers XP BOOST: while ads can play and no boost runs. A running boost still shows its tile, ads or
        /// none; an ad back without its reward takes the offer away until the menu opens again.
        /// </summary>
        public bool CanOfferBoost => tuning.Ads.XpBoost && !platform.IsAdBlocked && !IsBoosted && !isBoostTried;

        /// <summary>The boost's ad is playing: the menu takes no presses meanwhile.</summary>
        public bool IsAdPlaying { get; private set; }

        /// <summary><c>ad / boost / visible</c>: the menu shows the boost's offer.</summary>
        public void BoostShown() => platform.MeasureBoostShown();

        /// <summary>The tile opened the offer's popup: <c>menu / boost / interact</c>.</summary>
        public void BoostOpened() => platform.MeasureMenuPressed("boost");

        /// <summary>NO THANKS (or the close button) on the offer: <c>menu / boost-no / interact</c>.</summary>
        public void BoostDeclined() => platform.MeasureMenuPressed("boost-no");

        /// <summary>
        /// WATCH on the offer: a rewarded ad, the game held still under it; with its reward, the next
        /// <see cref="BoostMatches"/> matches earn <see cref="BoostMultiplier"/> times their XP. False when the ad gave
        /// nothing.
        /// </summary>
        public async UniTask<bool> WatchBoost(CancellationToken token)
        {
            if (!CanOfferBoost || IsAdPlaying || IsDecided) {
                return false;
            }

            platform.MeasureBoostPressed();
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
                isBoostTried = true;
                platform.MeasureBoostEnded(Time.realtimeSinceStartup - started < AdPlayedSeconds
                    ? "unavailable" : "dismissed");
                return false;
            }

            platform.MeasureBoostEnded("granted");
            progress.StartBoost();
            return true;
        }

        // -- Daily gift ----------------------------------------------------------------------------------------------

        /// <summary>The clock the daily gift reads: the player's, or a test's.</summary>
        public Func<DateTime> Clock { get; set; } = () => DateTime.Now;

        private DateTime Now => Clock();

        /// <summary>The calendar day on the clock: the menu redraws when it changes (new tasks, a new first win).</summary>
        public int Today => CalendarDay.Of(Now);

        /// <summary>Whether the menu has the DAILY tile at all: the gift, or the tasks and the first win.</summary>
        public bool HasDaily => daily.IsEnabled || HasDailyTasks;

        /// <summary>Whether the popup has its TASKS tab: the day's tasks or the day's first win.</summary>
        public bool HasDailyTasks => dailyTasks.IsEnabled || tuning.Daily.HasFirstWin;

        /// <summary>Whether the popup has its GIFT tab, the calendar.</summary>
        public bool HasDailyGift => daily.IsEnabled;

        /// <summary>Today's three tasks and how far the player got; drawn on the first look of the day.</summary>
        public IReadOnlyList<DailyTask> TodaysTasks => dailyTasks.Today(Now);

        public int DailyTasksDone => dailyTasks.DoneCount(Now);
        public int DailyTaskXp(GameTuning.MissionDefinition task) => dailyTasks.XpOf(task);
        public int DailyTaskCoins(GameTuning.MissionDefinition task) => dailyTasks.CoinsOf(task);

        /// <summary>The time left until the next tasks and the next first win: to midnight on the player's clock.</summary>
        public TimeSpan DailyTasksTimeLeft => DailyTasks.TimeToNext(Now);

        /// <summary>Whether today's first win, which pays twice, is still to come; false without one in the build.</summary>
        public bool HasFirstWin => daily.HasFirstWin(Now);
        public bool IsFirstWinOn => tuning.Daily.HasFirstWin;
        public int FirstWinMultiplier => daily.FirstWinMultiplier;

        /// <summary>The TASKS tab shown: <c>daily / tasks / visible</c>.</summary>
        public void DailyTasksShown() => platform.MeasureDailyTasksShown();

        /// <summary>Today's gift waits: DAILY reads READY and its popup offers CLAIM.</summary>
        public bool CanClaimDaily => daily.CanClaim(Now);

        /// <summary>The days in a row the calendar shows, from 1, and today's slot of it, from 0.</summary>
        public int DailyStreak => daily.Streak(Now);
        public int DailySlot => daily.Slot(Now);
        public int DailyDays => tuning.Daily.Days;
        public int DailyAdMultiplier => tuning.Daily.AdMultiplier;

        /// <summary>A slot's coins, or its hat (null once owned: the last day pays coins then).</summary>
        public int DailyCoinsOf(int slot) => daily.CoinsOf(slot);
        public string DailyHatOf(int slot) => daily.HatOf(slot);

        /// <summary>Tomorrow's slot, for COME BACK TOMORROW FOR.</summary>
        public int NextDailySlot => daily.NextSlot(Now);

        /// <summary>Time to the next gift, for the tile's countdown; zero while one waits.</summary>
        public TimeSpan DailyTimeLeft => daily.TimeToNext(Now);

        /// <summary>
        /// CLAIM ×2 is offered: today's gift is coins (not the hat), ads can play, and no ad came back empty this time.
        /// </summary>
        public bool CanDoubleDaily => CanClaimDaily && DailyAdMultiplier > 1 && !platform.IsAdBlocked &&
            DailyHatOf(DailySlot) == null && !isDailyAdTried;

        /// <summary>
        /// Whether the menu should open the daily gift by itself: the first menu of the visit with a gift to claim.
        /// Taking it marks the visit's offer made, so call it as the popup opens.
        /// </summary>
        public bool TakeDailyOffer()
        {
            if (isDailyOffered || !CanClaimDaily || IsDecided) {
                return false;
            }

            isDailyOffered = true;
            return true;
        }

        /// <summary>The popup is up: <c>daily / offer / visible</c> with a gift to claim, and its CLAIM ×2 offer.</summary>
        public void DailyShown()
        {
            if (!CanClaimDaily) {
                return;
            }

            platform.MeasureDailyShown();
            if (CanDoubleDaily) {
                platform.MeasureDailyAdShown();
            }
        }

        /// <summary>The DAILY tile opened the popup: <c>menu / daily / interact</c>.</summary>
        public void DailyOpened() => platform.MeasureMenuPressed("daily");

        /// <summary>The popup closed with its gift still to claim: <c>menu / daily-no / interact</c>.</summary>
        public void DailyDeclined() => platform.MeasureMenuPressed("daily-no");

        /// <summary>CLAIM: today's gift, saved at once. Null when it was taken already.</summary>
        public DailyClaim ClaimDaily() => Claim(1);

        /// <summary>
        /// CLAIM ×2: a rewarded ad, the game held still under it; with its reward, today's coins times
        /// <see cref="DailyAdMultiplier"/>. Null when the ad gave nothing: the gift still waits for CLAIM, and the
        /// popup stops offering the ad.
        /// </summary>
        public async UniTask<DailyClaim> WatchDaily(CancellationToken token)
        {
            if (!CanDoubleDaily || IsAdPlaying || IsDecided) {
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
                isDailyAdTried = true;
                platform.MeasureDailyAdEnded(Time.realtimeSinceStartup - started < AdPlayedSeconds
                    ? "unavailable" : "dismissed");
                return null;
            }

            platform.MeasureDailyAdEnded("granted");
            return Claim(DailyAdMultiplier);
        }

        private DailyClaim Claim(int multiplier)
        {
            if (IsDecided) {
                return null;
            }

            var claim = daily.Claim(Now, multiplier);
            if (claim != null) {
                platform.MeasureDailyClaimed(claim);
            }

            return claim;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVisit() => isDailyOffered = false;

        public bool IsGarageOpen { get; private set; }
        public bool IsSettingsOpen { get; private set; }

        /// <summary>
        /// The garage over the menu. It has no PLAY of its own: back returns to the menu, whose PLAY starts the round.
        /// </summary>
        public async UniTask OpenGarage(CancellationToken token)
        {
            if (IsGarageOpen || IsDecided) {
                return;
            }

            IsGarageOpen = true;
            try {
                platform.MeasureMenuPressed("garage");
                progress.MarkGarageSeen();
                await navigator.RunAsync<GarageViewModel, bool>(token);
            }
            finally {
                IsGarageOpen = false;
            }
        }

        /// <summary>The settings popup over the menu, which stays under it.</summary>
        public async UniTask OpenSettings(CancellationToken token)
        {
            if (IsSettingsOpen) {
                return;
            }

            IsSettingsOpen = true;
            try {
                await navigator.RunAsync<SettingsViewModel, bool>(token);
            }
            finally {
                IsSettingsOpen = false;
            }
        }

        /// <summary>The menu's own popups opened: <c>menu / missions, mode, leagues, boost / interact</c>.</summary>
        public void MissionsOpened() => platform.MeasureMenuPressed("missions");
        public void ModesOpened() => platform.MeasureMenuPressed("mode");
        public void LeaguesOpened() => platform.MeasureMenuPressed("leagues");

        /// <summary>Whether PLAY was pressed: the menu takes no more input while the lobby comes down over it.</summary>
        public bool IsDecided { get; private set; }

        public void Play()
        {
            if (IsDecided) {
                return;
            }

            IsDecided = true;
            platform.MeasureMenuPressed("play");
            SetResult(true);
        }
    }
}
