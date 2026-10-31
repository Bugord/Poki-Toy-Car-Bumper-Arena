using System.Collections.Generic;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.App;
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

        public GarageViewModel(PlayerProgress progress, CarCatalog catalog, IGamePlatform platform,
            IScreenNavigator navigator)
        {
            this.progress = progress;
            this.catalog = catalog;
            this.platform = platform;
            this.navigator = navigator;
        }

        public bool IsSettingsOpen { get; private set; }

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

        public bool Select(string id)
        {
            if (id == SelectedCar || !progress.Select(id)) {
                return false;
            }

            platform.MeasureGarageEquip(id);
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
