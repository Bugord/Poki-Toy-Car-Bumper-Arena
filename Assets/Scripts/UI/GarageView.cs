using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
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
        private readonly LiveText text = new();
        private Button playButton;
        private CarPortrait preview;
        private bool isClosing;
        private CancellationTokenSource bounce;
        private VisualElement bouncingCard;
        private readonly List<(string Id, Button Button, Label Status, VisualElement Coin)> topperCards = new();
        private VisualElement carGrid;
        private VisualElement topperGrid;
        private VisualElement colorRow;
        private Button carsTab;
        private Button toppersTab;
        private VisualElement toppersDot;
        private VisualElement previewTopper;
        private Label coinCount;
        private bool isToppersTab;

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
            SafeArea.Watch(Root, insets => {
                closeButton.style.top = settingsButton.style.top = 40f + insets.Top;
                closeButton.style.right = 40f + insets.Right;
                settingsButton.style.right = 184f + insets.Right;
            });
            lifetime = new CancellationTokenSource();
            preview = new CarPortrait(Root.Q<VisualElement>("garage-preview"));
            colorRow = Root.Q<VisualElement>("garage-colors");
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
            carGrid = grid;
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
                var name = new Label { pickingMode = PickingMode.Ignore };
                text.Set(name, () => GameText.Car(car));
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

            BuildToppers();
            RefreshSelection();
            // The settings popup can change the language over the garage.
            text.OnChanged(RefreshSelection);
        }

        /// <summary>
        /// The CARS and TOPPERS tabs over the grid, the toppers' own grid (none first, then each with its price in
        /// coins until it is owned), the player's coins after the tabs, and the topper worn over the preview.
        /// </summary>
        private void BuildToppers()
        {
            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.AddToClassList("garage__tabs");
            carsTab = Tab("garage.cars", () => ShowTab(false));
            toppersTab = Tab("garage.toppers", () => ShowTab(true));
            toppersDot = new VisualElement { pickingMode = PickingMode.Ignore };
            toppersDot.AddToClassList("garage__tab-dot");
            toppersTab.Add(toppersDot);
            tabs.Add(carsTab);
            tabs.Add(toppersTab);
            colorRow.parent.Insert(colorRow.parent.IndexOf(colorRow), tabs);

            topperGrid = new VisualElement { name = "garage-toppers", pickingMode = PickingMode.Ignore };
            topperGrid.AddToClassList("garage__grid");
            topperGrid.AddToClassList("garage__grid--toppers");
            carGrid.parent.Insert(carGrid.parent.IndexOf(carGrid) + 1, topperGrid);
            var ids = new List<string> { "" };
            foreach (var topper in ViewModel.Toppers) {
                ids.Add(topper.Id);
            }

            foreach (var id in ids) {
                var button = new Button { name = $"garage-topper-{(id.Length == 0 ? "none" : id)}" };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__car");
                button.AddToClassList("garage__topper");
                button.Add(TopperIcon.Create(id, "garage__topper-icon"));
                var name = new Label { pickingMode = PickingMode.Ignore };
                var topperId = id;
                text.Set(name, () => topperId.Length == 0 ? Strings.Get("garage.none") : GameText.Topper(topperId));
                name.AddToClassList("garage__name");
                button.Add(name);
                var statusRow = new VisualElement { pickingMode = PickingMode.Ignore };
                statusRow.AddToClassList("pill");
                statusRow.AddToClassList("garage__status");
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("garage__check");
                statusRow.Add(check);
                var coin = RoundOverMeta.Coin();
                coin.AddToClassList("garage__price-coin");
                statusRow.Add(coin);
                var status = new Label { pickingMode = PickingMode.Ignore };
                statusRow.Add(status);
                button.Add(statusRow);
                button.clicked += () => PickTopper(topperId, button);
                topperCards.Add((id, button, status, coin));
                topperGrid.Add(button);
            }

            var pill = new VisualElement { pickingMode = PickingMode.Ignore };
            pill.AddToClassList("pill");
            pill.AddToClassList("coin-pill");
            pill.AddToClassList("garage__coins");
            pill.Add(RoundOverMeta.Coin());
            coinCount = new Label { pickingMode = PickingMode.Ignore };
            coinCount.AddToClassList("coin-pill__count");
            pill.Add(coinCount);
            tabs.Add(pill);

            previewTopper = TopperIcon.Create(ViewModel.SelectedTopper, "garage__preview-topper");
            Root.Q<VisualElement>("garage-preview").Add(previewTopper);
            ViewModel.ToppersShown();
            ShowTab(false);
        }

        private Button Tab(string key, System.Action pressed)
        {
            var tab = new Button();
            tab.RemoveFromClassList(Button.ussClassName);
            tab.AddToClassList("pill");
            tab.AddToClassList("garage__tab");
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("garage__tab-text");
            text.Set(label, () => Strings.Get(key));
            tab.Add(label);
            tab.clicked += () => {
                if (IsShown && !isClosing) {
                    audioService.PlayUiClick();
                    pressed();
                }
            };
            return tab;
        }

        private void ShowTab(bool toppers)
        {
            if (toppers && !isToppersTab) {
                ViewModel.ToppersPressed();
            }

            isToppersTab = toppers;
            carsTab.EnableInClassList("garage__tab--active", !toppers);
            toppersTab.EnableInClassList("garage__tab--active", toppers);
            carGrid.style.display = toppers ? DisplayStyle.None : DisplayStyle.Flex;
            colorRow.style.display = toppers ? DisplayStyle.None : DisplayStyle.Flex;
            topperGrid.style.display = toppers ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void PickTopper(string id, Button button)
        {
            if (!IsShown || isClosing || !ViewModel.PickTopper(id)) {
                return;
            }

            audioService.PlayUiClick();
            RefreshSelection();
            StartBounce(button);
        }

        private void RefreshToppers()
        {
            var worn = ViewModel.SelectedTopper;
            coinCount.text = ViewModel.Coins.ToString();
            toppersDot.style.display = ViewModel.CanBuyTopper ? DisplayStyle.Flex : DisplayStyle.None;
            TopperIcon.Set(previewTopper, worn);
            foreach (var (id, button, status, coin) in topperCards) {
                var topper = id.Length == 0 ? null : FindTopper(id);
                var isOwned = topper == null || ViewModel.IsOwned(id);
                var isWorn = id == worn;
                button.EnableInClassList("garage__car--selected", isWorn);
                button.EnableInClassList("garage__topper--price", !isOwned);
                button.EnableInClassList("garage__topper--dear", !isOwned && !ViewModel.CanAfford(topper));
                coin.style.display = isOwned ? DisplayStyle.None : DisplayStyle.Flex;
                status.text = isWorn ? Strings.Get("garage.in_use") : isOwned ? Strings.Get("garage.use")
                    : topper.Price.ToString();
            }
        }

        private ToyCarArena.Gameplay.GameTuning.TopperDefinition FindTopper(string id)
        {
            foreach (var topper in ViewModel.Toppers) {
                if (topper.Id == id) {
                    return topper;
                }
            }

            return null;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt) =>
            Root.EnableInClassList("garage--landscape", evt.newRect.width > evt.newRect.height);

        private void RefreshSelection()
        {
            var selectedCar = ViewModel.SelectedEntry;
            preview.Set(selectedCar, ViewModel.SelectedColor);
            Root.Q<Label>("garage-preview-name").text = GameText.Car(selectedCar);
            Root.Q<Label>("garage-preview-name-face").text = GameText.Car(selectedCar);
            for (var id = 0; id < colors.Length; id++) {
                colors[id].EnableInClassList("garage__color--selected", id == ViewModel.SelectedColor);
            }
            foreach (var (car, button, status, portrait) in cards) {
                var selected = car.Id == ViewModel.SelectedCar;
                var unlocked = ViewModel.IsUnlocked(car.Id);
                button.EnableInClassList("garage__car--selected", selected);
                button.EnableInClassList("garage__car--locked", !unlocked);
                status.text = selected ? Strings.Get("garage.in_use") : unlocked ? Strings.Get("garage.use")
                    : GameText.Level(car.UnlockLevel);
                button.tooltip = unlocked ? GameText.Car(car) : Strings.Format("garage.unlock_at", car.UnlockLevel);
                portrait.SetColor(ViewModel.SelectedColor);
            }

            RefreshToppers();
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
            text.Dispose();
            cards.Clear();
            topperCards.Clear();
            base.Dispose();
        }
    }
}
