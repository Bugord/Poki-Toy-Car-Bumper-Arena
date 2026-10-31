using System;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// Plays one round on the loaded arena: begins it, shows the HUD and lifts the curtain, and waits for the round to
    /// be decided. The platform hears gameplayStart as the round begins and gameplayStop the moment it is decided, the
    /// player's fall included; the cars still on the arena go on for <see cref="GameTuning.RoundSettings.ResultDelay"/>
    /// (<see cref="GameTuning.RoundSettings.OutResultDelay"/> after a fall, which reads at once), so the last fall plays
    /// out before the result. The first round loads the arena itself; a restart comes here with a fresh one already
    /// loaded under the curtain.
    /// <para>
    /// The first round of a visit waits for the player: the cars stand on the arena under the HUD's start prompt until
    /// the player first steers or boosts (<see cref="PlayerDriver.HasActed"/>), and only then does the round begin and
    /// the platform hear gameplayStart, so nothing plays before the player is there. A later round comes from PLAY
    /// AGAIN, which is that input, and begins at once.
    /// </para>
    /// <para>
    /// The decided round moves the session on (<see cref="SessionProgress"/>, before the next arena is built from it) and
    /// is held against the player's bests, and the round's analytics go out (<see cref="RoundEvents"/>).
    /// </para>
    /// </summary>
    public sealed class RoundState : StateBase
    {
        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;
        private readonly SessionProgress progress;
        private readonly PersonalBests bests;
        private readonly PlayerProgress playerProgress;
        private readonly IArenaRotation arenaRotation;

        public RoundState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            SessionProgress progress, PersonalBests bests, PlayerProgress playerProgress, IArenaRotation arenaRotation)
        {
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
            var roundNumber = progress.Round;
            // Record() may auto-equip a new unlock; analytics must name the car that actually played this round.
            var playedCar = playerProgress.SelectedCar;
            // The arena the loaded scene was built for, before the rotation moves on to the next one.
            var arenaMap = levelScene.Resolve<ArenaMap>();
            var playedMap = arenaMap.Id;
            var playedVariant = arenaMap.Variant;
            var waitsForPlayer = roundNumber == 1;

            void BeginRound()
            {
                round.Begin();
                platform.GameplayStart();
                platform.MeasureRoundStart(roundNumber);
                arenaRotation.MarkSeen(playedMap);
                arenaRotation.MarkPlayed(playedMap, playedVariant);
                isPlaying = true;
            }

            try {
                round.Place();
                if (!waitsForPlayer) {
                    BeginRound();
                }

                var hudRound = new HudRound(round, driver, tuning.Controls, waitsForPlayer, roundNumber,
                    playerProgress.TotalXp == 0);
                hud = await navigator.OpenAsync<HudViewModel, HudRound>(hudRound, token);
                await curtain.Hide();

                if (waitsForPlayer) {
                    await UniTask.WaitUntil(() => driver.HasActed, cancellationToken: token);
                    BeginRound();
                }

                var result = await finished.Task.AttachExternalCancellation(token);
                platform.GameplayStop();
                isPlaying = false;

                progress.Record(result);
                var record = bests.Record(result).WithXp(playerProgress.Record(result)).WithKnocker(
                    result.KnockedOutBy == RoundResult.Nobody ? "" : round.NameOf(result.KnockedOutBy)).WithNames(round.Names);
                arenaRotation.Next(record.XpGain.After.Level);
                levelScene.Resolve<ISessionRoster>().NextRound(result.CarCount - 1,
                    RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis, tuning.Players);
                platform.MeasureRoundEnd(roundNumber, result, tuning.Round.Duration, driver);
                platform.MeasureRoundCar(roundNumber, playedCar);
                platform.MeasureProgress(record.XpGain);
                platform.MeasureRoundMap(roundNumber, playedMap);
                platform.MeasureRoundVariant(roundNumber, playedMap, playedVariant);
                platform.MeasureMapUnlocks(record.XpGain, tuning);

                var delay = result.Outcome == RoundOutcome.Out ? tuning.Round.OutResultDelay : tuning.Round.ResultDelay;
                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                return Transition.GoTo<ResultState, RoundRecord>(record);
            }
            finally {
                round.Finished -= OnFinished;
                // Cancelled with the app, or failed: the platform must not think the game still plays.
                if (isPlaying) {
                    platform.GameplayStop();
                }

                if (hud != null) {
                    await hud.CloseAsync();
                }
            }
        }
    }
}
