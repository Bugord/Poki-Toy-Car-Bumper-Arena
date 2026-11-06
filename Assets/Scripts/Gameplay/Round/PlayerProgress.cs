using System;
using System.Collections.Generic;
using Bugord.Core.Storage;
using ToyCarArena.Gameplay.Cars;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Saves total XP, cosmetic model and free colour selection. Levels and available cars follow the tuning and
    /// catalog, so unlocks never disagree with saved experience. A car with a price in coins
    /// (<see cref="GameTuning.CoinSettings.PriceOf"/>) is opened by its level and then bought; the bought ones are
    /// saved. Lives in the app scope to survive arena reloads.
    /// </summary>
    public sealed class PlayerProgress : IPlayerCar
    {
        private const string TotalKey = "xp_total";
        private const string SelectedKey = "car_selected";
        private const string ColorKey = "car_color";
        private const string OwnedKey = "cars_owned";
        private const string GarageSeenKey = "garage_seen_level";
        private const string LeagueSeenKey = "league_seen";

        public event Action<int> ColorChanged;

        private readonly IKeyValueStorage storage;
        private readonly GameTuning.ProgressSettings settings;
        private readonly GameTuning.CoinSettings coins;
        private readonly CarCatalog catalog;
        // The cap of the last recorded result, for the ad's bonus on it (CLAIM ×2): one level a result.
        private int resultCap = int.MaxValue;

        public PlayerProgress(IKeyValueStorage storage, GameTuning tuning, CarCatalog catalog)
        {
            this.storage = storage;
            this.settings = tuning.Progress;
            this.coins = tuning.Coins;
            this.catalog = catalog;

            if (settings.LevelXp == null || settings.LevelXp.Length == 0) {
                throw new ArgumentException("GameTuning.Progress.LevelXp must contain at least one level price.");
            }

            foreach (var cost in settings.LevelXp) {
                if (cost <= 0) {
                    throw new ArgumentException("GameTuning.Progress.LevelXp prices must be positive.");
                }
            }

            // Players from before the paid cars keep every car their level had already given them.
            if (!storage.HasKey(OwnedKey)) {
                var owned = new List<string>();
                foreach (var car in catalog.Cars) {
                    if (coins.PriceOf(car.Id) > 0 && car.UnlockLevel <= Level) {
                        owned.Add(car.Id);
                    }
                }

                storage.SetString(OwnedKey, string.Join(",", owned));
            }

            // The garage counts as seen at the level a player had before the menu marked new cars.
            if (!storage.HasKey(GarageSeenKey)) {
                storage.SetInt(GarageSeenKey, Level);
            }
        }

        /// <summary>
        /// Whether a level reached since the garage was last opened (<see cref="MarkGarageSeen"/>) opened a car: the
        /// menu's GARAGE carries NEW for it.
        /// </summary>
        public bool HasUnseenCar
        {
            get {
                var seen = storage.GetInt(GarageSeenKey);
                var level = Level;
                foreach (var car in catalog.Cars) {
                    if (car.UnlockLevel > seen && car.UnlockLevel <= level) {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The player opened the garage: the cars their level has opened so far are no longer new.</summary>
        public void MarkGarageSeen() => storage.SetInt(GarageSeenKey, Level);

        /// <summary>
        /// The highest league (a rank tier, 0 rookie) the menu has greeted with its NEW LEAGUE! popup. A player from
        /// before the leagues has none: the first menu in bronze or higher greets the league they are in, once.
        /// </summary>
        public int SeenLeague => Math.Max(0, storage.GetInt(LeagueSeenKey));

        public void MarkLeagueSeen(int league) => storage.SetInt(LeagueSeenKey, Math.Max(league, SeenLeague));

        public int TotalXp => Math.Max(0, storage.GetInt(TotalKey));
        public int Level => At(TotalXp).Level;
        public CarCatalog.Entry NextCar => catalog.NextLocked(Level);
        public int SelectedColor => CarPaint.OrGreen(storage.GetInt(ColorKey));

        public bool SelectColor(int id)
        {
            if (!CarPaint.IsValid(id) || id == SelectedColor) {
                return false;
            }

            storage.SetInt(ColorKey, id);
            ColorChanged?.Invoke(id);
            return true;
        }

        public string SelectedCar
        {
            get {
                var saved = storage.GetString(SelectedKey);
                return IsOwned(saved) ? saved : catalog.Cars[0].Id;
            }
        }

        public bool Select(string id)
        {
            if (!IsOwned(id)) {
                return false;
            }

            storage.SetString(SelectedKey, id);
            return true;
        }

        public bool IsUnlocked(string id)
        {
            var car = catalog.Find(id);
            return car != null && car.UnlockLevel <= Level;
        }

        /// <summary>The car's price in coins once its level opens it; 0 when the level alone gives it.</summary>
        public int PriceOf(string id) => coins.PriceOf(id);

        /// <summary>Open and, where it has a price, bought: the cars that can be driven.</summary>
        public bool IsOwned(string id)
        {
            if (!IsUnlocked(id)) {
                return false;
            }

            if (PriceOf(id) == 0) {
                return true;
            }

            foreach (var owned in storage.GetString(OwnedKey).Split(',')) {
                if (owned == id) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Open, priced and not bought yet: what coins can buy.</summary>
        public bool IsForSale(string id) => IsUnlocked(id) && !IsOwned(id);

        /// <summary>
        /// Pays for an open car from <paramref name="wallet"/> and drives it next. False, with nothing spent, when it is
        /// not for sale or the coins are short.
        /// </summary>
        public bool Buy(string id, PlayerWallet wallet)
        {
            if (!IsForSale(id) || !wallet.Spend(PriceOf(id))) {
                return false;
            }

            var saved = storage.GetString(OwnedKey);
            storage.SetString(OwnedKey, saved.Length == 0 ? id : saved + "," + id);
            Select(id);
            return true;
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

        /// <summary>
        /// Saves a decided round's XP: its own (a streak round's multiplied, <paramref name="streakBonus"/> being the
        /// streak's share of it) plus the XP of the missions it completed.
        /// </summary>
        public XpGain Record(RoundResult result, GameTuning.StreakSettings streakBonus = null, int missionXp = 0)
        {
            var totalBefore = TotalXp;
            var before = At(totalBefore);
            var roundXp = settings.RoundXp;
            var placeXp = Math.Max(0, result.CarCount - result.Place) * settings.PlaceXp;
            var winXp = result.Outcome == RoundOutcome.Won ? settings.WinXp : 0;
            var knockoutXp = result.RewardKnockouts * settings.KnockoutXp;
            var streakXp = streakBonus?.Bonus(roundXp + placeXp + winXp + knockoutXp) ?? 0;
            // The ad's bonus on this result counts towards the same one level.
            resultCap = CapAbove(before);
            var totalAfter = Math.Min(resultCap,
                checked(totalBefore + roundXp + placeXp + winXp + knockoutXp + streakXp + missionXp));
            var after = At(totalAfter);
            var unlocked = Save(before, after, totalAfter);
            return new XpGain(totalBefore, totalAfter, roundXp, placeXp, winXp, knockoutXp, before, after,
                unlocked, streakXp, missionXp);
        }

        /// <summary>
        /// Saves the extra XP a watched ad gave the result (CLAIM ×2): a reward of its own, from the XP after the round
        /// to after the bonus, with the cars its levels open.
        /// </summary>
        public XpGain AddBonus(int xp)
        {
            var totalBefore = TotalXp;
            var before = At(totalBefore);
            var totalAfter = (int)Math.Min(resultCap, (long)totalBefore + Math.Max(0, xp));
            var after = At(totalAfter);
            var unlocked = Save(before, after, totalAfter);
            return new XpGain(totalBefore, totalAfter, totalAfter - totalBefore, 0, 0, 0, before, after, unlocked);
        }

        // The most XP a result starting at this level may leave: the next level and a share of the one after.
        private int CapAbove(XpLevel level)
        {
            var next = (long)TotalXp - level.Xp + level.Cost;
            var cost = At((int)Math.Min(int.MaxValue, next)).Cost;
            return (int)Math.Min(int.MaxValue, next + Math.Min(cost - 1, (int)(cost * settings.LevelCarry)));
        }

        // Saves the new total and lists the cars the levels crossed open.
        private IReadOnlyList<string> Save(XpLevel before, XpLevel after, int totalAfter)
        {
            var unlocked = new List<string>();
            foreach (var car in catalog.Cars) {
                if (car.UnlockLevel > before.Level && car.UnlockLevel <= after.Level) {
                    unlocked.Add(car.Id);
                }
            }

            // The car stays the player's choice: a new one waits in the garage, or for EQUIP on its popup.
            storage.SetInt(TotalKey, totalAfter);
            return unlocked.AsReadOnly();
        }

        /// <summary>
        /// Development commands edit the same key as real rounds, without leaving another source of progress behind.
        /// </summary>
        internal void SetXp(int total) => storage.SetInt(TotalKey, Math.Max(0, total));

        internal void AddXp(int amount) => SetXp((int)Math.Max(0L, Math.Min(int.MaxValue, (long)TotalXp + amount)));
    }
}
