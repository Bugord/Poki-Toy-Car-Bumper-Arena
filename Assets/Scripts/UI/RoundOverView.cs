using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    public sealed class RoundOverView : FadingView<RoundOverViewModel>
    {
        private const string WonClass = "round-over__title--won";
        private const string OutClass = "round-over__title--out";

        private Button playAgainButton;

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

            ShowTitle();
            ShowPlace();
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            base.Dispose();
        }

        private void ShowTitle()
        {
            var title = ViewModel.Outcome switch {
                RoundOutcome.Won => "YOU WIN!",
                RoundOutcome.Out => "KNOCKED OUT",
                _ => "TIME UP!",
            };

            // The title is the same text twice (the outline under, the face over it, see .stroke in ui_screens).
            foreach (var label in Root.Query<Label>(className: "round-over__title-text").ToList()) {
                label.text = title;
                label.EnableInClassList(WonClass, ViewModel.Outcome == RoundOutcome.Won);
                label.EnableInClassList(OutClass, ViewModel.Outcome == RoundOutcome.Out);
            }
        }

        private void ShowPlace()
        {
            // A round of one car (the player alone) has no place to speak of.
            var hasPlace = ViewModel.CarCount > 1;
            Root.Q<VisualElement>("place").style.display = hasPlace ? DisplayStyle.Flex : DisplayStyle.None;
            Root.Q<Label>("place-value").text = $"#{ViewModel.Place} of {ViewModel.CarCount}";
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
            // A key held down in the round, or a tap that ended it, must not skip the screen before it is up.
            if (IsShown) {
                ViewModel.PlayAgain();
            }
        }
    }
}
