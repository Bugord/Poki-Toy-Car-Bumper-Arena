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
    /// The result screen: the title, how the player went out and by whom, the round's XP and coins with CLAIM ×2 to
    /// double them for a rewarded ad, the level track with the saved XP and car unlocks, CAR and NEXT MATCH. The arena
    /// stays undimmed behind it, as it ended.
    /// </summary>
    public sealed class RoundOverView : FadingView<RoundOverViewModel>
    {
        // How long after the screen shows its parts start to move in: long enough for them to be laid out where they
        // start, or their transitions would have nothing to move from.
        private const long EnterDelay = 80;

        private const string WonClass = "round-over__title--won";
        private const string GiftClass = "round-over--gift";
        private const string OutClass = "round-over__title--out";
        private const string EnterClass = "round-over--enter";
        // The title's spring in (round-over__title in ui_screens) before the confetti bursts out of it, in milliseconds.
        private const int ConfettiDelay = 200;
        private const string KnockedByShownClass = "round-over__by--shown";
        private const string BestShownClass = "best--shown";
        // Any wide screen, a phone held sideways or a desktop: the title and the podium at the left, the missions, the
        // level card and the buttons in a column at the right, a size up so they read on a phone's short side.
        private const string ColumnsClass = "round-over--columns";
        // Each side keeps at least this far from the edge: a phone's camera can sit in the middle of either side, and
        // in Poki's frame the browser often reports no safe area at all.
        private const float ColumnsSide = 240f;
        // The right column starts under the coins and the gear, and stops this far above the bottom.
        private const float ColumnsTop = 180f;
        private const float ColumnsBottom = 36f;
        private const float ColumnsGap = 40f;
        // How much the right column may grow, and the share of the width it may take.
        private const float ColumnsMaxScale = 1.6f;
        private const float ColumnsShare = 0.42f;

        private readonly IAudioService audioService;
        private readonly HudControls controls;

        private Button playAgainButton;
        private Button garageButton;
        private Button homeButton;
        private RoundOverMatch match;
        private Button settingsButton;
        private CarPortrait garagePortrait;
        private CancellationTokenSource lifetime;
        private bool isPlayAgainPressed;
        private RoundOverProgress xpProgress;
        private RoundOverMeta meta;
        private RoundOverRewards rewards;
        private ResultDaily daily;
        private CancellationTokenSource rewardRun;
        private bool isTouch;
        private bool isColumns;
        private SafeInsets safeInsets;
        private VisualElement screen;
        private VisualElement bottom;
        private VisualElement missions;

        public RoundOverView(IAudioService audioService, HudControls controls)
        {
            this.audioService = audioService;
            this.controls = controls;
        }

        protected override void OnAttached()
        {
            // The navigator focuses the screen, which hands the focus to the button, so Space and Enter reach us.
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            lifetime = new CancellationTokenSource();

            playAgainButton = Root.Q<Button>("play-again-button");
            playAgainButton.clicked += PlayAgain;
            // The button's look is the sticker button in ui_screens, not the theme's.
            playAgainButton.RemoveFromClassList(Button.ussClassName);
            garageButton = Root.Q<Button>("garage-button");
            garageButton.RemoveFromClassList(Button.ussClassName);
            garageButton.clicked += OpenGarage;
            homeButton = Root.Q<Button>("home-button");
            homeButton.RemoveFromClassList(Button.ussClassName);
            homeButton.clicked += GoHome;
            Root.Q<VisualElement>("home-icon").generateVisualContent += MenuView.DrawHome;
            // With a mouse NEXT MATCH carries its SPACE key in the columns (ui_screens).
            isTouch = controls.CheckTouchScreen();
            Root.EnableInClassList("round-over--desktop", !isTouch);
            settingsButton = Root.Q<Button>("settings-button");
            settingsButton.RemoveFromClassList(Button.ussClassName);
            settingsButton.clicked += OpenSettings;
            meta = new RoundOverMeta(Root, ViewModel);
            screen = Root.Q<VisualElement>("round-over");
            bottom = Root.Q<VisualElement>(className: "round-over__bottom");
            missions = Root.Q<VisualElement>("missions");
            // The daily gift over the visit's first result, above the coin corner it pays into.
            daily = new ResultDaily(screen ?? Root, ViewModel, audioService, meta.CoinPill, isTouch);
            daily.Claimed += OnDailyClaimed;
            bottom.RegisterCallback<GeometryChangedEvent>(OnColumnGeometry);
            missions?.RegisterCallback<GeometryChangedEvent>(OnColumnGeometry);
            SafeArea.Watch(Root, insets => {
                safeInsets = insets;
                settingsButton.style.top = 40f + insets.Top;
                settingsButton.style.right = 40f + insets.Right;
                meta.CoinPill.style.top = 52f + insets.Top;
                meta.CoinPill.style.right = 184f + insets.Right;
                PlaceColumns();
                daily.Place(Root.layout.size, insets);
            });
            garagePortrait = new CarPortrait(Root.Q<VisualElement>("garage-portrait"));
            RefreshGarage();
            ShowNewCar(ViewModel.HasNewCar, ViewModel.CanShop);
            ViewModel.CarBought += RefreshGarage;
            ViewModel.CarEquipped += RefreshGarage;

            // The key hint is for a keyboard; a phone or a tablet has none to press. A wide desktop screen shows the key on
            // NEXT MATCH itself (ui_screens), so the style sheet decides there.
            if (isTouch) {
                Root.Q<VisualElement>("hint").style.display = DisplayStyle.None;
            }

            ShowTitle();
            // The settings popup can change the language over this screen: the code-set text follows it.
            Strings.Changed += ShowTitle;
            Root.Q<Label>("revenge").EnableInClassList(BestShownClass, ViewModel.RevengeTaken);
            xpProgress = new RoundOverProgress(Root, ViewModel, isTouch);
            xpProgress.RewardTaken += OnRewardTaken;
            xpProgress.RewardRefused += OnRewardRefused;
            rewards = new RoundOverRewards(Root, ViewModel, isTouch);
            rewards.Claim.clicked += ClaimDouble;
            match = RoundOverMatch.Create(Root, ViewModel);
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            // The title and the buttons start small or low and spring into place while the screen fades in.
            Root.AddToClassList(EnterClass);
            Root.schedule.Execute(() => Root?.RemoveFromClassList(EnterClass)).StartingIn(EnterDelay);
            if (ViewModel.Outcome == RoundOutcome.Won) {
                Celebrate(lifetime.Token).Forget();
            }
            match?.Play();
            await base.ShowAsync(token);
            PlayReward(token).Forget();
        }

        /// <summary>
        /// The daily gift first, if this result offers it (a visit's first, with no menu before it), then the reward:
        /// nothing of the reward runs under the gift's ad, and nothing plays unseen under its dim. The reward runs
        /// beside input; no part of it delays NEXT MATCH, Space or Enter.
        /// </summary>
        private async UniTaskVoid PlayReward(CancellationToken token)
        {
            rewardRun?.Cancel();
            rewardRun?.Dispose();
            rewardRun = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            // The cards wait out of sight under the gift: still empty, they looked like a broken stub under its card.
            Root.AddToClassList(GiftClass);
            var isCancelled = await daily.Run(rewardRun.Token).SuppressCancellationThrow();
            Root.RemoveFromClassList(GiftClass);
            if (isCancelled || rewardRun.IsCancellationRequested) {
                return;
            }

            xpProgress.Play(token);
            meta.Play(token);
            rewards.Play(token);
        }

        // Today's gift taken over the result: its coins count into the corner, and CAR's dot follows what they buy.
        private void OnDailyClaimed(DailyClaim claim)
        {
            if (claim.Coins > 0) {
                meta.CountGift(claim.Coins, lifetime.Token);
            }

            ShowNewCar(ViewModel.HasNewCar, ViewModel.CanShop);
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            rewardRun?.Cancel();
            daily.Stop();
            xpProgress.Stop();
            meta.Stop();
            rewards.Stop();
            match?.ReleasePodium();
            await base.HideAsync(token);
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            bottom?.UnregisterCallback<GeometryChangedEvent>(OnColumnGeometry);
            missions?.UnregisterCallback<GeometryChangedEvent>(OnColumnGeometry);
            Strings.Changed -= ShowTitle;
            if (ViewModel != null) {
                ViewModel.CarBought -= RefreshGarage;
                ViewModel.CarEquipped -= RefreshGarage;
            }

            rewardRun?.Cancel();
            rewardRun?.Dispose();
            if (daily != null) {
                daily.Claimed -= OnDailyClaimed;
            }

            lifetime?.Cancel();
            lifetime?.Dispose();
            if (xpProgress != null) {
                xpProgress.RewardTaken -= OnRewardTaken;
                xpProgress.RewardRefused -= OnRewardRefused;
                xpProgress.Dispose();
            }

            meta?.Dispose();
            if (rewards != null) {
                rewards.Claim.clicked -= ClaimDouble;
                rewards.Dispose();
            }

            match?.Dispose();
            if (playAgainButton != null) {
                playAgainButton.clicked -= PlayAgain;
            }
            if (homeButton != null) {
                homeButton.clicked -= GoHome;
            }

            if (garageButton != null) {
                garageButton.clicked -= OpenGarage;
            }

            if (settingsButton != null) {
                settingsButton.clicked -= OpenSettings;
            }
            base.Dispose();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var isLandscape = evt.newRect.width > evt.newRect.height;
            Root.EnableInClassList("round-over--landscape", isLandscape);
            isColumns = isLandscape;
            Root.EnableInClassList(ColumnsClass, isColumns);
            match?.SetWide(isLandscape);
            PlaceColumns();
            daily.Place(evt.newRect.size, safeInsets);
        }

        private void OnColumnGeometry(GeometryChangedEvent evt) => PlaceColumns();

        /// <summary>
        /// Sizes the right column to the height it has, keeps both sides off the edges, and gives the title and the
        /// podium the rest of the width. The column grows by its scale, not its layout, so the cards keep the
        /// proportions they have upright.
        /// </summary>
        // The height the column leaves free above and below it, as last placed.
        private float columnsSpare;

        private void PlaceColumns()
        {
            if (bottom == null) {
                return;
            }

            if (!isColumns) {
                screen.style.paddingLeft = StyleKeyword.Null;
                screen.style.paddingRight = StyleKeyword.Null;
                foreach (var part in new[] { bottom, missions }) {
                    if (part == null) {
                        continue;
                    }

                    part.style.top = StyleKeyword.Null;
                    part.style.right = StyleKeyword.Null;
                    part.style.bottom = StyleKeyword.Null;
                    part.style.scale = StyleKeyword.Null;
                }

                return;
            }

            var size = screen.layout.size;
            var width = bottom.layout.width;
            if (float.IsNaN(size.x) || size.x <= 0f || width <= 0f) {
                return;
            }

            var side = Mathf.Max(ColumnsSide, Mathf.Max(safeInsets.Left, safeInsets.Right));
            var top = ColumnsTop + safeInsets.Top;
            var low = ColumnsBottom + safeInsets.Bottom;
            var missionsHeight = missions != null && missions.resolvedStyle.display != DisplayStyle.None
                ? missions.layout.height + ColumnsGap
                : 0f;
            var scale = Mathf.Min(ColumnsMaxScale, size.x * ColumnsShare / width,
                (size.y - top - low) / Mathf.Max(1f, missionsHeight + bottom.layout.height));
            var grown = new Scale(new Vector3(scale, scale, 1f));
            // A screen wider than the column needs (a 16:9 desktop) left a hole between the missions and the cards
            // under them: the column keeps together, in the middle of the height it has.
            // Kept until it moves by more than snapping can: layout snaps each edge of the moved cards to the device's
            // pixels, so moving them changes their heights by a device pixel, which moved them back, and again (the
            // layout failed to stabilize and froze the game on a 1289x725 window, where a device pixel is 2.65
            // reference pixels). The two heights each change by a pixel, scaled with the column and halved.
            var spare = Mathf.Round(Mathf.Max(0f,
                size.y - top - low - (missionsHeight + bottom.layout.height) * scale) * 0.5f);
            var pixel = 1f / Mathf.Max(0.01f, screen.panel?.scaledPixelsPerPoint ?? 1f);
            if (Mathf.Abs(spare - columnsSpare) <= pixel * ColumnsMaxScale + 1.5f) {
                spare = columnsSpare;
            }

            columnsSpare = spare;
            bottom.style.right = side;
            bottom.style.bottom = low + spare;
            bottom.style.scale = grown;
            if (missions != null) {
                missions.style.right = side;
                missions.style.top = top + spare;
                missions.style.scale = grown;
            }

            screen.style.paddingLeft = side;
            screen.style.paddingRight = side + width * scale + ColumnsGap;
        }

        private async UniTaskVoid Celebrate(CancellationToken token)
        {
            var confetti = new ConfettiBurst(Root);
            if (await UniTask.Delay(ConfettiDelay, DelayType.UnscaledDeltaTime, cancellationToken: token)
                    .SuppressCancellationThrow()) {
                return;
            }

            await confetti.Play(Root.Q<VisualElement>("title"), token).SuppressCancellationThrow();
        }

        // The red dot on CAR: a car this round opened, or something the coins buy, waits in the garage until it is
        // visited.
        private void ShowNewCar(bool isNewCar, bool canShop)
        {
            Root.Q<VisualElement>("garage-dot").style.display =
                isNewCar || canShop ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshGarage() => garagePortrait.Set(ViewModel.SelectedCar, ViewModel.SelectedColor);

        // A popup's reward taken: the click and the garage's purchase chime, the win jingle over them.
        private void OnRewardTaken()
        {
            audioService.PlayUiClick();
            audioService.Play(audioService.Library.KnockoutCredit);
            audioService.PlayJingle(audioService.Library.Win);
        }

        private void OnRewardRefused() => audioService.PlayUiClick();

        private void OpenSettings()
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.OpenSettings(lifetime.Token).Forget();
        }

        private void OpenGarage()
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            ShowGarage(lifetime.Token).Forget();
        }

        private async UniTask ShowGarage(CancellationToken token)
        {
            await ViewModel.OpenGarage(token);
            token.ThrowIfCancellationRequested();
            RefreshGarage();
            xpProgress.RefreshColor();
            ShowNewCar(false, false);
        }

        private void ShowTitle()
        {
            var (title, subtitle) = ViewModel.Outcome switch {
                RoundOutcome.Won => (Strings.Get("result.won"), Strings.Get("result.won_subtitle")),
                RoundOutcome.Out => (Strings.Get("result.out"), Strings.Get("result.out_subtitle")),
                _ => (Strings.Get("result.time_up"), Strings.Get("result.time_up_subtitle")),
            };

            // A match always runs out its time: first place has the most knock-outs, the others hear where they ended.
            if (ViewModel.IsMatch) {
                subtitle = ViewModel.Outcome == RoundOutcome.Won
                    ? Strings.Get("result.match_won_subtitle")
                    : Strings.Format("result.match_place", ViewModel.Place, ViewModel.Knockouts);
            }

            // The title is the same text twice (the outline under, the face over it, see .stroke in ui_screens).
            foreach (var label in Root.Query<Label>(className: "round-over__title-text").ToList()) {
                label.text = title;
                label.EnableInClassList(WonClass, ViewModel.Outcome == RoundOutcome.Won);
                label.EnableInClassList(OutClass, ViewModel.Outcome == RoundOutcome.Out);
            }

            // Knocked out by a car, the subtitle names it by its visit handle and colour: someone to beat next round.
            var knockedOutBy = ViewModel.Outcome == RoundOutcome.Out ? ViewModel.KnockedOutBy : RoundResult.Nobody;
            var isKnockedByCar = knockedOutBy != RoundResult.Nobody;
            // With that car turning on the podium under the title, the podium names it: the subtitle only says so.
            if (isKnockedByCar && ViewModel.Podium != null) {
                subtitle = Strings.Get("result.pushed_by");
                isKnockedByCar = false;
            }

            // A round's podium tells the rest: the title says how it ended, the car under it is the player's own or the
            // one that pushed them off (named on the podium), so the subtitle would only repeat them. A match keeps its
            // place and knock-outs there.
            var isRoundPodium = !ViewModel.IsMatch && ViewModel.Podium != null;
            var subtitleLabel = Root.Q<Label>("subtitle");
            subtitleLabel.text = subtitle;
            subtitleLabel.style.display = isKnockedByCar || isRoundPodium ? DisplayStyle.None : DisplayStyle.Flex;
            // A won round's WINNER, a ribbon under the title instead of a tag over the car, unless REVENGE! or the
            // streak already has the one sticker there.
            var streak = ViewModel.Streak;
            var isRibbon = isRoundPodium && ViewModel.Outcome == RoundOutcome.Won && !ViewModel.RevengeTaken &&
                !streak.IsStreak && !streak.IsLost;
            Root.Q<Label>("winner-ribbon").EnableInClassList(BestShownClass, isRibbon);
            Root.Q<VisualElement>("knocked-by").EnableInClassList(KnockedByShownClass, isKnockedByCar);
            if (isKnockedByCar) {
                Root.Q<VisualElement>("by-car-body").AddToClassList(CarColor.ClassOf(ViewModel.ColorOf(knockedOutBy)));
                Root.Q<Label>("by-name").text = ViewModel.KnockedOutByName;
                Root.Q<Label>("by-name-face").text = ViewModel.KnockedOutByName;
            }

        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            // The daily gift takes every key while it is up: Space claims it there, not NEXT MATCH.
            if (daily.HandleKey(evt.keyCode)) {
                evt.StopPropagation();
                return;
            }

            if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                evt.StopPropagation();
                PlayAgain();
            }
            else if (evt.keyCode == KeyCode.H) {
                evt.StopPropagation();
                GoHome();
            }
            else if (evt.keyCode == KeyCode.G) {
                evt.StopPropagation();
                OpenGarage();
            }
            else if (evt.keyCode == KeyCode.X) {
                evt.StopPropagation();
                // A reward popup takes the presses: X waits under it as a tap would.
                if (!xpProgress.IsPopupOpen) {
                    ClaimDouble();
                }
            }
        }

        // Before it is up, after NEXT MATCH, over a popup screen, under the daily gift (and in the frame it closed in,
        // whose key must not reach a button under it), or while an ad plays, the screen takes no presses.
        private bool IsBusy => !IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen ||
            ViewModel.IsAdPlaying || daily.IsOpen || daily.ClosedFrame == Time.frameCount;

        private void ClaimDouble()
        {
            if (IsBusy || !ViewModel.CanDouble) {
                return;
            }

            audioService.PlayUiClick();
            Double(lifetime.Token).Forget();
        }

        /// <summary>
        /// The replay of the reward settles where it ends, so nothing runs out under the ad; once it is back, the
        /// skipped popups show and, with the reward paid, the bonus plays on from there.
        /// </summary>
        private async UniTaskVoid Double(CancellationToken token)
        {
            xpProgress.Settle();
            rewards.SetWaiting(true);
            bool isDoubled;
            try {
                isDoubled = await ViewModel.Double(token);
            }
            catch (System.OperationCanceledException) {
                return;
            }
            finally {
                rewards.SetWaiting(false);
            }

            if (!isDoubled) {
                rewards.HideClaim();
                xpProgress.Resume(null, token);
                return;
            }

            rewards.ShowDoubled(token);
            meta.Recount(token);
            xpProgress.Resume(ViewModel.BonusXp, token);
            ShowNewCar(ViewModel.HasNewCar, ViewModel.CanShop);
        }

        private void PlayAgain()
        {
            // A key held down in the round, or a tap that ended it, must not skip the screen before it is up; a second
            // press while the curtain comes down changes nothing, so it does not click either.
            if (IsBusy) {
                return;
            }

            isPlayAgainPressed = true;
            audioService.PlayUiClick();
            ViewModel.PlayAgain();
        }

        /// <summary>HOME: back to the main menu, behind the curtain; taken like NEXT MATCH, once.</summary>
        private void GoHome()
        {
            if (IsBusy) {
                return;
            }

            isPlayAgainPressed = true;
            audioService.PlayUiClick();
            ViewModel.GoHome();
        }
    }
}
