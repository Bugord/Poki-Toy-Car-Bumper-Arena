using System;
using System.Threading;
using Bugord.App.Localization;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.UI;

namespace ToyCarArena.App
{
    /// <summary>
    /// The way into the next round from a screen: the curtain comes down, the commercial break goes under it once the
    /// visit has played long enough (<see cref="AdBreaks"/>), the lobby finds the players while the screen closes and the
    /// next arena loads, and READY! leaves the curtain to the round. NEXT MATCH on the result and PLAY in the menu both
    /// take it, so the two never drift apart.
    /// </summary>
    public sealed class MatchLobby
    {
        private readonly LevelScene levelScene;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;
        private readonly ISessionRoster roster;
        private readonly ISessionProgress progress;
        private readonly GameTuning tuning;
        private readonly IArenaRotation arenaRotation;
        private readonly ArenaCatalog arenaCatalog;
        private readonly MissionTracker missions;
        private readonly AdBreaks adBreaks;

        public MatchLobby(LevelScene levelScene, ScreenCurtain curtain, IGamePlatform platform, ISessionRoster roster,
            ISessionProgress progress, GameTuning tuning, IArenaRotation arenaRotation, ArenaCatalog arenaCatalog,
            MissionTracker missions, AdBreaks adBreaks)
        {
            this.levelScene = levelScene;
            this.curtain = curtain;
            this.platform = platform;
            this.roster = roster;
            this.progress = progress;
            this.tuning = tuning;
            this.arenaRotation = arenaRotation;
            this.arenaCatalog = arenaCatalog;
            this.missions = missions;
            this.adBreaks = adBreaks;
        }

        /// <summary>
        /// Whether a round of this visit was reached through the lobby: the menu's PLAY leads to the visit's first round
        /// that way, which then counts down like any later round instead of waiting on the start screen.
        /// </summary>
        public bool HasLed { get; private set; }

        /// <summary>
        /// Lowers the curtain over the screen, shows the lobby on it, closes the screen with
        /// <paramref name="closeScreen"/> and loads the next arena under it; returns once the curtain says READY!.
        /// The screen is closed on failure too.
        /// </summary>
        public async UniTask Run(Func<UniTask> closeScreen, CancellationToken token)
        {
            // The rotation already moved on (RoundState), so Current is the arena of the round this lobby leads to.
            var mapId = arenaRotation.Current;
            try {
                await curtain.Show(token);
                // Before the lobby and its clocks: it neither runs out under the ad nor reports a slow arena, and no
                // event goes out while the ad plays.
                if (adBreaks.IsDue(tuning.Ads.FreePlaySeconds)) {
                    platform.MeasureCommercialBreak();
                    await AdPause.Run(() => platform.CommercialBreak(token));
                }

                var arena = arenaCatalog.Find(mapId);
                curtain.ShowLobby(roster, RoundDifficulty.BotCountFor(tuning, progress), progress.Nemesis,
                    GameText.Arena(arena), arena != null && arenaRotation.IsNew(arena.Id),
                    progress.WinStreak >= tuning.Streaks.Start ? progress.WinStreak : 0, NearestMission(),
                    RoundRules.For(tuning, progress.Round));
            }
            catch {
                await closeScreen();
                throw;
            }

            // Under the curtain the screen's fade-out shows nowhere, so the next arena loads meanwhile: the brief wants
            // a lost round back in play within about 3 seconds.
            // A load still running past the lobby is told apart, so a visit lost here reads as a slow arena or a wait.
            // Awaited twice, by the race and after it, while still running: a lazy task, not a preserved one.
            var loading = UniTask.WhenAll(closeScreen(), levelScene.Load(token)).ToAsyncLazy().Task;
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

            HasLed = true;
        }

        /// <summary>
        /// The lobby's line about the mission closest to done (<see cref="MissionTracker.Nearest"/>), so the next round
        /// starts with something to go for; null before the missions are saved.
        /// </summary>
        private string NearestMission()
        {
            var nearest = missions.Nearest();
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
