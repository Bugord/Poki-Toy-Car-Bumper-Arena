using System;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Pickups;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// Plays one round on the loaded arena: places the cars, shows the HUD and lifts the curtain, counts the kick-off
    /// down and begins the round at GO, and waits for the round to be decided. The platform hears gameplayStart as the round begins and gameplayStop the moment it is decided, the
    /// player's fall included; the cars still on the arena go on for <see cref="GameTuning.RoundSettings.ResultDelay"/>
    /// (<see cref="GameTuning.RoundSettings.OutResultDelay"/> after a fall, which reads at once), so the last fall plays
    /// out before the result. The first round loads the arena itself; a restart comes here with a fresh one already
    /// loaded under the curtain.
    /// <para>
    /// The first round of a visit waits for the player: the cars stand on the arena under the HUD's start prompt until
    /// the player first steers or boosts (<see cref="PlayerDriver.HasActed"/>), so nothing plays before the player is
    /// there. A later round comes from PLAY AGAIN, which is that input, and goes on as soon as the curtain is up.
    /// </para>
    /// <para>
    /// Then every later round counts down 3, 2, 1 on the HUD (<see cref="GameTuning.RoundSettings.KickoffFrom"/>) and
    /// begins at GO; the first begins on that first input. Until then the cars stand still and the clock, the bots and
    /// the floor wait, so a round never starts in the middle of the curtain lifting or of the player reaching for the
    /// controls. The platform hears gameplayStart and the
    /// round's start at GO; the match's time runs from there.
    /// </para>
    /// <para>
    /// The decided round moves the session on (<see cref="SessionProgress"/>, before the next arena is built from it) and
    /// is held against the player's bests, and the round's analytics go out (<see cref="RoundEvents"/>).
    /// </para>
    /// </summary>
    public sealed class RoundState : StateBase
    {
        // How often a played round's missions are counted for MISSION!, in milliseconds: well under the sticker's own
        // spring, and nothing a frame would notice.
        private const int MissionWatchInterval = 200;

        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;
        private readonly SessionProgress progress;
        private readonly PersonalBests bests;
        private readonly PlayerProgress playerProgress;
        private readonly IArenaRotation arenaRotation;
        private readonly PlayerWallet wallet;
        private readonly MissionTracker missions;
        private readonly AdBreaks adBreaks;

        public RoundState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            SessionProgress progress, PersonalBests bests, PlayerProgress playerProgress, IArenaRotation arenaRotation,
            PlayerWallet wallet, MissionTracker missions, AdBreaks adBreaks)
        {
            this.adBreaks = adBreaks;
            this.wallet = wallet;
            this.missions = missions;
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
            this.progress = progress;
            this.bests = bests;
            this.playerProgress = playerProgress;
            this.arenaRotation = arenaRotation;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            if (!levelScene.IsLoaded) {
                await levelScene.Load(token);
            }

            var round = levelScene.Resolve<ArenaRound>();
            var tuning = levelScene.Resolve<GameTuning>();
            var driver = levelScene.Resolve<PlayerDriver>();
            var finished = new UniTaskCompletionSource<RoundResult>();
            void OnFinished(RoundResult result) => finished.TrySetResult(result);

            round.Finished += OnFinished;
            ScreenHandle<HudViewModel> hud = null;
            var isPlaying = false;
            var playStarted = 0f;
            var roundNumber = progress.Round;
            // Record() may auto-equip a new unlock; analytics must name the car that actually played this round.
            var playedCar = playerProgress.SelectedCar;
            // The arena the loaded scene was built for, before the rotation moves on to the next one.
            var arenaMap = levelScene.Resolve<ArenaMap>();
            var playedMap = arenaMap.Id;
            var playedVariant = arenaMap.Variant;
            // A match turns through the arena's variants with the floor's cycles; each one is measured on its own.
            var variants = levelScene.Resolve<ArenaVariants>();
            var currentVariant = playedVariant;
            var waitsForPlayer = roundNumber == 1;
            // The boxes the player breaks in a match, counted for the end, and its items, each reported as its roll lands
            // and as it is used.
            var pickups = levelScene.Resolve<ArenaPickups>();
            var pickupCount = 0;
            Car pickupCar = null;
            void OnCollected(Car car, ItemKind kind, Vector3 position)
            {
                if (car == round.Player) {
                    pickupCount++;
                }
            }

            // Knock-outs of cars the player's item hit last, for the item missions.
            var itemKnockouts = 0;
            void OnKnockedOut(Car car, Car by)
            {
                if (by == round.Player && car.LastHitByItem) {
                    itemKnockouts++;
                }
            }

            void OnItemLanded(ItemKind kind) => platform.MeasureItemGet(roundNumber, kind, round.IsMatch);
            void OnItemUsed(ItemKind kind) => platform.MeasureItemUse(roundNumber, kind, round.IsMatch);

            void OnVariantChanged(string variant)
            {
                if (isPlaying) {
                    platform.MeasureVariantChange(playedMap, currentVariant, variant);
                }

                currentVariant = variant;
            }

            // Missions the round completes while it is played (knock-outs, boosts, revenge, items, boxes) pop MISSION!
            // on the HUD at once; the ones only a decided round counts show on the result.
            MissionStats LiveStats() => new(false, RoundOutcome.Survived, 0, round.PlayerKnockouts, driver.Boosts,
                round.RevengeTaken, driver.ItemsUsed, pickupCount, itemKnockouts, playedMap);

            async UniTaskVoid WatchMissions(HudViewModel hudModel)
            {
                var shown = missions.CountDone(LiveStats());
                while (!round.IsFinished) {
                    if (await UniTask.Delay(MissionWatchInterval, true, cancellationToken: token)
                            .SuppressCancellationThrow()) {
                        return;
                    }

                    var done = missions.CountDone(LiveStats());
                    if (done > shown && !round.IsFinished) {
                        shown = done;
                        hudModel.ShowMissionDone();
                    }
                }
            }

            void BeginRound()
            {
                round.Begin();
                platform.GameplayStart();
                // A build of long matches reports its own progress events, so the rounds of the versions before stay
                // comparable.
                if (round.IsMatch) {
                    platform.MeasureMatchStart(roundNumber);
                }
                else {
                    platform.MeasureRoundStart(roundNumber);
                }

                platform.MeasureVariantStart(playedMap, playedVariant);
                arenaRotation.MarkSeen(playedMap);
                arenaRotation.MarkPlayed(playedMap, playedVariant);
                isPlaying = true;
                playStarted = Time.realtimeSinceStartup;
            }

            variants.VariantChanged += OnVariantChanged;
            round.KnockedOut += OnKnockedOut;
            try {
                round.Place();
                pickupCar = round.Player;
                pickupCar.ItemLanded += OnItemLanded;
                pickupCar.ItemUsed += OnItemUsed;
                pickups.Collected += OnCollected;

                var hudRound = new HudRound(round, driver, tuning.Controls, waitsForPlayer, roundNumber,
                    playerProgress.TotalXp == 0,
                    progress.WinStreak >= tuning.Streaks.Start ? progress.WinStreak : 0);
                hud = await navigator.OpenAsync<HudViewModel, HudRound>(hudRound, token);
                await curtain.Hide();
                // The page's loading screen stays up until the first round can be seen, past Poki's own loader.
                LoadingScreen.Hide();

                // The visit's first round begins on the player's first input, with no count: the new player came to
                // drive, not to wait. Later rounds count down after the curtain.
                if (waitsForPlayer) {
                    platform.MeasureStartShown(Time.realtimeSinceStartup);
                    await UniTask.WaitUntil(() => driver.HasActed, cancellationToken: token);
                    platform.MeasureStartPressed();
                }
                else {
                    await Kickoff(hud.ViewModel, tuning.Round, token);
                }

                BeginRound();
                WatchMissions(hud.ViewModel).Forget();

                var result = await finished.Task.AttachExternalCancellation(token);
                platform.GameplayStop();
                isPlaying = false;
                // Only the rounds count towards the commercial break between them: not the start screen or the kickoff.
                adBreaks.AddPlay(Time.realtimeSinceStartup - playStarted);

                // A streak round's reward is multiplied by the streak it was played on, before this round moves it.
                var streaks = tuning.Streaks;
                var streakBefore = progress.WinStreak;
                var isStreakRound = streakBefore >= streaks.Start;
                progress.Record(result, streaks.Start);
                // Missions count first, so their XP lands in the same saved reward and fills the same bar.
                var missionRound = missions.Record(new MissionStats(true, result.Outcome, result.Place,
                        result.Knockouts, driver.Boosts, result.RevengeTaken, driver.ItemsUsed, pickupCount,
                        itemKnockouts, playedMap), playerProgress.Level, progress.Round);
                var xpGain = playerProgress.Record(result, isStreakRound ? streaks : null, missionRound.Xp);
                var coinGain = wallet.Record(result, isStreakRound, missionRound.Coins);
                // The free topper of its level: the result offers to wear it.
                wallet.TryGift(xpGain.After.Level);
                var record = bests.Record(result).WithXp(xpGain).WithKnocker(
                    result.KnockedOutBy == RoundResult.Nobody ? "" : round.NameOf(result.KnockedOutBy)).WithRoster(
                    round.Names, round.Levels, IndexOf(round, round.Nemesis)).WithMeta(coinGain, missionRound,
                    new StreakChange(streakBefore, progress.WinStreak, streaks.Start));
                arenaRotation.Next(record.XpGain.After.Level);
                levelScene.Resolve<ISessionRoster>().NextRound(result.CarCount - 1,
                    RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis, tuning.Players);
                platform.MeasureRoundEnd(roundNumber, result, tuning.Round.Duration, driver, tuning.Boost.IsEnabled);
                if (pickups.IsEnabled) {
                    platform.MeasureMatchPickups(roundNumber, pickupCount, result.IsMatch);
                }

                // The variant playing as it ended: the rotation's own in a round, the last cycle's in a match.
                platform.MeasureVariantEnd(playedMap, currentVariant, result.Outcome);
                platform.MeasureRoundCar(roundNumber, playedCar);
                platform.MeasureProgress(record.XpGain);
                platform.MeasureRoundMap(roundNumber, playedMap);
                platform.MeasureRoundVariant(roundNumber, playedMap, playedVariant);
                platform.MeasureMapUnlocks(record.XpGain, tuning);
                platform.MeasureMeta(roundNumber, record, missions.DoneThisVisit);

                var delay = result.IsMatch ? tuning.Match.ResultDelay :
                    result.Outcome == RoundOutcome.Out ? tuning.Round.OutResultDelay : tuning.Round.ResultDelay;
                // Real time: the buzzer's hit-stop at the end of a match must not stretch the wait.
                await UniTask.Delay(TimeSpan.FromSeconds(delay), true, cancellationToken: token);
                return Transition.GoTo<ResultState, RoundRecord>(record);
            }
            finally {
                round.Finished -= OnFinished;
                round.KnockedOut -= OnKnockedOut;
                variants.VariantChanged -= OnVariantChanged;
                pickups.Collected -= OnCollected;
                if (pickupCar != null) {
                    pickupCar.ItemLanded -= OnItemLanded;
                    pickupCar.ItemUsed -= OnItemUsed;
                }

                // Cancelled with the app, or failed: the platform must not think the game still plays.
                if (isPlaying) {
                    platform.GameplayStop();
                }

                if (hud != null) {
                    await hud.CloseAsync();
                }
            }
        }

        /// <summary>
        /// Counts 3, 2, 1 on the HUD, a step at a time on unscaled time, and puts up GO! as it returns: the round begins
        /// under GO!, which the HUD takes away on its own.
        /// </summary>
        private static async UniTask Kickoff(HudViewModel hud, GameTuning.RoundSettings settings, CancellationToken token)
        {
            if (settings.KickoffFrom <= 0) {
                return;
            }

            for (var step = settings.KickoffFrom; step > 0; step--) {
                hud.ShowKickoff(step, settings.KickoffStep);
                await UniTask.Delay(TimeSpan.FromSeconds(settings.KickoffStep), true, cancellationToken: token);
            }

            hud.ShowKickoff(0, settings.KickoffGo);
        }

        // The car's index in the round, as the result names cars; Nobody for none.
        private static int IndexOf(ArenaRound round, Car car)
        {
            for (var i = 0; car != null && i < round.Cars.Count; i++) {
                if (round.Cars[i] == car) {
                    return i;
                }
            }

            return RoundResult.Nobody;
        }
    }
}
