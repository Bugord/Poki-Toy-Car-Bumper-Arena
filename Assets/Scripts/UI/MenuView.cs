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
    /// The main menu (<see cref="MenuViewModel"/>), built in code over the drifting checker of <c>Menu.uxml</c> and styled by
    /// <c>ui_menu.uss</c>: the level, XP and coins along the top, the rail of GARAGE, MISSIONS and AWARDS at the left,
    /// the player's 3D car on its 3D pedestal in the middle (tap it for the garage), and the nearest mission, the mode and
    /// PLAY at the bottom. MISSIONS and the mode open popups of the menu's own; the garage and the settings are screens
    /// of their own over it.
    /// <para>
    /// Upright the groups keep their reference size. On a wide screen the bottom becomes one row; on a phone held
    /// sideways, where the Expand panel is small, the anchored groups grow by <see cref="TouchWideScale"/> so every
    /// button stays a thumb wide. The top bar clears Poki's pill as the HUD does: under it upright, beside it sideways.
    /// Keys: Space or Enter PLAY, G the garage, M the missions, Esc closes a popup.
    /// </para>
    /// <para>
    /// The player's league (their rank, <see cref="LeagueLook"/>) tints the sky and the pedestal's top; a press on the
    /// level opens the leagues list, and the first menu in a new league greets it with NEW LEAGUE! (<see cref="MenuLeagues"/>).
    /// </para>
    /// </summary>
    public sealed class MenuView : FadingView<MenuViewModel>
    {
        private const string WideClass = "menu--wide";
        private const string DesktopClass = "menu--desktop";
        private const string OpenClass = "menu__popup--open";

        // A screen at least this much wider than tall lays out sideways.
        private const float WideAspect = 1.2f;

        // The anchored groups on a phone held sideways: 150 reference pixels of a tile are about 45 CSS px at this scale.
        private const float TouchWideScale = 1.5f;

        // The groups' distance from the screen's edges, and from Poki's pill (as the HUD keeps).
        private const float Edge = 40f;
        private const float PillGap = 18f;
        // How far a silver diamond or a gold or legend crown stands above the level badge: the bar keeps it on screen
        // when the touch layout grows the bar from its top, and clear of Poki's button over it.
        private const float BadgeOrnament = 32f;
        // The wide leagues list keeps its five cards in from the sides, as the board does.
        private const float WideLeaguesSide = 200f;

        // The least distance of the groups from a side on a phone held sideways (about 48 CSS px).
        private const float TouchWideSide = 240f;

        private static readonly Color CheckerColor = new(1f, 1f, 1f, 0.07f);
        private const float CheckerShare = 1f / 6f;

        // The widest the car's picture grows, in reference pixels: on a tall screen it would otherwise fill the width.
        // Sideways the height bounds it anyway, and the car is the screen's subject, so it may grow much wider.
        private const float HeroMaxWidth = 1400f;
        private const float WideHeroMaxWidth = 2600f;
        // Space kept on each side of the car's name when it drops between the mission and the mode sideways.
        private const float NameGap = 40f;

        // The showroom's light behind the car: a soft white glow and rays that fade out towards their tips, turning
        // slowly, so the light gathers round the car instead of lying over the checker as a second pattern.
        private static readonly Color32 RayCore = new(255, 255, 255, 120);
        private static readonly Color32 GlowCore = new(255, 255, 255, 150);
        private static readonly Color32 Clear = new(255, 255, 255, 0);
        private const int RayCount = 10;
        // The share of each ray's slot it fills; the rest is the gap to the next.
        private const float RayFill = 0.36f;
        // The glow's radius against the rays'.
        private const float GlowShare = 0.42f;
        private const int GlowSides = 48;
        private const float RaysTurnSeconds = 24f;
        // The rays' span against the car's picture: past its sides, where their faded tips are lost in the checker.
        private const float RaysScale = 1.7f;

        // The bottom row's own width sideways (ui_menu: menu--wide .menu__bottom).
        private const float WideBottomWidth = 2000f;

        // The XP bar sideways: this share of the top row, never shorter than upright and never into the coins and the
        // gear (their width and gaps, reference pixels).
        private const float WideLevelShare = 0.42f;
        private const float LevelMinWidth = 640f;
        private const float WalletWidth = 420f;
        // The bar's text starts this far past the badge that covers the bar's left end.
        private const float XpTextGap = 22f;

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color White = Color.white;

        private readonly IAudioService audioService;
        private readonly HudControls controls;
        private readonly LiveText text = new();
        private CancellationTokenSource lifetime;
        private bool isTouch;
        private bool isWide;
        private SafeInsets insets;

        private VisualElement top;
        private VisualElement level;
        private VisualElement levelBadgeSlot;
        private VisualElement xp;
        private VisualElement xpText;
        private VisualElement xpFill;
        private Label rank;
        private Label xpCount;
        private Label coins;
        private VisualElement rail;
        private VisualElement garageTile;
        private VisualElement missionsTile;
        private VisualElement stage;
        private VisualElement rays;
        private VisualElement plinth;
        private VisualElement plinthTop;
        private VisualElement carNameBox;
        private Button hero;
        private VisualElement heroPortraitRoot;
        private CarPortrait heroPortrait;
        private float heroAspect = 1.6f;
        private Label carName;
        private Label carNameFace;
        private VisualElement bottom;
        private VisualElement missionCard;
        private VisualElement actions;
        private Label missionTask;
        private Label missionCount;
        private VisualElement missionFill;
        private Label missionReward;
        private VisualElement missionsPopup;
        private VisualElement missionsList;
        private VisualElement modesPopup;
        private CarPortrait modePortrait;
        private GarageStage garageStage;
        private MenuLeagues leagues;

        public MenuView(IAudioService audioService, HudControls controls)
        {
            this.audioService = audioService;
            this.controls = controls;
        }

        protected override void OnAttached()
        {
            lifetime = new CancellationTokenSource();
            Root.focusable = true;
            Root.RegisterCallback<KeyDownEvent>(OnKeyDown);
            isTouch = controls.CheckTouchScreen();
            var menu = Menu;
            menu.EnableInClassList(DesktopClass, !isTouch);

            // The checker drifting sideways, as behind the garage and the lobby, on the sky blue of the theme: lighter
            // than their navy, and in squares a sixth of the short side, so a desktop's wide panel stays calm.
            DriftingChecker.AddTo(menu, CheckerColor, CheckerShare);

            BuildStage(menu);
            BuildTop(menu);
            BuildRail(menu);
            BuildBottom(menu);
            BuildMissionsPopup(menu);
            BuildModesPopup(menu);
            leagues = new MenuLeagues(menu, audioService.PlayUiClick, () => Root?.Focus());

            Root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            SafeArea.Watch(Root, safe => {
                insets = safe;
                Place();
            });
            PokiPill.Watch(Root, Place);
            text.OnChanged(Refresh);
            Refresh();
        }

        public override async UniTask ShowAsync(CancellationToken token)
        {
            await base.ShowAsync(token);
            Root.Focus();
            // The first menu in a new league greets it, over the league's sky and plinth already behind the dim.
            if (!ViewModel.IsDecided && ViewModel.TakeNewLeague()) {
                ClosePopups();
                leagues.ShowNewLeague(ViewModel.Level.Level, !isTouch);
            }
        }

        public override async UniTask HideAsync(CancellationToken token)
        {
            await base.HideAsync(token);
            garageStage?.Hide();
        }

        public override void Dispose()
        {
            Root?.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            Root?.UnregisterCallback<GeometryChangedEvent>(OnGeometry);
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            text.Dispose();
            base.Dispose();
        }

        // The screen's own element, which carries the style sheets: the root UIKit gives the view, or under it.
        private VisualElement Menu => Root.name == "menu" ? Root : Root.Q<VisualElement>("menu") ?? Root;

        // Before it is up, after PLAY, or under the garage or the settings, the menu takes no presses.
        private bool IsBusy => !IsShown || ViewModel.IsDecided || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen;

        private bool IsPopupOpen => missionsPopup.ClassListContains(OpenClass) || modesPopup.ClassListContains(OpenClass) ||
            leagues.IsNewLeagueOpen || leagues.IsListOpen;

        // -- Building ------------------------------------------------------------------------------------------------

        private void BuildTop(VisualElement menu)
        {
            top = Element("menu__top");
            level = Element("menu__level");
            // The level and its bar open the leagues: the bar alone is too thin to aim at.
            level.pickingMode = PickingMode.Position;
            level.RegisterCallback<ClickEvent>(_ => OpenLeagues());
            levelBadgeSlot = Element("menu__level-badge");
            levelBadgeSlot.RegisterCallback<GeometryChangedEvent>(_ => PlaceXp());
            // The bar: its navy drop behind it, its left end square and under the badge, its right end round.
            xp = Element("menu__xp");
            xp.Add(Element("menu__xp-drop"));
            var track = Element("menu__xp-track");
            xpFill = Element("menu__xp-fill");
            xpFill.Add(Element("menu__xp-shade"));
            track.Add(xpFill);
            xpText = Element("menu__xp-text");
            rank = Text("menu__rank");
            var count = Element("menu__xp-numbers");
            xpCount = Text("menu__xp-count");
            var unit = Text("menu__xp-unit");
            unit.text = "XP";
            count.Add(xpCount);
            count.Add(unit);
            xpText.Add(rank);
            xpText.Add(count);
            track.Add(xpText);
            xp.Add(track);
            level.Add(xp);
            // The level badge goes over the bar's left end, which tucks under it.
            level.Add(levelBadgeSlot);
            top.Add(level);
            top.Add(Element("menu__spacer"));

            var coinPill = Element("menu__coins");
            coinPill.Add(Element("menu__coins-drop"));
            var coinFace = Element("menu__coins-face");
            coinFace.Add(RoundOverMeta.Coin());
            coins = Text("menu__coins-count");
            coinFace.Add(coins);
            coinPill.Add(coinFace);
            top.Add(coinPill);

            var gear = Sticker("menu__gear", OpenSettings);
            gear.Q<VisualElement>(className: "menu__face").Add(Gear());
            top.Add(gear);
            menu.Add(top);
        }

        private void BuildRail(VisualElement menu)
        {
            rail = Element("menu__rail");
            garageTile = Tile(DrawGarage, () => Strings.Get("garage.title"), "G", OpenGarage);
            missionsTile = Tile(DrawTarget, () => Strings.Get("mission.title"), "M", OpenMissions);
            var awards = Tile(DrawLock, () => Strings.Get("menu.awards"), null, Locked);
            awards.AddToClassList("menu__tile--locked");
            rail.Add(garageTile);
            rail.Add(missionsTile);
            rail.Add(awards);
            menu.Add(rail);
        }

        private void BuildStage(VisualElement menu)
        {
            stage = Element("menu__stage");
            rays = Element("menu__rays");
            rays.pickingMode = PickingMode.Ignore;
            rays.generateVisualContent += DrawLight;
            // Unscaled clock, so the ad pause (timeScale 0) does not stop the light.
            rays.schedule.Execute(() =>
                rays.style.rotate = new Rotate(Time.unscaledTime / RaysTurnSeconds * 360f % 360f)).Every(16);
            stage.Add(rays);
            plinth = Element("menu__plinth");
            plinth.Add(Element("menu__plinth-base"));
            plinth.Add(Element("menu__plinth-side"));
            plinthTop = Element("menu__plinth-top");
            plinth.Add(plinthTop);
            stage.Add(plinth);

            hero = new Button(OpenGarage);
            hero.RemoveFromClassList(Button.ussClassName);
            hero.AddToClassList("menu__hero");
            hero.focusable = false;
            heroPortraitRoot = Element("menu__hero-portrait");
            heroPortraitRoot.AddToClassList("car-portrait");
            hero.Add(heroPortraitRoot);
            heroPortrait = new CarPortrait(heroPortraitRoot);
            stage.Add(hero);

            var name = carNameBox = Element("menu__car-name");
            var stroke = Element("stroke");
            carName = Text("display");
            // A longer or shorter name may or may not fit between the bottom cards sideways (PlaceStage).
            carName.RegisterCallback<GeometryChangedEvent>(_ => PlaceStage());
            carName.AddToClassList("menu__car-name-text");
            carNameFace = Text("display");
            carNameFace.AddToClassList("menu__car-name-text");
            carNameFace.AddToClassList("stroke__face");
            stroke.Add(carName);
            stroke.Add(carNameFace);
            name.Add(stroke);
            stage.Add(name);
            stage.RegisterCallback<GeometryChangedEvent>(_ => PlaceHero());
            menu.Add(stage);
        }

        private void BuildBottom(VisualElement menu)
        {
            bottom = Element("menu__bottom");
            missionCard = Sticker("menu__mission", OpenMissions);
            var face = missionCard.Q<VisualElement>(className: "menu__face");
            face.Add(Icon("menu__mission-icon", DrawTarget));
            var body = Element("menu__mission-body");
            var head = Element("menu__mission-head");
            missionTask = Text("menu__mission-task");
            missionCount = Text("menu__mission-count");
            head.Add(missionTask);
            head.Add(missionCount);
            body.Add(head);
            var bar = Element("menu__bar");
            missionFill = Element("menu__bar-fill");
            bar.Add(missionFill);
            body.Add(bar);
            face.Add(body);
            var reward = Element("menu__reward");
            reward.Add(RoundOverMeta.Coin());
            missionReward = Text("menu__reward-count");
            reward.Add(missionReward);
            face.Add(reward);
            bottom.Add(missionCard);

            actions = Element("menu__actions");
            var mode = Sticker("menu__mode", OpenModes);
            var modeFace = mode.Q<VisualElement>(className: "menu__face");
            var modeText = Element("menu__mode-texts");
            var caption = Text("menu__mode-caption");
            text.Set(caption, () => Strings.Get("menu.mode"));
            var modeName = Text("menu__mode-name");
            text.Set(modeName, () => Strings.Get("menu.quick_match"));
            modeText.Add(caption);
            modeText.Add(modeName);
            modeFace.Add(modeText);
            modeFace.Add(Icon("menu__chevron", DrawChevron));
            actions.Add(mode);
            actions.Add(PlayButton("menu__play", true));
            bottom.Add(actions);
            menu.Add(bottom);
        }

        private void BuildMissionsPopup(VisualElement menu)
        {
            missionsPopup = Popup(menu, () => Strings.Get("mission.title"), out var sheet);
            missionsList = Element(null);
            sheet.Add(missionsList);
        }

        private void BuildModesPopup(VisualElement menu)
        {
            modesPopup = Popup(menu, () => Strings.Get("menu.choose_mode"), out var sheet);

            var quick = Element("menu__mode-card");
            quick.AddToClassList("menu__mode-card--selected");
            quick.pickingMode = PickingMode.Position;
            quick.RegisterCallback<ClickEvent>(_ => {
                audioService.PlayUiClick();
                ClosePopups();
            });
            var quickText = Element("menu__mode-text");
            var quickName = Text("menu__mode-card-name");
            text.Set(quickName, () => Strings.Get("menu.quick_match"));
            var quickHint = Text("menu__mode-hint");
            text.Set(quickHint, () => Strings.Get("menu.quick_match_hint"));
            quickText.Add(quickName);
            quickText.Add(quickHint);
            quick.Add(quickText);
            var portrait = Element("menu__mode-portrait");
            portrait.AddToClassList("car-portrait");
            modePortrait = new CarPortrait(portrait);
            quick.Add(portrait);
            var selected = Text("menu__tag");
            selected.AddToClassList("menu__tag--selected");
            text.Set(selected, () => Strings.Get("menu.selected"));
            quick.Add(selected);
            sheet.Add(quick);

            sheet.Add(LockedMode("menu.cup", "menu.cup_hint"));
            sheet.Add(LockedMode("menu.survival", "menu.survival_hint"));

            var play = PlayButton("menu__modes-play", false);
            sheet.Add(play);
        }

        private VisualElement LockedMode(string nameKey, string hintKey)
        {
            var card = Element("menu__mode-card");
            card.AddToClassList("menu__mode-card--locked");
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<ClickEvent>(_ => Locked());
            card.Add(Icon("menu__mode-lock", DrawLock));
            var body = Element("menu__mode-text");
            var name = Text("menu__mode-card-name");
            text.Set(name, () => Strings.Get(nameKey));
            var hint = Text("menu__mode-hint");
            text.Set(hint, () => Strings.Get(hintKey));
            body.Add(name);
            body.Add(hint);
            card.Add(body);
            var soon = Text("menu__tag");
            text.Set(soon, () => Strings.Get("menu.soon"));
            card.Add(soon);
            return card;
        }

        private VisualElement Popup(VisualElement menu, System.Func<string> title, out VisualElement sheet)
        {
            var popup = Element("menu__popup");
            popup.pickingMode = PickingMode.Position;
            // A tap on the dim around the card closes it.
            popup.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == popup) {
                    ClosePopups();
                }
            });
            sheet = Element("card");
            sheet.AddToClassList("menu__sheet");
            sheet.pickingMode = PickingMode.Position;
            var head = Element("menu__sheet-head");
            var stroke = Element("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = Text("display");
                label.AddToClassList("menu__sheet-title");
                label.EnableInClassList("stroke__face", isFace);
                text.Set(label, title);
                stroke.Add(label);
            }

            head.Add(stroke);
            head.Add(CloseSticker(() => {
                audioService.PlayUiClick();
                ClosePopups();
            }));
            sheet.Add(head);
            popup.Add(sheet);
            menu.Add(popup);
            return popup;
        }

        private Button PlayButton(string className, bool hasKey)
        {
            var play = new Button(PressPlay);
            play.RemoveFromClassList(Button.ussClassName);
            play.AddToClassList("button");
            play.AddToClassList("button--green");
            play.AddToClassList("button--big");
            play.AddToClassList(className);
            play.focusable = false;
            play.Add(Element("button__face"));
            var stroke = Element("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = Text("button__label");
                label.EnableInClassList("stroke__face", isFace);
                text.Set(label, () => Strings.Get("menu.play"));
                stroke.Add(label);
            }

            play.Add(stroke);
            if (hasKey) {
                var key = Element("key");
                key.AddToClassList("menu__play-key");
                var keyText = Text("key__text");
                text.Set(keyText, () => Strings.Get("common.space"));
                key.Add(keyText);
                play.Add(key);
            }

            return play;
        }

        private VisualElement Tile(System.Action<MeshGenerationContext> draw, System.Func<string> caption, string key,
            System.Action pressed)
        {
            var tile = Element("menu__tile");
            var button = Sticker("menu__tile-button", pressed);
            var face = button.Q<VisualElement>(className: "menu__face");
            face.Add(Icon("menu__icon", draw));
            var isNew = Text("menu__new");
            text.Set(isNew, () => Strings.Get("common.new"));
            button.Add(isNew);
            button.Add(Element("menu__dot"));
            if (key != null) {
                var cap = Element("key");
                cap.AddToClassList("menu__key");
                var keyText = Text("key__text");
                keyText.text = key;
                cap.Add(keyText);
                button.Add(cap);
            }

            tile.Add(button);
            var stroke = Element("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = Text("display");
                label.AddToClassList("menu__tile-caption");
                label.EnableInClassList("stroke__face", isFace);
                text.Set(label, caption);
                stroke.Add(label);
            }

            tile.Add(stroke);
            return tile;
        }

        /// <summary>A button drawn as a sticker: the navy drop is its own layer behind the face, so the face's content
        /// stays centred, and the face sinks onto the drop while pressed.</summary>
        private static Button Sticker(string className, System.Action pressed)
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

        /// <summary>The round white close button of the menu's popups, its cross drawn by two bars.</summary>
        internal static Button CloseSticker(System.Action pressed)
        {
            var close = Sticker("menu__close", pressed);
            var face = close.Q<VisualElement>(className: "menu__face");
            face.Add(Element("menu__close-bar"));
            var turned = Element("menu__close-bar");
            turned.AddToClassList("menu__close-bar--turned");
            face.Add(turned);
            return close;
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

        private static VisualElement Icon(string className, System.Action<MeshGenerationContext> draw)
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

        // -- State ---------------------------------------------------------------------------------------------------

        /// <summary>Shows the player's level, coins, car, garage marks and nearest mission as they are now.</summary>
        private void Refresh()
        {
            var at = ViewModel.Level;
            levelBadgeSlot.Clear();
            levelBadgeSlot.Add(HudView.CreateLevelBadge(at.Level, "level-badge--big"));
            xpFill.style.width = Length.Percent(100f * at.Xp / Mathf.Max(1, at.Cost));
            rank.text = PlayerRank.NameOf(at.Level);
            xpCount.text = $"{at.Xp}/{at.Cost}";
            coins.text = ViewModel.Coins.ToString();
            // The league's sky under the checker, and its colour on the pedestal's top (ShowCar).
            var league = ViewModel.League;
            Menu.style.backgroundImage = new StyleBackground(LeagueLook.SkyOf(league));
            plinthTop.style.backgroundColor = LeagueLook.PedestalOf(league);

            garageTile.EnableInClassList("menu__tile--new", ViewModel.HasNewCar);
            garageTile.EnableInClassList("menu__tile--dot", ViewModel.CanShop);

            var car = ViewModel.Car;
            carName.text = carNameFace.text = GameText.Car(car);
            modePortrait.Set(car, ViewModel.CarColor);
            ShowCar(car);

            var nearest = ViewModel.NearestMission;
            missionCard.style.display = nearest == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (nearest != null) {
                var definition = nearest.Definition;
                missionTask.text = ViewModel.MissionText(definition);
                missionCount.text = $"{nearest.Progress}/{definition.Target}";
                missionFill.style.width = Length.Percent(100f * nearest.Progress / Mathf.Max(1, definition.Target));
                missionReward.text = ViewModel.MissionCoins(definition).ToString();
            }

            if (missionsPopup.ClassListContains(OpenClass)) {
                FillMissions();
            }
        }

        private void ShowCar(CarCatalog.Entry car)
        {
            garageStage = ViewModel.Stage;
            if (garageStage != null && car != null) {
                // The 3D car stands on a 3D pedestal drawn with it; the flat plinth is only under a portrait.
                var texture = garageStage.ShowCar(car, ViewModel.CarColor, ViewModel.Topper, true,
                    LeagueLook.PedestalOf(ViewModel.League));
                heroAspect = (float)texture.width / texture.height;
                hero.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(texture));
                heroPortraitRoot.style.display = DisplayStyle.None;
                plinth.style.display = DisplayStyle.None;
            }
            else {
                hero.style.backgroundImage = StyleKeyword.None;
                heroPortraitRoot.style.display = DisplayStyle.Flex;
                plinth.style.display = DisplayStyle.Flex;
                heroPortrait.Set(car, ViewModel.CarColor);
            }

            PlaceHero();
        }

        private void FillMissions()
        {
            missionsList.Clear();
            foreach (var mission in ViewModel.Missions) {
                var definition = mission.Definition;
                var row = Element("mission");
                var check = Element("mission__check");
                check.Add(Element("mission__mark"));
                row.Add(check);
                var task = Text("mission__task");
                task.text = ViewModel.MissionText(definition);
                row.Add(task);
                var progress = Text("mission__progress");
                progress.text = $"{mission.Progress}/{definition.Target}";
                row.Add(progress);
                var reward = Element("menu__reward");
                reward.Add(RoundOverMeta.Coin());
                var count = Text("menu__reward-count");
                count.text = ViewModel.MissionCoins(definition).ToString();
                reward.Add(count);
                row.Add(reward);
                missionsList.Add(row);
            }
        }

        // -- Layout --------------------------------------------------------------------------------------------------

        private void OnGeometry(GeometryChangedEvent evt) => Place();

        /// <summary>
        /// Lays the groups out for the screen: sideways or upright, their scale, and clear of the safe area and Poki's
        /// pill. The groups grow by their scale, not their layout, so they keep the proportions they have upright.
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
            Menu.EnableInClassList(WideClass, isWide);
            var scale = isWide && isTouch ? TouchWideScale : 1f;
            // A phone held sideways may have its camera in the middle of a side, which the safe area does not always
            // report inside Poki's frame: the groups keep off both sides, as the result's columns do.
            var side = isWide && isTouch ? TouchWideSide : Edge;
            var left = Mathf.Max(side, Edge + insets.Left);
            var right = Mathf.Max(side, Edge + insets.Right);
            var ornament = ViewModel.League >= 2 ? BadgeOrnament : 0f;
            var topEdge = Edge + insets.Top + (scale - 1f) * ornament;
            var topLeft = left;
            if (PokiPill.TryGetRect(Root.panel, out var pill)) {
                pill = Root.WorldToLocal(pill);
                if (pill.xMax > 0f && pill.yMax > 0f) {
                    if (isWide) {
                        topLeft = Mathf.Max(topLeft, Mathf.Ceil(pill.xMax + PillGap));
                    }
                    else {
                        topEdge = Mathf.Max(topEdge, Mathf.Ceil(pill.yMax + PillGap + ornament));
                    }
                }
            }

            var grown = new Scale(new Vector3(scale, scale, 1f));
            var topWidth = (size.x - topLeft - right) / scale;
            top.style.left = topLeft;
            top.style.top = topEdge;
            top.style.right = StyleKeyword.Auto;
            top.style.width = topWidth;
            top.style.scale = grown;
            // Upright the bar takes what the row leaves; sideways a long one, as the screen is long.
            level.style.width = isWide
                ? Mathf.Max(LevelMinWidth, Mathf.Min(topWidth * WideLevelShare, topWidth - WalletWidth))
                : StyleKeyword.Null;

            var topHeight = 130f * scale;
            rail.style.left = left;
            rail.style.top = topEdge + topHeight + 50f * scale;
            rail.style.scale = grown;

            var low = 50f + insets.Bottom;
            bottom.style.bottom = low;
            bottom.style.scale = grown;
            // Sideways the row spans the screen, the mission at the left and PLAY at the right; it shrinks rather than
            // let the two meet.
            var bottomScale = isWide ? Mathf.Min(scale, (size.x - left - right) / WideBottomWidth) : scale;
            bottom.style.scale = new Scale(new Vector3(bottomScale, bottomScale, 1f));
            bottom.style.left = left;
            bottom.style.right = StyleKeyword.Auto;
            bottom.style.width = (size.x - left - right) / bottomScale;
            bottom.style.transformOrigin = new TransformOrigin(0f, Length.Percent(100f));
            plinth.style.scale = grown;

            var sheetFit = Mathf.Max(0.6f,
                Mathf.Min(isWide && isTouch ? 1.3f : 1f, (size.y - topEdge - low) / 1500f * 1.3f));
            foreach (var popup in new[] { missionsPopup, modesPopup }) {
                var sheet = popup.Q<VisualElement>(className: "menu__sheet");
                sheet.style.scale = new Scale(new Vector3(sheetFit, sheetFit, 1f));
            }

            leagues.Top = topEdge;
            // The new league's card is taller than the menu's sheets.
            leagues.Place(isWide, scale, Mathf.Max(0.55f, Mathf.Min(sheetFit, (size.y - 2f * Edge) / 1750f)), size,
                isWide ? Mathf.Max(left, WideLeaguesSide) : left, isWide ? Mathf.Max(right, WideLeaguesSide) : right,
                Edge + insets.Bottom);

            Root.schedule.Execute(PlaceStage);
        }

        /// <summary>
        /// The bar's left end under the badge and its text clear of it: the badge grows with the level's digits.
        /// </summary>
        private void PlaceXp()
        {
            var badge = levelBadgeSlot.layout.width;
            if (float.IsNaN(badge) || badge <= 0f) {
                return;
            }

            xp.style.left = badge / 2f;
            xpText.style.left = badge / 2f + XpTextGap;
        }

        /// <summary>The car between the top groups and the bottom ones, with room for the rail at its left.</summary>
        private void PlaceStage()
        {
            if (Root?.panel == null) {
                return;
            }

            var size = Root.layout.size;
            var railBox = rail.worldBound;
            var topBox = Root.WorldToLocal(top.worldBound);
            var bottomBox = Root.WorldToLocal(bottom.worldBound);
            var railRight = Root.WorldToLocal(railBox).xMax;
            stage.style.top = topBox.yMax + 20f;
            stage.style.bottom = size.y - bottomBox.yMin + 20f;
            if (isWide && missionCard != null && actions != null) {
                // Sideways the bottom row leaves its middle free: the car's name drops into it, level with the
                // cards, when it fits between the mission and the mode, and the car above grows into the room.
                var missionBox = Root.WorldToLocal(missionCard.worldBound);
                var actionsBox = Root.WorldToLocal(actions.worldBound);
                var nameWidth = Root.WorldToLocal(carName.worldBound).width;
                // The name stays centred under the car, and the gap need not be centred on the screen.
                var reach = nameWidth / 2f + NameGap;
                var middle = size.x / 2f;
                if (!float.IsNaN(nameWidth) && nameWidth > 0f && middle - reach >= missionBox.xMax &&
                    middle + reach <= actionsBox.xMin) {
                    stage.style.bottom = size.y - missionBox.yMax;
                }
            }

            // Centred on the screen, as wide as the rail leaves on both sides.
            var side = isWide ? railRight + 20f : 0f;
            stage.style.left = side;
            stage.style.right = side;
        }

        /// <summary>
        /// The 3D car on its pedestal (one picture, GarageStage frames both) as large as fits over the car's name, its
        /// middle on the stage's; without the stage, the catalog's portrait over the flat plinth.
        /// </summary>
        private void PlaceHero()
        {
            var box = stage.layout;
            if (float.IsNaN(box.width) || box.width <= 0f || plinthTop.panel == null) {
                return;
            }

            if (garageStage == null) {
                // By the drawn bounds, which take the plinth's centring translate and its scale.
                var centre = stage.WorldToLocal(plinthTop.worldBound).center;
                var portrait = heroPortraitRoot.layout.size;
                hero.style.left = centre.x - portrait.x / 2f;
                hero.style.top = centre.y - portrait.y * 0.85f;
                hero.style.width = portrait.x;
                hero.style.height = portrait.y;
                heroPortraitRoot.style.left = 0f;
                heroPortraitRoot.style.top = 0f;
                PlaceRays(centre, portrait.x);
                return;
            }

            var room = carNameBox.layout.y;
            if (float.IsNaN(room) || room <= 0f) {
                room = box.height;
            }

            var width = Mathf.Min(box.width, room * heroAspect, isWide ? WideHeroMaxWidth : HeroMaxWidth);
            var height = width / heroAspect;
            hero.style.left = (box.width - width) / 2f;
            hero.style.top = room - height;
            hero.style.width = width;
            hero.style.height = height;
            // The light comes from behind the car: its middle is the pedestal's top, where the car stands.
            var ground = garageStage.HeroGround;
            PlaceRays(new Vector2(box.width / 2f + (ground.x - 0.5f) * width, room - height + ground.y * height),
                width);
        }

        // A square on the car's ground, wider than its picture; the stage clips nothing, so the faded tips run out.
        private void PlaceRays(Vector2 middle, float width)
        {
            var span = width * RaysScale;
            rays.style.left = middle.x - span / 2f;
            rays.style.top = middle.y - span / 2f;
            rays.style.width = span;
            rays.style.height = span;
        }

        /// <summary>
        /// The glow and the rays as one mesh, white at the middle and clear at the rim, by the vertices' tint: USS has
        /// no gradients, and the podium's sunburst is one flat colour to its tips.
        /// </summary>
        internal static void DrawLight(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            if (float.IsNaN(radius) || radius <= 0f) {
                return;
            }

            var centre = rect.center;
            var mesh = context.Allocate(RayCount * 3 + GlowSides + 1, (RayCount + GlowSides) * 3);
            var half = Mathf.PI / RayCount * RayFill;
            ushort next = 0;
            for (var i = 0; i < RayCount; i++) {
                var angle = i * Mathf.PI * 2f / RayCount;
                mesh.SetNextVertex(LightVertex(centre, RayCore));
                mesh.SetNextVertex(LightVertex(centre + Direction(angle - half) * radius, Clear));
                mesh.SetNextVertex(LightVertex(centre + Direction(angle + half) * radius, Clear));
                mesh.SetNextIndex(next);
                mesh.SetNextIndex((ushort)(next + 1));
                mesh.SetNextIndex((ushort)(next + 2));
                next += 3;
            }

            var middle = next;
            mesh.SetNextVertex(LightVertex(centre, GlowCore));
            for (var i = 0; i < GlowSides; i++) {
                var rim = centre + Direction(i * Mathf.PI * 2f / GlowSides) * radius * GlowShare;
                mesh.SetNextVertex(LightVertex(rim, Clear));
            }

            for (var i = 0; i < GlowSides; i++) {
                mesh.SetNextIndex(middle);
                mesh.SetNextIndex((ushort)(middle + 1 + i));
                mesh.SetNextIndex((ushort)(middle + 1 + (i + 1) % GlowSides));
            }
        }

        private static Vector2 Direction(float angle) => new(Mathf.Cos(angle), Mathf.Sin(angle));

        private static Vertex LightVertex(Vector2 position, Color32 tint) =>
            new() { position = new Vector3(position.x, position.y, Vertex.nearZ), tint = tint };

        // -- Input ---------------------------------------------------------------------------------------------------

        private void OnKeyDown(KeyDownEvent evt)
        {
            // NEW LEAGUE! takes every key that would close it, and nothing under it acts.
            if (leagues.IsNewLeagueOpen) {
                evt.StopPropagation();
                if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape) {
                    audioService.PlayUiClick();
                    leagues.Continue();
                }

                return;
            }

            // The leagues list closes with Esc; PLAY's and the tiles' keys do not reach through it.
            if (leagues.IsListOpen && evt.keyCode is not KeyCode.Escape) {
                evt.StopPropagation();
                return;
            }

            switch (evt.keyCode) {
                case KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter:
                    evt.StopPropagation();
                    PressPlay();
                    break;
                case KeyCode.G:
                    evt.StopPropagation();
                    OpenGarage();
                    break;
                case KeyCode.M:
                    evt.StopPropagation();
                    OpenMissions();
                    break;
                case KeyCode.Escape:
                    evt.StopPropagation();
                    ClosePopups();
                    break;
            }
        }

        private void PressPlay()
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            ClosePopups();
            ViewModel.Play();
        }

        private void OpenGarage()
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            ClosePopups();
            RunGarage(lifetime.Token).Forget();
        }

        private async UniTaskVoid RunGarage(CancellationToken token)
        {
            try {
                await ViewModel.OpenGarage(token);
            }
            catch (System.OperationCanceledException) {
                return;
            }

            // The garage stopped drawing the stage as it closed, and the car, its colour or the coins may have changed.
            if (!ViewModel.IsDecided && Root?.panel != null) {
                Refresh();
                Root.Focus();
            }
        }

        private void OpenSettings()
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            RunSettings(lifetime.Token).Forget();
        }

        private async UniTaskVoid RunSettings(CancellationToken token)
        {
            try {
                await ViewModel.OpenSettings(token);
            }
            catch (System.OperationCanceledException) {
                return;
            }

            Root?.Focus();
        }

        private void OpenMissions()
        {
            if (IsBusy || IsPopupOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.MissionsOpened();
            FillMissions();
            missionsPopup.AddToClassList(OpenClass);
        }

        private void OpenLeagues()
        {
            if (IsBusy || IsPopupOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.LeaguesOpened();
            leagues.ShowList(ViewModel.Level.Level, !isTouch);
        }

        private void OpenModes()
        {
            if (IsBusy || IsPopupOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.ModesOpened();
            modesPopup.AddToClassList(OpenClass);
        }

        private void ClosePopups()
        {
            missionsPopup.RemoveFromClassList(OpenClass);
            modesPopup.RemoveFromClassList(OpenClass);
            leagues?.Close();
        }

        // A locked tile or mode: a click and nothing else for now.
        private void Locked()
        {
            if (!IsBusy) {
                audioService.PlayUiClick();
            }
        }

        // -- Icons ---------------------------------------------------------------------------------------------------

        private static void DrawGarage(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.fillColor = Navy;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.5f), Y(0.06f)));
            p.LineTo(new Vector2(X(0.98f), Y(0.42f)));
            p.LineTo(new Vector2(X(0.88f), Y(0.42f)));
            p.LineTo(new Vector2(X(0.88f), Y(0.94f)));
            p.LineTo(new Vector2(X(0.12f), Y(0.94f)));
            p.LineTo(new Vector2(X(0.12f), Y(0.42f)));
            p.LineTo(new Vector2(X(0.02f), Y(0.42f)));
            p.ClosePath();
            p.Fill();
            // The door, gold with navy slats.
            p.fillColor = Gold;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.28f), Y(0.52f)));
            p.LineTo(new Vector2(X(0.72f), Y(0.52f)));
            p.LineTo(new Vector2(X(0.72f), Y(0.94f)));
            p.LineTo(new Vector2(X(0.28f), Y(0.94f)));
            p.ClosePath();
            p.Fill();
            p.strokeColor = Navy;
            p.lineWidth = r.height * 0.06f;
            foreach (var y in new[] { 0.66f, 0.8f }) {
                p.BeginPath();
                p.MoveTo(new Vector2(X(0.28f), Y(y)));
                p.LineTo(new Vector2(X(0.72f), Y(y)));
                p.Stroke();
            }
        }

        /// <summary>A house with a gold door: HOME on the result.</summary>
        internal static void DrawHome(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.fillColor = Navy;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.5f), Y(0.08f)));
            p.LineTo(new Vector2(X(0.96f), Y(0.48f)));
            p.LineTo(new Vector2(X(0.84f), Y(0.48f)));
            p.LineTo(new Vector2(X(0.84f), Y(0.92f)));
            p.LineTo(new Vector2(X(0.16f), Y(0.92f)));
            p.LineTo(new Vector2(X(0.16f), Y(0.48f)));
            p.LineTo(new Vector2(X(0.04f), Y(0.48f)));
            p.ClosePath();
            p.Fill();
            p.fillColor = Gold;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.4f), Y(0.6f)));
            p.LineTo(new Vector2(X(0.6f), Y(0.6f)));
            p.LineTo(new Vector2(X(0.6f), Y(0.92f)));
            p.LineTo(new Vector2(X(0.4f), Y(0.92f)));
            p.ClosePath();
            p.Fill();
        }

        private static void DrawTarget(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            var centre = r.center;
            var radius = Mathf.Min(r.width, r.height) / 2f;
            foreach (var (share, color) in new[] { (1f, Navy), (0.8f, Red), (0.58f, White), (0.38f, Red), (0.16f, Navy) }) {
                p.fillColor = color;
                p.BeginPath();
                p.Arc(centre, radius * share, 0f, 360f);
                p.Fill();
            }
        }

        internal static void DrawLock(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.strokeColor = Navy;
            p.lineWidth = r.width * 0.12f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.3f), Y(0.48f)));
            p.LineTo(new Vector2(X(0.3f), Y(0.32f)));
            p.Arc(new Vector2(X(0.5f), Y(0.32f)), r.width * 0.2f, 180f, 360f);
            p.LineTo(new Vector2(X(0.7f), Y(0.48f)));
            p.Stroke();
            p.fillColor = Navy;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.14f), Y(0.44f)));
            p.LineTo(new Vector2(X(0.86f), Y(0.44f)));
            p.LineTo(new Vector2(X(0.86f), Y(0.96f)));
            p.LineTo(new Vector2(X(0.14f), Y(0.96f)));
            p.ClosePath();
            p.Fill();
            p.fillColor = Gold;
            p.BeginPath();
            p.Arc(new Vector2(X(0.5f), Y(0.66f)), r.width * 0.09f, 0f, 360f);
            p.Fill();
        }

        private static void DrawChevron(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.strokeColor = Navy;
            p.lineWidth = r.width * 0.3f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + r.width * 0.2f, r.y + r.height * 0.15f));
            p.LineTo(new Vector2(r.x + r.width * 0.75f, r.y + r.height * 0.5f));
            p.LineTo(new Vector2(r.x + r.width * 0.2f, r.y + r.height * 0.85f));
            p.Stroke();
        }
    }
}
