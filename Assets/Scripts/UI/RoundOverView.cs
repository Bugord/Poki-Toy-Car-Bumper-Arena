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
    /// The result screen: the title, how the player went out and by whom, the level track with the saved XP and car
    /// unlocks, CAR and NEXT MATCH. The arena stays undimmed behind it, as it ended.
    /// </summary>
    public sealed class RoundOverView : FadingView<RoundOverViewModel>
    {
        // How long after the screen shows its parts start to move in: long enough for them to be laid out where they
        // start, or their transitions would have nothing to move from.
        private const long EnterDelay = 80;

        private const string WonClass = "round-over__title--won";
        private const string OutClass = "round-over__title--out";
        private const string EnterClass = "round-over--enter";
        // The title's spring in (round-over__title in ui_screens) before the confetti bursts out of it, in milliseconds.
        private const int ConfettiDelay = 200;
        private const string KnockedByShownClass = "round-over__by--shown";
        private const string BestShownClass = "best--shown";

        private readonly IAudioService audioService;
        private readonly HudControls controls;

        private Button playAgainButton;
        private Button garageButton;
        private Button garageWideButton;
        private RoundOverMatch match;
        private Button settingsButton;
        private CarPortrait garagePortrait;
        private CancellationTokenSource lifetime;
        private bool isPlayAgainPressed;
        private RoundOverProgress xpProgress;
        private RoundOverMeta meta;

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
            garageWideButton = Root.Q<Button>("garage-wide-button");
            garageWideButton.RemoveFromClassList(Button.ussClassName);
            garageWideButton.clicked += OpenGarage;
            // Steering with a mouse, the garage is a plain button under NEXT MATCH with its key; a thumb keeps the round
            // CAR button beside it.
            var isTouch = controls.CheckTouchScreen();
            Root.EnableInClassList("round-over--desktop", !isTouch);
            settingsButton = Root.Q<Button>("settings-button");
            settingsButton.RemoveFromClassList(Button.ussClassName);
            settingsButton.clicked += OpenSettings;
            meta = new RoundOverMeta(Root, ViewModel);
            SafeArea.Watch(Root, insets => {
                settingsButton.style.top = 40f + insets.Top;
                settingsButton.style.right = 40f + insets.Right;
                meta.CoinPill.style.top = 52f + insets.Top;
                meta.CoinPill.style.right = 184f + insets.Right;
            });
            garagePortrait = new CarPortrait(Root.Q<VisualElement>("garage-portrait"));
            RefreshGarage();
            ShowNewCar(ViewModel.HasNewCar, ViewModel.CanShop);
            ViewModel.CarBought += RefreshGarage;

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
            // The reward runs beside input; no part of it delays NEXT MATCH, Space or Enter.
            xpProgress.Play(token);
            meta.Play(token);
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            xpProgress.Stop();
            meta.Stop();
            match?.ReleasePodium();
            await base.HideAsync(token);
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            Strings.Changed -= ShowTitle;
            if (ViewModel != null) {
                ViewModel.CarBought -= RefreshGarage;
            }

            lifetime?.Cancel();
            lifetime?.Dispose();
            xpProgress?.Dispose();
            meta?.Dispose();
            match?.Dispose();
            if (playAgainButton != null) {
                playAgainButton.clicked -= PlayAgain;
            }
            if (garageButton != null) {
                garageButton.clicked -= OpenGarage;
            }

            if (garageWideButton != null) {
                garageWideButton.clicked -= OpenGarage;
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
            match?.SetWide(isLandscape);
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

        // The red dot on CAR, the NEW badge on GARAGE: a car this round opened waits in the garage until it is visited.
        // Something the coins buy lights the dot alone; a visit puts both out.
        private void ShowNewCar(bool isNewCar, bool canShop)
        {
            Root.Q<VisualElement>("garage-dot").style.display =
                isNewCar || canShop ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q<VisualElement>("garage-new").style.display = isNewCar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshGarage() => garagePortrait.Set(ViewModel.SelectedCar, ViewModel.SelectedColor);

        private void OpenSettings()
        {
            if (!IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.OpenSettings(lifetime.Token).Forget();
        }

        private void OpenGarage()
        {
            if (!IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen) {
                return;
            }

            audioService.PlayUiClick();
            ShowGarage(lifetime.Token).Forget();
        }

        private async UniTask ShowGarage(CancellationToken token)
        {
            var isNextMatch = await ViewModel.OpenGarage(token);
            token.ThrowIfCancellationRequested();
            RefreshGarage();
            xpProgress.RefreshColor();
            ShowNewCar(false, false);
            if (isNextMatch) {
                // The garage already clicked for its NEXT MATCH.
                isPlayAgainPressed = true;
                ViewModel.PlayAgain();
            }
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

            var subtitleLabel = Root.Q<Label>("subtitle");
            subtitleLabel.text = subtitle;
            subtitleLabel.style.display = isKnockedByCar ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q<VisualElement>("knocked-by").EnableInClassList(KnockedByShownClass, isKnockedByCar);
            if (isKnockedByCar) {
                Root.Q<VisualElement>("by-car-body").AddToClassList(CarColor.ClassOf(ViewModel.ColorOf(knockedOutBy)));
                Root.Q<Label>("by-name").text = ViewModel.KnockedOutByName;
                Root.Q<Label>("by-name-face").text = ViewModel.KnockedOutByName;
            }

        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                evt.StopPropagation();
                PlayAgain();
            }
            else if (evt.keyCode == KeyCode.G) {
                evt.StopPropagation();
                OpenGarage();
            }
        }

        private void PlayAgain()
        {
            // A key held down in the round, or a tap that ended it, must not skip the screen before it is up; a second
            // press while the curtain comes down changes nothing, so it does not click either.
            if (!IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen) {
                return;
            }

            isPlayAgainPressed = true;
            audioService.PlayUiClick();
            ViewModel.PlayAgain();
        }
    }
}
