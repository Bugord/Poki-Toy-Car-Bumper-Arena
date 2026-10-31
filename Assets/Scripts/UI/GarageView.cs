using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A full-screen popup: UIKit keeps RoundOver attached underneath and gives only the garage focus. NEXT MATCH (and
    /// Space or Enter) starts the next round straight from here; the close button and Esc go back to the result.
    /// </summary>
    public sealed class GarageView : FadingView<GarageViewModel>
    {
        private const float BounceDuration = 0.3f;
        private readonly IAudioService audioService;
        private readonly List<(CarCatalog.Entry Car, Button Button, Label Status, CarPortrait Portrait)> cards = new();
        private readonly Button[] colors = new Button[CarPaint.Count];
        private Button closeButton;
        private Button settingsButton;
        private CancellationTokenSource lifetime;
        private Button playButton;
        private CarPortrait preview;
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
            closeButton = Root.Q<Button>("garage-close");
            closeButton.RemoveFromClassList(Button.ussClassName);
            closeButton.clicked += Back;
            playButton = Root.Q<Button>("garage-play");
            playButton.RemoveFromClassList(Button.ussClassName);
            playButton.clicked += NextMatch;
            settingsButton = Root.Q<Button>("settings-button");
            settingsButton.RemoveFromClassList(Button.ussClassName);
            settingsButton.clicked += OpenSettings;
            lifetime = new CancellationTokenSource();
            preview = new CarPortrait(Root.Q<VisualElement>("garage-preview"));
            var colorRow = Root.Q<VisualElement>("garage-colors");
            for (var id = 0; id < colors.Length; id++) {
                var colorId = id;
                var button = new Button {
                    name = $"garage-color-{CarPaint.NameOf(id)}",
                    tooltip = CarPaint.NameOf(id).ToUpperInvariant(),
                };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__color");
                button.AddToClassList(CarColor.ClassOf(id));
                var ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList("garage__color-ring");
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("garage__color-check");
                ring.Add(check);
                button.Add(ring);
                button.clicked += () => SelectColor(colorId, button);
                colors[id] = button;
                colorRow.Add(button);
            }
            var grid = Root.Q<VisualElement>("garage-grid");
            foreach (var car in ViewModel.Cars) {
                var button = new Button { name = $"garage-car-{car.Id}" };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__car");
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                portrait.AddToClassList("garage__portrait");
                var image = new CarPortrait(portrait);
                image.Set(car, ViewModel.SelectedColor, !ViewModel.IsUnlocked(car.Id));
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
                cards.Add((car, button, status, image));
                grid.Add(button);
            }

            RefreshSelection();
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) =>
            Root.EnableInClassList("garage--landscape", evt.newRect.width > evt.newRect.height);

        private void RefreshSelection()
        {
            var selectedCar = ViewModel.SelectedEntry;
            preview.Set(selectedCar, ViewModel.SelectedColor);
            Root.Q<Label>("garage-preview-name").text = selectedCar?.DisplayName;
            Root.Q<Label>("garage-preview-name-face").text = selectedCar?.DisplayName;
            for (var id = 0; id < colors.Length; id++) {
                colors[id].EnableInClassList("garage__color--selected", id == ViewModel.SelectedColor);
            }
            foreach (var (car, button, status, portrait) in cards) {
                var selected = car.Id == ViewModel.SelectedCar;
                var unlocked = ViewModel.IsUnlocked(car.Id);
                button.EnableInClassList("garage__car--selected", selected);
                button.EnableInClassList("garage__car--locked", !unlocked);
                status.text = selected ? "IN USE" : unlocked ? "USE" : $"LVL {car.UnlockLevel}";
                button.tooltip = unlocked ? car.DisplayName : $"UNLOCK AT LVL {car.UnlockLevel}";
                portrait.SetColor(ViewModel.SelectedColor);
            }
        }

        private void SelectColor(int id, Button button)
        {
            if (!IsShown || isClosing || !ViewModel.SelectColor(id)) {
                return;
            }

            audioService.PlayUiClick();
            RefreshSelection();
            StartBounce(button);
        }

        private void Select(CarCatalog.Entry car, Button button)
        {
            if (!IsShown || isClosing || !ViewModel.IsUnlocked(car.Id)) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.Select(car.Id);
            RefreshSelection();
            StartBounce(button);
        }

        private void StartBounce(VisualElement button)
        {
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
                // The keys mean NEXT MATCH here as on the result, never a press of whichever card has the focus.
                // A repeat of the result's restart key reaches us before the fade-in ends and is dropped by IsShown.
                evt.StopImmediatePropagation();
                NextMatch();
            }
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            Back();
        }

        private void OpenSettings()
        {
            if (!IsShown || isClosing || ViewModel.IsSettingsOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.OpenSettings(lifetime.Token).Forget();
        }

        private void Back()
        {
            if (!IsShown || isClosing || ViewModel.IsSettingsOpen) {
                return;
            }

            isClosing = true;
            audioService.PlayUiClick();
            ViewModel.Back();
        }

        private void NextMatch()
        {
            if (!IsShown || isClosing || ViewModel.IsSettingsOpen) {
                return;
            }

            isClosing = true;
            audioService.PlayUiClick();
            ViewModel.NextMatch();
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
            if (closeButton != null) {
                closeButton.clicked -= Back;
            }
            if (playButton != null) {
                playButton.clicked -= NextMatch;
            }
            if (settingsButton != null) {
                settingsButton.clicked -= OpenSettings;
            }
            lifetime?.Cancel();
            lifetime?.Dispose();
            cards.Clear();
            base.Dispose();
        }
    }
}
