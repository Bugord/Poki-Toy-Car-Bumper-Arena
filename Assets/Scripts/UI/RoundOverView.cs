using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The result screen: the title, how the player went out and by whom, the place and the knock-outs against the
    /// player's bests, the time on the arena, and PLAY AGAIN.
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
        private const string PlacePillClass = "round-over__place-pill";
        private const string KnockedByShownClass = "round-over__by--shown";
        private const string BestShownClass = "best--shown";
        private const string BestNewClass = "best--new";
        private const string CarColorClass = "car-color--";

        // The highest car colour in ui_screens (car-color--0 to 7), as many as CarLook has swatches.
        private const int LastCarColor = 7;

        private readonly IAudioService audioService;

        private Button playAgainButton;
        private bool isPlayAgainPressed;

        public RoundOverView(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        protected override void OnAttached()
        {
            // The navigator focuses the screen, which hands the focus to the button, so Space and Enter reach us.
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            playAgainButton = Root.Q<Button>("play-again-button");
            playAgainButton.clicked += PlayAgain;
            // The button's look is the sticker button in ui_screens, not the theme's.
            playAgainButton.RemoveFromClassList(Button.ussClassName);

            // The key hint is for a keyboard; a phone or a tablet has none to press.
            Root.Q<VisualElement>("hint").style.display =
                Application.isMobilePlatform ? DisplayStyle.None : DisplayStyle.Flex;

            ShowTitle();
            ShowPlace();
            ShowTime();
            ShowKnockouts();
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            // The title, the card and the button start small or low and spring into place while the screen fades in.
            Root.AddToClassList(EnterClass);
            Root.schedule.Execute(() => Root?.RemoveFromClassList(EnterClass)).StartingIn(EnterDelay);
            await base.ShowAsync(token);
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            base.Dispose();
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

            // Knocked out by a car, the subtitle names it by its colour instead: someone to beat next round.
            var knockedOutBy = ViewModel.Outcome == RoundOutcome.Out ? ViewModel.KnockedOutBy : RoundResult.Nobody;
            var isKnockedByCar = knockedOutBy != RoundResult.Nobody;
            var subtitleLabel = Root.Q<Label>("subtitle");
            subtitleLabel.text = subtitle;
            subtitleLabel.style.display = isKnockedByCar ? DisplayStyle.None : DisplayStyle.Flex;
            Root.Q<VisualElement>("knocked-by").EnableInClassList(KnockedByShownClass, isKnockedByCar);
            if (isKnockedByCar) {
                var color = Mathf.Clamp(knockedOutBy, 0, LastCarColor);
                Root.Q<VisualElement>("by-car-body").AddToClassList(CarColorClass + color);
            }

            Root.Q<VisualElement>("confetti").EnableInClassList(ConfettiShownClass, ViewModel.Outcome == RoundOutcome.Won);
        }

        private void ShowPlace()
        {
            // A round of one car (the player alone) has no place to speak of; the card keeps the time alone.
            var hasPlace = ViewModel.CarCount > 1;
            Root.Q<VisualElement>("place").style.display = hasPlace ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q<Label>("place-value").text = $"#{ViewModel.Place}";
            Root.Q<Label>("place-of").text = $"OF {ViewModel.CarCount} CARS";

            // The podium in its metals, every place past it plain.
            var tier = ViewModel.Place switch {
                1 => "gold",
                2 => "silver",
                3 => "bronze",
                _ => null,
            };

            if (tier != null) {
                Root.Q<VisualElement>("place-pill").AddToClassList($"{PlacePillClass}--{tier}");
            }

            var bestText = ViewModel.BestPlace > 0 ? $"BEST #{ViewModel.BestPlace}" : null;
            ShowBest(Root.Q<Label>("place-best"), hasPlace && ViewModel.IsBestPlace, hasPlace ? bestText : null);
        }

        private void ShowKnockouts()
        {
            Root.Q<Label>("ko-value").text = ViewModel.Knockouts.ToString();
            var bestText = ViewModel.BestKnockouts > 0 ? $"BEST {ViewModel.BestKnockouts}" : null;
            ShowBest(Root.Q<Label>("ko-best"), ViewModel.IsBestKnockouts, bestText);
        }

        /// <summary>
        /// The chip beside a number: NEW BEST! when the round beat the player's best, else the best to beat, else
        /// nothing (no best yet).
        /// </summary>
        private static void ShowBest(Label chip, bool isNew, string bestText)
        {
            var text = isNew ? "NEW BEST!" : bestText;
            chip.text = text ?? "";
            chip.EnableInClassList(BestShownClass, text != null);
            chip.EnableInClassList(BestNewClass, isNew);
        }

        private void ShowTime()
        {
            // Down, so a car that fell a moment before the minute does not read as having lasted it.
            var seconds = Mathf.FloorToInt(ViewModel.Seconds);
            Root.Q<Label>("time-value").text = $"{seconds / 60}:{seconds % 60:00}";
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
            if (!IsShown || isPlayAgainPressed) {
                return;
            }

            isPlayAgainPressed = true;
            audioService.PlayUiClick();
            ViewModel.PlayAgain();
        }
    }
}
