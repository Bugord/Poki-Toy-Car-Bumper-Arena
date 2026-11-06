using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;

namespace ToyCarArena.App
{
    /// <summary>
    /// The result of the round just played, with the player's bests, over the arena as it ended, until the player picks
    /// NEXT MATCH or HOME. NEXT MATCH takes the lobby (<see cref="MatchLobby"/>): the next round's arena loads under the
    /// curtain, so the old one never shows reset, and the commercial break between rounds goes under the same curtain
    /// once the visit has played long enough (<see cref="AdBreaks"/>). HOME goes to the main menu (<see cref="MenuState"/>).
    /// </summary>
    public sealed class ResultState : StateBase<RoundRecord>
    {
        // Seconds the result waits for the car portraits still downloading (ContentBundles); they come well within a
        // first round, so this only shows on a very slow line.
        private const float PortraitWait = 2f;

        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;
        private readonly ISessionProgress progress;
        private readonly MissionTracker missions;
        private readonly ContentBundles content;
        private readonly MatchLobby lobby;

        public ResultState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            ISessionProgress progress, MissionTracker missions, ContentBundles content, MatchLobby lobby)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
            this.progress = progress;
            this.missions = missions;
            this.content = content;
            this.lobby = lobby;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            // The result shows a car turning on a podium (a match's winner, a round's player or the car that pushed
            // them off), drawn by the arena still loaded behind the screen.
            var record = Payload;
            if (record.Result.PodiumCar != RoundResult.Nobody) {
                record = record.WithPodium(levelScene.Resolve<WinnerPodium>().Show(record.Result.PodiumCar));
            }

            await content.PrepareCars(PortraitWait, token);
            var screen = await navigator.OpenAsync<RoundOverViewModel, RoundRecord>(record, token);
            // RoundState already moved the session on, so the round on this screen is the one before Round.
            var outcome = Payload.Result.Outcome;
            var playedRound = progress.Round - 1;
            // The played arena is still loaded, and with it the driver that knows whether the player found the boost.
            var firstAgain = playedRound <= 1 ? RoundEvents.FirstAgainOf(levelScene.Resolve<PlayerDriver>()) : null;
            ResultChoice choice;
            try {
                platform.MeasurePlayAgainShown(outcome, playedRound, Payload);
                platform.MeasureHomeShown();
                if (Payload.Missions != null && missions.MarkShown()) {
                    platform.MeasureMissionsShown();
                }

                if (firstAgain != null) {
                    platform.MeasureFirstAgainShown(firstAgain);
                }

                platform.MeasureGarageShown();
                if (record.Result.IsMatch) {
                    platform.MeasureScoreboardShown();
                }

                choice = await screen.ViewModel.WaitForResultAsync(token);
            }
            catch {
                await screen.CloseAsync();
                throw;
            }

            if (choice == ResultChoice.Home) {
                platform.MeasureHomePressed();
                return await GoHome(screen, token);
            }

            platform.MeasurePlayAgainPressed(outcome, playedRound, Payload);
            if (firstAgain != null) {
                platform.MeasureFirstAgainPressed(firstAgain);
            }

            await lobby.Run(() => screen.CloseAsync(), token);
            return Transition.GoTo<RoundState>();
        }

        /// <summary>
        /// HOME: the curtain comes down, the result closes and a fresh arena loads under it (the old one's bots would
        /// go on knocking each other off behind the menu), and the menu takes over. No commercial break here: PLAY in the
        /// menu passes the same gate as NEXT MATCH.
        /// </summary>
        private async UniTask<StateTransitionInfo> GoHome(ScreenHandle<RoundOverViewModel> screen, CancellationToken token)
        {
            try {
                await curtain.Show(token);
            }
            catch {
                await screen.CloseAsync();
                throw;
            }

            await UniTask.WhenAll(screen.CloseAsync(), levelScene.Load(token));
            return Transition.GoTo<MenuState>();
        }
    }
}
