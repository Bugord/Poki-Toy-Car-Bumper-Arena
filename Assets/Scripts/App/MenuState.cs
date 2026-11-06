using System.Threading;
using Bugord.App.Platform;
using Cysharp.Threading.Tasks;
using ToyCarArena.UI;
using UIKit.Runtime.Abstractions;
using UniState;
using UnityEngine;

namespace ToyCarArena.App
{
    /// <summary>
    /// The main menu: where a returning visit opens (<see cref="EntryState"/>) and where HOME on the result leads. The
    /// first round's arena loads behind it, as the menu's 3D car is drawn by that scene (<see cref="Gameplay.Cars.GarageStage"/>).
    /// PLAY, in the menu or as the garage's NEXT MATCH, takes the same lobby as NEXT MATCH on the result
    /// (<see cref="MatchLobby"/>): curtain, the commercial break once due, the players found and a fresh arena, and then
    /// the round counts down like any later one.
    /// </summary>
    public sealed class MenuState : StateBase
    {
        private readonly LevelScene levelScene;
        private readonly IScreenNavigator navigator;
        private readonly ScreenCurtain curtain;
        private readonly IGamePlatform platform;
        private readonly MatchLobby lobby;
        private readonly ContentBundles content;

        // The visit's first menu reports how long it took to come up, as the start screen of a first visit does.
        private static bool isShown;

        public MenuState(LevelScene levelScene, IScreenNavigator navigator, ScreenCurtain curtain, IGamePlatform platform,
            MatchLobby lobby, ContentBundles content)
        {
            this.levelScene = levelScene;
            this.navigator = navigator;
            this.curtain = curtain;
            this.platform = platform;
            this.lobby = lobby;
            this.content = content;
        }

        public override async UniTask<StateTransitionInfo> Execute(CancellationToken token)
        {
            if (!levelScene.IsLoaded) {
                await levelScene.Load(token);
            }

            // The menu's car shows its portrait until the 3D one is drawn, and a missing portrait is only blank: a short
            // wait, as the result's.
            await content.PrepareCars(PortraitWait, token);
            var screen = await navigator.OpenAsync<MenuViewModel>(token);
            try {
                platform.MeasureMenuShown(isShown ? null : Time.realtimeSinceStartup);
                isShown = true;
                await curtain.Hide();
                LoadingScreen.Hide();
                await screen.ViewModel.WaitForResultAsync(token);
            }
            catch {
                await screen.CloseAsync();
                throw;
            }

            await lobby.Run(() => screen.CloseAsync(), token);
            return Transition.GoTo<RoundState>();
        }

        // Seconds the menu waits for the car portraits still downloading (ContentBundles).
        private const float PortraitWait = 2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetVisit() => isShown = false;
    }
}
