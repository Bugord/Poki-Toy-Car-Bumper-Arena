using System;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
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

        public RoundState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            SessionProgress progress, PersonalBests bests)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
            this.progress = progress;
            this.bests = bests;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            if (!levelScene.IsLoaded) {
                await levelScene.Load(token);
            }

            var round = levelScene.Resolve<ArenaRound>();
            var tuning = levelScene.Resolve<GameTuning>();
            var finished = new UniTaskCompletionSource<RoundResult>();
            void OnFinished(RoundResult result) => finished.TrySetResult(result);

            round.Finished += OnFinished;
            ScreenHandle<HudViewModel> hud = null;
            var isPlaying = false;
            var roundNumber = progress.Round;
            try {
                round.Begin();
                platform.GameplayStart();
                platform.MeasureRoundStart(roundNumber);
                isPlaying = true;

                hud = await navigator.OpenAsync<HudViewModel, ArenaRound>(round, token);
                await curtain.Hide();

                var result = await finished.Task.AttachExternalCancellation(token);
                platform.GameplayStop();
                isPlaying = false;

                progress.Record(result);
                var record = bests.Record(result);
                platform.MeasureRoundEnd(roundNumber, result, tuning.Round.Duration, levelScene.Resolve<PlayerDriver>());

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
