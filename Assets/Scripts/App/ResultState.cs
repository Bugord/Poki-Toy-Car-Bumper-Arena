using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The result of the round just played, over the arena as it ended, until the player picks PLAY AGAIN. The next
    /// round's arena then loads under the curtain, so the old one never shows reset. The commercial break between
    /// rounds comes in step 7 of the brief, under the same curtain.
    /// </summary>
    public sealed class ResultState : StateBase<RoundResult>
    {
        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;

        public ResultState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            var screen = await navigator.OpenAsync<RoundOverViewModel, RoundResult>(Payload, token);
            try {
                await screen.ViewModel.WaitForResultAsync(token);
                await curtain.Show(token);
            }
            finally {
                await screen.CloseAsync();
            }

            await levelScene.Load(token);
            return Transition.GoTo<RoundState>();
        }
    }
}
