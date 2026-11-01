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
            // RoundState already moved the session on, so the round on this screen is the one before Round.
            var outcome = Payload.Result.Outcome;
            var playedRound = progress.Round - 1;
            var mapId = arenaRotation.Current;
            // The played arena is still loaded, and with it the driver that knows whether the player found the boost.
            var firstAgain = playedRound <= 1 ? RoundEvents.FirstAgainOf(levelScene.Resolve<PlayerDriver>()) : null;
            try {
                platform.MeasurePlayAgainShown(outcome, playedRound);
                if (firstAgain != null) {
                    platform.MeasureFirstAgainShown(firstAgain);
                }

                platform.MeasureGarageShown();
                await screen.ViewModel.WaitForResultAsync(token);
                platform.MeasurePlayAgainPressed(outcome, playedRound);
                if (firstAgain != null) {
                    platform.MeasureFirstAgainPressed(firstAgain);
                }

                await curtain.Show(token);
                // RoundState already moved the rotation on, so Current is the arena of the round this lobby leads to.
                var arena = arenaCatalog.Find(mapId);
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
            // A load still running past the lobby is told apart, so a visit lost here reads as a slow arena or a wait.
            var loading = UniTask.WhenAll(screen.CloseAsync(), levelScene.Load(token)).Preserve();
            var slowAfter = TimeSpan.FromSeconds(RoundEvents.SlowLobbySeconds);
            var isSlow = await UniTask.WhenAny(loading,
                UniTask.Delay(slowAfter, DelayType.Realtime, cancellationToken: token)) == 1;
            if (isSlow) {
                platform.MeasureLobbySlow(mapId);
            }

            await loading;
            await curtain.Ready(token);
            if (isSlow) {
                platform.MeasureLobbySlowDone(mapId);
            }

            return Transition.GoTo<RoundState>();
        }
    }
}
