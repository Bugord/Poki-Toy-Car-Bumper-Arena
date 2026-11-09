using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The end of a round: how it ended, the saved XP reward on the level track, HOME, CAR and NEXT MATCH. The result
    /// is the player's way on, the next match or the main menu, regardless of how much of the reward animation has
    /// played. Places, knock-outs and bests are still recorded and measured, just not shown: the screen keeps to the
    /// long-term goal.
    /// </summary>
    public sealed class RoundOverViewModel : ResultViewModel<ResultChoice>, IPayloadReceiver<RoundRecord>
    {
        /// <summary>How many rewards the track shows ahead of the level the round started at, the next rank aside.</summary>
        private const int TrackRewards = 3;

        // A rewarded ad back without its reward sooner than this never played (none to show, or blocked), as in SMUP.
        private const float AdPlayedSeconds = 2f;

        private readonly CarCatalog catalog;
        private readonly ArenaCatalog arenaCatalog;
        private readonly GameTuning tuning;
        private readonly PlayerProgress progress;
        private readonly IScreenNavigator navigator;
        private readonly IGamePlatform platform;
        private readonly PlayerWallet wallet;
        private readonly LevelScene levelScene;
        private GarageStage stage;
        private IReadOnlyList<int> colors;
        private IReadOnlyList<string> names;
        private IReadOnlyList<int> levels;
        private bool isDoubleTried;

        public RoundOverViewModel(CarCatalog catalog, ArenaCatalog arenaCatalog, GameTuning tuning,
            PlayerProgress progress, IScreenNavigator navigator, IGamePlatform platform, PlayerWallet wallet,
            LevelScene levelScene, PlayerDaily daily = null)
        {
            this.Daily = daily != null ? new DailyGift(daily, tuning, platform) : null;
            this.wallet = wallet;
            this.levelScene = levelScene;
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

        public int MissionCoins(GameTuning.MissionDefinition mission) => tuning.Missions.CoinsOf(mission.Tier);

        /// <summary>A mission's task in the language shown, its arena named.</summary>
        public string MissionText(GameTuning.MissionDefinition mission) =>
            GameText.Mission(mission, mission.Map.Length > 0 ? GameText.Arena(arenaCatalog.Find(mission.Map)) : null);
        public bool IsGarageOpen { get; private set; }
        public bool HasNewCar => XpGain.UnlockedCarIds.Count > 0 || BonusXp.UnlockedCarIds?.Count > 0;

        /// <summary>
        /// The round's own XP on its sticker: what CLAIM ×2 pays once more, the XP boost's share included. The missions'
        /// XP stays on their rows and is not doubled.
        /// </summary>
        public int RoundXp => XpGain.Total - XpGain.MissionXp;

        /// <summary>What the XP boost multiplies a round's own XP by: the stamp on a boosted round's XP sticker.</summary>
        public int BoostMultiplier => tuning.Ads.BoostMultiplier;

        /// <summary>The round's own coins on their sticker, the missions' left out, as <see cref="RoundXp"/>.</summary>
        public int RoundCoins => Coins.Total - Coins.MissionCoins;

        /// <summary>
        /// CLAIM ×2 is offered: once per result, while ads can play and the round gave something to double. An ad that
        /// comes back without its reward takes the offer away for this result.
        /// </summary>
        public bool CanDouble => tuning.Ads.DoubleReward && !platform.IsAdBlocked && !isDoubleTried &&
            (RoundXp > 0 || RoundCoins > 0);

        /// <summary>A rewarded ad (CLAIM ×2's or the daily gift's) is playing: the screen takes no presses meanwhile.</summary>
        public bool IsAdPlaying => isDoublePlaying || Daily is { IsAdPlaying: true };

        private bool isDoublePlaying;

        public bool IsDoubled { get; private set; }

        /// <summary>The XP CLAIM ×2 added, from the total after the round on; default before.</summary>
        public XpGain BonusXp { get; private set; }

        public CoinGain BonusCoins { get; private set; }

        public void DoubleShown() => platform.MeasureDoubleShown();

        /// <summary>
        /// CLAIM ×2: a rewarded ad, the game held still under it; with its reward, the round's own XP and coins once
        /// more, saved at once (<see cref="BonusXp"/>, <see cref="BonusCoins"/>). False when the ad gave nothing.
        /// </summary>
        public async UniTask<bool> Double(CancellationToken token)
        {
            if (!CanDouble || IsAdPlaying) {
                return false;
            }

            isDoubleTried = true;
            platform.MeasureDoublePressed();
            var started = Time.realtimeSinceStartup;
            bool isRewarded;
            isDoublePlaying = true;
            try {
                isRewarded = await AdPause.Run(() => platform.RewardedBreak(token));
            }
            finally {
                isDoublePlaying = false;
            }

            if (!isRewarded) {
                platform.MeasureDoubleEnded(Time.realtimeSinceStartup - started < AdPlayedSeconds
                    ? "unavailable" : "dismissed");
                return false;
            }

            platform.MeasureDoubleEnded("granted");
            BonusXp = progress.AddBonus(RoundXp);
            BonusCoins = wallet.AddBonus(RoundCoins);
            IsDoubled = true;
            // The levels and the cars the bonus opens count as any others; the arena rotation already moved on with
            // the round's level, so an arena the bonus opens joins it from the round after next.
            platform.MeasureProgress(BonusXp);
            platform.MeasureMapUnlocks(BonusXp, tuning);
            return true;
        }

        /// <summary>
        /// The coins buy a topper not owned yet or a car its level opened: the garage button carries its red dot for
        /// it too.
        /// </summary>
        public bool CanShop => GarageViewModel.CanBuyAnyTopper(wallet, tuning) ||
            GarageViewModel.CanBuyAnyCar(progress, wallet, catalog);

        /// <summary>The player's coins now: the round's, less what the screen has bought since.</summary>
        public int CoinsNow => wallet.Coins;

        /// <summary>A car was bought on this screen: the coins and the CAR button follow.</summary>
        public event Action CarBought;

        public bool IsForSale(CarCatalog.Entry car) => progress.IsForSale(car.Id);

        /// <summary>The car's speed, power and boost for the player, shown on the popup that opens it.</summary>
        public GameTuning.CarStatLine StatsOf(CarCatalog.Entry car) => tuning.CarStats.Of(car.Id);

        /// <summary>Whether cars boost in this build, so the boost stat means something.</summary>
        public bool HasBoost => tuning.Boost.IsEnabled;
        public int PriceOf(CarCatalog.Entry car) => progress.PriceOf(car.Id);
        public bool CanAfford(CarCatalog.Entry car) => wallet.Coins >= progress.PriceOf(car.Id);

        /// <summary>The level's popup offers a car for coins.</summary>
        public void CarOffered(CarCatalog.Entry car) => platform.MeasureCarOfferShown(car.Id);

        /// <summary>Buys a car from its level's popup and drives it next; false when the coins are short.</summary>
        public bool BuyCar(CarCatalog.Entry car)
        {
            if (!progress.Buy(car.Id, wallet)) {
                return false;
            }

            platform.MeasureCarOfferPressed(car.Id);
            platform.MeasureCarBought(car.Id);
            platform.MeasureGarageEquip(car.Id);
            CarBought?.Invoke();
            return true;
        }
        /// <summary>A free car's popup opened: its EQUIP and CONTINUE wait for the player.</summary>
        public void CarIntroduced(CarCatalog.Entry car) => platform.MeasureCarEquipShown(car.Id);

        /// <summary>EQUIP on a free car's popup: the car drives next.</summary>
        public bool EquipCar(CarCatalog.Entry car)
        {
            if (!progress.Select(car.Id)) {
                return false;
            }

            platform.MeasureCarEquipPressed(car.Id);
            platform.MeasureGarageEquip(car.Id);
            CarEquipped?.Invoke();
            return true;
        }

        /// <summary>A car was equipped from its popup: the CAR button follows.</summary>
        public event Action CarEquipped;

        /// <summary>The topper this round gave free (<see cref="PlayerWallet.TryGift"/>), or null.</summary>
        public string Gift { get; private set; }

        /// <summary>The gift's popup is up: the wallet gives it up and <c>gift / &lt;id&gt; / visible</c> goes.</summary>
        public void GiftShown()
        {
            wallet.TakeGift();
            platform.MeasureGiftShown(Gift);
        }

        /// <summary>The player's league now (<see cref="PlayerRank.TierOf"/>): its sky and pedestal frame the rewards.</summary>
        public int League => PlayerRank.TierOf(progress.Level);

        /// <summary>The player's level now, this round's XP in.</summary>
        public int PlayerLevel => progress.Level;

        /// <summary>
        /// Whether the result greets the player's league with NEW LEAGUE!: it is above the last one they saw
        /// (<see cref="PlayerProgress.SeenLeague"/>). Asking marks it seen and sends <c>result / league-&lt;id&gt; /
        /// visible</c>, so the menu does not greet it again; a result left before the popup leaves it to the menu.
        /// </summary>
        public bool TakeNewLeague()
        {
            var league = League;
            if (league <= 0 || league <= progress.SeenLeague) {
                return false;
            }

            progress.MarkLeagueSeen(league);
            platform.MeasureResultLeagueShown(LeagueLook.IdOf(league));
            return true;
        }

        /// <summary>The topper the player wears now, or empty.</summary>
        public string WornTopper => wallet.SelectedTopper;

        /// <summary>
        /// <paramref name="car"/> (null: the player's own) in the player's colour, wearing <paramref name="topper"/>, on
        /// the league's pedestal and turning, as the garage's hero shows it (<see cref="GarageStage.ShowCar"/>); null
        /// where there is no stage (tests). <see cref="HideHero"/> stops it.
        /// </summary>
        public RenderTexture ShowCarHero(CarCatalog.Entry car, string topper) =>
            Stage?.ShowCar(car ?? SelectedCar, progress.SelectedColor, topper, true, LeagueLook.PedestalOf(League));

        /// <summary>
        /// Whether a car this result opened gets its popup: every one does, each on a level of its own, so the player
        /// sees each new car the moment it is theirs to take or buy. Asking still marks the first free and the first
        /// paid car shown (<see cref="PlayerWallet.MarkIntroduced"/>).
        /// </summary>
        public bool IntroduceCar(CarCatalog.Entry car)
        {
            wallet.MarkIntroduced(IsForSale(car) ? "paid-car" : "free-car");
            return true;
        }

        /// <summary>
        /// The topper the result offers for coins once ever: the cheapest the coins pay for, to a player who already
        /// wears a hat (the free top hat of its level, or one from the garage), from
        /// <see cref="GameTuning.TopperSettings.HatOfferLevel"/>. Null when there is none to offer.
        /// </summary>
        public string HatOffer() =>
            !wallet.IsIntroduced("paid-hat") && wallet.HasAnyTopper
            && progress.Level >= tuning.Toppers.HatOfferLevel ? wallet.AffordableTopper() : null;

        public void HatOffered(string id)
        {
            wallet.MarkIntroduced("paid-hat");
            platform.MeasureHatOfferShown(id);
        }

        public int TopperPrice(string id) => tuning.Toppers.Find(id)?.Price ?? 0;

        /// <summary>Buys the offered topper and puts it on; false when the coins are short.</summary>
        public bool BuyHat(string id)
        {
            if (!wallet.Buy(id)) {
                return false;
            }

            platform.MeasureHatOfferPressed(id);
            platform.MeasureTopperBought(id);
            platform.MeasureTopperEquip(id);
            return true;
        }

        /// <summary>Stops the reward popups' 3D car (<see cref="ShowCarHero"/>).</summary>
        public void HideHero() => Stage?.Hide();

        /// <summary>
        /// Today's daily gift (<see cref="DailyGift"/>), which a visit's first result offers when no menu has (a first
        /// visit goes straight to its round); null where nothing keeps it (tests).
        /// </summary>
        public DailyGift Daily { get; }

        /// <summary>
        /// Whether this result opens the daily gift before anything else plays: the visit's offer not made yet, with a
        /// gift to claim, and the tuning asking for it on the result. Taking it marks the offer made.
        /// </summary>
        public bool TakeDailyOffer() => Daily != null && tuning.Daily.IsOnResult && Daily.IsEnabled && Daily.TakeOffer();

        /// <summary>The gift's popup is up over the result: <c>daily / offer / visible</c> and <c>offer-result</c>.</summary>
        public void DailyShown() => Daily?.Shown(true);

        /// <summary>The popup closed over the result with its gift still to claim: <c>result / daily-no / interact</c>.</summary>
        public void DailyDeclined() => platform.MeasureDailyResultDeclined();

        /// <summary>The garage's stage in the arena scene under the result (or the one a test gives); null without one.</summary>
        public GarageStage Stage
        {
            get => stage ?? (levelScene is { IsLoaded: true } ? levelScene.Resolve<GarageStage>() : null);
            set => stage = value;
        }

        /// <summary>Puts the gift on: the next round's car wears it.</summary>
        public bool WearGift()
        {
            if (Gift == null || !wallet.Wear(Gift)) {
                return false;
            }

            platform.MeasureGiftWorn(Gift);
            platform.MeasureTopperEquip(Gift);
            return true;
        }

        public CarCatalog.Entry SelectedCar => catalog.Find(progress.SelectedCar);
        public int SelectedColor => progress.SelectedColor;
        public int ColorOf(int index) => colors[index];

        /// <summary>
        /// The popup leaves this view attached and its reward animation running exactly once. The garage has no NEXT
        /// MATCH of its own: back returns here.
        /// </summary>
        public async UniTask OpenGarage(CancellationToken token)
        {
            if (IsGarageOpen) {
                return;
            }

            IsGarageOpen = true;
            try {
                platform.MeasureGaragePressed();
                progress.MarkGarageSeen();
                await navigator.RunAsync<GarageViewModel, bool>(token);
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
            var bonus = BonusXp.UnlockedCarIds ?? System.Array.Empty<string>();
            foreach (var id in XpGain.UnlockedCarIds.Concat(bonus)) {
                var car = catalog.Find(id);
                if (car != null && car.UnlockLevel == level) {
                    yield return car;
                }
            }
        }

        /// <summary>Arenas the levels this reward crossed opened at <paramref name="level"/>.</summary>
        public IEnumerable<ArenaCatalog.Entry> NewArenasAt(int level)
        {
            if (level <= XpGain.Before.Level || level > (IsDoubled ? BonusXp.After.Level : XpGain.After.Level)) {
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
            // The gift stays with the wallet until its popup shows (GiftShown): a result left before it carries it to the
            // next one. Once a hat is on, the popup's EQUIP has nothing to give.
            if (wallet.UnseenGift != null && !string.IsNullOrEmpty(wallet.SelectedTopper)) {
                wallet.TakeGift();
            }

            Gift = wallet.UnseenGift;
            Streak = payload.Streak;
            Outcome = result.Outcome;
            IsMatch = result.IsMatch;
            Place = result.Place;
            Knockouts = result.Knockouts;
            KnockedOutBy = result.KnockedOutBy;
            KnockedOutByName = payload.KnockedOutByName;
            RevengeTaken = result.RevengeTaken;
        }

        public void PlayAgain() => SetResult(ResultChoice.NextMatch);

        /// <summary>HOME: the main menu, behind the curtain.</summary>
        public void GoHome() => SetResult(ResultChoice.Home);
    }

    /// <summary>Where the result leads: the next match through the lobby, or the main menu.</summary>
    public enum ResultChoice
    {
        NextMatch,
        Home,
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
