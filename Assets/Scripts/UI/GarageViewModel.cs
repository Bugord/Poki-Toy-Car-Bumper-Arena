using System.Collections.Generic;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Cosmetic selection for the next round, backed by the same saved progress as the reward screen. The result is
    /// true when the player asks for the next match from here, false when they go back to the result.
    /// </summary>
    public sealed class GarageViewModel : ResultViewModel<bool>
    {
        private readonly PlayerProgress progress;
        private readonly CarCatalog catalog;
        private readonly IGamePlatform platform;
        private readonly IScreenNavigator navigator;
        private readonly PlayerWallet wallet;
        private readonly GameTuning tuning;
        private readonly LevelScene levelScene;
        private GarageStage stage;
        private bool isToppersPressed;

        public GarageViewModel(PlayerProgress progress, CarCatalog catalog, IGamePlatform platform,
            IScreenNavigator navigator, PlayerWallet wallet, GameTuning tuning, LevelScene levelScene)
        {
            this.levelScene = levelScene;
            this.wallet = wallet;
            this.tuning = tuning;
            this.progress = progress;
            this.catalog = catalog;
            this.platform = platform;
            this.navigator = navigator;
        }

        public bool IsSettingsOpen { get; private set; }

        /// <summary>
        /// The 3D pictures of the car and the toppers, from the arena scene under the result (or the one a test gives);
        /// null with no arena loaded, when the screen falls back to the catalog's portraits and flat icons.
        /// </summary>
        public GarageStage Stage
        {
            get => stage ?? (levelScene is { IsLoaded: true } ? levelScene.Resolve<GarageStage>() : null);
            set => stage = value;
        }

        /// <summary>The settings popup over this screen, which stays attached under it.</summary>
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

        public IReadOnlyList<CarCatalog.Entry> Cars => catalog.Cars;
        public string SelectedCar => progress.SelectedCar;
        public CarCatalog.Entry SelectedEntry => catalog.Find(progress.SelectedCar);
        public int SelectedColor => progress.SelectedColor;
        public bool IsUnlocked(string id) => progress.IsUnlocked(id);

        /// <summary>The car's speed, power and boost for the player.</summary>
        public GameTuning.CarStatLine StatsOf(CarCatalog.Entry car) => tuning.CarStats.Of(car.Id);

        /// <summary>Whether cars boost in this build, so the boost stat means something.</summary>
        public bool HasBoost => tuning.Boost.IsEnabled;
        public bool IsForSale(string id) => progress.IsForSale(id);
        public int PriceOf(string id) => progress.PriceOf(id);
        public bool CanAffordCar(string id) => wallet.Coins >= progress.PriceOf(id);

        /// <summary>Buys an open car the coins pay for and drives it next; false when it is not for sale or too dear.</summary>
        public bool BuyCar(string id)
        {
            if (!progress.Buy(id, wallet)) {
                return false;
            }

            platform.MeasureCarBought(id);
            platform.MeasureGarageEquip(id);
            return true;
        }

        public bool Select(string id)
        {
            if (id == SelectedCar || !progress.Select(id)) {
                return false;
            }

            platform.MeasureGarageEquip(id);
            return true;
        }

        public int Coins => wallet.Coins;
        public IReadOnlyList<GameTuning.TopperDefinition> Toppers => tuning.Toppers.All;
        public string SelectedTopper => wallet.SelectedTopper;
        public bool IsOwned(string id) => wallet.IsOwned(id);
        public bool CanAfford(GameTuning.TopperDefinition topper) => wallet.Coins >= topper.Price;

        /// <summary>Whether coins buy a topper not owned yet: the tab, and CAR on the result, show a dot.</summary>
        public bool CanBuyTopper => CanBuyAnyTopper(wallet, tuning);

        /// <summary>Whether coins buy a car its level has opened: CAR on the result shows a dot for it too.</summary>
        public static bool CanBuyAnyCar(PlayerProgress progress, PlayerWallet wallet, CarCatalog catalog)
        {
            foreach (var car in catalog.Cars) {
                if (progress.IsForSale(car.Id) && wallet.Coins >= progress.PriceOf(car.Id)) {
                    return true;
                }
            }

            return false;
        }

        public static bool CanBuyAnyTopper(PlayerWallet wallet, GameTuning tuning)
        {
            foreach (var topper in tuning.Toppers.All) {
                if (!wallet.IsOwned(topper.Id) && wallet.Coins >= topper.Price) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The garage shows its TOPPERS tab; the first press of it this time is counted too.</summary>
        public void ToppersShown() => platform.MeasureToppersShown();

        public void ToppersPressed()
        {
            if (!isToppersPressed) {
                isToppersPressed = true;
                platform.MeasureToppersPressed();
            }
        }

        /// <summary>
        /// Wears an owned topper (or none for an empty id), or buys and wears one the coins pay for. False when nothing
        /// changes: it is worn already, or too dear.
        /// </summary>
        public bool PickTopper(string id)
        {
            if (string.IsNullOrEmpty(id) || wallet.IsOwned(id)) {
                if (!wallet.Wear(id)) {
                    return false;
                }

                platform.MeasureTopperEquip(id);
                return true;
            }

            if (!wallet.Buy(id)) {
                return false;
            }

            platform.MeasureTopperBought(id);
            platform.MeasureTopperEquip(id);
            return true;
        }

        public void Back() => SetResult(false);

        public void NextMatch() => SetResult(true);

        public bool SelectColor(int id)
        {
            if (!progress.SelectColor(id)) {
                return false;
            }
            platform.MeasureGarageColor(id);
            return true;
        }
    }
}
