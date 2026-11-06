using System.Collections.Generic;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
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
    /// MATCH); the garage, the settings, the missions and the modes open over the menu and come back to it.
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
        private GarageStage stage;

        public MenuViewModel(PlayerProgress progress, PlayerWallet wallet, MissionTracker missions, CarCatalog catalog,
            ArenaCatalog arenaCatalog, GameTuning tuning, IScreenNavigator navigator, IGamePlatform platform,
            LevelScene levelScene)
        {
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

        public bool IsGarageOpen { get; private set; }
        public bool IsSettingsOpen { get; private set; }

        /// <summary>
        /// The garage over the menu. Its NEXT MATCH is PLAY from here, so it sets the menu's result and the menu goes
        /// to the lobby; BACK returns to the menu.
        /// </summary>
        public async UniTask OpenGarage(CancellationToken token)
        {
            if (IsGarageOpen || IsDecided) {
                return;
            }

            IsGarageOpen = true;
            bool play;
            try {
                platform.MeasureMenuPressed("garage");
                progress.MarkGarageSeen();
                play = await navigator.RunAsync<GarageViewModel, bool>(token);
            }
            finally {
                IsGarageOpen = false;
            }

            if (play) {
                Play();
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

        /// <summary>The menu's own popups opened: <c>menu / missions, mode / interact</c>.</summary>
        public void MissionsOpened() => platform.MeasureMenuPressed("missions");
        public void ModesOpened() => platform.MeasureMenuPressed("mode");

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
