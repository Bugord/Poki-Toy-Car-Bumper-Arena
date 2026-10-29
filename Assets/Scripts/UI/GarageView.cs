using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>A full-screen popup: UIKit keeps RoundOver attached underneath and gives only the garage focus.</summary>
    public sealed class GarageView : FadingView<GarageViewModel>
    {
        private const float BounceDuration = 0.3f;
        private readonly IAudioService audioService;
        private readonly List<(CarCatalog.Entry Car, Button Button, Label Status)> cards = new();
        private Button backButton;
        private bool isClosing;
        private CancellationTokenSource bounce;
        private VisualElement bouncingCard;

        public GarageView(IAudioService audioService)
        {
            this.audioService = audioService;
        }

        protected override void OnAttached()
        {
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            backButton = Root.Q<Button>("garage-back");
            backButton.RemoveFromClassList(Button.ussClassName);
            backButton.clicked += Back;

            var level = ViewModel.Level;
            Root.Q<Label>("garage-level-value").text = $"LVL {level.Level}";
            Root.Q<VisualElement>("garage-xp-fill").style.width = Length.Percent(100f * level.Xp / level.Cost);
            Root.Q<Label>("garage-xp-value").text = $"{level.Xp} / {level.Cost} XP";
            var grid = Root.Q<VisualElement>("garage-grid");
            foreach (var car in ViewModel.Cars) {
                var button = new Button { name = $"garage-car-{car.Id}" };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__car");
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                portrait.AddToClassList("garage__portrait");
                portrait.style.backgroundImage = new StyleBackground(car.Portrait);
                portrait.EnableInClassList("car-portrait--locked", !ViewModel.IsUnlocked(car.Id));
                button.Add(portrait);
                var name = new Label(car.DisplayName) { pickingMode = PickingMode.Ignore };
                name.AddToClassList("garage__name");
                button.Add(name);
                var statusRow = new VisualElement { pickingMode = PickingMode.Ignore };
                statusRow.AddToClassList("pill");
                statusRow.AddToClassList("garage__status");
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("garage__check");
                statusRow.Add(check);
                var status = new Label { pickingMode = PickingMode.Ignore };
                statusRow.Add(status);
                button.Add(statusRow);
                button.clicked += () => Select(car, button);
                cards.Add((car, button, status));
                grid.Add(button);
            }

            RefreshSelection();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) =>
            Root.EnableInClassList("garage--landscape", evt.newRect.width > evt.newRect.height);

        private void RefreshSelection()
        {
            foreach (var (car, button, status) in cards) {
                var selected = car.Id == ViewModel.SelectedCar;
                var unlocked = ViewModel.IsUnlocked(car.Id);
                button.EnableInClassList("garage__car--selected", selected);
                button.EnableInClassList("garage__car--locked", !unlocked);
                status.text = selected ? "IN USE" : unlocked ? "USE" : $"LVL {car.UnlockLevel}";
                button.tooltip = unlocked ? car.DisplayName : $"UNLOCK AT LVL {car.UnlockLevel}";
            }
        }

        private void Select(CarCatalog.Entry car, Button button)
        {
            if (!IsShown || isClosing || !ViewModel.IsUnlocked(car.Id)) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.Select(car.Id);
            RefreshSelection();
            StopBounce();
            bouncingCard = button;
            bounce = new CancellationTokenSource();
            Bounce(button, bounce.Token).Forget();
        }

        private static async UniTask Bounce(VisualElement card, CancellationToken token)
        {
            for (var time = 0f; time < BounceDuration; time += Time.unscaledDeltaTime) {
                card.style.translate = new Translate(0f, -22f * Mathf.Sin(Mathf.PI * time / BounceDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            card.style.translate = new Translate(0f, 0f);
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) {
                evt.StopImmediatePropagation();
                Back();
            }
            else if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                // Do not let a repeat of the result screen's restart key activate a newly focused card or BACK.
                evt.StopImmediatePropagation();
            }
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            Back();
        }

        private void Back()
        {
            if (!IsShown || isClosing) {
                return;
            }

            isClosing = true;
            audioService.PlayUiClick();
            ViewModel.Back();
        }

        private void StopBounce()
        {
            bounce?.Cancel();
            bounce?.Dispose();
            bounce = null;
            if (bouncingCard != null) {
                bouncingCard.style.translate = new Translate(0f, 0f);
                bouncingCard = null;
            }
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            isClosing = true;
            StopBounce();
            await base.HideAsync(token);
        }

        public override void Dispose()
        {
            StopBounce();
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root?.UnregisterCallback<NavigationCancelEvent>(OnCancel);
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (backButton != null) {
                backButton.clicked -= Back;
            }
            cards.Clear();
            base.Dispose();
        }
    }
}
