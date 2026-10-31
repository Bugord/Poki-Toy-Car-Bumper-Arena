using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
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
        private readonly ISessionRoster roster;
        private readonly ISessionProgress progress;
        private readonly GameTuning tuning;
        private readonly IArenaRotation arenaRotation;
        private readonly ArenaCatalog arenaCatalog;

        public ResultState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            ISessionRoster roster, ISessionProgress progress, GameTuning tuning, IArenaRotation arenaRotation,
            ArenaCatalog arenaCatalog)
        {
            this.arenaRotation = arenaRotation;
            this.arenaCatalog = arenaCatalog;
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
            this.roster = roster;
            this.progress = progress;
            this.tuning = tuning;
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
                // RoundState already moved the rotation on, so Current is the arena of the round this lobby leads to.
                var arena = arenaCatalog.Find(arenaRotation.Current);
                curtain.ShowLobby(roster, RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis,
                    GameText.Arena(arena), arena != null && arenaRotation.IsNew(arena.Id));
                platform.MeasureLobbyShown();
            }
            catch {
                await screen.CloseAsync();
                throw;
            }

            // Under the curtain the screen's fade-out shows nowhere, so the next arena loads meanwhile: the brief wants
            // a lost round back in play within about 3 seconds.
            await UniTask.WhenAll(screen.CloseAsync(), levelScene.Load(token));
            await curtain.Ready(token);
            return Transition.GoTo<RoundState>();
        }
    }
}
