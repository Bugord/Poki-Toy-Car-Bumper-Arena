using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A full-screen popup: UIKit keeps RoundOver attached underneath and gives only the garage focus. NEXT MATCH (and
    /// Space or Enter) starts the next round straight from here; the close button and Esc go back to the result.
    /// The car the player drives next stands big on its plinth with the paint under it; the shop beside it (under it
    /// upright) holds the cars and the toppers as cards whose tag says at a glance what a tap does: drive it, buy it,
    /// save up for it, or reach its level. A tap that cannot do anything still answers, with a shake and the reason.
    /// </summary>
    public sealed class GarageView : FadingView<GarageViewModel>
    {
        private const float BounceDuration = 0.3f;
        private const float ShakeDuration = 0.35f;
        private const float PopDuration = 0.32f;
        private const float ToastDuration = 0.9f;
        private const float ToastWidth = 900f;
        private const float ToastHeight = 120f;

        // The classes that give a card its state; RefreshSelection sets them, ui_screens draws them.
        private const string SelectedClass = "garage__card--selected";
        private const string LockedClass = "garage__card--locked";
        private const string PriceClass = "garage__card--price";
        private const string DearClass = "garage__card--dear";

        private readonly IAudioService audioService;
        private readonly HudControls controls;
        private readonly List<(CarCatalog.Entry Car, Card Card, CarPortrait Portrait)> cards = new();
        private readonly List<(string Id, Card Card)> topperCards = new();
        private readonly Button[] colors = new Button[CarPaint.Count];
        private readonly LiveText text = new();
        private readonly Tween cardTween = new();
        private readonly Tween heroTween = new();
        private readonly Tween toastTween = new();
        private readonly Tween coinTween = new();
        private Button closeButton;
        private Button settingsButton;
        private Button playButton;
        private CancellationTokenSource lifetime;
        // The 3D car on the plinth and its stage; with no stage (no arena loaded) the catalog's portrait instead.
        private GarageStage garageStage;
        private VisualElement hero;
        private CarPortrait preview;
        private bool isClosing;
        private VisualElement body;
        private VisualElement stage;
        private VisualElement carGrid;
        private VisualElement topperGrid;
        private VisualElement colorRow;
        private Button carsTab;
        private Button toppersTab;
        private VisualElement toppersDot;
        private VisualElement previewTopper;
        private VisualElement coinPill;
        private Label coinCount;
        private VisualElement toast;
        private VisualElement toastCoin;
        private Label toastText;
        private Label toastFace;
        private Label previewName;
        private Label previewNameFace;
        private bool isToppersTab;

        public GarageView(IAudioService audioService, HudControls controls)
        {
            this.audioService = audioService;
            this.controls = controls;
        }

        protected override void OnAttached()
        {
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            Root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            // A finger needs bigger targets than a mouse and has no keys to show: the style sheet sizes both looks.
            Root.EnableInClassList("garage--touch", controls != null && controls.CheckTouchScreen());
            closeButton = Root.Q<Button>("garage-close");
            closeButton.RemoveFromClassList(Button.ussClassName);
            closeButton.clicked += Back;
            playButton = Root.Q<Button>("garage-play");
            playButton.RemoveFromClassList(Button.ussClassName);
            playButton.clicked += NextMatch;
            settingsButton = Root.Q<Button>("settings-button");
            settingsButton.RemoveFromClassList(Button.ussClassName);
            settingsButton.clicked += OpenSettings;
            body = Root.Q<VisualElement>("garage-body");
            // The gear and the close button side by side in one corner, so their sizes alone (bigger for a thumb on a
            // wide phone) set where they stand.
            var corner = new VisualElement { pickingMode = PickingMode.Ignore };
            corner.AddToClassList("garage__corner");
            settingsButton.parent.Add(corner);
            corner.Add(settingsButton);
            corner.Add(closeButton);
            SafeArea.Watch(Root, insets => {
                corner.style.top = 40f + insets.Top;
                corner.style.right = 40f + insets.Right;
                // A notch or a home bar takes its side of the screen from the hero, the shop and NEXT MATCH alike.
                body.style.paddingTop = 40f + insets.Top;
                body.style.paddingLeft = 40f + insets.Left;
                body.style.paddingRight = 40f + insets.Right;
                body.style.paddingBottom = 32f + insets.Bottom;
            });
            lifetime = new CancellationTokenSource();
            stage = Root.Q<VisualElement>("garage-stage");
            garageStage = ViewModel.Stage;
            var previewCar = Root.Q<VisualElement>("garage-preview");
            if (garageStage != null) {
                previewCar.RemoveFromClassList("car-portrait");
                // A background, not an Image: an Image measures itself by its texture and widened the hero's column.
                hero = new VisualElement { pickingMode = PickingMode.Ignore };
                hero.AddToClassList("garage__hero-car");
                previewCar.Add(hero);
            }
            else {
                preview = new CarPortrait(previewCar);
            }

            previewName = Root.Q<Label>("garage-preview-name");
            previewNameFace = Root.Q<Label>("garage-preview-name-face");
            BuildToast();
            BuildColors();
            var shop = Root.Q<VisualElement>("garage-shop");
            BuildHead(shop);
            carGrid = Root.Q<VisualElement>("garage-grid");
            carGrid.AddToClassList("garage__grid--cars");
            foreach (var car in ViewModel.Cars) {
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                portrait.AddToClassList("garage__portrait");
                var image = new CarPortrait(portrait);
                image.Set(car, ViewModel.SelectedColor, !ViewModel.IsUnlocked(car.Id));
                var card = new Card($"garage-car-{car.Id}", portrait, text, () => GameText.Car(car));
                card.Button.clicked += () => Select(car, card.Button);
                cards.Add((car, card, image));
                carGrid.Add(card.Button);
            }

            BuildToppers(shop);
            RefreshSelection();
            // The settings popup can change the language over the garage.
            text.OnChanged(RefreshSelection);
        }

        /// <summary>
        /// A floating line over the card just tapped: what a purchase cost, or why nothing happened. One for the screen,
        /// built here so PillShape sees its coin.
        /// </summary>
        private void BuildToast()
        {
            toast = Root.Q<VisualElement>("garage-toast-row");
            toastText = Root.Q<Label>("garage-toast");
            toastFace = Root.Q<Label>("garage-toast-face");
            toastCoin = RoundOverMeta.Coin();
            toastCoin.AddToClassList("garage__toast-coin");
            toast.Insert(0, toastCoin);
            toast.style.display = DisplayStyle.None;
        }

        /// <summary>The paint: eight round stickers in a white bar, so their navy outlines read on the navy page.</summary>
        private void BuildColors()
        {
            colorRow = Root.Q<VisualElement>("garage-colors");
            for (var id = 0; id < colors.Length; id++) {
                var colorId = id;
                var button = new Button {
                    name = $"garage-color-{CarPaint.NameOf(id)}",
                    tooltip = CarPaint.NameOf(id).ToUpperInvariant(),
                };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__color");
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("garage__color-dot");
                dot.AddToClassList(CarColor.ClassOf(id));
                var ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList("garage__color-ring");
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("garage__color-check");
                ring.Add(check);
                dot.Add(ring);
                button.Add(dot);
                button.clicked += () => SelectColor(colorId, button);
                colors[id] = button;
                colorRow.Add(button);
            }
        }

        /// <summary>
        /// The shop's head: the CARS and TOPPERS tabs (a red dot on TOPPERS while coins buy one) and the player's coins,
        /// which bump as a purchase spends them.
        /// </summary>
        private void BuildHead(VisualElement shop)
        {
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("garage__head");
            var tabs = new VisualElement { pickingMode = PickingMode.Ignore };
            tabs.AddToClassList("garage__tabs");
            carsTab = Tab("garage.cars", () => ShowTab(false));
            toppersTab = Tab("garage.toppers", () => ShowTab(true));
            toppersDot = new VisualElement { pickingMode = PickingMode.Ignore };
            toppersDot.AddToClassList("garage__tab-dot");
            toppersTab.Add(toppersDot);
            tabs.Add(carsTab);
            tabs.Add(toppersTab);
            head.Add(tabs);

            coinPill = new VisualElement { pickingMode = PickingMode.Ignore };
            coinPill.AddToClassList("coin-pill");
            coinPill.AddToClassList("garage__coins");
            coinPill.Add(RoundOverMeta.Coin());
            coinCount = new Label { pickingMode = PickingMode.Ignore };
            coinCount.AddToClassList("coin-pill__count");
            coinPill.Add(coinCount);
            head.Add(coinPill);
            shop.Insert(0, head);
        }

        /// <summary>The toppers' grid (none first, then each with its price until it is owned) and the worn one on the hero.</summary>
        private void BuildToppers(VisualElement shop)
        {
            topperGrid = new VisualElement { name = "garage-toppers", pickingMode = PickingMode.Ignore };
            topperGrid.AddToClassList("garage__grid");
            topperGrid.AddToClassList("garage__grid--toppers");
            shop.Add(topperGrid);
            var ids = new List<string> { "" };
            foreach (var topper in ViewModel.Toppers) {
                ids.Add(topper.Id);
            }

            // The toppers' 3D models, all drawn into one texture that each card crops to its own; NONE keeps its icon.
            var models = garageStage?.ShowToppers(ids.GetRange(1, ids.Count - 1));
            foreach (var id in ids) {
                var topperId = id;
                VisualElement icon;
                if (models != null && id.Length > 0) {
                    icon = new Image {
                        image = models,
                        uv = garageStage.CellOf(id),
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore,
                    };
                    icon.AddToClassList("garage__topper-icon");
                    icon.AddToClassList("garage__topper-model");
                }
                else {
                    icon = TopperIcon.Create(id, "garage__topper-icon");
                }

                var card = new Card($"garage-topper-{(id.Length == 0 ? "none" : id)}", icon, text,
                    () => topperId.Length == 0 ? Strings.Get("garage.none") : GameText.Topper(topperId));
                card.Button.AddToClassList("garage__card--topper");
                card.Button.clicked += () => PickTopper(topperId, card.Button);
                topperCards.Add((id, card));
                topperGrid.Add(card.Button);
            }

            // The 3D car wears its topper itself; the portrait gets a flat one over its roof.
            if (garageStage == null) {
                previewTopper = TopperIcon.Create(ViewModel.SelectedTopper, "garage__preview-topper");
                Root.Q<VisualElement>("garage-preview").Add(previewTopper);
            }

            ViewModel.ToppersShown();
            ShowTab(false);
        }

        private Button Tab(string key, Action pressed)
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
            topperGrid.style.display = toppers ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void PickTopper(string id, Button button)
        {
            if (!IsShown || isClosing) {
                return;
            }

            var topper = id.Length == 0 ? null : FindTopper(id);
            var wasOwned = topper == null || ViewModel.IsOwned(id);
            if (!wasOwned && !ViewModel.CanAfford(topper)) {
                TooDear(button, topper.Price);
                return;
            }

            if (!ViewModel.PickTopper(id)) {
                return;
            }

            if (wasOwned) {
                audioService.PlayUiClick();
            }
            else {
                Bought(button, topper.Price);
            }

            RefreshSelection();
            StartBounce(button);
            PopHero();
        }

        private void RefreshToppers()
        {
            var worn = ViewModel.SelectedTopper;
            coinCount.text = ViewModel.Coins.ToString();
            toppersDot.style.display = ViewModel.CanBuyTopper ? DisplayStyle.Flex : DisplayStyle.None;
            if (previewTopper != null) {
                TopperIcon.Set(previewTopper, worn);
                // No topper is no topper on the hero: the NONE card's crossed circle belongs to the shop.
                previewTopper.style.display = string.IsNullOrEmpty(worn) ? DisplayStyle.None : DisplayStyle.Flex;
            }
            foreach (var (id, card) in topperCards) {
                var topper = id.Length == 0 ? null : FindTopper(id);
                var isOwned = topper == null || ViewModel.IsOwned(id);
                var isWorn = id == worn;
                card.SetState(isWorn, false, !isOwned, !isOwned && !ViewModel.CanAfford(topper),
                    isWorn ? Strings.Get("garage.in_use") : isOwned ? Strings.Get("garage.use") : topper.Price.ToString());
            }
        }

        private GameTuning.TopperDefinition FindTopper(string id)
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
            if (garageStage != null) {
                hero.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(
                    garageStage.ShowCar(selectedCar, ViewModel.SelectedColor, ViewModel.SelectedTopper)));
            }
            else {
                preview.Set(selectedCar, ViewModel.SelectedColor);
            }

            previewName.text = GameText.Car(selectedCar);
            previewNameFace.text = GameText.Car(selectedCar);
            for (var id = 0; id < colors.Length; id++) {
                colors[id].EnableInClassList("garage__color--selected", id == ViewModel.SelectedColor);
            }

            foreach (var (car, card, portrait) in cards) {
                var selected = car.Id == ViewModel.SelectedCar;
                var unlocked = ViewModel.IsUnlocked(car.Id);
                var forSale = ViewModel.IsForSale(car.Id);
                card.SetState(selected, !unlocked, forSale, forSale && !ViewModel.CanAffordCar(car.Id),
                    selected ? Strings.Get("garage.in_use") : forSale ? ViewModel.PriceOf(car.Id).ToString()
                    : unlocked ? Strings.Get("garage.use") : GameText.Level(car.UnlockLevel));
                card.Button.tooltip = unlocked ? GameText.Car(car) : Strings.Format("garage.unlock_at", car.UnlockLevel);
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
            PopHero();
        }

        private void Select(CarCatalog.Entry car, Button button)
        {
            if (!IsShown || isClosing) {
                return;
            }

            // A tap on a car the player cannot have yet still answers: it shakes and says what opens it.
            if (!ViewModel.IsUnlocked(car.Id)) {
                Refuse(button);
                ShowToast(button, Strings.Format("garage.unlock_at", car.UnlockLevel), false, false);
                return;
            }

            // An open car with a price is bought by the click, when the coins pay for it.
            if (ViewModel.IsForSale(car.Id)) {
                var price = ViewModel.PriceOf(car.Id);
                if (!ViewModel.CanAffordCar(car.Id)) {
                    TooDear(button, price);
                    return;
                }

                if (!ViewModel.BuyCar(car.Id)) {
                    return;
                }

                Bought(button, price);
                PopHero();
            }
            else {
                if (ViewModel.Select(car.Id)) {
                    PopHero();
                }

                audioService.PlayUiClick();
            }

            RefreshSelection();
            StartBounce(button);
        }

        /// <summary>The coins went: a ring of the till, the price rising off the card and the coins bumping.</summary>
        private void Bought(VisualElement card, int price)
        {
            audioService.PlayUiClick();
            if (audioService.Library != null) {
                audioService.Play(audioService.Library.KnockoutCredit);
            }

            ShowToast(card, $"-{price}", true, true);
            BumpCoins(false);
        }

        /// <summary>Not enough coins: the card shakes, says how many are missing, and the coins flash red.</summary>
        private void TooDear(VisualElement card, int price)
        {
            Refuse(card);
            ShowToast(card, Strings.Format("garage.need_more", Mathf.Max(1, price - ViewModel.Coins)), true, false);
            BumpCoins(true);
        }

        private void Refuse(VisualElement card)
        {
            cardTween.Play(ShakeDuration, t => {
                card.style.translate = new Translate(28f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
            });
        }

        private void StartBounce(VisualElement button)
        {
            cardTween.Play(BounceDuration, t => {
                button.style.translate = new Translate(0f, -22f * Mathf.Sin(Mathf.PI * t));
            });
        }

        /// <summary>The hero answers every change of car, paint or topper with a little hop on its plinth.</summary>
        private void PopHero()
        {
            heroTween.Play(PopDuration, t => {
                var hop = Mathf.Sin(Mathf.PI * t);
                stage.style.scale = new Scale(new Vector3(1f + 0.08f * hop, 1f + 0.08f * hop, 1f));
                stage.style.translate = new Translate(0f, -18f * hop);
            });
        }

        private void BumpCoins(bool isShort)
        {
            coinTween.Stop();
            coinPill.EnableInClassList("garage__coins--short", isShort);
            coinTween.Play(isShort ? ShakeDuration : PopDuration, t => {
                if (isShort) {
                    coinPill.style.translate = new Translate(16f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
                    coinPill.style.scale = StyleKeyword.Null;
                }
                else {
                    var bump = 1f + 0.18f * Mathf.Sin(Mathf.PI * t);
                    coinPill.style.scale = new Scale(new Vector3(bump, bump, 1f));
                    coinPill.style.translate = StyleKeyword.Null;
                }

                if (t >= 1f) {
                    coinPill.RemoveFromClassList("garage__coins--short");
                }
            });
        }

        /// <summary>
        /// The floating line over a card: pops up, rises and fades. Gold for coins, white for a reason; placed over the
        /// card and kept on the screen.
        /// </summary>
        private void ShowToast(VisualElement card, string message, bool withCoin, bool isGold)
        {
            toastTween.Stop();
            toastText.text = message;
            toastFace.text = message;
            toastCoin.style.display = withCoin ? DisplayStyle.Flex : DisplayStyle.None;
            toast.EnableInClassList("garage__toast--gold", isGold);
            var center = toast.parent.WorldToLocal(card.worldBound.center);
            var width = toast.parent.layout.width;
            toast.style.left = Mathf.Clamp(center.x - ToastWidth / 2f, 0f, Mathf.Max(0f, width - ToastWidth));
            toast.style.top = toast.parent.WorldToLocal(new Vector2(0f, card.worldBound.yMin)).y - ToastHeight + 20f;
            toast.style.display = DisplayStyle.Flex;
            toastTween.Play(ToastDuration, t => {
                var pop = t < 0.15f ? Mathf.Lerp(0.4f, 1.1f, t / 0.15f) : Mathf.Lerp(1.1f, 1f, Mathf.Clamp01((t - 0.15f) / 0.1f));
                toast.style.scale = new Scale(new Vector3(pop, pop, 1f));
                toast.style.translate = new Translate(0f, -90f * (1f - (1f - t) * (1f - t)));
                toast.style.opacity = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                if (t >= 1f) {
                    toast.style.display = DisplayStyle.None;
                }
            });
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

        private void StopAnimations()
        {
            cardTween.Stop();
            heroTween.Stop();
            toastTween.Stop();
            coinTween.Stop();
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            isClosing = true;
            StopAnimations();
            await base.HideAsync(token);
            garageStage?.Hide();
        }

        public override void Dispose()
        {
            StopAnimations();
            garageStage?.Hide();
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

        /// <summary>
        /// One card of the shop, a car or a topper: its picture, its name and the tag under them, which carries the
        /// state: a check and IN USE, USE, a coin and the price (gold when the coins pay, grey when not), or a lock and
        /// the level that opens it.
        /// </summary>
        private sealed class Card
        {
            public readonly Button Button;
            private readonly Label status;
            private readonly VisualElement coin;
            private readonly VisualElement padlock;

            public Card(string name, VisualElement art, LiveText text, Func<string> title)
            {
                Button = new Button { name = name };
                Button.RemoveFromClassList(Button.ussClassName);
                Button.AddToClassList("garage__card");
                var frame = new VisualElement { pickingMode = PickingMode.Ignore };
                frame.AddToClassList("garage__art");
                frame.Add(art);
                Button.Add(frame);
                var label = new Label { pickingMode = PickingMode.Ignore };
                text.Set(label, title);
                label.AddToClassList("garage__name");
                Button.Add(label);
                var tag = new VisualElement { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("pill");
                tag.AddToClassList("garage__tag");
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("garage__check");
                tag.Add(check);
                padlock = Padlock();
                tag.Add(padlock);
                coin = RoundOverMeta.Coin();
                coin.AddToClassList("garage__price-coin");
                tag.Add(coin);
                status = new Label { pickingMode = PickingMode.Ignore };
                status.AddToClassList("garage__tag-text");
                tag.Add(status);
                Button.Add(tag);
            }

            public void SetState(bool selected, bool locked, bool forSale, bool dear, string label)
            {
                Button.EnableInClassList(SelectedClass, selected);
                Button.EnableInClassList(LockedClass, locked);
                Button.EnableInClassList(PriceClass, forSale && !selected);
                Button.EnableInClassList(DearClass, dear && !selected);
                coin.style.display = forSale && !selected ? DisplayStyle.Flex : DisplayStyle.None;
                padlock.style.display = locked ? DisplayStyle.Flex : DisplayStyle.None;
                status.text = label;
            }

            /// <summary>A small padlock of elements: a navy shackle over a gold body.</summary>
            private static VisualElement Padlock()
            {
                var root = new VisualElement { pickingMode = PickingMode.Ignore };
                root.AddToClassList("garage__lock");
                var shackle = new VisualElement { pickingMode = PickingMode.Ignore };
                shackle.AddToClassList("garage__lock-shackle");
                var lockBody = new VisualElement { pickingMode = PickingMode.Ignore };
                lockBody.AddToClassList("garage__lock-body");
                root.Add(shackle);
                root.Add(lockBody);
                return root;
            }
        }

        /// <summary>
        /// One running animation on unscaled time: a new one replaces it, and stopping it puts its last frame in place,
        /// so nothing is left mid-way when the garage closes.
        /// </summary>
        private sealed class Tween
        {
            private CancellationTokenSource cancel;
            private Action<float> step;

            public void Play(float duration, Action<float> next)
            {
                Stop();
                step = next;
                cancel = new CancellationTokenSource();
                Run(duration, next, cancel.Token).Forget();
            }

            public void Stop()
            {
                if (cancel == null) {
                    return;
                }

                cancel.Cancel();
                cancel.Dispose();
                cancel = null;
                step?.Invoke(1f);
                step = null;
            }

            private static async UniTaskVoid Run(float duration, Action<float> step, CancellationToken token)
            {
                for (var time = 0f; time < duration; time += Time.unscaledDeltaTime) {
                    step(time / duration);
                    if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) {
                        return;
                    }
                }

                step(1f);
            }
        }
    }
}
