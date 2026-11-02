using System;
using System.Collections.Generic;
using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The player's coins and the toppers they bought, in the kit's storage, so a returning player keeps both. A round
    /// pays in <see cref="Record"/>; the garage spends in <see cref="Buy"/>. Lives in the app scope beside
    /// <see cref="PlayerProgress"/>, and like it saves at once, so leaving the result screen early loses nothing.
    /// </summary>
    public sealed class PlayerWallet : IPlayerTopper
    {
        private const string CoinsKey = "coins";
        private const string OwnedKey = "toppers_owned";
        private const string SelectedKey = "topper_selected";

        public event Action Changed;

        private readonly IKeyValueStorage storage;
        private readonly GameTuning tuning;

        public PlayerWallet(IKeyValueStorage storage, GameTuning tuning)
        {
            this.storage = storage;
            this.tuning = tuning;
        }

        public int Coins => Math.Max(0, storage.GetInt(CoinsKey));

        /// <summary>The topper worn, or empty for none; one no longer owned or on sale reads as none.</summary>
        public string SelectedTopper
        {
            get {
                var id = storage.GetString(SelectedKey);
                return IsOwned(id) ? id : "";
            }
        }

        public bool IsOwned(string id)
        {
            if (string.IsNullOrEmpty(id) || tuning.Toppers.Find(id) == null) {
                return false;
            }

            foreach (var owned in Owned()) {
                if (owned == id) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Pays for a topper and wears it. False, with nothing spent, when it is owned already, not on sale or too dear.
        /// </summary>
        public bool Buy(string id)
        {
            var topper = tuning.Toppers.Find(id);
            if (topper == null || IsOwned(id) || Coins < topper.Price) {
                return false;
            }

            storage.SetInt(CoinsKey, Coins - topper.Price);
            var owned = new List<string>(Owned()) { id };
            storage.SetString(OwnedKey, string.Join(",", owned));
            storage.SetString(SelectedKey, id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Wears an owned topper, or none for an empty id. False when nothing changes.</summary>
        public bool Wear(string id)
        {
            id ??= "";
            if ((id.Length > 0 && !IsOwned(id)) || id == SelectedTopper) {
                return false;
            }

            storage.SetString(SelectedKey, id);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Pays a decided round: its own coins (a streak round's multiplied, <paramref name="isStreakRound"/>) plus the
        /// coins of the missions it completed.
        /// </summary>
        public CoinGain Record(RoundResult result, bool isStreakRound, int missionCoins)
        {
            var settings = tuning.Coins;
            var before = Coins;
            var round = settings.RoundCoins;
            var knockouts = result.Knockouts * settings.KnockoutCoins;
            var win = result.Outcome == RoundOutcome.Won ? settings.WinCoins : 0;
            var streak = isStreakRound ? tuning.Streaks.Bonus(round + knockouts + win) : 0;
            var after = (int)Math.Min(int.MaxValue, (long)before + round + knockouts + win + streak + missionCoins);
            storage.SetInt(CoinsKey, after);
            Changed?.Invoke();
            return new CoinGain(before, after, round, knockouts, win, streak, missionCoins);
        }

        /// <summary>Development commands and tests edit the same key as real rounds.</summary>
        public void SetCoins(int coins)
        {
            storage.SetInt(CoinsKey, Math.Max(0, coins));
            Changed?.Invoke();
        }

        private IEnumerable<string> Owned()
        {
            var saved = storage.GetString(OwnedKey);
            if (string.IsNullOrEmpty(saved)) {
                yield break;
            }

            foreach (var id in saved.Split(',')) {
                if (id.Length > 0) {
                    yield return id;
                }
            }
        }
    }

    /// <summary>The topper the player's car wears in a round; the Game scene opened alone wears none.</summary>
    public interface IPlayerTopper
    {
        string SelectedTopper { get; }
    }

    /// <summary>A saved coin reward's before/after and its parts, as the result screen counts them up.</summary>
    public readonly struct CoinGain
    {
        public readonly int Before;
        public readonly int After;
        public readonly int RoundCoins;
        public readonly int KnockoutCoins;
        public readonly int WinCoins;
        public readonly int StreakCoins;
        public readonly int MissionCoins;

        public int Total => After - Before;

        public CoinGain(int before, int after, int roundCoins, int knockoutCoins, int winCoins, int streakCoins,
            int missionCoins)
        {
            this.Before = before;
            this.After = after;
            this.RoundCoins = roundCoins;
            this.KnockoutCoins = knockoutCoins;
            this.WinCoins = winCoins;
            this.StreakCoins = streakCoins;
            this.MissionCoins = missionCoins;
        }
    }
}
