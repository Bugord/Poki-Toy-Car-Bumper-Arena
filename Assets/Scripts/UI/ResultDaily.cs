using System;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The daily gift over a visit's first result, when no menu has offered it (a first visit goes straight to its
    /// round, so a new player would otherwise not hear of the calendar until a second visit). The same popup as the
    /// menu's (<see cref="MenuDaily"/>, the menu's frame and style sheet): the calendar, CLAIM and CLAIM ×2. It comes
    /// before anything else of the result plays, so nothing runs under its ad; the result's reward plays once it closes.
    /// </summary>
    internal sealed class ResultDaily
    {
        private const string OpenClass = "menu__popup--open";
        // The menu's classes for a wide screen and a mouse, which lay the calendar out in one row and show the keys.
        private const string WideClass = "menu--wide";
        private const string DesktopClass = "menu--desktop";
        private const float WideRatio = 1.2f;
        private const float Edge = 40f;
        private const float TouchWideSide = 240f;
        private const float DailyWidth = 960f;
        private const float DailyWideWidth = 1760f;

        private readonly RoundOverViewModel viewModel;
        private readonly IAudioService audioService;
        private readonly VisualElement coinCounter;
        private readonly bool isTouch;
        private readonly VisualElement host;
        private readonly VisualElement popup;
        private readonly Label[] titles;
        private readonly MenuDaily daily;
        private CancellationTokenSource lifetime;
        private bool isClaimed;

        public ResultDaily(VisualElement screen, RoundOverViewModel viewModel, IAudioService audioService,
            VisualElement coinCounter, bool isTouch)
        {
            this.viewModel = viewModel;
            this.audioService = audioService;
            this.coinCounter = coinCounter;
            this.isTouch = isTouch;
            host = new VisualElement { pickingMode = PickingMode.Ignore };
            host.style.position = Position.Absolute;
            host.style.left = 0f;
            host.style.top = 0f;
            host.style.right = 0f;
            host.style.bottom = 0f;
            host.EnableInClassList(DesktopClass, !isTouch);
            popup = MenuView.PopupFrame(host, out var sheet, out titles, () => {
                audioService.PlayUiClick();
                Close();
            }, Close);
            popup.AddToClassList("menu__daily-popup");
            daily = new MenuDaily(sheet, PressDouble, PressClaim, () => {
                audioService.PlayUiClick();
                Close();
            });
            screen.Add(host);
        }

        /// <summary>The popup is up: it takes every press and key of the result.</summary>
        public bool IsOpen => popup.ClassListContains(OpenClass);

        /// <summary>The frame it closed in: a key that closed it must not press what lies under it in the same frame.</summary>
        public int ClosedFrame { get; private set; } = -1;

        /// <summary>Today's gift was taken: the result counts its coins and redraws what they buy.</summary>
        public event Action<DailyClaim> Claimed;

        /// <summary>
        /// Opens the popup if this result offers the gift (<see cref="RoundOverViewModel.TakeDailyOffer"/>) and waits
        /// until it closes; at once when it does not.
        /// </summary>
        public async UniTask Run(CancellationToken token)
        {
            if (viewModel.Daily == null || !viewModel.TakeDailyOffer()) {
                return;
            }

            lifetime?.Dispose();
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            isClaimed = false;
            foreach (var label in titles) {
                label.text = Strings.Get("daily.title");
            }

            daily.Fill(viewModel.Daily);
            popup.AddToClassList(OpenClass);
            viewModel.DailyShown();
            try {
                await UniTask.WaitUntil(() => !IsOpen, cancellationToken: lifetime.Token);
            }
            finally {
                popup.RemoveFromClassList(OpenClass);
                viewModel.HideGiftHat();
            }
        }

        /// <summary>The result is leaving: the popup goes with nothing more measured.</summary>
        public void Stop()
        {
            lifetime?.Cancel();
            popup.RemoveFromClassList(OpenClass);
        }

        /// <summary>
        /// A key while the popup is up: Space or Enter claims, X watches the ad for ×2, Esc closes it; the others do
        /// nothing, so NEXT MATCH, HOME and the garage wait under it. False with the popup closed.
        /// </summary>
        public bool HandleKey(KeyCode key)
        {
            if (!IsOpen) {
                return false;
            }

            switch (key) {
                case KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter:
                    PressClaim();
                    break;
                case KeyCode.X:
                    PressDouble();
                    break;
                case KeyCode.Escape:
                    if (!isClaimed && !viewModel.IsAdPlaying) {
                        audioService.PlayUiClick();
                        Close();
                    }

                    break;
            }

            return true;
        }

        /// <summary>
        /// Fits the card to the screen as the menu does: the calendar in one row on a screen at least
        /// <see cref="WideRatio"/> as wide as high, a size up on a phone held sideways, inside the safe sides.
        /// </summary>
        public void Place(Vector2 size, SafeInsets insets)
        {
            if (size.x <= 0f || size.y <= 0f) {
                return;
            }

            var isWide = size.x >= WideRatio * size.y;
            host.EnableInClassList(WideClass, isWide);
            var side = isWide && isTouch ? TouchWideSide : Edge;
            var left = Mathf.Max(side, Edge + insets.Left);
            var right = Mathf.Max(side, Edge + insets.Right);
            var high = size.y - 2f * Edge - insets.Top - insets.Bottom;
            var sheetFit = Mathf.Max(0.6f, Mathf.Min(isWide && isTouch ? 1.3f : 1f, high / 1500f * 1.3f));
            var fit = Mathf.Min(sheetFit, (size.x - left - right) / (isWide ? DailyWideWidth : DailyWidth));
            daily.Sheet.style.scale = new Scale(new Vector3(fit, fit, 1f));
        }

        private void PressClaim()
        {
            if (!IsOpen || isClaimed || viewModel.IsAdPlaying || viewModel.Daily is not { CanClaim: true }) {
                return;
            }

            audioService.PlayUiClick();
            var claim = viewModel.Daily.Claim();
            if (claim != null) {
                Celebrate(claim);
            }
        }

        private void PressDouble()
        {
            if (!IsOpen || isClaimed || viewModel.IsAdPlaying || viewModel.Daily is not { CanDouble: true }) {
                return;
            }

            audioService.PlayUiClick();
            WatchAd(lifetime.Token).Forget();
        }

        private async UniTaskVoid WatchAd(CancellationToken token)
        {
            DailyClaim claim;
            try {
                claim = await viewModel.Daily.Watch(token);
            }
            catch (OperationCanceledException) {
                return;
            }

            if (!IsOpen) {
                return;
            }

            if (claim == null) {
                // No reward: CLAIM ×2 goes, and CLAIM still takes the gift.
                daily.Fill(viewModel.Daily);
                daily.Refuse(popup, token);
                return;
            }

            Celebrate(claim);
        }

        // The gift is taken: the cell turns done, the celebration plays, the coins land in the counter, and the card
        // closes once it is over, as in the menu.
        private void Celebrate(DailyClaim claim)
        {
            isClaimed = true;
            var token = lifetime.Token;
            daily.Celebrate(popup, claim, viewModel.Stage, coinCounter, token);
            audioService.Play(audioService.Library.KnockoutCredit);
            audioService.PlayJingle(audioService.Library.Win);
            Claimed?.Invoke(claim);
            popup.schedule.Execute(() => {
                if (!token.IsCancellationRequested && IsOpen) {
                    CloseNow();
                }
            }).StartingIn((long)(RewardCelebration.Duration * 1000f));
        }

        // Closing by choice: not mid-celebration (it closes by itself) nor under the ad; still to claim, it is declined.
        private void Close()
        {
            if (!IsOpen || isClaimed || viewModel.IsAdPlaying) {
                return;
            }

            if (viewModel.Daily is { CanClaim: true }) {
                viewModel.DailyDeclined();
            }

            CloseNow();
        }

        private void CloseNow()
        {
            popup.RemoveFromClassList(OpenClass);
            ClosedFrame = Time.frameCount;
        }
    }
}
