using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The result of the round just played, with the player's bests, over the arena as it ended, until the player picks
    /// PLAY AGAIN. The next round's arena then loads under the curtain, so the old one never shows reset. The commercial
    /// break between rounds would go under the same curtain; it is left out for now, so the first tests measure the
    /// game without ads.
    /// </summary>
    public sealed class ResultState : StateBase<RoundRecord>
    {
        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;

        public ResultState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            var screen = await navigator.OpenAsync<RoundOverViewModel, RoundRecord>(Payload, token);
            try {
                platform.MeasurePlayAgainShown();
                platform.MeasureGarageShown();
                await screen.ViewModel.WaitForResultAsync(token);
                platform.MeasurePlayAgainPressed();
                await curtain.Show(token);
            }
            catch {
                await screen.CloseAsync();
                throw;
            }

            // Under the curtain the screen's fade-out shows nowhere, so the next arena loads meanwhile: the brief wants
            // a lost round back in play within about 3 seconds.
            await UniTask.WhenAll(screen.CloseAsync(), levelScene.Load(token));
            return Transition.GoTo<RoundState>();
        }
    }
}
