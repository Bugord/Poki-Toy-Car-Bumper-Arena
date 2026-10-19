using System.Threading;
using Cysharp.Threading.Tasks;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The root state for now: loads the arena and leaves it running. The round (gameplayStart/Stop, the result screen,
    /// the commercial break before a restart) takes over here in step 3 of the brief.
    /// </summary>
    public sealed class ArenaState : StateBase
    {
        private readonly LevelScene levelScene;

        public ArenaState(LevelScene levelScene)
        {
            this.levelScene = levelScene;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            await levelScene.Load(token);

            // Ends only with the app, which cancels the token.
            await UniTask.WaitUntilCanceled(token);
            return Transition.GoToExit();
        }
    }
}
