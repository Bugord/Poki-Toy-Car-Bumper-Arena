using System;
using System.Collections.Generic;
using Bugord.Core.Storage;
using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Keeps only total XP in storage: levels and available cars follow the tuning and catalog, so unlocks never
    /// disagree with the saved experience. Lives in the app scope to survive arena reloads.
    /// </summary>
    public sealed class PlayerProgress
    {
        private const string TotalKey = "xp_total";

        private readonly IKeyValueStorage storage;
        private readonly GameTuning.ProgressSettings settings;
        private readonly CarCatalog catalog;

        public PlayerProgress(IKeyValueStorage storage, GameTuning tuning, CarCatalog catalog)
        {
            this.storage = storage;
            this.settings = tuning.Progress;
            this.catalog = catalog;

            if (settings.LevelXp == null || settings.LevelXp.Length == 0) {
                throw new ArgumentException("GameTuning.Progress.LevelXp must contain at least one level price.");
            }

            foreach (var cost in settings.LevelXp) {
                if (cost <= 0) {
                    throw new ArgumentException("GameTuning.Progress.LevelXp prices must be positive.");
                }
            }
        }

        public int TotalXp => Math.Max(0, storage.GetInt(TotalKey));
        public int Level => At(TotalXp).Level;
        public CarCatalog.Entry NextCar => catalog.NextLocked(Level);

        public bool IsUnlocked(string id)
        {
            var car = catalog.Find(id);
            return car != null && car.UnlockLevel <= Level;
        }

        /// <summary>
        /// The final price repeats indefinitely; division avoids walking millions of levels after a console edit.
        /// </summary>
        public XpLevel At(int total)
        {
            var xp = Math.Max(0, total);
            for (var index = 0; index < settings.LevelXp.Length; index++) {
                var cost = settings.LevelXp[index];
                if (xp < cost) {
                    return new XpLevel(index + 1, xp, cost);
                }

                xp -= cost;
            }

            var lastCost = settings.LevelXp[settings.LevelXp.Length - 1];
            return new XpLevel(settings.LevelXp.Length + 1 + xp / lastCost, xp % lastCost, lastCost);
        }

        public XpGain Record(RoundResult result)
        {
            var totalBefore = TotalXp;
            var before = At(totalBefore);
            var roundXp = settings.RoundXp;
            var placeXp = Math.Max(0, result.CarCount - result.Place) * settings.PlaceXp;
            var winXp = result.Outcome == RoundOutcome.Won ? settings.WinXp : 0;
            var knockoutXp = result.Knockouts * settings.KnockoutXp;
            var totalAfter = checked(totalBefore + roundXp + placeXp + winXp + knockoutXp);
            var after = At(totalAfter);
            var unlocked = new List<string>();
            foreach (var car in catalog.Cars) {
                if (car.UnlockLevel > before.Level && car.UnlockLevel <= after.Level) {
                    unlocked.Add(car.Id);
                }
            }

            storage.SetInt(TotalKey, totalAfter);
            return new XpGain(totalBefore, totalAfter, roundXp, placeXp, winXp, knockoutXp, before, after,
                unlocked.AsReadOnly());
        }

        /// <summary>
        /// Development commands edit the same key as real rounds, without leaving another source of progress behind.
        /// </summary>
        internal void SetXp(int total) => storage.SetInt(TotalKey, Math.Max(0, total));

        internal void AddXp(int amount) => SetXp((int)Math.Max(0L, Math.Min(int.MaxValue, (long)TotalXp + amount)));
    }
}
