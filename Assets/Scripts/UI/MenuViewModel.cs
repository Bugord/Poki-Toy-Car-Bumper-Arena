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
    /// first menu of a visit with a gift to claim opens it by itself.
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
        private readonly SessionProgress session;
        // The mode picked without a session (the screenshots and tests build the menu alone).
        private EventMode mode;
        private GarageStage stage;

        // An ad back without its reward sooner than this had no ad to show (as CLAIM ×2 on the result reads it).
        private const float AdPlayedSeconds = 2f;
        private bool isBoostTried;
        private bool isBoostPlaying;

        public MenuViewModel(PlayerProgress progress, PlayerWallet wallet, MissionTracker missions, CarCatalog catalog,
            ArenaCatalog arenaCatalog, GameTuning tuning, IScreenNavigator navigator, IGamePlatform platform,
            LevelScene levelScene, PlayerDaily daily, SessionProgress session = null)
        {
            this.session = session;
            this.Daily = new DailyGift(daily, tuning, platform);
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

        /// <summary>What the player's league gives besides its look, for NEW LEAGUE!; null for none.</summary>
        internal NewLeaguePopup.Prize LeaguePrize =>
            LeagueRewards.PrizeAt(tuning, catalog, progress.Level, progress.SelectedColor, Stage);
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

        public int MissionXp(GameTuning.MissionDefinition mission) => tuning.Missions.XpOf(mission.Tier);

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

        /// <summary>An ad is playing (the boost's or the daily gift's): the menu takes no presses meanwhile.</summary>
        public bool IsAdPlaying => isBoostPlaying || Daily.IsAdPlaying;

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
            isBoostPlaying = true;
            try {
                isRewarded = await AdPause.Run(() => platform.RewardedBreak(token));
            }
            finally {
                isBoostPlaying = false;
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

        /// <summary>Today's gift, as the DAILY tile and its popup (<see cref="MenuDaily"/>) show it.</summary>
        public DailyGift Daily { get; }

        /// <summary>The clock the daily gift reads: the player's, or a test's.</summary>
        public Func<DateTime> Clock
        {
            get => Daily.Clock;
            set => Daily.Clock = value;
        }

        /// <summary>Whether the menu has the daily gift at all.</summary>
        public bool HasDaily => Daily.IsEnabled;

        /// <summary>Today's gift waits: DAILY reads READY and its popup offers CLAIM.</summary>
        public bool CanClaimDaily => Daily.CanClaim;

        /// <summary>The days in a row the calendar shows, from 1, and today's slot of it, from 0.</summary>
        public int DailyStreak => Daily.Streak;
        public int DailySlot => Daily.Slot;
        public int DailyDays => Daily.Days;
        public int DailyAdMultiplier => Daily.AdMultiplier;

        /// <summary>A slot's coins, or its hat (null once owned: the last day pays coins then).</summary>
        public int DailyCoinsOf(int slot) => Daily.CoinsOf(slot);
        public string DailyHatOf(int slot) => Daily.HatOf(slot);

        /// <summary>Tomorrow's slot, for COME BACK TOMORROW FOR.</summary>
        public int NextDailySlot => Daily.NextSlot;

        /// <summary>Time to the next gift, for the tile's countdown; zero while one waits.</summary>
        public TimeSpan DailyTimeLeft => Daily.TimeLeft;

        /// <summary>CLAIM ×2 is offered (<see cref="DailyGift.CanDouble"/>).</summary>
        public bool CanDoubleDaily => Daily.CanDouble;

        /// <summary>
        /// Whether the menu should open the daily gift by itself: the visit's first offer with a gift to claim (a
        /// first visit's result may have made it already). Taking it marks the offer made, so call it as the popup opens.
        /// </summary>
        public bool TakeDailyOffer() => !IsDecided && Daily.TakeOffer();

        /// <summary>The popup is up: <c>daily / offer / visible</c> with a gift to claim, and its CLAIM ×2 offer.</summary>
        public void DailyShown() => Daily.Shown(false);

        /// <summary>The DAILY tile opened the popup: <c>menu / daily / interact</c>.</summary>
        public void DailyOpened() => platform.MeasureMenuPressed("daily");

        /// <summary>The popup closed with its gift still to claim: <c>menu / daily-no / interact</c>.</summary>
        public void DailyDeclined() => platform.MeasureMenuPressed("daily-no");

        /// <summary>CLAIM: today's gift, saved at once. Null when it was taken already.</summary>
        public DailyClaim ClaimDaily() => IsDecided ? null : Daily.Claim();

        /// <summary>
        /// CLAIM ×2: a rewarded ad, the game held still under it; with its reward, today's coins times
        /// <see cref="DailyAdMultiplier"/>. Null when the ad gave nothing: the gift still waits for CLAIM, and the
        /// popup stops offering the ad.
        /// </summary>
        public UniTask<DailyClaim> WatchDaily(CancellationToken token) =>
            IsAdPlaying || IsDecided ? UniTask.FromResult<DailyClaim>(null) : Daily.Watch(token);

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
        /// <remarks>Not sent in v0.17.1, where MODE was a plate; after it MODE opens the modes again, CUP and SURVIVAL as SOON.</remarks>
        public void ModesOpened() => platform.MeasureMenuPressed("mode");
        public void LeaguesOpened() => platform.MeasureMenuPressed("leagues");

        /// <summary>
        /// The mode PLAY starts: QUICK MATCH (<see cref="EventMode.None"/>) or HOT POTATO. Kept for the visit by the
        /// session, so NEXT MATCH and HOME back to the menu keep it.
        /// </summary>
        public EventMode Mode => session?.Mode ?? mode;

        /// <summary>
        /// Whether the menu offers the HOT POTATO event: it is a round of last car standing with one rule changed, so a
        /// build of matches has none.
        /// </summary>
        public bool HasEvents => !tuning.IsMatch;

        /// <summary>
        /// Makes <paramref name="next"/> the mode PLAY starts and counts the pick, <c>menu / mode-hot-potato|mode-quick
        /// / interact</c>, a pick of the mode already in play too: it says the card was understood as a choice.
        /// </summary>
        public void SelectMode(EventMode next)
        {
            if (!HasEvents) {
                next = EventMode.None;
            }

            if (session != null) {
                session.Mode = next;
            }

            mode = next;
            platform.MeasureMenuPressed(next == EventMode.HotPotato ? "mode-hot-potato" : "mode-quick");
        }

        /// <summary>
        /// <c>menu / event-hot-potato / visible</c>: the menu shows the HOT POTATO tile under XP BOOST. Its press is the
        /// same name's <c>interact</c> (<see cref="PlayEvent"/>): Poki drops an interact without a visible of its name,
        /// which is why v0.17.9 has no <c>mode-hot-potato</c> at all.
        /// </summary>
        public void EventShown() => platform.MeasureMenuVisible(EventHotPotato);

        /// <summary>
        /// The HOT POTATO tile: makes the event the mode in play for the visit, as its card in CHOOSE MODE does, and
        /// starts it at once, <c>menu / event-hot-potato / interact</c> before PLAY's own <c>menu / play</c>.
        /// </summary>
        public void PlayEvent()
        {
            if (IsDecided || !HasEvents) {
                return;
            }

            if (session != null) {
                session.Mode = EventMode.HotPotato;
            }

            mode = EventMode.HotPotato;
            platform.MeasureMenuPressed(EventHotPotato);
            Play();
        }

        private const string EventHotPotato = "event-hot-potato";

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
