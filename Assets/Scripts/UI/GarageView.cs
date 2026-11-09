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
    /// The garage, a full-screen popup over the menu or the result, built in code like the menu (GarageB2 in Claude
    /// Design): the league's sky with the drifting checker, a top bar (back, GARAGE, the coins, the gear), the car on
    /// the menu's 3D pedestal under the turning light with its name and stats, and below it (beside it sideways) the
    /// CARS / HATS / PAINT switcher over one panel of cards and a single bottom button.
    /// <para>
    /// A tap on a card puts it on the pedestal, whatever it is; the bottom button then says what can be done with what
    /// stands there: SELECTED, SELECT, BUY with the price (grey when the coins are short), the level that opens it, or
    /// UNLOCK for an ad car (BUGGY). BUY and UNLOCK ask once more in a window of their own before the coins or the ad
    /// go. The garage has no PLAY: back goes to the menu or the result, whose PLAY starts the round.
    /// </para>
    /// <para>
    /// Keys: 1 2 3 switch the tabs, Space or Enter presses the bottom button (in a window, its main button), X watches
    /// the ad in the ad window, Esc closes the window and then the garage.
    /// </para>
    /// </summary>
    public sealed class GarageView : FadingView<GarageViewModel>
    {
        private const string WideClass = "garage--wide";
        private const string DesktopClass = "garage--desktop";

        // A screen at least this much wider than tall lays out sideways, as the menu does.
        private const float WideAspect = 1.2f;
        // On a phone held sideways the panel is small: the groups grow so every button stays a thumb wide.
        private const float TouchWideScale = 1.4f;
        private const float Edge = 40f;
        private const float PillGap = 18f;
        private const float TouchWideSide = 240f;
        // The top bar's height and the gap under it, reference pixels.
        private const float TopHeight = 112f;
        private const float TopGap = 30f;
        // Upright: the tabs, the gaps and the bottom button under the panel, and the least the car keeps above them.
        private const float SideChrome = 124f + 24f + ActionRoom;
        private const float ActionRoom = 30f + 190f;
        // The stats pill and its gap under the car's name.
        private const float StatsRoom = 170f;
        private const float ShowMin = 580f;
        private const float PanelMin = 440f;
        private const float PanelMax = 980f;
        // Sideways the switcher, the panel and the button take this share of the width at the right.
        private const float WideSideShare = 0.4f;
        private const float WideGap = 60f;
        private const float HeroMaxWidth = 1300f;
        private const float WideHeroMaxWidth = 1900f;
        private const float RaysScale = 1.7f;
        private const float RaysTurnSeconds = 24f;

        private const int Columns = 3;
        // Sideways too: four columns made the cards too small to read on a phone.
        private const int WideColumns = 3;
        private const float CardGap = 30f;
        // Squarer cards, so upright two full rows show under the car, with the next row's top peeking.
        private const float CardAspect = 1f;
        // Under each paint the room its name takes (garage__paint margin-bottom).
        private const float PaintNameRoom = 64f;

        private const float BounceDuration = 0.3f;
        private const float ShakeDuration = 0.35f;
        private const float PopDuration = 0.32f;
        private const float ToastDuration = 0.9f;

        private static readonly Color CheckerColor = new(1f, 1f, 1f, 0.07f);
        private const float CheckerShare = 1f / 6f;
        // The confirm window's picture is small, so its checker is brighter and its squares a bigger share of it.
        private static readonly Color SheetChecker = new(1f, 1f, 1f, 0.12f);
        private const float SheetCheckerShare = 0.16f;

        private enum Tab
        {
            Cars,
            Hats,
            Paint,
        }

        // What the bottom button does with what stands on the pedestal.
        private enum Act
        {
            Selected,
            Select,
            Buy,
            Dear,
            Locked,
            Ad,
        }

        private static readonly string[] StatNames = { "speed", "power", "weight", "boost" };

        private readonly IAudioService audioService;
        private readonly HudControls controls;
        private readonly LiveText text = new();
        private readonly List<(CarCatalog.Entry Car, Card Card, CarPortrait Portrait)> carCards = new();
        private readonly List<(string Id, Card Card)> hatCards = new();
        private readonly Button[] paints = new Button[CarPaint.Count];
        private readonly Button[] tabs = new Button[3];
        private readonly Tween cardTween = new();
        private readonly Tween heroTween = new();
        private readonly Tween toastTween = new();
        private readonly Tween coinTween = new();
        private readonly Tween actionTween = new();
        private CancellationTokenSource lifetime;
        private bool isTouch;
        private bool isWide;
        private SafeInsets insets;
        private bool isClosing;
        private Tab tab = Tab.Cars;
        // What stands on the pedestal in place of the player's own choice after a tap on its card; null shows the choice.
        private CarCatalog.Entry previewedCar;
        private string previewedHat;

        private VisualElement top;
        private VisualElement coinsPill;
        private Label coins;
        private VisualElement body;
        private VisualElement show;
        private VisualElement rays;
        private VisualElement hero;
        private VisualElement heroPortraitRoot;
        private CarPortrait heroPortrait;
        private VisualElement caption;
        private Label[] nameLabels;
        private VisualElement stats;
        private VisualElement side;
        private VisualElement hatsDot;
        private VisualElement panel;
        private ScrollView scroll;
        private VisualElement carGrid;
        private VisualElement hatGrid;
        private VisualElement paintGrid;
        private Button action;
        private VisualElement actionTick;
        private VisualElement actionLock;
        private VisualElement actionPlay;
        private Label[] actionLabels;
        private Label actionPlain;
        private VisualElement actionPrice;
        private Label actionPriceCount;
        private VisualElement actionTag;
        private VisualElement actionKey;
        private VisualElement toast;
        private Label[] toastLabels;
        private VisualElement toastCoin;
        private GarageStage garageStage;
        private float heroAspect = 1.6f;
        private Texture heroTexture;

        // The window that asks before BUY or UNLOCK.
        private VisualElement confirm;
        private VisualElement sheet;
        private Label[] sheetTitle;
        private VisualElement sheetArt;
        private VisualElement sheetPortraitRoot;
        private CarPortrait sheetPortrait;
        private VisualElement sheetHat;
        private VisualElement sheetStats;
        private VisualElement sheetCoins;
        private Label sheetBefore;
        private Label sheetAfter;
        private VisualElement sheetFree;
        private Button sheetBuy;
        private Label[] sheetBuyLabels;
        private VisualElement sheetBuyCoin;
        private VisualElement sheetBuyPrice;
        private Label[] sheetBuyPriceLabels;
        private Button sheetWatch;
        private bool isConfirmOpen;
        private bool isCelebrating;
        private CarCatalog.Entry confirmCar;
        private GameTuning.TopperDefinition confirmHat;

        public GarageView(IAudioService audioService, HudControls controls)
        {
            this.audioService = audioService;
            this.controls = controls;
        }

        // The screen's own element, which carries the style sheets: the root UIKit gives the view, or under it.
        private VisualElement Screen => Root.name == "garage" ? Root : Root.Q<VisualElement>("garage") ?? Root;

        // Before it is up, while it closes, under the settings or while an ad plays, the garage takes no presses.
        private bool IsBusy => !IsShown || isClosing || ViewModel.IsSettingsOpen || ViewModel.IsAdPlaying;

        protected override void OnAttached()
        {
            lifetime = new CancellationTokenSource();
            Root.focusable = true;
            Root.delegatesFocus = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Root.RegisterCallback<NavigationCancelEvent>(OnCancel);
            isTouch = controls != null && controls.CheckTouchScreen();
            var screen = Screen;
            screen.EnableInClassList(DesktopClass, !isTouch);
            screen.style.backgroundImage = new StyleBackground(LeagueLook.SkyOf(ViewModel.League));
            DriftingChecker.AddTo(screen, CheckerColor, CheckerShare);
            garageStage = ViewModel.Stage;

            body = Element("garage__body");
            screen.Add(body);
            BuildShow();
            BuildSide();
            BuildTop(screen);
            BuildToast(screen);
            BuildConfirm(screen);

            Root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            SafeArea.Watch(Root, safe => {
                insets = safe;
                Place();
            });
            PokiPill.Watch(Root, Place);
            ViewModel.ToppersShown();
            ShowTab(Tab.Cars);
            // The settings popup can change the language over the garage.
            text.OnChanged(Refresh);
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            await base.ShowAsync(token);
            Root.Focus();
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
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            text.Dispose();
            carCards.Clear();
            hatCards.Clear();
            base.Dispose();
        }

        // -- Building ------------------------------------------------------------------------------------------------

        private void BuildTop(VisualElement screen)
        {
            top = Element("garage__top");
            var back = Sticker("garage__back", Back);
            back.Q<VisualElement>(className: "menu__face").Add(Icon("garage__back-arrow", DrawBack));
            top.Add(back);
            var title = RoundOverProgress.Outlined("", "garage__title", out var titleLabels);
            foreach (var label in titleLabels) {
                text.Set(label, () => Strings.Get("garage.title"));
            }

            top.Add(title);
            top.Add(Element("menu__spacer"));
            coinsPill = Element("menu__coins");
            coinsPill.AddToClassList("garage__coins");
            coinsPill.Add(Element("menu__coins-drop"));
            var coinFace = Element("menu__coins-face");
            coinFace.Add(RoundOverMeta.Coin());
            coins = Text("menu__coins-count");
            coinFace.Add(coins);
            coinsPill.Add(coinFace);
            top.Add(coinsPill);
            var gear = Sticker("menu__gear", OpenSettings);
            gear.Q<VisualElement>(className: "menu__face").Add(Gear());
            top.Add(gear);
            screen.Add(top);
        }

        /// <summary>The car on its pedestal under the turning light, and its name and stats under it.</summary>
        private void BuildShow()
        {
            show = Element("garage__show");
            rays = Element("garage__rays");
            rays.generateVisualContent += MenuView.DrawLight;
            // Unscaled clock, so the ad pause (timeScale 0) does not stop the light.
            rays.schedule.Execute(() =>
                rays.style.rotate = new Rotate(Time.unscaledTime / RaysTurnSeconds * 360f % 360f)).Every(16);
            show.Add(rays);
            hero = Element("garage__hero");
            heroPortraitRoot = Element("garage__hero-portrait");
            heroPortraitRoot.AddToClassList("car-portrait");
            hero.Add(heroPortraitRoot);
            heroPortrait = new CarPortrait(heroPortraitRoot);
            show.Add(hero);

            caption = Element("garage__caption");
            caption.Add(RoundOverProgress.Outlined("", "garage__name", out nameLabels));
            stats = BuildStats("garage__stats");
            caption.Add(stats);
            show.Add(caption);
            caption.RegisterCallback<GeometryChangedEvent>(_ => PlaceHero());
            show.RegisterCallback<GeometryChangedEvent>(_ => PlaceHero());
            body.Add(show);
        }

        /// <summary>
        /// The stats as one white pill: each stat's name over five pips, filled to its value in the stat's colour, and
        /// beside the name how much a car tried on the pedestal has more or less than the chosen one.
        /// </summary>
        private static VisualElement BuildStats(string className)
        {
            var pill = Element("garage-stats");
            pill.AddToClassList(className);
            foreach (var name in StatNames) {
                var stat = Element("garage-stats__stat");
                stat.AddToClassList($"garage-stats__stat--{name}");
                var head = Element("garage-stats__head");
                head.Add(Text("garage-stats__label"));
                var diff = Element("garage-stats__diff");
                diff.Add(Icon("garage-stats__arrow", DrawDiffArrow));
                diff.Add(Text("garage-stats__diff-text"));
                head.Add(diff);
                stat.Add(head);
                var pips = Element("garage-stats__pips");
                for (var i = 0; i < GameTuning.CarStatSettings.Max; i++) {
                    var pip = Element("garage-stats__pip");
                    // USS has no :first-child, so the round ends of the track are classes.
                    pip.EnableInClassList("garage-stats__pip--first", i == 0);
                    pip.EnableInClassList("garage-stats__pip--last", i == GameTuning.CarStatSettings.Max - 1);
                    pips.Add(pip);
                }

                stat.Add(pips);
                pill.Add(stat);
            }

            return pill;
        }

        /// <summary>
        /// Fills the pill with a car's stats. With <paramref name="against"/> (the chosen car, while another is tried
        /// on) each stat also says what changes: the pips it would add pale, those it would lose outlined in red, and
        /// ▲N or ▼N beside its name.
        /// </summary>
        private void SetStats(VisualElement pill, GameTuning.CarStatLine line, GameTuning.CarStatLine against = null)
        {
            var values = new[] { line.Speed, line.Power, line.Weight, line.Boost };
            var bases = against == null ? values : new[] { against.Speed, against.Power, against.Weight, against.Boost };
            for (var i = 0; i < StatNames.Length; i++) {
                var stat = pill[i];
                // Without boosts in the build the boost stat would promise nothing.
                stat.style.display = StatNames[i] != "boost" || ViewModel.HasBoost ? DisplayStyle.Flex : DisplayStyle.None;
                stat.Q<Label>(className: "garage-stats__label").text = Strings.Get($"car.stat_{StatNames[i]}");
                var change = values[i] - bases[i];
                var diff = stat.Q<VisualElement>(className: "garage-stats__diff");
                Show(diff, change != 0);
                diff.EnableInClassList("garage-stats__diff--up", change > 0);
                diff.EnableInClassList("garage-stats__diff--down", change < 0);
                diff.Q<Label>(className: "garage-stats__diff-text").text = Mathf.Abs(change).ToString();
                diff.Q<VisualElement>(className: "garage-stats__arrow").MarkDirtyRepaint();
                var pips = stat.Q<VisualElement>(className: "garage-stats__pips");
                for (var pip = 0; pip < pips.childCount; pip++) {
                    pips[pip].EnableInClassList("garage-stats__pip--on", pip < values[i]);
                    pips[pip].EnableInClassList("garage-stats__pip--gain", pip >= bases[i] && pip < values[i]);
                    pips[pip].EnableInClassList("garage-stats__pip--loss", pip >= values[i] && pip < bases[i]);
                }
            }
        }

        private void BuildSide()
        {
            side = Element("garage__side");
            var switcher = Element("garage__tabs");
            switcher.Add(Element("menu__drop"));
            var face = Element("garage__tabs-face");
            var keys = new[] { "garage.cars", "garage.toppers", "garage.paint" };
            for (var i = 0; i < tabs.Length; i++) {
                var shown = (Tab)i;
                var key = keys[i];
                var button = new Button(() => PressTab(shown));
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__tab");
                // A navy line between the segments (USS has no :first-child).
                button.EnableInClassList("garage__tab--split", i > 0);
                button.focusable = false;
                // Two layers, as the titles: the navy outline under a clean face, so the white of the chosen tab stays
                // white (one outlined label eats its own fill at this size).
                button.Add(RoundOverProgress.Outlined("", "garage__tab-label", out var tabLabels));
                foreach (var label in tabLabels) {
                    text.Set(label, () => Strings.Get(key));
                }
                button.Add(KeyCap((i + 1).ToString(), "garage__tab-key"));
                if (shown == Tab.Hats) {
                    hatsDot = Element("garage__tab-dot");
                    button.Add(hatsDot);
                }

                tabs[i] = button;
                face.Add(button);
            }

            switcher.Add(face);
            side.Add(switcher);

            panel = Element("garage__panel");
            scroll = new ScrollView(ScrollViewMode.Vertical) {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden,
                // Clamped, not Elastic: Elastic unclamps the scrollers, so a wheel tick past either end pushes the
                // grid out and the spring pulls it back, which jerks. A finger still drags and flicks with inertia.
                touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped,
            };
            scroll.AddToClassList("garage__scroll");
            panel.Add(scroll);
            BuildCars();
            BuildHats();
            BuildPaints();
            panel.RegisterCallback<GeometryChangedEvent>(_ => LayoutCards());
            side.Add(panel);

            BuildAction();
            side.Add(action);
            body.Add(side);
        }

        private void BuildCars()
        {
            carGrid = Element("garage__grid");
            foreach (var car in ViewModel.Cars) {
                var art = Element("car-portrait");
                art.AddToClassList("garage__card-portrait");
                var portrait = new CarPortrait(art);
                var card = new Card($"garage-car-{car.Id}", art, text, () => GameText.Car(car));
                card.Button.clicked += () => PickCar(car, card.Button);
                carCards.Add((car, card, portrait));
                carGrid.Add(card.Button);
            }

            scroll.Add(carGrid);
        }

        private void BuildHats()
        {
            hatGrid = Element("garage__grid");
            var ids = new List<string> { "" };
            foreach (var topper in ViewModel.Toppers) {
                ids.Add(topper.Id);
            }

            // The hats' 3D models, all drawn into one texture that each card crops to its own; NONE keeps its icon.
            var models = garageStage?.ShowToppers(ids.GetRange(1, ids.Count - 1));
            foreach (var id in ids) {
                var hatId = id;
                VisualElement art;
                if (models != null && id.Length > 0) {
                    art = new Image {
                        image = models,
                        uv = garageStage.CellOf(id),
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore,
                    };
                    art.AddToClassList("garage__card-hat");
                }
                else {
                    art = TopperIcon.Create(id, "garage__card-hat");
                }

                var card = new Card($"garage-topper-{(id.Length == 0 ? "none" : id)}", art, text,
                    () => hatId.Length == 0 ? Strings.Get("garage.none") : GameText.Topper(hatId));
                card.Button.clicked += () => PickHat(hatId, card.Button);
                hatCards.Add((id, card));
                hatGrid.Add(card.Button);
            }

            scroll.Add(hatGrid);
        }

        /// <summary>The eight paints as big round stickers, two rows of four; a tap paints the car at once.</summary>
        private void BuildPaints()
        {
            paintGrid = Element("garage__paints");
            for (var id = 0; id < paints.Length; id++) {
                var colorId = id;
                var button = new Button { name = $"garage-color-{CarPaint.NameOf(id)}" };
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("garage__paint");
                button.focusable = false;
                button.tooltip = CarPaint.NameOf(id).ToUpperInvariant();
                button.Add(Element("garage__paint-drop"));
                var dot = Element("garage__paint-dot");
                dot.AddToClassList(CarColor.ClassOf(id));
                // The dark paint on its navy rim reads as a hole: a light ring inside keeps it a paint.
                if (CarPaint.NameOf(id) == "dark") {
                    dot.Add(Element("garage__paint-ring"));
                }

                dot.Add(Element("garage__paint-shade"));
                dot.Add(Element("garage__paint-check"));
                button.Add(dot);
                // Its name under it, so close shades (orange and yellow, purple and pink) are told apart.
                var name = Text("garage__paint-name");
                var key = $"paint.{CarPaint.NameOf(id)}";
                text.Set(name, () => Strings.Get(key));
                button.Add(name);
                button.clicked += () => PickPaint(colorId, button);
                paints[id] = button;
                paintGrid.Add(button);
            }

            panel.Add(paintGrid);
        }

        /// <summary>
        /// The one button at the bottom, drawn as the game's big buttons (a coloured face over its darker shade), its
        /// colour and content set by what stands on the pedestal (RefreshAction).
        /// </summary>
        private void BuildAction()
        {
            action = new Button(PressAction) { name = "garage-action" };
            action.RemoveFromClassList(Button.ussClassName);
            action.AddToClassList("button");
            action.AddToClassList("garage__action");
            action.focusable = false;
            action.Add(Element("button__face"));
            var face = action;
            actionTick = Element("garage__action-tick");
            face.Add(actionTick);
            actionLock = Icon("garage__action-lock", DrawWhiteLock);
            face.Add(actionLock);
            actionPlay = RoundOverRewards.PlaySign();
            actionPlay.AddToClassList("garage__action-play");
            face.Add(actionPlay);
            face.Add(RoundOverProgress.Outlined("", "garage__action-label", out actionLabels));
            foreach (var label in actionLabels) {
                label.AddToClassList("button__label");
            }

            actionPlain = Text("garage__action-plain");
            actionPlain.AddToClassList("button__label");
            face.Add(actionPlain);
            actionPrice = Element("garage__action-price");
            actionPrice.Add(RoundOverMeta.Coin());
            actionPriceCount = Text("garage__action-count");
            actionPrice.Add(actionPriceCount);
            face.Add(actionPrice);
            actionTag = Text("garage__action-tag");
            text.Set((Label)actionTag, () => Strings.Get("result.ad"));
            action.Add(actionTag);
            actionKey = Element("key");
            actionKey.AddToClassList("garage__action-key");
            var keyText = Text("key__text");
            text.Set(keyText, () => Strings.Get("common.space"));
            actionKey.Add(keyText);
            action.Add(actionKey);
        }

        private void BuildToast(VisualElement screen)
        {
            toast = Element("garage__toast");
            toastCoin = RoundOverMeta.Coin();
            toastCoin.AddToClassList("garage__toast-coin");
            toast.Add(toastCoin);
            toast.Add(RoundOverProgress.Outlined("", "garage__toast-label", out toastLabels));
            toast.style.display = DisplayStyle.None;
            screen.Add(toast);
        }

        /// <summary>
        /// The window that asks before the coins or the ad go: the title, the car (or the car in the hat) as on the
        /// pedestal, its stats, the coins before and after (or FREE), the main button and CANCEL.
        /// </summary>
        private void BuildConfirm(VisualElement screen)
        {
            confirm = Element("menu__popup");
            confirm.AddToClassList("garage__confirm");
            confirm.pickingMode = PickingMode.Position;
            // A tap on the dim around the card closes it.
            confirm.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == confirm) {
                    CloseConfirm(true);
                }
            });
            sheet = Element("card");
            sheet.AddToClassList("menu__sheet");
            sheet.AddToClassList("garage__sheet");
            sheet.pickingMode = PickingMode.Position;
            // As the design: a plain navy question in the middle, the cross in the corner.
            var title = Text("garage__sheet-title");
            sheetTitle = new[] { title };
            title.RegisterCallback<GeometryChangedEvent>(_ => RefitTitle());
            sheet.Add(title);
            var close = MenuView.CloseSticker(() => CloseConfirm(true));
            close.AddToClassList("garage__sheet-close");
            sheet.Add(close);

            // The car in a framed piece of the garage's sky: the checker and the turning light behind it.
            var frame = Element("garage__sheet-frame");
            frame.style.backgroundImage = new StyleBackground(LeagueLook.SkyOf(ViewModel.League));
            DriftingChecker.AddTo(frame, SheetChecker, SheetCheckerShare);
            var light = Element("garage__sheet-light");
            light.generateVisualContent += MenuView.DrawLight;
            light.schedule.Execute(() =>
                light.style.rotate = new Rotate(Time.unscaledTime / RaysTurnSeconds * 360f % 360f)).Every(16);
            frame.Add(light);
            sheetArt = Element("garage__sheet-art");
            sheetPortraitRoot = Element("car-portrait");
            sheetPortraitRoot.AddToClassList("garage__sheet-portrait");
            sheetArt.Add(sheetPortraitRoot);
            sheetPortrait = new CarPortrait(sheetPortraitRoot);
            frame.Add(sheetArt);
            sheet.Add(frame);
            sheetStats = BuildStats("garage__sheet-stats");
            sheet.Add(sheetStats);

            sheetCoins = Element("garage__sheet-coins");
            sheetBefore = CoinPill(sheetCoins, "garage__sheet-pill");
            sheetCoins.Add(Icon("garage__sheet-arrow", DrawArrow));
            sheetAfter = CoinPill(sheetCoins, "garage__sheet-pill garage__sheet-pill--after");
            sheet.Add(sheetCoins);
            sheetFree = Element("garage__sheet-free");
            sheetFree.Add(RoundOverRewards.PlaySign());
            var free = Text("garage__sheet-free-text");
            text.Set(free, () => Strings.Get("garage.free"));
            sheetFree.Add(free);
            sheet.Add(sheetFree);

            sheetBuy = new Button(Confirm);
            sheetBuy.RemoveFromClassList(Button.ussClassName);
            sheetBuy.AddToClassList("button");
            sheetBuy.AddToClassList("garage__sheet-buy");
            sheetBuy.focusable = false;
            sheetBuy.Add(Element("button__face"));
            var buyLabel = RoundOverProgress.Outlined("", "garage__sheet-buy-label", out sheetBuyLabels);
            foreach (var label in sheetBuyLabels) {
                label.AddToClassList("button__label");
                text.Set(label, () => Strings.Get("result.buy"));
            }

            sheetBuy.Add(buyLabel);
            sheetBuyCoin = RoundOverMeta.Coin();
            sheetBuyCoin.AddToClassList("garage__sheet-buy-coin");
            sheetBuy.Add(sheetBuyCoin);
            // Two layers, as BUY beside it: one outlined label eats the fill of its own digits at this size.
            sheetBuyPrice = RoundOverProgress.Outlined("", "garage__sheet-buy-price", out sheetBuyPriceLabels);
            sheetBuy.Add(sheetBuyPrice);
            sheetBuy.Add(KeyCap(null, "garage__sheet-key", () => Strings.Get("common.space")));
            sheet.Add(sheetBuy);

            sheetWatch = new Button(Confirm);
            sheetWatch.RemoveFromClassList(Button.ussClassName);
            sheetWatch.AddToClassList("button");
            sheetWatch.AddToClassList("button--purple");
            sheetWatch.AddToClassList("menu__watch");
            sheetWatch.AddToClassList("garage__sheet-watch");
            sheetWatch.focusable = false;
            sheetWatch.Add(Element("button__face"));
            sheetWatch.Add(RoundOverRewards.PlaySign());
            var watchLabel = RoundOverProgress.Outlined("", "menu__watch-label", out var watchLabels);
            foreach (var label in watchLabels) {
                label.AddToClassList("button__label");
                text.Set(label, () => Strings.Get("garage.watch_ad"));
            }

            FitText.Fit(watchLabel, watchLabels);
            sheetWatch.Add(watchLabel);
            sheetWatch.Add(KeyCap("X", "garage__sheet-watch-key"));
            var tag = Text("menu__watch-tag");
            text.Set(tag, () => Strings.Get("result.ad"));
            sheetWatch.Add(tag);
            sheet.Add(sheetWatch);

            var cancel = new Button(() => CloseConfirm(true));
            cancel.RemoveFromClassList(Button.ussClassName);
            cancel.AddToClassList("button");
            cancel.AddToClassList("button--white");
            cancel.AddToClassList("garage__sheet-cancel");
            cancel.focusable = false;
            cancel.Add(Element("button__face"));
            var cancelLabel = Text("button__label");
            cancelLabel.AddToClassList("garage__sheet-cancel-label");
            text.Set(cancelLabel, () => Strings.Get("garage.cancel"));
            cancel.Add(cancelLabel);
            cancel.Add(KeyCap("ESC", "garage__sheet-cancel-key"));
            sheet.Add(cancel);

            confirm.Add(sheet);
            screen.Add(confirm);
        }

        /// <summary>
        /// Keeps the question on one line between the cross and the other edge: a long name or language (BUY
        /// SPEEDSTER? in Russian) shrinks it, down to half its size, rather than running under the cross.
        /// </summary>
        private void FitTitle()
        {
            sheetTitle[0].style.fontSize = StyleKeyword.Null;
            sheetTitle[0].schedule.Execute(RefitTitle);
        }

        /// <summary>
        /// Down to the width the title has once it is laid out; also on its first layout, as the window opens hidden.
        /// </summary>
        private void RefitTitle()
        {
            var title = sheetTitle[0];
            var width = title.contentRect.width;
            var size = title.resolvedStyle.fontSize;
            if (float.IsNaN(width) || width <= 0f || size <= 0f || string.IsNullOrEmpty(title.text)) {
                return;
            }

            var text = title.MeasureTextSize(title.text, 0f, VisualElement.MeasureMode.Undefined, 0f,
                VisualElement.MeasureMode.Undefined).x;
            if (text > width) {
                title.style.fontSize = Mathf.Max(size * 0.5f, size * width * 0.96f / text);
            }
        }

        private Label CoinPill(VisualElement parent, string classNames)
        {
            var pill = Element(null);
            foreach (var name in classNames.Split(' ')) {
                pill.AddToClassList(name);
            }

            pill.Add(RoundOverMeta.Coin());
            var count = Text("garage__sheet-pill-count");
            pill.Add(count);
            parent.Add(pill);
            return count;
        }

        // -- State ---------------------------------------------------------------------------------------------------

        private CarCatalog.Entry ShownCar => tab == Tab.Cars && previewedCar != null ? previewedCar : ViewModel.SelectedEntry;

        private string ShownHat => tab == Tab.Hats && previewedHat != null ? previewedHat : ViewModel.SelectedTopper;

        private void ShowTab(Tab shown)
        {
            tab = shown;
            previewedCar = null;
            previewedHat = null;
            for (var i = 0; i < tabs.Length; i++) {
                tabs[i].EnableInClassList("garage__tab--on", i == (int)shown);
            }

            carGrid.style.display = shown == Tab.Cars ? DisplayStyle.Flex : DisplayStyle.None;
            hatGrid.style.display = shown == Tab.Hats ? DisplayStyle.Flex : DisplayStyle.None;
            scroll.style.display = shown == Tab.Paint ? DisplayStyle.None : DisplayStyle.Flex;
            paintGrid.style.display = shown == Tab.Paint ? DisplayStyle.Flex : DisplayStyle.None;
            scroll.scrollOffset = Vector2.zero;
            Refresh();
            Place();
            LayoutCards();
        }

        /// <summary>The pedestal, the cards, the paints, the coins and the bottom button as they are now.</summary>
        private void Refresh()
        {
            var car = ShownCar;
            var hat = ShownHat;
            ShowHero(car, hat);
            foreach (var label in nameLabels) {
                label.text = GameText.Car(car);
            }

            // The stats belong to the cars: on the hats and the paints their room goes to the panel.
            Show(stats, tab == Tab.Cars);
            SetStats(stats, ViewModel.StatsOf(car),
                previewedCar != null && tab == Tab.Cars ? ViewModel.StatsOf(ViewModel.SelectedEntry) : null);
            // A paint takes at once, so the paints have no bottom button.
            Show(action, tab != Tab.Paint);
            coins.text = ViewModel.Coins.ToString();
            hatsDot.style.display = ViewModel.CanBuyTopper ? DisplayStyle.Flex : DisplayStyle.None;
            var color = ViewModel.SelectedColor;

            var selectedCar = ViewModel.SelectedCar;
            foreach (var (entry, card, portrait) in carCards) {
                var id = entry.Id;
                var isOwned = ViewModel.IsOwnedCar(id);
                var isAd = ViewModel.IsAdCar(id);
                // Without ads an ad car the player does not own has no way in: it is left out.
                card.Button.style.display = isAd && !isOwned && !ViewModel.CanWatchAds ? DisplayStyle.None : DisplayStyle.Flex;
                var isLocked = !isAd && !ViewModel.IsUnlocked(id);
                portrait.Set(entry, color, isLocked);
                var badge = id == selectedCar ? Card.Badge.Selected
                    : isOwned ? Card.Badge.None
                    : isAd ? Card.Badge.Ad
                    : isLocked ? Card.Badge.Locked
                    : ViewModel.CanAffordCar(id) ? Card.Badge.Price : Card.Badge.Dear;
                // The lock already says "level": the badge carries only its number, so it fits inside the corner.
                card.Set(badge, badge == Card.Badge.Locked ? entry.UnlockLevel.ToString()
                    : badge == Card.Badge.Ad ? Strings.Get("result.ad")
                    : ViewModel.PriceOf(id).ToString(), id == selectedCar, isLocked, entry == previewedCar);
                card.Button.tooltip = isLocked ? Strings.Format("garage.unlock_at", entry.UnlockLevel) : GameText.Car(entry);
            }

            var worn = ViewModel.SelectedTopper;
            foreach (var (id, card) in hatCards) {
                var topper = FindHat(id);
                var isOwned = topper == null || ViewModel.IsOwned(id);
                var badge = id == worn ? Card.Badge.Selected
                    : isOwned ? Card.Badge.None
                    : ViewModel.CanAfford(topper) ? Card.Badge.Price : Card.Badge.Dear;
                card.Set(badge, topper?.Price.ToString(), id == worn, false, id == previewedHat);
            }

            for (var id = 0; id < paints.Length; id++) {
                paints[id].EnableInClassList("garage__paint--on", id == color);
            }

            RefreshAction();
        }

        private void ShowHero(CarCatalog.Entry car, string hat)
        {
            if (garageStage != null && car != null) {
                var texture = garageStage.ShowCar(car, ViewModel.SelectedColor, hat, true,
                    LeagueLook.PedestalOf(ViewModel.League));
                heroTexture = texture;
                heroAspect = (float)texture.width / texture.height;
                hero.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                heroPortraitRoot.style.display = DisplayStyle.None;
            }
            else {
                heroTexture = null;
                hero.style.backgroundImage = StyleKeyword.None;
                heroPortraitRoot.style.display = DisplayStyle.Flex;
                heroPortrait.Set(car, ViewModel.SelectedColor);
            }

            PlaceHero();
        }

        /// <summary>What the bottom button does with what stands on the pedestal.</summary>
        private (Act Act, int Value) ActionNow()
        {
            if (tab == Tab.Paint) {
                return (Act.Selected, 0);
            }

            if (tab == Tab.Hats) {
                var hat = ShownHat ?? "";
                if (hat == (ViewModel.SelectedTopper ?? "")) {
                    return (Act.Selected, 0);
                }

                var topper = FindHat(hat);
                if (topper == null || ViewModel.IsOwned(hat)) {
                    return (Act.Select, 0);
                }

                return (ViewModel.CanAfford(topper) ? Act.Buy : Act.Dear, topper.Price);
            }

            var car = ShownCar;
            if (car.Id == ViewModel.SelectedCar) {
                return (Act.Selected, 0);
            }

            if (ViewModel.IsOwnedCar(car.Id)) {
                return (Act.Select, 0);
            }

            if (ViewModel.IsAdCar(car.Id)) {
                return (Act.Ad, 0);
            }

            if (!ViewModel.IsUnlocked(car.Id)) {
                return (Act.Locked, car.UnlockLevel);
            }

            var price = ViewModel.PriceOf(car.Id);
            return (ViewModel.CanAffordCar(car.Id) ? Act.Buy : Act.Dear, price);
        }

        private void RefreshAction()
        {
            var (act, value) = ActionNow();
            foreach (var kind in new[] { "selected", "select", "buy", "dear", "locked", "ad" }) {
                action.RemoveFromClassList($"garage__action--{kind}");
            }

            action.AddToClassList($"garage__action--{act.ToString().ToLowerInvariant()}");
            var label = act switch {
                Act.Selected => Strings.Get("garage.selected"),
                Act.Select => Strings.Get("garage.select"),
                Act.Locked => GameText.Level(value),
                Act.Ad => Strings.Get("garage.unlock"),
                _ => Strings.Get("result.buy"),
            };
            foreach (var labelText in actionLabels) {
                labelText.text = label;
            }

            actionPlain.text = label;
            var hasPrice = act is Act.Buy or Act.Dear;
            actionPriceCount.text = value.ToString();
            Show(actionPrice, hasPrice);
            Show(actionTick, act == Act.Selected);
            Show(actionLock, act == Act.Locked);
            Show(actionPlay, act == Act.Ad);
            Show(actionTag, act == Act.Ad);
            Show(actionPlain, act == Act.Selected);
            Show(actionLabels[0].parent, act != Act.Selected);
            // The key shows only with a mouse (garage--desktop), and not on a button that does nothing.
            actionKey.EnableInClassList("garage__action-key--off", act == Act.Selected);
        }

        private static void Show(VisualElement element, bool isShown) =>
            element.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;

        private GameTuning.TopperDefinition FindHat(string id)
        {
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            foreach (var topper in ViewModel.Toppers) {
                if (topper.Id == id) {
                    return topper;
                }
            }

            return null;
        }

        // -- Presses -------------------------------------------------------------------------------------------------

        private void PressTab(Tab shown)
        {
            if (IsBusy || isConfirmOpen || shown == tab) {
                return;
            }

            audioService.PlayUiClick();
            if (shown == Tab.Hats) {
                ViewModel.ToppersPressed();
            }

            ShowTab(shown);
        }

        /// <summary>A tap on a car puts it on the pedestal; a tap on the one shown again presses the bottom button.</summary>
        private void PickCar(CarCatalog.Entry car, Button button)
        {
            if (IsBusy || isConfirmOpen) {
                return;
            }

            if (ShownCar == car && previewedCar != null) {
                PressAction();
                return;
            }

            audioService.PlayUiClick();
            previewedCar = car.Id == ViewModel.SelectedCar ? null : car;
            Refresh();
            Bounce(button);
            PopHero();
        }

        private void PickHat(string id, Button button)
        {
            if (IsBusy || isConfirmOpen) {
                return;
            }

            if (previewedHat == id) {
                PressAction();
                return;
            }

            audioService.PlayUiClick();
            previewedHat = id == (ViewModel.SelectedTopper ?? "") ? null : id;
            Refresh();
            Bounce(button);
            PopHero();
        }

        private void PickPaint(int id, Button button)
        {
            if (IsBusy || isConfirmOpen || !ViewModel.SelectColor(id)) {
                return;
            }

            audioService.PlayUiClick();
            Refresh();
            Bounce(button);
            PopHero();
        }

        private void PressAction()
        {
            if (IsBusy || isConfirmOpen) {
                return;
            }

            var (act, value) = ActionNow();
            switch (act) {
                case Act.Selected:
                    Bounce(action);
                    break;
                case Act.Select:
                    audioService.PlayUiClick();
                    var isDone = tab == Tab.Hats ? ViewModel.PickTopper(ShownHat ?? "") : ViewModel.Select(ShownCar.Id);
                    previewedCar = null;
                    previewedHat = null;
                    Refresh();
                    if (isDone) {
                        Bounce(action);
                        PopHero();
                    }

                    break;
                case Act.Buy:
                case Act.Ad:
                    audioService.PlayUiClick();
                    OpenConfirm(act == Act.Ad);
                    break;
                case Act.Dear:
                    Refuse(action);
                    ShowToast(action, Strings.Format("garage.need_more", Mathf.Max(1, value - ViewModel.Coins)), true,
                        false);
                    BumpCoins(true);
                    break;
                case Act.Locked:
                    // The button already reads the level that opens it: it and the card shake, no line repeats it.
                    Refuse(action);
                    ShakeCard(ShownCar);
                    break;
            }
        }

        // -- The window ----------------------------------------------------------------------------------------------

        private void OpenConfirm(bool isAd)
        {
            confirmCar = tab == Tab.Cars ? ShownCar : null;
            confirmHat = tab == Tab.Hats ? FindHat(ShownHat) : null;
            if (confirmCar == null && confirmHat == null) {
                return;
            }

            var name = confirmCar != null ? GameText.Car(confirmCar) : GameText.Topper(confirmHat.Id);
            foreach (var label in sheetTitle) {
                label.text = Strings.Format(isAd ? "garage.unlock_title" : "garage.buy_title", name);
            }

            FitTitle();

            // The picture is the pedestal's, the car (in the hat) turning as it does behind the dim.
            if (heroTexture != null) {
                sheetArt.style.backgroundImage = new StyleBackground(Background.FromRenderTexture((RenderTexture)heroTexture));
                sheetPortraitRoot.style.display = DisplayStyle.None;
            }
            else {
                sheetArt.style.backgroundImage = StyleKeyword.None;
                sheetPortraitRoot.style.display = DisplayStyle.Flex;
                sheetPortrait.Set(ShownCar, ViewModel.SelectedColor);
            }

            Show(sheetStats, confirmCar != null);
            if (confirmCar != null) {
                SetStats(sheetStats, ViewModel.StatsOf(confirmCar));
            }

            var price = confirmCar != null ? ViewModel.PriceOf(confirmCar.Id) : confirmHat.Price;
            sheetBefore.text = ViewModel.Coins.ToString();
            sheetAfter.text = Mathf.Max(0, ViewModel.Coins - price).ToString();
            foreach (var label in sheetBuyPriceLabels) {
                label.text = price.ToString();
            }
            Show(sheetCoins, !isAd);
            Show(sheetBuy, !isAd);
            Show(sheetFree, isAd);
            Show(sheetWatch, isAd);
            if (isAd) {
                ViewModel.AdCarShown(confirmCar.Id);
            }

            isConfirmOpen = true;
            confirm.AddToClassList("menu__popup--open");
        }

        private void CloseConfirm(bool withClick)
        {
            if (!isConfirmOpen || isCelebrating || ViewModel.IsAdPlaying) {
                return;
            }

            if (withClick) {
                audioService.PlayUiClick();
            }

            isConfirmOpen = false;
            confirm.RemoveFromClassList("menu__popup--open");
            Root.Focus();
        }

        /// <summary>The window's main button: BUY pays and wears it, WATCH AD plays the ad and, if it pays, gives the car.</summary>
        private void Confirm()
        {
            if (!isConfirmOpen || isCelebrating || IsBusy) {
                return;
            }

            if (confirmCar != null && ViewModel.IsAdCar(confirmCar.Id)) {
                WatchAd(confirmCar, lifetime.Token).Forget();
                return;
            }

            var isBought = confirmCar != null ? ViewModel.BuyCar(confirmCar.Id) : ViewModel.PickTopper(confirmHat.Id);
            if (!isBought) {
                RewardCelebration.Refuse(confirm, sheet, sheetBuy, Strings.Format("garage.need_more",
                    Mathf.Max(1, (confirmCar != null ? ViewModel.PriceOf(confirmCar.Id) : confirmHat.Price) -
                        ViewModel.Coins)), lifetime.Token);
                return;
            }

            Celebrate(sheetBuyPrice, lifetime.Token).Forget();
        }

        private async UniTaskVoid WatchAd(CarCatalog.Entry car, CancellationToken token)
        {
            audioService.PlayUiClick();
            bool isGranted;
            try {
                isGranted = await ViewModel.WatchAdCar(car.Id, token);
            }
            catch (OperationCanceledException) {
                return;
            }

            if (Root?.panel == null || token.IsCancellationRequested) {
                return;
            }

            if (!isGranted) {
                // No ad came, or it was closed early: the window stays, and says so.
                RewardCelebration.Refuse(confirm, sheet, sheetWatch, Strings.Get("garage.no_ad"), token);
                return;
            }

            Celebrate(null, token).Forget();
        }

        /// <summary>
        /// The reward taken, as on the result's popups: a flash, EQUIPPED! slammed over the picture, confetti and, for
        /// coins, a stream of them into the counter; then the window closes on the new choice.
        /// </summary>
        private async UniTaskVoid Celebrate(VisualElement coinsFrom, CancellationToken token)
        {
            isCelebrating = true;
            if (audioService.Library != null) {
                audioService.Play(audioService.Library.KnockoutCredit);
            }

            previewedCar = null;
            previewedHat = null;
            RewardCelebration.Play(confirm, sheet, sheetArt, Strings.Get("result.equipped"), coinsFrom, coinsPill, token);
            Refresh();
            try {
                await UniTask.Delay(TimeSpan.FromSeconds(RewardCelebration.Duration), DelayType.UnscaledDeltaTime,
                    cancellationToken: token);
            }
            catch (OperationCanceledException) {
                return;
            }
            finally {
                isCelebrating = false;
            }

            CloseConfirm(false);
            PopHero();
            BumpCoins(false);
        }

        // -- Layout --------------------------------------------------------------------------------------------------

        private void OnGeometry(GeometryChangedEvent evt) => Place();

        /// <summary>
        /// Lays the groups out for the screen, as the menu does: the top bar under Poki's pill upright and beside it
        /// sideways; the car above the switcher upright, at the left of it sideways.
        /// </summary>
        private void Place()
        {
            if (Root?.panel == null) {
                return;
            }

            var size = Root.layout.size;
            if (float.IsNaN(size.x) || size.x <= 0f || size.y <= 0f) {
                return;
            }

            isWide = size.x >= size.y * WideAspect;
            Screen.EnableInClassList(WideClass, isWide);
            var scale = isWide && isTouch ? TouchWideScale : 1f;
            var sideEdge = isWide && isTouch ? TouchWideSide : Edge;
            var left = Mathf.Max(sideEdge, Edge + insets.Left);
            var right = Mathf.Max(sideEdge, Edge + insets.Right);
            var topEdge = Edge + insets.Top;
            var topLeft = left;
            if (PokiPill.TryGetRect(Root.panel, out var pill)) {
                pill = Root.WorldToLocal(pill);
                if (pill.xMax > 0f && pill.yMax > 0f) {
                    if (isWide) {
                        topLeft = Mathf.Max(topLeft, Mathf.Ceil(pill.xMax + PillGap));
                    }
                    else {
                        topEdge = Mathf.Max(topEdge, Mathf.Ceil(pill.yMax + PillGap));
                    }
                }
            }

            var grown = new Scale(new Vector3(scale, scale, 1f));
            top.style.left = topLeft;
            top.style.top = topEdge;
            top.style.width = (size.x - topLeft - right) / scale;
            top.style.scale = grown;

            var bodyTop = topEdge + (TopHeight + TopGap) * scale;
            var bottom = Edge + insets.Bottom;
            body.style.left = left;
            body.style.right = right;
            body.style.top = bodyTop;
            body.style.bottom = bottom;
            var width = size.x - left - right;
            var height = size.y - bodyTop - bottom;
            if (isWide) {
                var sideWidth = width * WideSideShare;
                side.style.width = sideWidth / scale;
                side.style.height = height / scale;
                side.style.scale = grown;
                // Anchored at the bottom right, where it grows from (transform-origin), so the scale keeps it in the body.
                side.style.left = StyleKeyword.Auto;
                side.style.right = 0f;
                side.style.top = StyleKeyword.Auto;
                side.style.bottom = 0f;
                panel.style.height = StyleKeyword.Null;
                show.style.left = 0f;
                show.style.right = sideWidth + WideGap;
                show.style.top = 0f;
                show.style.bottom = 0f;
            }
            else {
                // The hats have no stats under the car: the panel takes that room for a further row. The paints have no
                // stats and no bottom button: that room goes to the car, the thing being painted.
                var chrome = SideChrome - (tab == Tab.Paint ? ActionRoom : 0f);
                var spare = tab == Tab.Hats ? StatsRoom : 0f;
                var panelHeight = Mathf.Clamp(height - SideChrome - ShowMin + spare, PanelMin, PanelMax + spare);
                side.style.width = width;
                side.style.height = StyleKeyword.Auto;
                side.style.scale = new Scale(Vector3.one);
                side.style.left = 0f;
                side.style.right = StyleKeyword.Auto;
                side.style.top = StyleKeyword.Auto;
                side.style.bottom = 0f;
                panel.style.height = panelHeight;
                show.style.left = 0f;
                show.style.right = 0f;
                show.style.top = 0f;
                show.style.bottom = panelHeight + chrome + 10f;
            }

            var sheetFit = Mathf.Max(0.55f, Mathf.Min(isWide && isTouch ? 1.3f : 1f, (size.y - 2f * Edge) / 1750f));
            sheet.style.scale = new Scale(new Vector3(sheetFit, sheetFit, 1f));
            Root.schedule.Execute(LayoutCards);
        }

        /// <summary>The cards three to a row upright (four sideways), as wide as the panel leaves them.</summary>
        private void LayoutCards()
        {
            if (panel == null) {
                return;
            }

            var inner = scroll.contentViewport.layout.width;
            if (float.IsNaN(inner) || inner <= 0f) {
                inner = panel.contentRect.width;
            }

            if (float.IsNaN(inner) || inner <= 0f) {
                return;
            }

            var columns = isWide ? WideColumns : Columns;
            var padding = 2f * 36f;
            var cardWidth = Mathf.Floor((inner - padding - CardGap * (columns - 1)) / columns);
            var cardHeight = Mathf.Floor(cardWidth * CardAspect);
            foreach (var grid in new[] { carGrid, hatGrid }) {
                var shown = 0;
                foreach (var child in grid.Children()) {
                    if (child.resolvedStyle.display == DisplayStyle.None && child.style.display == DisplayStyle.None) {
                        continue;
                    }

                    child.style.width = cardWidth;
                    child.style.height = cardHeight;
                    child.style.marginRight = shown % columns == columns - 1 ? 0f : CardGap;
                    child.style.marginBottom = CardGap;
                    shown++;
                }
            }

            // The paints: two rows of four, as big as the panel holds, each with its name under it.
            var paintBox = panel.contentRect;
            var across = (paintBox.width - 2f * 60f - 3f * 50f) / 4f;
            var down = (paintBox.height - 2f * 50f - 70f - 2f * PaintNameRoom) / 2f;
            var diameter = Mathf.Max(80f, Mathf.Min(across, down, 240f));
            foreach (var paint in paints) {
                paint.style.width = diameter;
                paint.style.height = diameter;
            }
        }

        /// <summary>
        /// The 3D car on its pedestal (one picture, as in the menu) as large as fits over the name and stats, its
        /// middle on the area's; the light's middle on the pedestal's top.
        /// </summary>
        private void PlaceHero()
        {
            var box = show.layout;
            if (float.IsNaN(box.width) || box.width <= 0f) {
                return;
            }

            var room = caption.layout.y;
            if (float.IsNaN(room) || room <= 0f) {
                room = box.height;
            }

            if (garageStage == null) {
                var portrait = 600f;
                hero.style.left = (box.width - portrait) / 2f;
                hero.style.top = room - portrait;
                hero.style.width = portrait;
                hero.style.height = portrait;
                PlaceRays(new Vector2(box.width / 2f, room - portrait * 0.3f), portrait);
                return;
            }

            var width = Mathf.Min(box.width, room * heroAspect, isWide ? WideHeroMaxWidth : HeroMaxWidth);
            var height = width / heroAspect;
            hero.style.left = (box.width - width) / 2f;
            hero.style.top = room - height;
            hero.style.width = width;
            hero.style.height = height;
            var ground = garageStage.HeroGround;
            PlaceRays(new Vector2(box.width / 2f + (ground.x - 0.5f) * width, room - height + ground.y * height), width);
        }

        private void PlaceRays(Vector2 middle, float width)
        {
            var span = width * RaysScale;
            rays.style.left = middle.x - span / 2f;
            rays.style.top = middle.y - span / 2f;
            rays.style.width = span;
            rays.style.height = span;
        }

        // -- Input ---------------------------------------------------------------------------------------------------

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (ViewModel.IsAdPlaying || isCelebrating) {
                evt.StopImmediatePropagation();
                return;
            }

            switch (evt.keyCode) {
                case KeyCode.Escape:
                    evt.StopImmediatePropagation();
                    if (isConfirmOpen) {
                        CloseConfirm(true);
                    }
                    else {
                        Back();
                    }

                    break;
                case KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter:
                    // The keys mean the bottom button (or the window's), never a press of whichever card has the focus.
                    // A repeat of the result's key reaches us before the fade-in ends and is dropped by IsShown.
                    evt.StopImmediatePropagation();
                    if (isConfirmOpen) {
                        Confirm();
                    }
                    else {
                        PressAction();
                    }

                    break;
                case KeyCode.X when isConfirmOpen && confirmCar != null && ViewModel.IsAdCar(confirmCar.Id):
                    evt.StopImmediatePropagation();
                    Confirm();
                    break;
                case KeyCode.Alpha1 or KeyCode.Keypad1:
                    evt.StopImmediatePropagation();
                    PressTab(Tab.Cars);
                    break;
                case KeyCode.Alpha2 or KeyCode.Keypad2:
                    evt.StopImmediatePropagation();
                    PressTab(Tab.Hats);
                    break;
                case KeyCode.Alpha3 or KeyCode.Keypad3:
                    evt.StopImmediatePropagation();
                    PressTab(Tab.Paint);
                    break;
            }
        }

        private void OnCancel(NavigationCancelEvent evt)
        {
            evt.StopPropagation();
            if (isConfirmOpen) {
                CloseConfirm(true);
            }
            else {
                Back();
            }
        }

        private void OpenSettings()
        {
            if (IsBusy || isConfirmOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.OpenSettings(lifetime.Token).Forget();
        }

        private void Back()
        {
            if (IsBusy || isConfirmOpen) {
                return;
            }

            isClosing = true;
            audioService.PlayUiClick();
            ViewModel.Back();
        }

        // -- Animation -----------------------------------------------------------------------------------------------

        private void Refuse(VisualElement element)
        {
            actionTween.Play(ShakeDuration, t => {
                element.style.translate = new Translate(28f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
            });
        }

        private void ShakeCard(CarCatalog.Entry car)
        {
            foreach (var (entry, card, _) in carCards) {
                if (entry == car) {
                    var button = card.Button;
                    cardTween.Play(ShakeDuration, t => {
                        button.style.translate = new Translate(18f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
                    });
                    return;
                }
            }
        }

        private void Bounce(VisualElement element)
        {
            var tween = element == action ? actionTween : cardTween;
            tween.Play(BounceDuration, t => {
                element.style.translate = new Translate(0f, -22f * Mathf.Sin(Mathf.PI * t));
            });
        }

        /// <summary>The car answers every change of car, paint or hat with a little hop on its pedestal.</summary>
        private void PopHero()
        {
            heroTween.Play(PopDuration, t => {
                var hop = Mathf.Sin(Mathf.PI * t);
                hero.style.scale = new Scale(new Vector3(1f + 0.06f * hop, 1f + 0.06f * hop, 1f));
                hero.style.translate = new Translate(0f, -18f * hop);
            });
        }

        private void BumpCoins(bool isShort)
        {
            coinTween.Stop();
            coinsPill.EnableInClassList("garage__coins--short", isShort);
            coinTween.Play(isShort ? ShakeDuration : PopDuration, t => {
                if (isShort) {
                    coinsPill.style.translate = new Translate(16f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
                    coinsPill.style.scale = StyleKeyword.Null;
                }
                else {
                    var bump = 1f + 0.18f * Mathf.Sin(Mathf.PI * t);
                    coinsPill.style.scale = new Scale(new Vector3(bump, bump, 1f));
                    coinsPill.style.translate = StyleKeyword.Null;
                }

                if (t >= 1f) {
                    coinsPill.RemoveFromClassList("garage__coins--short");
                }
            });
        }

        /// <summary>A floating line over the bottom button: why nothing happened. Pops up, rises and fades.</summary>
        private void ShowToast(VisualElement over, string message, bool withCoin, bool isGold)
        {
            toastTween.Stop();
            foreach (var label in toastLabels) {
                label.text = message;
            }

            toastCoin.style.display = withCoin ? DisplayStyle.Flex : DisplayStyle.None;
            toast.EnableInClassList("garage__toast--gold", isGold);
            var bound = toast.parent.WorldToLocal(over.worldBound);
            toast.style.left = bound.xMin;
            toast.style.width = bound.width;
            toast.style.top = bound.yMin - 150f;
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

        private void StopAnimations()
        {
            cardTween.Stop();
            heroTween.Stop();
            toastTween.Stop();
            coinTween.Stop();
            actionTween.Stop();
        }

        // -- Pieces --------------------------------------------------------------------------------------------------

        /// <summary>A button drawn as the menu's stickers: the navy drop its own layer behind the face.</summary>
        private static Button Sticker(string className, Action pressed)
        {
            var button = new Button(pressed);
            button.RemoveFromClassList(Button.ussClassName);
            button.AddToClassList("menu__sticker");
            button.AddToClassList(className);
            button.focusable = false;
            button.Add(Element("menu__drop"));
            button.Add(Element("menu__face"));
            return button;
        }

        private VisualElement KeyCap(string key, string className, Func<string> live = null)
        {
            var cap = Element("key");
            cap.AddToClassList(className);
            var keyText = Text("key__text");
            if (live != null) {
                text.Set(keyText, live);
            }
            else {
                keyText.text = key;
            }

            cap.Add(keyText);
            return cap;
        }

        private static VisualElement Gear()
        {
            var gear = Element("gear");
            gear.Add(Element("gear__tooth"));
            foreach (var turn in new[] { "gear__tooth--45", "gear__tooth--90", "gear__tooth--135" }) {
                var tooth = Element("gear__tooth");
                tooth.AddToClassList(turn);
                gear.Add(tooth);
            }

            var hub = Element("gear__hub");
            hub.Add(Element("gear__hole"));
            gear.Add(hub);
            return gear;
        }

        private static VisualElement Icon(string className, Action<MeshGenerationContext> draw)
        {
            var icon = Element(className);
            icon.generateVisualContent += draw;
            return icon;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            if (className != null) {
                element.AddToClassList(className);
            }

            return element;
        }

        private static Label Text(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        // The back arrow: a navy arrow pointing left, round at its ends.
        private static void DrawBack(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.strokeColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);
            p.lineWidth = r.width * 0.16f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + r.width * 0.86f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.16f, r.center.y));
            p.MoveTo(new Vector2(r.x + r.width * 0.46f, r.y + r.height * 0.18f));
            p.LineTo(new Vector2(r.x + r.width * 0.16f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.46f, r.y + r.height * 0.82f));
            p.Stroke();
        }

        // The padlock of a car its level has not opened, white on the navy of the badge and the button.
        private static void DrawWhiteLock(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.strokeColor = Color.white;
            p.lineWidth = r.width * 0.13f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.3f), Y(0.48f)));
            p.LineTo(new Vector2(X(0.3f), Y(0.32f)));
            p.Arc(new Vector2(X(0.5f), Y(0.32f)), r.width * 0.2f, 180f, 360f);
            p.LineTo(new Vector2(X(0.7f), Y(0.48f)));
            p.Stroke();
            p.fillColor = Color.white;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.14f), Y(0.44f)));
            p.LineTo(new Vector2(X(0.86f), Y(0.44f)));
            p.LineTo(new Vector2(X(0.86f), Y(0.96f)));
            p.LineTo(new Vector2(X(0.14f), Y(0.96f)));
            p.ClosePath();
            p.Fill();
            p.fillColor = new Color32(0xff, 0xc9, 0x3c, 0xff);
            p.BeginPath();
            p.Arc(new Vector2(X(0.5f), Y(0.68f)), r.width * 0.1f, 0f, 360f);
            p.Fill();
        }

        // The ▲ or ▼ of a stat's change, in the tag's text colour (the fonts have no such glyphs).
        private static void DrawDiffArrow(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var r = element.contentRect;
            var isUp = element.parent?.ClassListContains("garage-stats__diff--up") ?? true;
            var p = context.painter2D;
            p.fillColor = element.resolvedStyle.color;
            p.BeginPath();
            if (isUp) {
                p.MoveTo(new Vector2(r.center.x, r.y));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.x, r.yMax));
            }
            else {
                p.MoveTo(new Vector2(r.x, r.y));
                p.LineTo(new Vector2(r.xMax, r.y));
                p.LineTo(new Vector2(r.center.x, r.yMax));
            }

            p.ClosePath();
            p.Fill();
        }

        // The arrow between the coins before and after a purchase.
        private static void DrawArrow(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.strokeColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);
            p.lineWidth = r.height * 0.15f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + r.width * 0.1f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.85f, r.center.y));
            p.MoveTo(new Vector2(r.x + r.width * 0.55f, r.y + r.height * 0.2f));
            p.LineTo(new Vector2(r.x + r.width * 0.85f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.55f, r.y + r.height * 0.8f));
            p.Stroke();
        }

        /// <summary>
        /// One card of the panel, a car or a hat, drawn as a sticker: its picture, its name, and a badge on its corner
        /// that says what it is to the player: a green tick on the chosen one, nothing on one they own, the price in
        /// gold (grey when the coins are short), a lock and the level that opens it, or the video sign of an ad car.
        /// </summary>
        private sealed class Card
        {
            public enum Badge
            {
                None,
                Selected,
                Price,
                Dear,
                Locked,
                Ad,
            }

            public readonly Button Button;
            private readonly VisualElement badge;
            private readonly VisualElement tick;
            private readonly VisualElement coin;
            private readonly VisualElement padlock;
            private readonly VisualElement play;
            private readonly Label value;

            public Card(string name, VisualElement art, LiveText text, Func<string> title)
            {
                Button = Sticker("garage__card", null);
                Button.name = name;
                var face = Button.Q<VisualElement>(className: "menu__face");
                var frame = Element("garage__card-art");
                frame.Add(art);
                face.Add(frame);
                var label = Text("garage__card-name");
                text.Set(label, title);
                face.Add(label);
                badge = Element("garage__badge");
                tick = Element("garage__badge-tick");
                badge.Add(tick);
                coin = RoundOverMeta.Coin();
                coin.AddToClassList("garage__badge-coin");
                badge.Add(coin);
                padlock = Icon("garage__badge-lock", DrawWhiteLock);
                badge.Add(padlock);
                play = RoundOverRewards.PlaySign();
                play.AddToClassList("garage__badge-play");
                badge.Add(play);
                value = Text("garage__badge-text");
                badge.Add(value);
                Button.Add(badge);
            }

            public void Set(Badge kind, string label, bool isSelected, bool isLocked, bool isPreviewed)
            {
                Button.EnableInClassList("garage__card--selected", isSelected);
                Button.EnableInClassList("garage__card--locked", isLocked);
                Button.EnableInClassList("garage__card--previewed", isPreviewed);
                foreach (var each in new[] { "selected", "price", "dear", "locked", "ad" }) {
                    badge.RemoveFromClassList($"garage__badge--{each}");
                }

                if (kind != Badge.None) {
                    badge.AddToClassList($"garage__badge--{kind.ToString().ToLowerInvariant()}");
                }

                Show(badge, kind != Badge.None);
                Show(tick, kind == Badge.Selected);
                Show(coin, kind is Badge.Price or Badge.Dear);
                Show(padlock, kind == Badge.Locked);
                Show(play, kind == Badge.Ad);
                Show(value, kind is Badge.Price or Badge.Dear or Badge.Locked or Badge.Ad);
                value.text = label ?? "";
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
