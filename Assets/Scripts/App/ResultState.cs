using System;
using System.Threading;
using Bugord.App.Localization;
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
    /// break between rounds goes under the same curtain, before the lobby, once the visit has played long enough
    /// (<see cref="AdBreaks"/>).
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
        private readonly ISessionRoster roster;
        private readonly ISessionProgress progress;
        private readonly GameTuning tuning;
        private readonly IArenaRotation arenaRotation;
        private readonly ArenaCatalog arenaCatalog;
        private readonly MissionTracker missions;
        private readonly AdBreaks adBreaks;
        private readonly ContentBundles content;

        public ResultState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            ISessionRoster roster, ISessionProgress progress, GameTuning tuning, IArenaRotation arenaRotation,
            ArenaCatalog arenaCatalog, MissionTracker missions, AdBreaks adBreaks, ContentBundles content)
        {
            this.content = content;
            this.adBreaks = adBreaks;
            this.missions = missions;
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
            var mapId = arenaRotation.Current;
            // The played arena is still loaded, and with it the driver that knows whether the player found the boost.
            var firstAgain = playedRound <= 1 ? RoundEvents.FirstAgainOf(levelScene.Resolve<PlayerDriver>()) : null;
            try {
                platform.MeasurePlayAgainShown(outcome, playedRound, Payload);
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

                await screen.ViewModel.WaitForResultAsync(token);
                platform.MeasurePlayAgainPressed(outcome, playedRound, Payload);
                if (firstAgain != null) {
                    platform.MeasureFirstAgainPressed(firstAgain);
                }

                await curtain.Show(token);
                // Before the lobby and its clocks: it neither runs out under the ad nor reports a slow arena, and no
                // event goes out while the ad plays.
                if (adBreaks.IsDue(tuning.Ads.FreePlaySeconds)) {
                    platform.MeasureCommercialBreak();
                    await AdPause.Run(() => platform.CommercialBreak(token));
                }

                // RoundState already moved the rotation on, so Current is the arena of the round this lobby leads to.
                var arena = arenaCatalog.Find(mapId);
                curtain.ShowLobby(roster, RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis,
                    GameText.Arena(arena), arena != null && arenaRotation.IsNew(arena.Id),
                    progress.WinStreak >= tuning.Streaks.Start ? progress.WinStreak : 0, NearestMission());
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

        /// <summary>
        /// The lobby's line about the mission closest to done (the most of its target already counted), so the next
        /// round starts with something to go for; null before the missions are saved.
        /// </summary>
        private string NearestMission()
        {
            MissionState nearest = null;
            foreach (var slot in missions.Slots) {
                if (nearest == null || slot.Progress * nearest.Definition.Target
                    > nearest.Progress * slot.Definition.Target) {
                    nearest = slot;
                }
            }

            if (nearest == null) {
                return null;
            }

            var definition = nearest.Definition;
            var task = GameText.Mission(definition,
                definition.Map.Length > 0 ? GameText.Arena(arenaCatalog.Find(definition.Map)) : null);
            return Strings.Format("lobby.mission", task, $"{nearest.Progress}/{definition.Target}");
        }
    }
}
