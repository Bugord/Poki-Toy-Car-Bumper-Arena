using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.App;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;
using UnityEngine;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a round: how it ended, the saved XP reward on the level track, CAR and NEXT MATCH. The result is
    /// true once the player asks for another round, regardless of how much of the reward animation has played. Places,
    /// knock-outs and bests are still recorded and measured, just not shown: the screen keeps to the long-term goal.
    /// </summary>
    public sealed class RoundOverViewModel : ResultViewModel<bool>, IPayloadReceiver<RoundRecord>
    {
        /// <summary>How many rewards the track shows ahead of the level the round started at, the next rank aside.</summary>
        private const int TrackRewards = 3;

        private readonly CarCatalog catalog;
        private readonly ArenaCatalog arenaCatalog;
        private readonly GameTuning tuning;
        private readonly PlayerProgress progress;
        private readonly IScreenNavigator navigator;
        private readonly IGamePlatform platform;
        private readonly PlayerWallet wallet;
        private IReadOnlyList<int> colors;
        private IReadOnlyList<string> names;
        private IReadOnlyList<int> levels;

        public RoundOverViewModel(CarCatalog catalog, ArenaCatalog arenaCatalog, GameTuning tuning,
            PlayerProgress progress, IScreenNavigator navigator, IGamePlatform platform, PlayerWallet wallet)
        {
            this.wallet = wallet;
            this.catalog = catalog;
            this.arenaCatalog = arenaCatalog;
            this.tuning = tuning;
            this.progress = progress;
            this.navigator = navigator;
            this.platform = platform;
        }

        public XpGain XpGain { get; private set; }

        /// <summary>The saved coin reward: the result counts the player's coins up from before to after.</summary>
        public CoinGain Coins { get; private set; }

        /// <summary>What the round did for the missions; null where nothing tracks them.</summary>
        public MissionRound Missions { get; private set; }

        public StreakChange Streak { get; private set; }

        public int MissionXp(GameTuning.MissionDefinition mission) => tuning.Missions.XpOf(mission.Tier);

        /// <summary>A mission's task in the language shown, its arena named.</summary>
        public string MissionText(GameTuning.MissionDefinition mission) =>
            GameText.Mission(mission, mission.Map.Length > 0 ? GameText.Arena(arenaCatalog.Find(mission.Map)) : null);
        public bool IsGarageOpen { get; private set; }
        public bool HasNewCar => XpGain.UnlockedCarIds.Count > 0;

        /// <summary>The coins buy a topper not owned yet: the garage button carries its red dot for it too.</summary>
        public bool CanBuyTopper => GarageViewModel.CanBuyAnyTopper(wallet, tuning);
        public CarCatalog.Entry SelectedCar => catalog.Find(progress.SelectedCar);
        public int SelectedColor => progress.SelectedColor;
        public int ColorOf(int index) => colors[index];

        /// <summary>
        /// The popup leaves this view attached and its reward animation running exactly once. True when the player
        /// asked for the next match from the garage.
        /// </summary>
        public async UniTask<bool> OpenGarage(CancellationToken token)
        {
            if (IsGarageOpen) {
                return false;
            }

            IsGarageOpen = true;
            try {
                platform.MeasureGaragePressed();
                return await navigator.RunAsync<GarageViewModel, bool>(token);
            }
            finally {
                IsGarageOpen = false;
            }
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

        /// <summary>Only cars earned by this reward appear, even if storage is edited while the screen is open.</summary>
        public IEnumerable<CarCatalog.Entry> NewCarsAt(int level)
        {
            foreach (var id in XpGain.UnlockedCarIds) {
                var car = catalog.Find(id);
                if (car != null && car.UnlockLevel == level) {
                    yield return car;
                }
            }
        }

        /// <summary>Arenas the levels this reward crossed opened at <paramref name="level"/>.</summary>
        public IEnumerable<ArenaCatalog.Entry> NewArenasAt(int level)
        {
            if (level <= XpGain.Before.Level || level > XpGain.After.Level) {
                yield break;
            }

            foreach (var map in tuning.Maps) {
                var arena = arenaCatalog.Find(map.Id);
                if (map.UnlockLevel == level && arena != null) {
                    yield return arena;
                }
            }
        }

        /// <summary>
        /// The stops of the level track after the level the round started at: the next levels that open a car or an
        /// arena (a car wins a shared level, it is what the player drives), then the next rank. Read from the catalogs,
        /// so a new car or arena shows up without touching the screen.
        /// </summary>
        public IReadOnlyList<TrackStop> TrackStops()
        {
            var from = XpGain.Before.Level;
            var rewards = new SortedDictionary<int, TrackStop>();
            foreach (var car in catalog.Cars) {
                if (car.UnlockLevel > from && !rewards.ContainsKey(car.UnlockLevel)) {
                    rewards[car.UnlockLevel] = new TrackStop(car.UnlockLevel, GameText.Car(car), car, null, false);
                }
            }
            foreach (var map in tuning.Maps) {
                var arena = arenaCatalog.Find(map.Id);
                if (map.UnlockLevel > from && arena != null && !rewards.ContainsKey(map.UnlockLevel)) {
                    rewards[map.UnlockLevel] = new TrackStop(map.UnlockLevel, GameText.Arena(arena), null, arena, false);
                }
            }

            var stops = new List<TrackStop>();
            var rank = PlayerRank.NextRankLevel(from);
            foreach (var reward in rewards.Values) {
                if (stops.Count == TrackRewards || (rank > 0 && reward.Level >= rank)) {
                    break;
                }
                stops.Add(reward);
            }
            // Once the rewards run out, plain levels keep the track full, the next ones first, so the round's XP
            // still fills a bar that moves.
            for (var level = from + 1; stops.Count < TrackRewards && (rank == 0 || level < rank); level++) {
                if (!rewards.ContainsKey(level)) {
                    stops.Add(new TrackStop(level, null, null, null, false));
                }
            }
            stops.Sort((a, b) => a.Level.CompareTo(b.Level));
            if (rank > 0) {
                stops.Add(new TrackStop(rank, Strings.Format("result.rank_stop", PlayerRank.NextRankName(from)), null, null, true));
            }
            return stops;
        }

        public string KnockedOutByName { get; private set; }

        public bool RevengeTaken { get; private set; }

        public RoundOutcome Outcome { get; private set; }

        /// <summary>
        /// Whether the result is a long match's: the subtitle gives its place and knock-outs.
        /// </summary>
        public bool IsMatch { get; private set; }

        public int Place { get; private set; }

        public int Knockouts { get; private set; }

        /// <summary>
        /// The index of the car that knocked the player out, or <see cref="RoundResult.Nobody"/>.
        /// </summary>
        public int KnockedOutBy { get; private set; }

        /// <summary>A match's cars in place order, for the results table; empty after a round of the last car standing.</summary>
        public IReadOnlyList<RoundStanding> Standings { get; private set; }

        /// <summary>The REVENGE target as the match ended, or <see cref="RoundResult.Nobody"/>.</summary>
        public int Nemesis { get; private set; }

        /// <summary>The car turning on its podium; null when there is none to show.</summary>
        public Texture Podium { get; private set; }

        /// <summary>The index of the car on the podium (<see cref="RoundResult.PodiumCar"/>).</summary>
        public int PodiumCar { get; private set; }

        /// <summary>Whether the podium crowns a winner: a match's first place, or the player's own win of a round.</summary>
        public bool IsPodiumWinner => IsMatch || Outcome == RoundOutcome.Won;

        /// <summary>Whether the podium shows the car that knocked the player off a round: the one to get back at.</summary>
        public bool IsPodiumRival => !IsMatch && Outcome == RoundOutcome.Out && KnockedOutBy != RoundResult.Nobody;

        public string NameOf(int index) =>
            index == 0 ? Strings.Get("common.you") : index < names.Count ? names[index] : string.Empty;

        /// <summary>The player's level is the one this result reached; the others as the round showed them.</summary>
        public int LevelOf(int index) =>
            index == 0 && XpGain.After.Level > 0 ? XpGain.After.Level : index < levels.Count ? levels[index] : 1;

        public void SetPayload(RoundRecord payload)
        {
            var result = payload.Result;
            colors = result.Colors;
            names = payload.Names;
            levels = payload.Levels;
            Nemesis = payload.Nemesis;
            Podium = payload.Podium;
            PodiumCar = result.PodiumCar;
            Standings = result.IsMatch ? result.Standings : System.Array.Empty<RoundStanding>();
            XpGain = payload.XpGain;
            Coins = payload.Coins;
            Missions = payload.Missions;
            Streak = payload.Streak;
            Outcome = result.Outcome;
            IsMatch = result.IsMatch;
            Place = result.Place;
            Knockouts = result.Knockouts;
            KnockedOutBy = result.KnockedOutBy;
            KnockedOutByName = payload.KnockedOutByName;
            RevengeTaken = result.RevengeTaken;
        }

        public void PlayAgain() => SetResult(true);
    }

    /// <summary>
    /// A stop on the level track: the level and what it opens, one of a car, an arena or a rank, or nothing (a plain
    /// level, with no name).
    /// </summary>
    public sealed class TrackStop
    {
        public TrackStop(int level, string name, CarCatalog.Entry car, ArenaCatalog.Entry arena, bool isRank)
        {
            Level = level;
            Name = name;
            Car = car;
            Arena = arena;
            IsRank = isRank;
        }

        public int Level { get; }
        public string Name { get; }
        public CarCatalog.Entry Car { get; }
        public ArenaCatalog.Entry Arena { get; }
        public bool IsRank { get; }
    }
}
