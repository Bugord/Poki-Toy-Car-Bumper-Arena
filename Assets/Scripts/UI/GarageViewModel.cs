using System.Collections.Generic;
using Bugord.App.Platform;
using ToyCarArena.App;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>Cosmetic selection for the next round, backed by the same saved progress as the reward screen.</summary>
    public sealed class GarageViewModel : ResultViewModel<bool>
    {
        private readonly PlayerProgress progress;
        private readonly CarCatalog catalog;
        private readonly IGamePlatform platform;

        public GarageViewModel(PlayerProgress progress, CarCatalog catalog, IGamePlatform platform)
        {
            this.progress = progress;
            this.catalog = catalog;
            this.platform = platform;
        }

        public IReadOnlyList<CarCatalog.Entry> Cars => catalog.Cars;
        public XpLevel Level => progress.At(progress.TotalXp);
        public string SelectedCar => progress.SelectedCar;
        public bool IsUnlocked(string id) => progress.IsUnlocked(id);

        public bool Select(string id)
        {
            if (id == SelectedCar || !progress.Select(id)) {
                return false;
            }

            platform.MeasureGarageEquip(id);
            return true;
        }

        public void Back() => SetResult(true);
    }
}
