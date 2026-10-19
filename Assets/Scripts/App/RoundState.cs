using System;
using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// Plays one round on the loaded arena: begins it, shows the HUD and lifts the curtain, and waits for the round to
    /// be decided. The platform hears gameplayStart as the round begins and gameplayStop the moment it is decided, the
    /// player's fall included; the cars still on the arena go on for <see cref="GameTuning.RoundSettings.ResultDelay"/>,
    /// so the last fall plays out before the result. The first round loads the arena itself; a restart comes here with
    /// a fresh one already loaded under the curtain.
    /// </summary>
    public sealed class RoundState : StateBase
    {
        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;

        public RoundState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
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
            try {
                round.Begin();
                platform.GameplayStart();
                isPlaying = true;

                hud = await navigator.OpenAsync<HudViewModel, ArenaRound>(round, token);
                await curtain.Hide();

                var result = await finished.Task.AttachExternalCancellation(token);
                platform.GameplayStop();
                isPlaying = false;

                await UniTask.Delay(TimeSpan.FromSeconds(tuning.Round.ResultDelay), cancellationToken: token);
                return Transition.GoTo<ResultState, RoundResult>(result);
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
