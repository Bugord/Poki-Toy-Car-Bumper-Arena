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
        private IReadOnlyList<int> colors;

        public RoundOverViewModel(CarCatalog catalog, ArenaCatalog arenaCatalog, GameTuning tuning,
            PlayerProgress progress, IScreenNavigator navigator, IGamePlatform platform)
        {
            this.catalog = catalog;
            this.arenaCatalog = arenaCatalog;
            this.tuning = tuning;
            this.progress = progress;
            this.navigator = navigator;
            this.platform = platform;
        }

        public XpGain XpGain { get; private set; }
        public bool IsGarageOpen { get; private set; }
        public bool HasNewCar => XpGain.UnlockedCarIds.Count > 0;
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
                    rewards[car.UnlockLevel] = new TrackStop(car.UnlockLevel, car.DisplayName, car, null, false);
                }
            }
            foreach (var map in tuning.Maps) {
                var arena = arenaCatalog.Find(map.Id);
                if (map.UnlockLevel > from && arena != null && !rewards.ContainsKey(map.UnlockLevel)) {
                    rewards[map.UnlockLevel] = new TrackStop(map.UnlockLevel, arena.DisplayName, null, arena, false);
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
            if (rank > 0) {
                stops.Add(new TrackStop(rank, PlayerRank.NextRankName(from) + " RANK", null, null, true));
            }
            return stops;
        }

        public string KnockedOutByName { get; private set; }

        public bool RevengeTaken { get; private set; }

        public RoundOutcome Outcome { get; private set; }

        /// <summary>
        /// The index of the car that knocked the player out, or <see cref="RoundResult.Nobody"/>.
        /// </summary>
        public int KnockedOutBy { get; private set; }

        public void SetPayload(RoundRecord payload)
        {
            var result = payload.Result;
            colors = result.Colors;
            XpGain = payload.XpGain;
            Outcome = result.Outcome;
            KnockedOutBy = result.KnockedOutBy;
            KnockedOutByName = payload.KnockedOutByName;
            RevengeTaken = result.RevengeTaken;
        }

        public void PlayAgain() => SetResult(true);
    }

    /// <summary>A stop on the level track: the level and what it opens, one of a car, an arena or a rank.</summary>
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
