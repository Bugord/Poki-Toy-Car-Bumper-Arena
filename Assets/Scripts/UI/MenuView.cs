using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
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
    /// <para>
    /// A right rail holds XP BOOST: a tap opens a popup that offers double XP for the next matches for a rewarded ad
    /// (WATCH, or X; NO THANKS or Esc closes it). While the boost runs the tile turns blue with ×2 and the matches left,
    /// and the XP bar carries ×2 XP. B opens it from the keyboard.
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

        // The rails with a mouse: at the reference size a tile's caption was about 16 px on a 1080p screen.
        private const float DesktopRailScale = 1.25f;

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

        // The daily gift's card: its width in reference pixels upright and sideways (ui_menu), for the fit to the screen.
        private const float DailyWidth = 960f;
        private const float DailyWideWidth = 1760f;

        // The missions' card sideways. The Expand panel is always 1920 reference pixels tall there, so at the sheets'
        // usual scale the card read far smaller than upright; at this scale its text is about as large as upright on a
        // 16:9 screen. Its height (three rows, with room for a task on two lines) keeps it inside the screen's height.
        private const float WideMissionsScale = 1.7f;
        private const float MissionsHeight = 800f;

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
        // The colour of XP across the game (--xp in ui_theme).
        private static readonly Color XpBlue = new Color32(0x3d, 0x8b, 0xff, 0xff);
        private static readonly Color White = Color.white;
        // A mode not out yet keeps its sign, greyed: --shade filled, --muted outlined, its sand between.
        private static readonly Color SoonFill = new Color32(0xc7, 0xd3, 0xea, 0xff);
        private static readonly Color SoonLine = new Color32(0x5b, 0x6b, 0x8c, 0xff);
        private static readonly Color SoonSand = new Color32(0x9a, 0xa8, 0xc4, 0xff);
        // The desktop key that opens the modes: M is MISSIONS, and the plate reads QUICK MATCH.
        private const KeyCode ModesKey = KeyCode.Q;
        private const float ShakeSeconds = 0.3f;

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
        private VisualElement bottom;
        private VisualElement missionCard;
        private VisualElement actions;
        private Label missionTask;
        private MissionLook.Icon missionIcon;
        private MissionLook.Progress missionProgress;
        private VisualElement missionReward;
        private VisualElement missionsPopup;
        private VisualElement missionsSheet;
        private VisualElement missionsList;
        private VisualElement modesPopup;
        private CarPortrait modePortrait;
        private GarageStage garageStage;
        private MenuLeagues leagues;
        private VisualElement rightRail;
        private VisualElement boostTile;
        private Label boostLeft;
        private VisualElement xpBoost;
        private Label[] xpBoostLabels;
        private VisualElement boostPopup;
        private VisualElement boostPips;
        private VisualElement coinPill;
        private VisualElement dailyTile;
        private Label dailyTime;
        private VisualElement dailyPopup;
        private MenuDaily daily;
        // The daily popup shows a gift just taken: it keeps the claim on screen until it closes.
        private bool isDailyClaimed;
        private bool wasDailyReady;

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
            BuildBoost(menu);
            BuildBottom(menu);
            BuildMissionsPopup(menu);
            BuildModesPopup(menu);
            BuildBoostPopup(menu);
            BuildDailyPopup(menu);
            // The daily gift waits behind NEW LEAGUE!: it opens once that closes, unless PLAY or the garage closed it.
            leagues = new MenuLeagues(menu, audioService.PlayUiClick, () => {
                Root?.Focus();
                Root?.schedule.Execute(OfferDaily);
            });
            // The DAILY tile's timer counts down, and the gift turns ready at midnight while the menu is up.
            Root.schedule.Execute(TickDaily).Every(1000);

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
            if (ViewModel.CanOfferBoost) {
                ViewModel.BoostShown();
            }

            // The first menu in a new league greets it, over the league's sky and plinth already behind the dim.
            if (!ViewModel.IsDecided && ViewModel.TakeNewLeague()) {
                ClosePopups();
                leagues.ShowNewLeague(ViewModel.Level.Level, !isTouch);
                return;
            }

            // The first menu of the visit with a gift to claim opens the daily gift by itself.
            OfferDaily();
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
        private bool IsBusy => !IsShown || ViewModel.IsDecided || ViewModel.IsGarageOpen || ViewModel.IsSettingsOpen ||
            ViewModel.IsAdPlaying;

        private bool IsPopupOpen => missionsPopup.ClassListContains(OpenClass) || modesPopup.ClassListContains(OpenClass) ||
            IsBoostOpen || IsDailyOpen || leagues.IsNewLeagueOpen || leagues.IsListOpen;

        private bool IsBoostOpen => boostPopup.ClassListContains(OpenClass);

        private bool IsDailyOpen => dailyPopup.ClassListContains(OpenClass);

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
            var track = Face("menu__xp-track", "menu__xp-back");
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
            // ×2 XP on the bar's top edge while the XP boost runs.
            xpBoost = Element("menu__xp-boost");
            xpBoost.Add(RoundOverProgress.Outlined("", "menu__xp-boost-label", out xpBoostLabels));
            xp.Add(xpBoost);
            level.Add(xp);
            // The level badge goes over the bar's left end, which tucks under it.
            level.Add(levelBadgeSlot);
            top.Add(level);
            top.Add(Element("menu__spacer"));

            coinPill = Element("menu__coins");
            coinPill.Add(Element("menu__coins-drop"));
            var coinFace = Face("menu__coins-face", "menu__coins-fill");
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
            // AWARDS waits until there are awards: a locked tile that explains nothing only takes the rail.
            rail.Add(garageTile);
            rail.Add(missionsTile);
            menu.Add(rail);
        }

        /// <summary>
        /// The rail at the right: DAILY (a gift box, a green READY pill and a red dot while today's gift waits, the time
        /// to the next on a navy pill after), then XP BOOST (a blue star with an AD tag while it is offered, blue with ×2
        /// and the matches left while it runs).
        /// </summary>
        private void BuildBoost(VisualElement menu)
        {
            rightRail = Element("menu__rail");
            rightRail.AddToClassList("menu__rail--right");
            dailyTile = Tile(MenuDaily.DrawGift, () => Strings.Get("menu.daily"), "D", OpenDaily);
            dailyTile.AddToClassList("menu__tile--daily");
            dailyTime = Text("menu__daily-time");
            dailyTile.Q<VisualElement>(className: "menu__tile-button").Add(dailyTime);
            rightRail.Add(dailyTile);
            boostTile = Tile(DrawXpStar, () => Strings.Get("menu.xp_boost"), "B", OpenBoost);
            boostTile.AddToClassList("menu__tile--boost");
            var button = boostTile.Q<VisualElement>(className: "menu__tile-button");
            var times = RoundOverProgress.Outlined("", "menu__boost-times", out var timesLabels);
            foreach (var label in timesLabels) {
                text.Set(label, () => $"×{ViewModel.BoostMultiplier}");
            }

            button.Q<VisualElement>(className: "menu__face").Add(times);
            // A video sign on a purple tag, not a word: РЕКЛАМА or ANUNCIO ran into the tile beside it.
            var ad = Element("menu__ad");
            ad.Add(RoundOverRewards.PlaySign());
            button.Add(ad);
            boostLeft = Text("menu__boost-left");
            button.Add(boostLeft);
            rightRail.Add(boostTile);
            menu.Add(rightRail);
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

            // The car's name on a white pill with a green garage sign: smaller than PLAY, and it says the car opens
            // the garage.
            var name = carNameBox = Element("menu__car-name");
            var pill = Sticker("menu__car-pill", OpenGarage);
            var pillFace = pill.Q<VisualElement>(className: "menu__face");
            carName = Text("menu__car-name-text");
            // A longer or shorter name may or may not fit between the bottom cards sideways (PlaceStage).
            carName.RegisterCallback<GeometryChangedEvent>(_ => PlaceStage());
            pillFace.Add(carName);
            var sign = Element("menu__car-garage");
            sign.Add(Icon("menu__car-garage-icon", DrawGarage));
            pillFace.Add(sign);
            name.Add(pill);
            stage.Add(name);
            stage.RegisterCallback<GeometryChangedEvent>(_ => PlaceHero());
            menu.Add(stage);
        }

        private void BuildBottom(VisualElement menu)
        {
            bottom = Element("menu__bottom");
            // The slot's sign, the task (on two lines if it must: a Russian task is long) over its reward, and the count
            // with a thin bar at the right for a mission of more than one step.
            missionCard = Sticker("menu__mission", OpenMissions);
            var face = missionCard.Q<VisualElement>(className: "menu__face");
            missionIcon = MissionLook.IconOf(MissionSlot.Round);
            face.Add(missionIcon);
            var body = Element("menu__mission-body");
            missionTask = Text("menu__mission-task");
            body.Add(missionTask);
            missionReward = Element("menu__mission-reward");
            body.Add(missionReward);
            face.Add(body);
            missionProgress = MissionLook.ProgressOf();
            face.Add(missionProgress);
            bottom.Add(missionCard);

            actions = Element("menu__actions");
            // The mode in play, and a chevron: it opens the modes, where CUP and SURVIVAL wait as SOON. As a plate that
            // did nothing, players tapped it and took the menu for broken.
            var mode = Sticker("menu__mode", OpenModes);
            mode.Add(KeyCap(ModesKey.ToString(), "menu__mode-key"));
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
            missionsPopup = Popup(menu, () => Strings.Get("mission.title"), out missionsSheet);
            missionsList = Element(null);
            missionsSheet.Add(missionsList);
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

            sheet.Add(SoonMode("menu.cup", "menu.cup_hint", DrawCup));
            sheet.Add(SoonMode("menu.survival", "menu.survival_hint", DrawHourglass));

            var play = PlayButton("menu__modes-play", false);
            sheet.Add(play);
        }

        /// <summary>
        /// The XP boost's offer: ×N XP for the next matches, a pip a match, and WATCH (the rewarded ad) or NO THANKS.
        /// The ad plays only after WATCH.
        /// </summary>
        private void BuildBoostPopup(VisualElement menu)
        {
            boostPopup = Popup(menu, () => Strings.Get("menu.boost_title"), out var sheet);
            boostPopup.AddToClassList("menu__boost-popup");

            var offer = Element("menu__boost-offer");
            var orb = Element("menu__boost-orb");
            orb.Add(Icon("menu__boost-star", DrawXpStar));
            offer.Add(orb);
            var body = Element("menu__boost-body");
            var times = Text("menu__boost-xp");
            text.Set(times, () => Strings.Format("menu.boost_xp", ViewModel.BoostMultiplier));
            body.Add(times);
            var matches = Text("menu__boost-matches");
            text.Set(matches, () => Strings.Format("menu.boost_matches", ViewModel.BoostMatches));
            body.Add(matches);
            boostPips = Element("menu__boost-pips");
            body.Add(boostPips);
            offer.Add(body);
            sheet.Add(offer);

            var hint = Text("menu__boost-hint");
            text.Set(hint, () => Strings.Get("menu.boost_hint"));
            sheet.Add(hint);

            var watch = new Button(PressWatch);
            watch.RemoveFromClassList(Button.ussClassName);
            watch.AddToClassList("button");
            watch.AddToClassList("button--purple");
            watch.AddToClassList("menu__watch");
            watch.focusable = false;
            watch.Add(Element("button__face"));
            watch.Add(RoundOverRewards.PlaySign());
            var watchLabel = RoundOverProgress.Outlined("", "menu__watch-label", out var watchLabels);
            foreach (var label in watchLabels) {
                label.AddToClassList("button__label");
                text.Set(label, () => Strings.Get("menu.watch"));
            }

            watch.Add(watchLabel);
            // Shown only with a mouse, by `menu--desktop`, as the tiles' keys.
            watch.Add(KeyCap("X", "menu__watch-key"));

            var tag = Text("menu__watch-tag");
            text.Set(tag, () => Strings.Get("result.ad"));
            watch.Add(tag);
            sheet.Add(watch);

            var no = new Button(() => {
                if (!ViewModel.IsAdPlaying) {
                    audioService.PlayUiClick();
                    ClosePopups();
                }
            });
            no.RemoveFromClassList(Button.ussClassName);
            no.AddToClassList("button");
            no.AddToClassList("button--white");
            no.AddToClassList("menu__no-thanks");
            no.focusable = false;
            no.Add(Element("button__face"));
            var noLabel = Text("button__label");
            noLabel.AddToClassList("menu__no-thanks-label");
            text.Set(noLabel, () => Strings.Get("menu.no_thanks"));
            no.Add(noLabel);
            no.Add(KeyCap("ESC", "menu__no-thanks-key"));

            sheet.Add(no);
        }

        private static VisualElement KeyCap(string key, string className)
        {
            var cap = Element("key");
            cap.AddToClassList(className);
            var keyText = Text("key__text");
            keyText.text = key;
            cap.Add(keyText);
            return cap;
        }

        /// <summary>
        /// A mode that is not out yet: its sign, name and what it will be, all greyed, and a tilted SOON plate on its top
        /// edge. A press only shakes it; it never becomes the mode in play.
        /// </summary>
        private VisualElement SoonMode(string nameKey, string hintKey, System.Action<MeshGenerationContext> draw)
        {
            var card = Element("menu__mode-card");
            card.AddToClassList("menu__mode-card--soon");
            card.pickingMode = PickingMode.Position;
            card.RegisterCallback<ClickEvent>(_ => Soon(card));
            card.Add(Icon("menu__mode-sign", draw));
            var body = Element("menu__mode-text");
            var name = Text("menu__mode-card-name");
            text.Set(name, () => Strings.Get(nameKey));
            var hint = Text("menu__mode-hint");
            text.Set(hint, () => Strings.Get(hintKey));
            body.Add(name);
            body.Add(hint);
            card.Add(body);
            var soon = Text("menu__tag");
            soon.AddToClassList("menu__tag--soon");
            text.Set(soon, () => Strings.Get("menu.soon"));
            card.Add(soon);
            return card;
        }

        private VisualElement Popup(VisualElement menu, System.Func<string> title, out VisualElement sheet)
        {
            var popup = PopupFrame(menu, out sheet, out var titles, () => {
                audioService.PlayUiClick();
                ClosePopups();
            }, ClosePopups);
            foreach (var label in titles) {
                text.Set(label, title);
            }

            return popup;
        }

        /// <summary>
        /// The frame of the menu's popups, also for the daily gift over a result: a dim over the screen, which a tap
        /// closes with <paramref name="tapOutside"/>, and a white card with the title (twice, outline and face, for the
        /// caller to fill) and the close button at its top.
        /// </summary>
        internal static VisualElement PopupFrame(VisualElement parent, out VisualElement sheet, out Label[] titles,
            System.Action pressClose, System.Action tapOutside)
        {
            var popup = Element("menu__popup");
            popup.pickingMode = PickingMode.Position;
            popup.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == popup) {
                    tapOutside();
                }
            });
            sheet = Element("card");
            sheet.AddToClassList("menu__sheet");
            sheet.pickingMode = PickingMode.Position;
            var head = Element("menu__sheet-head");
            var stroke = Element("stroke");
            titles = new Label[2];
            foreach (var isFace in new[] { false, true }) {
                var label = Text("display");
                label.AddToClassList("menu__sheet-title");
                label.EnableInClassList("stroke__face", isFace);
                titles[isFace ? 1 : 0] = label;
                stroke.Add(label);
            }

            head.Add(stroke);
            head.Add(CloseSticker(pressClose));
            sheet.Add(head);
            popup.Add(sheet);
            parent.Add(popup);
            return popup;
        }

        /// <summary>The daily gift: its calendar, CLAIM and CLAIM ×2 (<see cref="MenuDaily"/>) on the menu's popup card.</summary>
        private void BuildDailyPopup(VisualElement menu)
        {
            dailyPopup = Popup(menu, () => Strings.Get("daily.title"), out var sheet);
            dailyPopup.AddToClassList("menu__daily-popup");
            daily = new MenuDaily(sheet, PressDailyDouble, PressDailyClaim, () => {
                audioService.PlayUiClick();
                ClosePopups();
            });
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
            button.Add(Face("menu__face"));
            return button;
        }

        /// <summary>A sticker's face: navy itself, its colour a layer of its own inside the border (menu__fill).</summary>
        /// <remarks>UI Toolkit blends a face's own background into the anti-aliased outer edge of its border, which
        /// draws a light seam where the face meets its navy drop. A navy face with the colour inside its border leaves
        /// that edge navy on navy.</remarks>
        internal static VisualElement Face(string className, string fillClass = "menu__fill")
        {
            var face = Element(className);
            face.Add(Element(fillClass));
            return face;
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
            // NEW says it already: the dot only when there is no new car to show.
            garageTile.EnableInClassList("menu__tile--dot", ViewModel.CanShop && !ViewModel.HasNewCar);
            RefreshBoost();
            RefreshDaily();
            rightRail.style.display = boostTile.style.display == DisplayStyle.Flex ||
                dailyTile.style.display == DisplayStyle.Flex ? DisplayStyle.Flex : DisplayStyle.None;
            // A gift just taken stays on the card as it was claimed (the hat's day would show coins once the hat is owned).
            if (IsDailyOpen && !isDailyClaimed) {
                daily.Fill(ViewModel.Daily);
            }

            var car = ViewModel.Car;
            carName.text = GameText.Car(car);
            modePortrait.Set(car, ViewModel.CarColor);
            ShowCar(car);

            var nearest = ViewModel.NearestMission;
            missionCard.style.display = nearest == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (nearest != null) {
                var definition = nearest.Definition;
                missionIcon.Set(definition.Slot, false);
                missionTask.text = ViewModel.MissionText(definition);
                missionProgress.Set(nearest.Progress, definition.Target);
                missionReward.Clear();
                missionReward.Add(MissionLook.Reward(ViewModel.MissionXp(definition),
                    ViewModel.MissionCoins(definition)));
            }

            if (missionsPopup.ClassListContains(OpenClass)) {
                FillMissions();
            }
        }

        /// <summary>The boost's tile (offered, running or gone), the bar's ×2 XP and the popup's pips.</summary>
        private void RefreshBoost()
        {
            var isBoosted = ViewModel.IsBoosted;
            boostTile.style.display = isBoosted || ViewModel.CanOfferBoost ? DisplayStyle.Flex : DisplayStyle.None;
            boostTile.EnableInClassList("menu__tile--boost-on", isBoosted);
            boostLeft.text = Strings.Format("menu.boost_left", ViewModel.BoostMatchesLeft);
            xpBoost.style.display = isBoosted ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var label in xpBoostLabels) {
                label.text = Strings.Format("menu.boost_xp", ViewModel.BoostMultiplier);
            }

            if (boostPips.childCount != ViewModel.BoostMatches) {
                boostPips.Clear();
                for (var i = 0; i < ViewModel.BoostMatches; i++) {
                    boostPips.Add(Element("menu__boost-pip"));
                }
            }
        }

        /// <summary>The DAILY tile: READY and a red dot while today's gift waits, otherwise the time to the next.</summary>
        private void RefreshDaily()
        {
            dailyTile.style.display = ViewModel.HasDaily ? DisplayStyle.Flex : DisplayStyle.None;
            var isReady = ViewModel.CanClaimDaily;
            wasDailyReady = isReady;
            dailyTile.EnableInClassList("menu__tile--dot", isReady);
            dailyTile.EnableInClassList("menu__tile--daily-ready", isReady);
            dailyTime.text = isReady ? Strings.Get("daily.ready") : TimeText(ViewModel.DailyTimeLeft);
        }

        private static string TimeText(System.TimeSpan left)
        {
            var hours = (int)left.TotalHours;
            var minutes = Mathf.Max(hours > 0 ? 0 : 1, left.Minutes);
            return hours > 0 ? Strings.Format("daily.time_hm", hours, minutes) : Strings.Format("daily.time_m", minutes);
        }

        // Once a second: the timer, and the whole menu when midnight brings a new gift.
        private void TickDaily()
        {
            // Hidden or past PLAY, the menu waits: a whole refresh would wake the garage stage's camera.
            if (Root?.panel == null || !IsShown || ViewModel.IsDecided || !ViewModel.HasDaily) {
                return;
            }

            if (ViewModel.CanClaimDaily != wasDailyReady) {
                Refresh();
            }
            else if (!wasDailyReady) {
                dailyTime.text = TimeText(ViewModel.DailyTimeLeft);
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
                // The slot's sign, the task over its XP and coins, and the count with its bar for more than one step.
                var definition = mission.Definition;
                var row = Element("menu__mission-row");
                row.Add(MissionLook.IconOf(definition.Slot));
                var body = Element("menu__mission-row-body");
                var task = Text("menu__mission-row-task");
                task.text = ViewModel.MissionText(definition);
                body.Add(task);
                body.Add(MissionLook.Reward(ViewModel.MissionXp(definition), ViewModel.MissionCoins(definition)));
                row.Add(body);
                var progress = MissionLook.ProgressOf();
                progress.Set(mission.Progress, definition.Target);
                row.Add(progress);
                missionsList.Add(row);
            }

            // USS has no :last-child, so the last row drops its gap under it by a class.
            if (missionsList.childCount > 0) {
                missionsList[missionsList.childCount - 1].AddToClassList("menu__mission-row--last");
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
            var railScale = isTouch ? scale : DesktopRailScale;
            var railGrown = new Scale(new Vector3(railScale, railScale, 1f));
            rail.style.left = left;
            rail.style.top = topEdge + topHeight + 50f * scale;
            rail.style.scale = railGrown;
            rightRail.style.left = StyleKeyword.Auto;
            rightRail.style.right = right;
            rightRail.style.top = rail.style.top;
            rightRail.style.scale = railGrown;

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
            foreach (var popup in new[] { missionsPopup, modesPopup, boostPopup }) {
                var sheet = popup.Q<VisualElement>(className: "menu__sheet");
                sheet.style.scale = new Scale(new Vector3(sheetFit, sheetFit, 1f));
            }

            if (isWide) {
                var missionsFit = Mathf.Min(WideMissionsScale, (size.y - 2f * Mathf.Max(topEdge, low)) / MissionsHeight);
                missionsSheet.style.scale = new Scale(new Vector3(missionsFit, missionsFit, 1f));
            }

            // The calendar's card is wider (one row of days sideways) and keeps inside the screen's width too.
            var dailyFit = Mathf.Min(sheetFit, (size.x - left - right) / (isWide ? DailyWideWidth : DailyWidth));
            daily.Sheet.style.scale = new Scale(new Vector3(dailyFit, dailyFit, 1f));

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
            // The car keeps clear of the boost's rail at the right as of the rail at the left.
            if (rightRail.resolvedStyle.display == DisplayStyle.Flex) {
                railRight = Mathf.Max(railRight, size.x - Root.WorldToLocal(rightRail.worldBound).xMin);
            }

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

            // Nothing acts while the boost's ad plays.
            if (ViewModel.IsAdPlaying) {
                evt.StopPropagation();
                return;
            }

            // The boost's offer: X watches the ad.
            if (IsBoostOpen && evt.keyCode == KeyCode.X) {
                evt.StopPropagation();
                PressWatch();
                return;
            }

            // The daily gift: Space or Enter claims it (or closes the card once claimed), X watches the ad for ×2, Esc
            // closes it; PLAY's and the tiles' keys do not reach through it.
            if (IsDailyOpen && evt.keyCode is not KeyCode.Escape) {
                evt.StopPropagation();
                if (evt.keyCode is KeyCode.Space or KeyCode.Return or KeyCode.KeypadEnter) {
                    if (ViewModel.CanClaimDaily) {
                        PressDailyClaim();
                    }
                    else {
                        audioService.PlayUiClick();
                        ClosePopups();
                    }
                }
                else if (evt.keyCode == KeyCode.X) {
                    PressDailyDouble();
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
                case ModesKey:
                    evt.StopPropagation();
                    OpenModes();
                    break;
                case KeyCode.B:
                    evt.StopPropagation();
                    OpenBoost();
                    break;
                case KeyCode.D:
                    evt.StopPropagation();
                    OpenDaily();
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

        private void OpenBoost()
        {
            if (IsBusy || IsPopupOpen || boostTile.style.display == DisplayStyle.None) {
                return;
            }

            audioService.PlayUiClick();
            if (!ViewModel.CanOfferBoost) {
                // A running boost has nothing to offer: its tile only answers the tap.
                Pop(boostTile);
                return;
            }

            ViewModel.BoostOpened();
            boostPopup.AddToClassList(OpenClass);
        }

        private void PressWatch()
        {
            if (IsBusy || !IsBoostOpen) {
                return;
            }

            audioService.PlayUiClick();
            RunBoost(lifetime.Token).Forget();
        }

        private async UniTaskVoid RunBoost(CancellationToken token)
        {
            bool isGranted;
            try {
                isGranted = await ViewModel.WatchBoost(token);
            }
            catch (System.OperationCanceledException) {
                return;
            }

            if (Root?.panel == null) {
                return;
            }

            // Granted or not, the offer is answered: the popup goes without counting as NO THANKS.
            boostPopup.RemoveFromClassList(OpenClass);
            Refresh();
            if (isGranted) {
                audioService.Play(audioService.Library.KnockoutCredit);
                Pop(boostTile);
                Pop(xpBoost);
            }

            Root.Focus();
        }

        private void OpenDaily()
        {
            if (IsBusy || IsPopupOpen || !ViewModel.HasDaily) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.DailyOpened();
            ShowDaily();
        }

        // The daily gift by itself: once a visit, on a menu that is up with nothing over it and a gift to claim.
        private void OfferDaily()
        {
            if (Root?.panel == null || IsBusy || IsPopupOpen || !ViewModel.TakeDailyOffer()) {
                return;
            }

            ShowDaily();
        }

        private void ShowDaily()
        {
            isDailyClaimed = false;
            daily.Fill(ViewModel.Daily);
            dailyPopup.AddToClassList(OpenClass);
            ViewModel.DailyShown();
        }

        private void PressDailyClaim()
        {
            if (IsBusy || !IsDailyOpen || isDailyClaimed || !ViewModel.CanClaimDaily) {
                return;
            }

            audioService.PlayUiClick();
            var claim = ViewModel.ClaimDaily();
            if (claim != null) {
                CelebrateDaily(claim);
            }
        }

        private void PressDailyDouble()
        {
            if (IsBusy || !IsDailyOpen || isDailyClaimed || !ViewModel.CanDoubleDaily) {
                return;
            }

            audioService.PlayUiClick();
            RunDailyAd(lifetime.Token).Forget();
        }

        private async UniTaskVoid RunDailyAd(CancellationToken token)
        {
            DailyClaim claim;
            try {
                claim = await ViewModel.WatchDaily(token);
            }
            catch (System.OperationCanceledException) {
                return;
            }

            if (Root?.panel == null || !IsDailyOpen) {
                return;
            }

            Root.Focus();
            if (claim == null) {
                // No reward: CLAIM ×2 goes, and CLAIM still takes the gift.
                daily.Fill(ViewModel.Daily);
                daily.Refuse(dailyPopup, lifetime.Token);
                return;
            }

            CelebrateDaily(claim);
        }

        // The gift is taken: the cell turns done, the celebration plays, the coins land in the counter, and the card
        // closes once it is over (as the garage's window does); the DAILY tile shows the time to the next.
        private void CelebrateDaily(DailyClaim claim)
        {
            isDailyClaimed = true;
            daily.Celebrate(dailyPopup, ViewModel.Daily, claim, garageStage ?? ViewModel.Stage, coinPill,
                lifetime.Token);
            audioService.Play(audioService.Library.KnockoutCredit);
            audioService.PlayJingle(audioService.Library.Win);
            RefreshDaily();
            Root.schedule.Execute(() => {
                if (Root?.panel == null || ViewModel.IsDecided) {
                    return;
                }

                if (IsDailyOpen && isDailyClaimed) {
                    dailyPopup.RemoveFromClassList(OpenClass);
                    isDailyClaimed = false;
                    Root.Focus();
                }

                Refresh();
            }).StartingIn((long)(RewardCelebration.Duration * 1000f));
        }

        // A quick jump, as a sticker answers a press: up and back over a fifth of a second, on the unscaled clock.
        private static void Pop(VisualElement element)
        {
            var started = Time.unscaledTime;
            element.schedule.Execute(() => {
                var t = Mathf.Clamp01((Time.unscaledTime - started) / PopSeconds);
                var size = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
                element.style.scale = new Scale(new Vector3(size, size, 1f));
            }).Every(16).Until(() => Time.unscaledTime - started > PopSeconds);
            element.schedule.Execute(() => element.style.scale = StyleKeyword.Null).StartingIn(260);
        }

        private const float PopSeconds = 0.22f;

        private void ClosePopups()
        {
            if (IsBoostOpen && !ViewModel.IsAdPlaying) {
                ViewModel.BoostDeclined();
            }

            if (IsDailyOpen && !ViewModel.IsAdPlaying && ViewModel.CanClaimDaily) {
                ViewModel.DailyDeclined();
            }

            boostPopup.RemoveFromClassList(OpenClass);
            dailyPopup.RemoveFromClassList(OpenClass);
            isDailyClaimed = false;
            missionsPopup.RemoveFromClassList(OpenClass);
            modesPopup.RemoveFromClassList(OpenClass);
            leagues?.Close();
        }

        // A mode not out yet answers a press with a click and a little shake, and stays as it is.
        private void Soon(VisualElement card)
        {
            if (IsBusy) {
                return;
            }

            audioService.PlayUiClick();
            Shake(card);
        }

        // Side to side and settling over ShakeSeconds, on the unscaled clock; a press during a shake starts it again.
        private static void Shake(VisualElement element)
        {
            var started = Time.unscaledTime;
            element.userData = started;
            bool IsOwn() => element.userData is float at && Mathf.Approximately(at, started);
            element.schedule.Execute(() => {
                var t = Mathf.Clamp01((Time.unscaledTime - started) / ShakeSeconds);
                element.style.translate = new Translate(16f * Mathf.Sin(t * Mathf.PI * 6f) * (1f - t), 0f);
            }).Every(16).Until(() => !IsOwn() || Time.unscaledTime - started > ShakeSeconds);
            element.schedule.Execute(() => {
                if (IsOwn()) {
                    element.style.translate = StyleKeyword.Null;
                }
            }).StartingIn((long)(ShakeSeconds * 1000f) + 40);
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

        /// <summary>The MODE plate's chevron: it says the plate opens the modes.</summary>
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

        /// <summary>CUP's sign, greyed while it is not out: a cup with two handles on a stem and a base.</summary>
        private static void DrawCup(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            p.strokeColor = SoonLine;
            p.lineWidth = r.width * 0.08f;
            p.BeginPath();
            p.Arc(new Vector2(X(0.24f), Y(0.32f)), r.width * 0.13f, 90f, 270f);
            p.Stroke();
            p.BeginPath();
            p.Arc(new Vector2(X(0.76f), Y(0.32f)), r.width * 0.13f, -90f, 90f);
            p.Stroke();

            p.fillColor = SoonFill;
            p.lineWidth = r.width * 0.07f;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.24f), Y(0.12f)));
            p.LineTo(new Vector2(X(0.76f), Y(0.12f)));
            p.LineTo(new Vector2(X(0.73f), Y(0.36f)));
            p.Arc(new Vector2(X(0.5f), Y(0.36f)), r.width * 0.23f, 0f, 180f);
            p.ClosePath();
            p.Fill();
            p.Stroke();
            Box(p, X(0.43f), Y(0.58f), X(0.57f), Y(0.76f));
            Box(p, X(0.27f), Y(0.76f), X(0.73f), Y(0.9f));
        }

        /// <summary>SURVIVAL's sign, greyed while it is not out: an hourglass, its sand running down.</summary>
        private static void DrawHourglass(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = SoonLine;
            p.lineWidth = r.width * 0.07f;
            p.fillColor = White;
            Glass();
            p.Fill();
            p.fillColor = SoonSand;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.37f), Y(0.27f)));
            p.LineTo(new Vector2(X(0.63f), Y(0.27f)));
            p.LineTo(new Vector2(X(0.5f), Y(0.45f)));
            p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.33f), Y(0.82f)));
            p.LineTo(new Vector2(X(0.5f), Y(0.6f)));
            p.LineTo(new Vector2(X(0.67f), Y(0.82f)));
            p.ClosePath();
            p.Fill();
            // The glass's outline over its sand, then the two caps.
            Glass();
            p.Stroke();
            p.fillColor = SoonFill;
            Box(p, X(0.18f), Y(0.06f), X(0.82f), Y(0.17f));
            Box(p, X(0.18f), Y(0.83f), X(0.82f), Y(0.94f));

            void Glass()
            {
                p.BeginPath();
                p.MoveTo(new Vector2(X(0.27f), Y(0.16f)));
                p.LineTo(new Vector2(X(0.73f), Y(0.16f)));
                p.LineTo(new Vector2(X(0.55f), Y(0.5f)));
                p.LineTo(new Vector2(X(0.73f), Y(0.84f)));
                p.LineTo(new Vector2(X(0.27f), Y(0.84f)));
                p.LineTo(new Vector2(X(0.45f), Y(0.5f)));
                p.ClosePath();
            }
        }

        // A filled and outlined rectangle in the painter's current colours.
        private static void Box(Painter2D p, float left, float top, float right, float bottom)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(left, top));
            p.LineTo(new Vector2(right, top));
            p.LineTo(new Vector2(right, bottom));
            p.LineTo(new Vector2(left, bottom));
            p.ClosePath();
            p.Fill();
            p.Stroke();
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

        /// <summary>The XP star: five points in the XP blue with a navy outline.</summary>
        internal static void DrawXpStar(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            var centre = r.center + new Vector2(0f, r.height * 0.04f);
            var outer = Mathf.Min(r.width, r.height) * 0.46f;
            var inner = outer * 0.48f;
            p.fillColor = XpBlue;
            p.strokeColor = Navy;
            p.lineWidth = outer * 0.16f;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            for (var i = 0; i < 10; i++) {
                var point = centre + Direction(-Mathf.PI / 2f + i * Mathf.PI / 5f) * (i % 2 == 0 ? outer : inner);
                if (i == 0) {
                    p.MoveTo(point);
                }
                else {
                    p.LineTo(point);
                }
            }

            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }
}
