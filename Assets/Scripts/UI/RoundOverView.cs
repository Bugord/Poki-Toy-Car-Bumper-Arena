using System.Threading;
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
        private const string ConfettiShownClass = "round-over__confetti--shown";
        private const string KnockedByShownClass = "round-over__by--shown";
        private const string BestShownClass = "best--shown";

        private readonly IAudioService audioService;
        private readonly HudControls controls;

        private Button playAgainButton;
        private Button garageButton;
        private CarPortrait garagePortrait;
        private CancellationTokenSource lifetime;
        private bool isPlayAgainPressed;
        private RoundOverProgress xpProgress;

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
            garagePortrait = new CarPortrait(Root.Q<VisualElement>("garage-portrait"));
            RefreshGarage();
            Root.Q<VisualElement>("garage-dot").style.display =
                ViewModel.HasNewCar ? DisplayStyle.Flex : DisplayStyle.None;

            // The key hint is for a keyboard; a phone or a tablet has none to press.
            Root.Q<VisualElement>("hint").style.display =
                controls.CheckTouchScreen() ? DisplayStyle.None : DisplayStyle.Flex;

            ShowTitle();
            Root.Q<Label>("revenge").EnableInClassList(BestShownClass, ViewModel.RevengeTaken);
            xpProgress = new RoundOverProgress(Root, ViewModel);
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            // The title and the buttons start small or low and spring into place while the screen fades in.
            Root.AddToClassList(EnterClass);
            Root.schedule.Execute(() => Root?.RemoveFromClassList(EnterClass)).StartingIn(EnterDelay);
            await base.ShowAsync(token);
            // The reward runs beside input; no part of it delays NEXT MATCH, Space or Enter.
            xpProgress.Play(token);
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            xpProgress.Stop();
            await base.HideAsync(token);
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            lifetime?.Cancel();
            lifetime?.Dispose();
            xpProgress?.Dispose();
            if (playAgainButton != null) {
                playAgainButton.clicked -= PlayAgain;
            }
            if (garageButton != null) {
                garageButton.clicked -= OpenGarage;
            }
            base.Dispose();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            Root.EnableInClassList("round-over--landscape", evt.newRect.width > evt.newRect.height);
        }

        private void RefreshGarage() => garagePortrait.Set(ViewModel.SelectedCar, ViewModel.SelectedColor);

        private void OpenGarage()
        {
            if (!IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen) {
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
            Root.Q<VisualElement>("garage-dot").style.display = DisplayStyle.None;
        }

        private void ShowTitle()
        {
            var (title, subtitle) = ViewModel.Outcome switch {
                RoundOutcome.Won => ("YOU WIN!", "LAST CAR STANDING!"),
                RoundOutcome.Out => ("KNOCKED OUT", "FELL OFF THE ARENA"),
                _ => ("TIME UP!", "STILL ON THE ARENA"),
            };

            // The title is the same text twice (the outline under, the face over it, see .stroke in ui_screens).
            foreach (var label in Root.Query<Label>(className: "round-over__title-text").ToList()) {
                label.text = title;
                label.EnableInClassList(WonClass, ViewModel.Outcome == RoundOutcome.Won);
                label.EnableInClassList(OutClass, ViewModel.Outcome == RoundOutcome.Out);
            }

            // Knocked out by a car, the subtitle names it by its visit handle and colour: someone to beat next round.
            var knockedOutBy = ViewModel.Outcome == RoundOutcome.Out ? ViewModel.KnockedOutBy : RoundResult.Nobody;
            var isKnockedByCar = knockedOutBy != RoundResult.Nobody;
            var subtitleLabel = Root.Q<Label>("subtitle");
            subtitleLabel.text = subtitle;
            subtitleLabel.style.display = isKnockedByCar ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q<VisualElement>("knocked-by").EnableInClassList(KnockedByShownClass, isKnockedByCar);
            if (isKnockedByCar) {
                Root.Q<VisualElement>("by-car-body").AddToClassList(CarColor.ClassOf(ViewModel.ColorOf(knockedOutBy)));
                Root.Q<Label>("by-name").text = ViewModel.KnockedOutByName;
                Root.Q<Label>("by-name-face").text = ViewModel.KnockedOutByName;
            }

            Root.Q<VisualElement>("confetti").EnableInClassList(ConfettiShownClass, ViewModel.Outcome == RoundOutcome.Won);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                evt.StopPropagation();
                PlayAgain();
            }
        }

        private void PlayAgain()
        {
            // A key held down in the round, or a tap that ended it, must not skip the screen before it is up; a second
            // press while the curtain comes down changes nothing, so it does not click either.
            if (!IsShown || isPlayAgainPressed || ViewModel.IsGarageOpen) {
                return;
            }

            isPlayAgainPressed = true;
            audioService.PlayUiClick();
            ViewModel.PlayAgain();
        }
    }
}
