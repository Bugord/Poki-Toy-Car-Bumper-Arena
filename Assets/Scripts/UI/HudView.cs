using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    public sealed class HudView : FadingView<HudViewModel>
    {
        // The numbers move at most once a second, so a tenth of one keeps them in step without running every frame.
        private const long RefreshInterval = 100;

        // The boost's charge grows smoothly, so it is redrawn about every frame while the button is up.
        private const long BoostRefreshInterval = 16;

        // Seconds left from which the time pill turns red.
        private const int UrgentSeconds = 10;

        // How long a beat or a pop holds its bigger scale before the transition in ui_screens eases it back.
        private const long PulseDuration = 120;

        private const string UrgentClass = "hud__timer--urgent";
        private const string BeatClass = "hud__timer--beat";
        private const string PopClass = "hud__cars--pop";
        private const string CountPunchClass = "hud__cars-count--punch";
        private const string ChipSlotClass = "hud__chip-slot";
        private const string ChipSlotClosingClass = "hud__chip-slot--closing";
        private const string ChipClass = "hud__chip";
        private const string ChipHopClass = "hud__chip--hop";
        private const string ChipFallClass = "hud__chip--fall";
        private const string BoostShownClass = "hud__boost--shown";
        private const string NoBoostClass = "hud--no-boost";
        private const string BoostReadyClass = "hud__boost--ready";
        private const string BoostDesktopClass = "hud__boost--desktop";
        private const string BoostPopClass = "hud__boost--pop";
        private const string BoostPressedClass = "hud__boost--pressed";
        private const string BoostLeftClass = "hud__boost-slot--left";
        private const string BoostMovingClass = "hud__boost-slot--moving";
        private const long BoostMoveDuration = 120;
        private const string KnockoutShownClass = "hud__ko--shown";
        private const string KnockoutPopClass = "hud__ko--pop";
        private const string StartShownClass = "hud__start--shown";
        private const string StartEnterClass = "hud__start--enter";
        private const string StartLeaveClass = "hud__start--leave";
        private const string StartBobClass = "hud__start--bob";
        private const string StartWideClass = "hud__start--wide";
        private const string StartTouchClass = "hud__start--touch";
        private const string StartDesktopClass = "hud__start--desktop";
        private const string StickShownClass = "hud__stick--shown";
        private const string PopupStartClass = "ko-pop--start";
        private const string PopupLeaveClass = "ko-pop--leave";
        private const string NameEnterClass = "car-name--enter";
        private const string NameLeaveClass = "car-name--leave";
        private const string NamePulseClass = "car-name--pulse";
        private const string SettingsUnderClass = "hud__settings--under";
        private const string RespawnShownClass = "hud__respawn--shown";
        private const string RespawnPopClass = "hud__respawn--pop";
        private const string ShieldShownClass = "hud__shield--shown";
        private const string StreakShownClass = "hud__streak--shown";
        private const string RevengeClass = "car-name--revenge";
        private const string NamesWaitingClass = "car-names--waiting";
        private const string PowerShownClass = "hud__power--shown";
        private const string ItemShownClass = "hud__item--shown";
        private const string ItemEmptyClass = "hud__item--empty";
        private const string ItemRollingClass = "hud__item--rolling";
        private const string ItemPopClass = "hud__item--pop";
        private const string ItemPressedClass = "hud__item--pressed";
        private const string ItemDesktopClass = "hud__item--desktop";
        private const string ItemHintPulseClass = "hud__item--hint-pulse";
        private const string ItemHintShownClass = "hud__item-hint--shown";
        private const string ItemHintDesktopClass = "hud__item-hint--desktop";
        private const string SlotItemsClass = "hud__boost-slot--items";
        private const string WideClass = "hud--wide";

        // The slot's roulette shows a new item this many times a second while the box's roll lasts, on unscaled time.
        private const float ItemSpinRate = 14f;

        // The first item of a visit says how to use it for this long, the button pulsing meanwhile, in unscaled seconds.
        private const float ItemHintDuration = 2.6f;
        private const float ItemHintPulsePeriod = 0.65f;

        // The kinds the slot can hold, in the order its roulette shows them.
        private static readonly ItemKind[] ItemKinds =
        {
            ItemKind.Mega, ItemKind.Shockwave, ItemKind.Bomb, ItemKind.Bowling, ItemKind.Balloon, ItemKind.Oil,
        };

        // The kinds that run for a while once used, with a badge of their own.
        private static readonly ItemKind[] TimedKinds =
        {
            ItemKind.Mega, ItemKind.Shield, ItemKind.Turbo, ItemKind.Balloon,
        };

        // The badge of a running ability blinks over the last seconds, as the ability ends.
        private const float PowerBlinkTime = 2f;
        private const float PowerBlinkRate = 5f;

        // From this width against the height the arena, fitted to the height, leaves a side free for the scoreboard's
        // full column; a narrower screen shows the leaders and the player's row only.
        private const float ScoreboardWideAspect = 1.65f;

        // Reference pixels, matching ui_screens. Keep every top row below Poki's pill without moving its touch target
        // into the arena; the gear is the highest element, 40 px from the top rather than the bar's 54.
        private const float BarTop = 54f;
        private const float SettingsTop = 40f;
        private const float SettingsUnderTop = 216f;
        private const float PillGap = 18f;
        private const float BoostSide = 64f;
        private const float BoostBottom = 220f;
        // A phone held wide with no BOOST: the larger item button stands further in from the side and the bottom,
        // where the thumb rests, not on the bezel.
        private const float WideItemSide = 190f;
        private const float WideItemBottom = 250f;

        // The safe sticker floats this high over the player's car, in metres: above its marker's arrow.
        private const float ShieldHeight = 4.2f;

        // Names sit above the body in panel pixels; a rival springs in, pulses twice, then stays quiet.
        // Close over the car, as the tail points at it.
        private const float NameLift = 58f;
        private const float NameMargin = 24f;
        private const float NameGap = 8f;
        private const float NameMaxLiftHeights = 3f;
        private const float NameShiftTime = 0.16f;
        private const long NameLeaveDuration = 400;
        private const long RevengeEnterDuration = 350;
        private const long RevengePulseInterval = 500;

        // The start prompt bobs: each toggle eases it a little bigger or back (hud__start-prompt's transition), so a bob
        // takes twice this, as SMUP's TAP TO START.
        private const long StartBobInterval = 600;

        // How long the start prompt takes to flash away (hud__start--leave) before it is taken off the screen.
        private const long StartLeaveDuration = 200;

        // The joystick's disc and knob against how far the knob goes from the middle: the disc a little wider, so the
        // knob pushed all the way still mostly sits on it, and the knob a thumb's size.
        private const float StickBaseScale = 1.3f;
        private const float StickKnobScale = 0.55f;

        // First-round touch teaching, in unscaled seconds and reference panel pixels. The ghost uses the real
        // joystick's proportions, with an opaque pointing hand whose fingertip follows the knob right, up, left, down.
        private const float GhostCycle = 2f;
        private const float GhostOpacity = 0.9f;
        // After the first round the ghost only reminds where the joystick is, fainter and without its caption.
        private const float GhostLaterOpacity = 0.5f;
        private const float GhostMaxRadius = 120f;
        private const float GhostTravel = 0.8f;
        // The hand's picture (sprite_ui_hand_point, Kenney's Input Prompts hand with our outline): its width against the
        // joystick's radius, its height against its width, and where its fingertip is, as a share of each. The finger
        // already leans left in the picture, so it is not turned.
        private const float GhostHandWidthScale = 2f;
        private const float GhostHandAspect = 1f;
        private static readonly Vector2 GhostHandTip = new(0.23f, 0.07f);
        private const float GhostFingerTilt = 0f;
        private const float GhostPortraitY = 0.8f;
        private const float GhostWideX = 0.64f;
        private const float GhostWideY = 0.84f;
        private const float GhostHalfWidth = 340f;
        private const float GhostCaptionLift = 280f;
        private const float GhostCarClearance = 100f;
        private const float TeachingEnterTime = 0.25f;
        private const float TeachingLeaveTime = 0.16f;
        private const float BoostHintInterval = 4f;
        private const float BoostHintDuration = 2f;
        private const float BoostHintPulsePeriod = 0.7f;
        private const string BoostHintPulseClass = "hud__boost--hint-pulse";
        private const string BoostHintDesktopClass = "hud__boost-hint--desktop";
        private const float TapHintDuration = 1f;
        private const float TapHintWidth = 560f;
        private const float TapHintHeight = 110f;
        private const float TapHintGap = 80f;
        private const float TapHintLift = 150f;
        private const float TeachingMargin = 24f;

        // The KNOCKOUT! sticker: how long it springs in and then holds before it flies off, and how long that takes
        // (the transitions of ko-pop in ui_screens), in milliseconds.
        private const long PopupEnterDuration = 300;
        private const long PopupHoldDuration = 800;
        private const long PopupLeaveDuration = 350;

        // A car's chip leaving the stack (the transitions of hud__chip in ui_screens), in milliseconds: it hops up,
        // then falls as it shrinks away while the chips after it close up, and its place goes once it is gone.
        private const long ChipHopDuration = 150;
        private const long ChipLeaveDuration = 400;

        // How far the sticker's centre keeps from the sides of the screen, from the top (under the time pill, the cars
        // pill and the KO badge, which end 458 px down) and from the bottom (over the boost button), in reference
        // pixels, so it is always whole and covers none of the HUD.
        private const float PopupSideMargin = 400f;
        private const float PopupTopMargin = 572f;
        private const float PopupBottomMargin = 480f;

        // About half the size of the sticker sprung to its full scale, in reference pixels, to keep it off the scoreboard.
        private const float PopupHalfWidth = 260f;
        private const float PopupHalfHeight = 90f;

        // The sticker sits this far above the point where the car went over the edge, in reference pixels, so the car
        // still shows under it.
        private const float PopupLift = 110f;

        // The tilt of the sticker, in degrees, every other one the other way so two at once do not look alike.
        private const float PopupTilt = 7f;

        // The kick-off's look, in unscaled seconds, degrees and reference pixels: a number springs in from small and
        // tilted (a little overshoot), swells a touch while it holds and shrinks away at the end of its step; GO! springs
        // harder, its ring and rays fly out, and it swells away. Its timing is the round's (GameTuning.Round.Kickoff*).
        private const string KickoffShownClass = "kickoff--shown";
        private const string KickoffSpecialShownClass = "kickoff__special--shown";
        private const float KickoffEnter = 0.24f;
        private const float KickoffLeave = 0.16f;
        private const float KickoffFadeIn = 0.06f;
        private const float KickoffStartScale = 0.2f;
        private const float KickoffHoldGrowth = 0.06f;
        private const float KickoffTilt = 6f;
        private const float KickoffStartTilt = 20f;
        private const float KickoffDrop = 90f;
        private const float KickoffOvershoot = 2.2f;
        private const float KickoffGoOvershoot = 3f;
        private const float KickoffGoTilt = -5f;
        private const float KickoffGoLeave = 0.24f;
        private const float KickoffGoLeaveScale = 1.4f;
        private const float KickoffBurstTime = 0.5f;
        private const float KickoffRingScale = 4.2f;
        private const float KickoffRingOpacity = 0.7f;
        private const float KickoffRayFrom = 190f;
        private const float KickoffRayTo = 640f;
        private const int KickoffRays = 12;

        // The digits the kick-off counts with, made once (GameTuning.Round.KickoffFrom is at most 5).
        private static readonly string[] KickoffDigits = { "0", "1", "2", "3", "4", "5" };

        private readonly HudControls controls;
        private readonly IAudioService audioService;
        private CancellationTokenSource lifetime;
        private Button settingsButton;
        private SafeInsets safeInsets;
        private float hudTopInset;
        private float contentTopInset;
        private HudSocial social;
        private VisualElement timePill;
        private Label timeLabel;
        private Label timeFaceLabel;
        private VisualElement carsPill;
        private VisualElement chipRow;
        private Label carsLeftLabel;
        // The place of each car's chip in the stack by the car's index, null once it has left (or before it is built).
        private VisualElement[] chipSlots;
        private int shownSeconds = -1;
        private int shownCarsLeft = -1;
        private VisualElement boostButton;
        private VisualElement boostSlot;
        private VisualElement boostCharge;
        private bool isBoostOnLeft;
        private bool isBoostMoving;
        private bool isBoostShown;
        private bool isBoostShownReady;
        private VisualElement knockoutBadge;
        private Label knockoutLabel;
        // A match's score: the scoreboard of every car, the comeback countdown and the safe sticker.
        private HudScoreboard scoreboard;
        private int shownRespawn = -1;
        private VisualElement respawn;
        private Label respawnCount;
        private Label respawnCountFace;
        private VisualElement shield;
        private VisualElement shieldFill;
        private VisualElement streak;
        private int streakShown = -1;
        // A match's pickups: the badge of the ability the player's car runs, with its icon, name and draining bar.
        private ItemKind shownAbility;
        private VisualElement power;
        private VisualElement powerIcon;
        private Label powerName;
        private VisualElement powerFill;
        // A match's item slot: the button, its icon, the kind it shows, and the one-time hint over it.
        private VisualElement itemButton;
        private VisualElement itemIcon;
        private VisualElement itemHint;
        private Label itemHintText;
        private Label itemHintFace;
        private ItemKind shownItem = ItemKind.None;
        private bool isItemShownRolling;
        private bool isItemHintShown;
        private float itemHintTime;
        private float nextItemPulse;
        private VisualElement popupLayer;
        private VisualElement nameLayer;
        private VisualElement[] carNames;
        private NamePlacement[] namePlacements;
        private int[] nameOrder;
        private float nameUpdateTime;
        private bool isRevengeIntroduced;
        private int shownKnockouts;
        private int popupCount;
        private VisualElement start;
        private IVisualElementScheduledItem startBob;
        private IVisualElementScheduledItem startWatch;
        private VisualElement stick;
        private VisualElement stickBase;
        private VisualElement stickKnob;
        private bool isStickShown;
        private VisualElement ghost;
        private VisualElement ghostBase;
        private VisualElement ghostKnob;
        private VisualElement ghostFinger;
        private VisualElement boostHint;
        private VisualElement tapHint;
        private float teachingTime;
        private float idleSince;
        private float seenTouchTime = float.NegativeInfinity;
        private int idleHintCount;
        private bool hasStartTouch;
        private bool isIdleHintShown;
        private bool isGhostShown;
        private float ghostTime;
        private float ghostProgress;
        private Vector2 ghostPanelSize;
        private Vector2 ghostCenter;
        private bool isBoostHintShown;
        private float boostHintTime;
        private float nextBoostHintTime;
        private float boostHintProgress;
        private bool hasTapHint;
        private float tapHintTime;
        private float tapHintProgress;
        private HudEndTimer endTimer;
        private HudPotato potato;
        private VisualElement kickoff;
        private VisualElement kickoffNumber;
        private Label kickoffDigit;
        private Label kickoffDigitFace;
        private VisualElement kickoffGo;
        private VisualElement kickoffRing;
        private VisualElement kickoffSpecial;
        private Label kickoffSpecialName;
        private Label kickoffSpecialNameFace;
        private Label kickoffSpecialHint;
        private VisualElement[] kickoffRays;
        private int shownKickoffStep = HudViewModel.NoKickoff;
        private bool isKickoffShown;

        private struct NamePlacement
        {
            public Vector2 Anchor;
            public float ScreenY;
            public Rect Bounds;
            public float Lift;
        }

        public HudView(HudControls controls, IAudioService audioService)
        {
            this.controls = controls;
            this.audioService = audioService;
        }

        protected override void OnAttached()
        {
            // The HUD lies over the game: nothing on it may take a click or a touch from the controls but the boost
            // button and the settings button, which tell the controls where they are so a finger on them does not steer.
            Root.pickingMode = PickingMode.Ignore;
            lifetime = new CancellationTokenSource();

            timePill = Root.Q<VisualElement>("time-pill");
            timeLabel = Root.Q<Label>("time");
            timeFaceLabel = Root.Q<Label>("time-face");
            carsPill = Root.Q<VisualElement>("cars-pill");
            chipRow = Root.Q<VisualElement>("chips");
            carsLeftLabel = Root.Q<Label>("cars-left");
            boostButton = Root.Q<VisualElement>("boost-button");
            boostSlot = Root.Q<VisualElement>("boost-slot");
            boostCharge = Root.Q<VisualElement>("boost-charge");
            knockoutBadge = Root.Q<VisualElement>("ko-badge");
            DrawAsBurst(knockoutBadge.Q<VisualElement>(className: "ko-star"));
            knockoutLabel = Root.Q<Label>("ko");
            respawn = Root.Q<VisualElement>("respawn");
            respawnCount = Root.Q<Label>("respawn-count");
            respawnCountFace = Root.Q<Label>("respawn-count-face");
            shield = Root.Q<VisualElement>("shield");
            shieldFill = Root.Q<VisualElement>("shield-fill");
            streak = CreateStreak(shield);
            power = Root.Q<VisualElement>("power");
            powerIcon = Root.Q<VisualElement>("power-icon");
            powerName = Root.Q<Label>("power-name");
            powerFill = Root.Q<VisualElement>("power-fill");
            itemButton = Root.Q<VisualElement>("item-button");
            itemIcon = Root.Q<VisualElement>("item-icon");
            itemHint = Root.Q<VisualElement>("item-hint");
            itemHintText = Root.Q<Label>("item-hint-text");
            itemHintFace = Root.Q<Label>("item-hint-face");
            // Only a round with boxes has items; one without keeps the slot as it was.
            itemButton.EnableInClassList(ItemShownClass, ViewModel.HasItems);
            itemButton.AddToClassList(ItemEmptyClass);
            // In a match the player's score is always there, 0 included: it is the point of the match. The scoreboard
            // shows the leader and everyone else, so the bar keeps only the time and the player's own knock-outs.
            knockoutBadge.EnableInClassList(KnockoutShownClass, ViewModel.IsMatch);
            carsPill.style.display = ViewModel.IsMatch ? DisplayStyle.None : DisplayStyle.Flex;
            scoreboard = HudScoreboard.Create(Root.Q<VisualElement>("scoreboard"), ViewModel);
            popupLayer = Root.Q<VisualElement>("ko-layer");
            nameLayer = Root.Q<VisualElement>("name-layer");
            start = Root.Q<VisualElement>("start");
            stick = Root.Q<VisualElement>("stick");
            stickBase = Root.Q<VisualElement>("stick-base");
            stickKnob = Root.Q<VisualElement>("stick-knob");
            ghost = Root.Q<VisualElement>("ghost");
            Root.Q<VisualElement>("ghost-stick").style.opacity = ViewModel.IsFirstRound ? GhostOpacity : GhostLaterOpacity;
            Root.Q<VisualElement>(className: "hud__ghost-caption").style.display =
                ViewModel.IsFirstRound ? DisplayStyle.Flex : DisplayStyle.None;
            // The side the player plays on carries over from the round before, without the button moving at the start.
            isBoostOnLeft = controls.IsBoostOnLeft;
            boostSlot.EnableInClassList(BoostLeftClass, isBoostOnLeft);
            ghostBase = Root.Q<VisualElement>("ghost-base");
            ghostKnob = Root.Q<VisualElement>("ghost-knob");
            ghostFinger = Root.Q<VisualElement>("ghost-finger");
            ghostFinger.style.rotate = new Rotate(GhostFingerTilt);
            boostHint = Root.Q<VisualElement>("boost-hint");
            tapHint = Root.Q<VisualElement>("tap-hint");
            BuildKickoff();
            endTimer = HudEndTimer.Create(Root, ViewModel, timePill, scoreboard?.Element, respawn, popupLayer);
            potato = HudPotato.Create(nameLayer, Root, ViewModel);
            settingsButton = Root.Q<Button>("settings-button");
            settingsButton.RemoveFromClassList(Button.ussClassName);
            // Space is the boost: a focused button would take it as a click and open the settings mid-round.
            settingsButton.focusable = false;
            settingsButton.clicked += OpenSettings;
            controls.SetSettingsButton(settingsButton);
            Root.RegisterCallback<GeometryChangedEvent>(PlaceHud);
            social = new HudSocial(Root, ViewModel, stick, boostSlot, timePill, carsPill, knockoutBadge, settingsButton,
                scoreboard);
            SafeArea.Watch(Root, _ => PlaceHud(null));
            PokiPill.Watch(Root, () => PlaceHud(null));
            PlaceHud(null);
            teachingTime = idleSince = Time.unscaledTime;
            ViewModel.PlayerKnockout += ShowKnockout;
            ViewModel.CarOut += OnCarOut;
            ViewModel.CarBack += OnCarBack;
            ViewModel.NemesisChanged += OnNemesisChanged;
            ViewModel.ItemRolled += OnItemRolled;
            ViewModel.ItemUsed += OnItemUsed;
            ViewModel.AutoBoosted += OnAutoBoosted;
            ViewModel.MissionDone += OnMissionDone;
            ViewModel.ItemHit += OnItemHit;

            // On the press, not the release: a boost is all about the moment.
            boostButton.RegisterCallback<PointerDownEvent>(OnBoostPointerDown);
            controls.SetBoostButton(boostButton);
            if (ViewModel.HasItems) {
                itemButton.RegisterCallback<PointerDownEvent>(OnItemPointerDown);
                controls.SetItemButton(itemButton);
            }

            // The very first start also says what to do: everyone else off the arena.
            Root.Q<VisualElement>("start-goal").style.display =
                ViewModel.IsNewPlayer ? DisplayStyle.Flex : DisplayStyle.None;
            if (ViewModel.IsWaitingForPlayer) {
                ShowStart();
            }

            Refresh();
            RefreshBoost();
            Root.schedule.Execute(Refresh).Every(RefreshInterval);
            Root.schedule.Execute(RefreshBoost).Every(BoostRefreshInterval);
            Root.schedule.Execute(RefreshPower).Every(BoostRefreshInterval);
            if (ViewModel.HasItems) {
                Root.schedule.Execute(RefreshItem).Every(BoostRefreshInterval);
            }

            Root.schedule.Execute(RefreshStick).Every(BoostRefreshInterval);
            Root.schedule.Execute(RefreshTeaching).Every(BoostRefreshInterval);
            Root.schedule.Execute(RefreshNames).Every(BoostRefreshInterval);
            Root.schedule.Execute(RefreshKickoff).Every(BoostRefreshInterval);
        }

        public override void Dispose()
        {
            social?.Dispose();
            scoreboard?.Dispose();
            endTimer?.Dispose();
            potato?.Dispose();
            potato = null;
            ViewModel.PlayerKnockout -= ShowKnockout;
            ViewModel.CarOut -= OnCarOut;
            ViewModel.CarBack -= OnCarBack;
            ViewModel.NemesisChanged -= OnNemesisChanged;
            ViewModel.ItemRolled -= OnItemRolled;
            ViewModel.ItemUsed -= OnItemUsed;
            ViewModel.AutoBoosted -= OnAutoBoosted;
            ViewModel.MissionDone -= OnMissionDone;
            ViewModel.ItemHit -= OnItemHit;

            if (boostButton != null) {
                boostButton.UnregisterCallback<PointerDownEvent>(OnBoostPointerDown);
                controls.ReleaseBoostButton(boostButton);
            }

            if (itemButton != null) {
                itemButton.UnregisterCallback<PointerDownEvent>(OnItemPointerDown);
                controls.ReleaseItemButton(itemButton);
            }

            if (settingsButton != null) {
                settingsButton.clicked -= OpenSettings;
                controls.ReleaseSettingsButton(settingsButton);
            }

            // The settings close with the HUD at the round's end, so the result never opens under a stale popup.
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;

            Root?.UnregisterCallback<GeometryChangedEvent>(OnResized);
            Root?.UnregisterCallback<GeometryChangedEvent>(PlaceHud);

            base.Dispose();
        }

        private void Refresh()
        {
            // Up, so the pill reads 0:00 only when the time is really out.
            var seconds = Mathf.CeilToInt(ViewModel.TimeLeft);
            if (seconds != shownSeconds) {
                var isTicking = shownSeconds >= 0;
                shownSeconds = seconds;

                // The time is two labels, the navy stroke of the last seconds under the face (.stroke in ui_screens).
                var text = $"{seconds / 60}:{seconds % 60:00}";
                timeLabel.text = text;
                timeFaceLabel.text = text;

                var isUrgent = seconds <= UrgentSeconds;
                timePill.EnableInClassList(UrgentClass, isUrgent);
                if (isUrgent && isTicking && seconds > 0) {
                    Pulse(timePill, BeatClass);
                }
            }

            if (ViewModel.IsMatch) {
                scoreboard?.RefreshCountdowns();
                ShowRespawn();
            }
            else {
                // The chips are built once the round has its cars; a car going out is shown as it happens (OnCarOut).
                if (chipSlots == null) {
                    BuildChips();
                }

                ShowCarsLeft();
            }

            // The badge stays away until the first knock-out, so the HUD starts with only the time and the cars.
            var knockouts = ViewModel.Knockouts;
            if (knockouts != shownKnockouts) {
                shownKnockouts = knockouts;
                knockoutLabel.text = knockouts.ToString();
                knockoutBadge.EnableInClassList(KnockoutShownClass, knockouts > 0 || ViewModel.IsMatch);
                Pulse(knockoutBadge, KnockoutPopClass);
            }
        }

        /// <summary>
        /// While the player's car is out in a match, a sticker in the middle of the screen counts down to its comeback,
        /// popping at each second.
        /// </summary>
        private void ShowRespawn()
        {
            var left = ViewModel.RespawnTimeLeft;
            var seconds = left > 0f ? Mathf.CeilToInt(left) : 0;
            if (seconds == shownRespawn) {
                return;
            }

            shownRespawn = seconds;
            respawn.EnableInClassList(RespawnShownClass, seconds > 0);
            if (seconds > 0) {
                var text = seconds.ToString();
                respawnCount.text = text;
                respawnCountFace.text = text;
                Pulse(respawn, RespawnPopClass);
            }
        }

        /// <summary>
        /// The scoreboard is up while the match runs, not over the start prompt, which takes the top of a narrow
        /// screen while every score is still 0. It fades while a finger steers under it or the ghost joystick is taught
        /// there, as the feed hides: the board never paints over the thumb.
        /// </summary>
        private void ShowScoreboard()
        {
            if (scoreboard == null) {
                return;
            }

            var shown = IsShown && ViewModel.HasBegun;
            var area = scoreboard.Element.worldBound;
            var faded = shown && ((isStickShown && area.Overlaps(stickBase.worldBound)) ||
                (ghostProgress > 0f && area.Overlaps(ghost.worldBound)));
            scoreboard.Show(shown, faded);
        }

        /// <summary>
        /// Moves something centred on <paramref name="point"/> and ending there at its bottom (a name, a sticker) off
        /// the scoreboard the shorter way: towards the middle of the screen, past the board's inner side, or under it.
        /// The board sits at the right, so towards the middle is to the left.
        /// </summary>
        private void ClearScoreboard(VisualElement layer, ref Vector2 point, float halfWidth, float height)
        {
            if (scoreboard == null || !scoreboard.IsVisible) {
                return;
            }

            var min = layer.WorldToLocal(scoreboard.Element.worldBound.min) - Vector2.one * NameMargin;
            var max = layer.WorldToLocal(scoreboard.Element.worldBound.max) + Vector2.one * NameMargin;
            if (point.x + halfWidth > min.x && point.x - halfWidth < max.x && point.y > min.y && point.y - height < max.y) {
                var aside = point.x + halfWidth - min.x;
                var below = max.y + height - point.y;
                if (aside <= below) {
                    point.x = min.x - halfWidth;
                }
                else {
                    point.y = max.y + height;
                }
            }
        }

        /// <summary>
        /// While the player's car is safe after coming back, a SAFE sticker floats over it, its bar running down with
        /// the safe time; the car itself is drawn see-through meanwhile.
        /// </summary>
        private void ShowShield(Camera camera)
        {
            var share = ViewModel.SafeShareLeft;
            var isShown = share > 0f && camera != null && shield.panel != null;
            shield.EnableInClassList(ShieldShownClass, isShown);
            if (!isShown) {
                return;
            }

            var position = ViewModel.PlayerPosition + Vector3.up * ShieldHeight;
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(shield.panel, position, camera);
            var point = nameLayer.WorldToLocal(panelPoint);
            var size = nameLayer.layout.size;
            var halfWidth = shield.layout.width * 0.5f + NameMargin;
            var height = shield.layout.height + NameMargin;
            var left = halfWidth + safeInsets.Left;
            var top = height + contentTopInset;
            point.x = Mathf.Clamp(point.x, left, Mathf.Max(left, size.x - halfWidth - safeInsets.Right));
            point.y = Mathf.Clamp(point.y, top, Mathf.Max(top, size.y - NameMargin - safeInsets.Bottom));
            ClearScoreboard(nameLayer, ref point, halfWidth, height);
            shield.style.left = point.x;
            shield.style.top = point.y;
            shieldFill.style.width = Length.Percent(share * 100f);
        }

        /// <summary>
        /// On a win streak a gold crown with ×N floats over the player's car: the round pays more, and the bots go for
        /// it a little more. It makes way for SAFE, and goes with the car when it falls.
        /// </summary>
        private void ShowStreak(Camera camera)
        {
            var isShown = ViewModel.Streak > 0 && camera != null && streak.panel != null && ViewModel.IsCarIn(0)
                && !shield.ClassListContains(ShieldShownClass);
            streak.EnableInClassList(StreakShownClass, isShown);
            if (!isShown) {
                return;
            }

            if (streakShown != ViewModel.Streak) {
                streakShown = ViewModel.Streak;
                streak.Query<Label>().ForEach(label => label.text = $"×{streakShown}");
            }

            var position = ViewModel.PlayerPosition + Vector3.up * ShieldHeight;
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(streak.panel, position, camera);
            var point = nameLayer.WorldToLocal(panelPoint);
            var size = nameLayer.layout.size;
            var halfWidth = streak.layout.width * 0.5f + NameMargin;
            var height = streak.layout.height + NameMargin;
            var left = halfWidth + safeInsets.Left;
            var top = height + contentTopInset;
            point.x = Mathf.Clamp(point.x, left, Mathf.Max(left, size.x - halfWidth - safeInsets.Right));
            point.y = Mathf.Clamp(point.y, top, Mathf.Max(top, size.y - NameMargin - safeInsets.Bottom));
            ClearScoreboard(nameLayer, ref point, halfWidth, height);
            streak.style.left = point.x;
            streak.style.top = point.y;
        }

        // The crown and ×N, built once beside the SAFE sticker (the same layer, the same anchor over the car).
        private VisualElement CreateStreak(VisualElement beside)
        {
            var element = new VisualElement { name = "streak", pickingMode = PickingMode.Ignore };
            element.AddToClassList("hud__streak");
            var crown = new VisualElement { pickingMode = PickingMode.Ignore };
            crown.AddToClassList("hud__streak-crown");
            crown.generateVisualContent += RoundOverMatch.DrawCrown;
            element.Add(crown);
            element.Add(CreateOutlinedText("", "hud__streak-text"));
            beside.parent.Insert(beside.parent.IndexOf(beside), element);
            return element;
        }

        /// <summary>
        /// Shows the start prompt while the cars wait for the player: TAP TO START and how to steer on a touch screen,
        /// CLICK TO START, how to steer and the boost key on a desktop. It springs in once the curtain is up, bobs, and
        /// flashes away the moment the round begins. A touch while it shows begins the round, so it never has to turn
        /// from the desktop one into the touch one.
        /// </summary>
        private void ShowStart()
        {
            var isTouch = controls.CheckTouchScreen();
            start.EnableInClassList(StartTouchClass, isTouch);
            start.EnableInClassList(StartDesktopClass, !isTouch);
            var text = Strings.Get(isTouch ? "hud.tap_to_start" : "hud.click_to_start");
            Root.Q<Label>("start-text").text = text;
            Root.Q<Label>("start-text-face").text = text;

            // Inside the ring of cars on a wide screen; on a narrow one the ring is too tight and it goes over the arena.
            Root.RegisterCallback<GeometryChangedEvent>(OnResized);
            OnResized(null);

            // The transition needs the prompt laid out small first; once it is, it springs to its size.
            void OnLaidOut(GeometryChangedEvent evt)
            {
                start.UnregisterCallback<GeometryChangedEvent>(OnLaidOut);
                start.RemoveFromClassList(StartEnterClass);
            }

            start.AddToClassList(StartShownClass);
            start.AddToClassList(StartEnterClass);
            start.RegisterCallback<GeometryChangedEvent>(OnLaidOut);

            startBob = Root.schedule.Execute(() => start.ToggleInClassList(StartBobClass)).Every(StartBobInterval);
            startWatch = Root.schedule.Execute(WatchStart).Every(BoostRefreshInterval);
        }

        private void WatchStart()
        {
            if (ViewModel.IsWaitingForPlayer) {
                return;
            }

            startWatch.Pause();
            startBob.Pause();
            Root.UnregisterCallback<GeometryChangedEvent>(OnResized);
            start.RemoveFromClassList(StartBobClass);
            start.AddToClassList(StartLeaveClass);
            start.schedule.Execute(() => start.RemoveFromClassList(StartShownClass)).StartingIn(StartLeaveDuration);
        }

        private void BuildKickoff()
        {
            kickoff = Root.Q<VisualElement>("kickoff");
            kickoffNumber = Root.Q<VisualElement>("kickoff-number");
            kickoffDigit = Root.Q<Label>("kickoff-digit");
            kickoffDigitFace = Root.Q<Label>("kickoff-digit-face");
            kickoffGo = Root.Q<VisualElement>("kickoff-go");
            kickoffRing = Root.Q<VisualElement>("kickoff-ring");
            kickoffSpecial = Root.Q<VisualElement>("kickoff-special");
            kickoffSpecialName = Root.Q<Label>("kickoff-special-name");
            kickoffSpecialNameFace = Root.Q<Label>("kickoff-special-name-face");
            kickoffSpecialHint = Root.Q<Label>("kickoff-special-hint");
            var burst = Root.Q<VisualElement>("kickoff-burst");
            kickoffRays = new VisualElement[KickoffRays];
            for (var i = 0; i < KickoffRays; i++) {
                var ray = new VisualElement { pickingMode = PickingMode.Ignore };
                ray.AddToClassList("kickoff__ray");
                ray.EnableInClassList("kickoff__ray--short", i % 2 == 1);
                burst.Add(ray);
                kickoffRays[i] = ray;
            }
        }

        /// <summary>
        /// Shows the step of the kick-off the round state has reached: a tick and a number popping in for 3, 2 and 1, the
        /// GO! sound and its burst for GO!, each animated by its age on unscaled time and gone at the end of its step.
        /// </summary>
        private void RefreshKickoff()
        {
            var step = ViewModel.KickoffStep;
            if (step == HudViewModel.NoKickoff) {
                SetKickoffShown(false);
                return;
            }

            var age = Time.unscaledTime - ViewModel.KickoffStepTime;
            var duration = ViewModel.KickoffStepDuration;
            var isGo = step == 0;
            if (step != shownKickoffStep) {
                shownKickoffStep = step;
                kickoffNumber.style.display = isGo ? DisplayStyle.None : DisplayStyle.Flex;
                kickoffGo.style.display = isGo ? DisplayStyle.Flex : DisplayStyle.None;
                kickoffRing.style.display = isGo ? DisplayStyle.Flex : DisplayStyle.None;
                // A special round's rule stays over the numbers, read while they count, and leaves for GO!.
                var special = isGo ? null : GameText.Special(ViewModel.Special);
                kickoffSpecial.EnableInClassList(KickoffSpecialShownClass, special != null);
                if (special != null) {
                    kickoffSpecialName.text = special;
                    kickoffSpecialNameFace.text = special;
                    kickoffSpecialHint.text = GameText.SpecialHint(ViewModel.Special);
                }
                if (!isGo) {
                    var digit = KickoffDigits[Mathf.Clamp(step, 0, KickoffDigits.Length - 1)];
                    kickoffDigit.text = digit;
                    kickoffDigitFace.text = digit;
                }

                if (age < duration) {
                    audioService.Play(isGo ? audioService.Library.KickoffGo : audioService.Library.CountdownTick);
                }
            }

            if (age >= duration) {
                SetKickoffShown(false);
                return;
            }

            SetKickoffShown(true);
            if (isGo) {
                AnimateKickoffGo(age, duration);
            }
            else {
                AnimateKickoffNumber(kickoffNumber, step, age, duration);
            }
        }

        private void SetKickoffShown(bool isShown)
        {
            if (isShown != isKickoffShown) {
                isKickoffShown = isShown;
                kickoff.EnableInClassList(KickoffShownClass, isShown);
            }
        }

        // Springs in from small, tilted the other way every other number and dropping into place, swells a touch while
        // it holds, and shrinks away over the end of its step.
        private static void AnimateKickoffNumber(VisualElement sticker, int step, float age, float duration)
        {
            var side = step % 2 == 0 ? -1f : 1f;
            var spring = EaseOutBack(Mathf.Clamp01(age / KickoffEnter), KickoffOvershoot);
            var hold = Mathf.Clamp01((age - KickoffEnter) / Mathf.Max(0.01f, duration - KickoffEnter));
            var scale = Mathf.LerpUnclamped(KickoffStartScale, 1f, spring) * (1f + KickoffHoldGrowth * hold);
            var opacity = Mathf.Clamp01(age / KickoffFadeIn);
            var leave = Mathf.Min(KickoffLeave, duration * 0.4f);
            var gone = Mathf.Clamp01((age - (duration - leave)) / leave);
            scale *= 1f - (1f - KickoffStartScale) * gone * gone;
            opacity *= 1f - gone;
            sticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
            sticker.style.rotate = new Rotate(side * Mathf.LerpUnclamped(KickoffStartTilt, KickoffTilt, spring));
            sticker.style.translate = new Translate(0f, -KickoffDrop * (1f - spring));
            sticker.style.opacity = opacity;
        }

        // GO! springs in harder over a gold ring that widens and fades and a burst of rays flying out, and swells away
        // over its last moment, while the round already runs under it.
        private void AnimateKickoffGo(float age, float duration)
        {
            var spring = EaseOutBack(Mathf.Clamp01(age / KickoffEnter), KickoffGoOvershoot);
            var scale = Mathf.LerpUnclamped(KickoffStartScale, 1f, spring);
            var opacity = Mathf.Clamp01(age / KickoffFadeIn);
            var leave = Mathf.Min(KickoffGoLeave, duration * 0.4f);
            var gone = Mathf.Clamp01((age - (duration - leave)) / leave);
            scale *= Mathf.Lerp(1f, KickoffGoLeaveScale, gone);
            opacity *= 1f - gone;
            kickoffGo.style.scale = new Scale(new Vector3(scale, scale, 1f));
            kickoffGo.style.rotate = new Rotate(Mathf.LerpUnclamped(-KickoffStartTilt, KickoffGoTilt, spring));
            kickoffGo.style.opacity = opacity;

            var burst = Mathf.Clamp01(age / KickoffBurstTime);
            var outward = 1f - (1f - burst) * (1f - burst) * (1f - burst);
            var ringScale = Mathf.Lerp(0.4f, KickoffRingScale, outward);
            kickoffRing.style.scale = new Scale(new Vector3(ringScale, ringScale, 1f));
            kickoffRing.style.opacity = KickoffRingOpacity * (1f - burst);

            var distance = Mathf.Lerp(KickoffRayFrom, KickoffRayTo, outward);
            var rayScale = burst < 0.35f ? Mathf.Clamp01(burst / 0.12f) : 1f - (burst - 0.35f) / 0.65f;
            for (var i = 0; i < kickoffRays.Length; i++) {
                var angle = (i + 0.5f) * 360f / kickoffRays.Length;
                var radians = angle * Mathf.Deg2Rad;
                var reach = i % 2 == 1 ? distance * 0.85f : distance;
                var ray = kickoffRays[i];
                ray.style.translate = new Translate(Mathf.Sin(radians) * reach, -Mathf.Cos(radians) * reach);
                ray.style.rotate = new Rotate(angle);
                ray.style.scale = new Scale(new Vector3(rayScale, rayScale, 1f));
            }
        }

        private static float EaseOutBack(float t, float overshoot)
        {
            var u = t - 1f;
            return 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
        }

        private void OpenSettings()
        {
            if (!IsShown || lifetime == null || ViewModel.IsSettingsOpen) {
                return;
            }

            audioService.PlayUiClick();
            ViewModel.OpenSettings(lifetime.Token).Forget();
        }

        // The top right corner, where the result and the garage keep theirs; on a narrow screen a full HUD bar reaches
        // it, so there the gear goes under the bar. A match's bar is only the time and the player's knock-outs, which
        // never reach the corner, and its scoreboard goes under the gear. The scoreboard is the whole column beside the
        // arena only where the screen is wide enough to leave room for it at the side.
        /// <summary>
        /// Reflows only on geometry or browser-inset changes. The full-screen projection layers stay put, so their
        /// world bounds continue to describe the moved HUD for ClearPill, LimitNameLift and ClearScoreboard.
        /// </summary>
        private void PlaceHud(GeometryChangedEvent evt)
        {
            var size = Root.layout.size;
            if (!(size.x > 0f) || !(size.y > 0f)) {
                return;
            }

            this.safeInsets = SafeArea.GetInsets(Root.panel);
            this.hudTopInset = this.contentTopInset = safeInsets.Top;
            // Upright, the bar and the names span the width under Poki's pill, so they all go below it. Held wide,
            // the pill keeps to the left corner, away from the centred bar, the gear and the arena: only the feed,
            // which starts in that corner, goes below it.
            var isWide = size.x >= size.y;
            var feedTopInset = hudTopInset;
            if (PokiPill.TryGetRect(Root.panel, out var pill)) {
                pill = Root.WorldToLocal(pill);
                if (pill.xMax > 0f && pill.xMin < size.x && pill.yMax > 0f && pill.yMin < size.y) {
                    var underPill = Mathf.Ceil(pill.yMax + PillGap);
                    if (isWide) {
                        feedTopInset = Mathf.Max(feedTopInset, underPill - BarTop);
                    }
                    else {
                        this.contentTopInset = Mathf.Max(contentTopInset, underPill);
                        this.hudTopInset = feedTopInset = Mathf.Max(hudTopInset, contentTopInset - SettingsTop);
                    }
                }
            }

            Root.EnableInClassList(WideClass, isWide);
            Root.style.paddingTop = BarTop + hudTopInset;
            Root.style.paddingLeft = safeInsets.Left;
            Root.style.paddingRight = safeInsets.Right;
            var isUnder = !ViewModel.IsMatch && size.x < size.y;
            settingsButton.EnableInClassList(SettingsUnderClass, isUnder);
            settingsButton.style.top = (isUnder ? SettingsUnderTop : SettingsTop) + hudTopInset;
            settingsButton.style.right = SettingsTop + safeInsets.Right;
            if (scoreboard != null) {
                var wide = size.x >= size.y * ScoreboardWideAspect;
                scoreboard.SetWide(wide);
                scoreboard.Element.style.top = (wide ? 200f : 240f) + hudTopInset;
                scoreboard.Element.style.right = (wide ? 48f : 24f) + safeInsets.Right;
            }

            social.Place(safeInsets, feedTopInset);
            PlaceBoost();
            PlaceStart();
            // Find room for the ghost again even if only the browser chrome changed, with no canvas resize.
            this.ghostPanelSize = Vector2.zero;
        }

        private void PlaceBoost()
        {
            var isWideItem = Root.ClassListContains(WideClass) && Root.ClassListContains(NoBoostClass)
                && boostSlot.ClassListContains(SlotItemsClass);
            var side = isWideItem ? WideItemSide : BoostSide;
            boostSlot.style.left = isBoostOnLeft ? new StyleLength(side + safeInsets.Left) : StyleKeyword.Auto;
            boostSlot.style.right = isBoostOnLeft ? StyleKeyword.Auto : new StyleLength(side + safeInsets.Right);
            boostSlot.style.bottom = (isWideItem ? WideItemBottom : BoostBottom) + safeInsets.Bottom;
        }

        private void PlaceStart()
        {
            var wide = Root.layout.width >= Root.layout.height;
            start.EnableInClassList(StartWideClass, wide);
            // Landscape keeps its central USS placement; portrait's prompt follows the lowered HUD bar.
            start.style.top = wide ? new StyleLength(StyleKeyword.Null) : new StyleLength(250f + hudTopInset);
            start.style.left = safeInsets.Left;
            start.style.right = safeInsets.Right;
        }

        private void OnResized(GeometryChangedEvent evt)
        {
            PlaceStart();
        }

        /// <summary>
        /// Pops a KNOCKOUT! sticker up over the point where the car the player knocked out went over the edge, kept
        /// clear of the screen's sides and the HUD: it springs in, holds, and flies up and away.
        /// </summary>
        private void ShowKnockout(Vector3 worldPosition, bool isRevenge, int combo) =>
            ShowPopup(worldPosition, isRevenge, ItemKind.None, null, false, combo);

        /// <summary>
        /// The same sticker for an item the player used (HEAVY!, SHOCKWAVE!, SHIELD!, TURBO!, BOMB!, GLOVE!), over the
        /// car, as it is used and not as it is taken: the box only fills the slot.
        /// </summary>
        private void OnItemUsed(ItemKind kind)
        {
            ShowPopup(ViewModel.PlayerPosition, false, kind, null);
            HideItemHint();
        }

        /// <summary>
        /// BOOST! over the player's car and a pop of the boost button when the game boosts for the player, so a new
        /// player sees what the button does and that it was a boost that sent the car.
        /// </summary>
        private void OnAutoBoosted()
        {
            ShowPopup(ViewModel.PlayerPosition, false, ItemKind.None, null, true);
            if (boostButton != null) {
                Pulse(boostButton, BoostPopClass);
            }
        }

        /// <summary>MISSION! over the player's car when the round completes one of the result's missions.</summary>
        private void OnMissionDone() => ShowPopup(ViewModel.PlayerPosition, false, ItemKind.None, "mission");

        /// <summary>
        /// A comic word where the player's item lands: POW! where the glove punches a car, STRIKE! on each car the
        /// bowling ball knocks aside, and SAVED! over the player's car when its balloon catches a fall.
        /// </summary>
        private void OnItemHit(ItemKind kind, Vector3 point)
        {
            var word = kind switch
            {
                ItemKind.Glove => "pow",
                ItemKind.Bowling => "strike",
                ItemKind.Balloon => "saved",
                _ => null,
            };

            if (word != null) {
                ShowPopup(point, false, kind, word);
            }
        }

        /// <param name="word">The comic word to show alone (pow, strike, saved: its string and class), or null.</param>
        /// <param name="combo">The player's knock-outs in a row (<see cref="Gameplay.Round.ArenaRound.PlayerCombo"/>): from 2 the
        /// sticker says DOUBLE!, TRIPLE! or RAMPAGE! instead of KNOCKOUT!, bigger each time.</param>
        private void ShowPopup(Vector3 worldPosition, bool isRevenge, ItemKind pickup, string word, bool isBoost = false,
            int combo = 1)
        {
            var panel = popupLayer.panel;
            if (panel == null) {
                return;
            }

            var size = popupLayer.layout.size;
            // With no camera (or no layout yet) it goes in the middle, under the HUD.
            var point = new Vector2(size.x * 0.5f, PopupTopMargin);
            var camera = Camera.main;
            if (camera != null) {
                var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(panel, worldPosition, camera);
                point = popupLayer.WorldToLocal(panelPoint) + Vector2.down * PopupLift;
            }

            var popupLeft = PopupSideMargin + safeInsets.Left;
            var popupTop = PopupTopMargin + hudTopInset;
            point.x = Mathf.Clamp(point.x, popupLeft, Mathf.Max(popupLeft, size.x - PopupSideMargin - safeInsets.Right));
            point.y = Mathf.Clamp(point.y, popupTop, Mathf.Max(popupTop, size.y - PopupBottomMargin - safeInsets.Bottom));
            // The sticker is centred on its point; keep the box it springs to off the scoreboard.
            point.y += PopupHalfHeight;
            ClearScoreboard(popupLayer, ref point, PopupHalfWidth, PopupHalfHeight * 2f);
            point.y -= PopupHalfHeight;

            var popup = CreatePopup(popupCount++ % 2 == 0 ? -PopupTilt : PopupTilt, isRevenge, pickup, word, isBoost, combo);
            popup.style.left = point.x;
            popup.style.top = point.y;
            popupLayer.Add(popup);

            // The transition needs the sticker laid out small first; once it is, it springs to its size.
            void OnLaidOut(GeometryChangedEvent evt)
            {
                popup.UnregisterCallback<GeometryChangedEvent>(OnLaidOut);
                popup.RemoveFromClassList(PopupStartClass);
            }

            popup.RegisterCallback<GeometryChangedEvent>(OnLaidOut);
            popup.schedule.Execute(() => popup.AddToClassList(PopupLeaveClass))
                .StartingIn(PopupEnterDuration + PopupHoldDuration);
            popup.schedule.Execute(popup.RemoveFromHierarchy)
                .StartingIn(PopupEnterDuration + PopupHoldDuration + PopupLeaveDuration);
        }

        // The sticker: the knock-out star and KNOCKOUT! in gold, outlined (.stroke in ui_screens). The outer element
        // holds the place, centred on it; the inner one springs, tilts and flies.
        private static VisualElement CreatePopup(float tilt, bool isRevenge, ItemKind pickup, string word, bool isBoost,
            int combo)
        {
            var popup = new VisualElement { pickingMode = PickingMode.Ignore };
            popup.AddToClassList("ko-pop");
            popup.AddToClassList(PopupStartClass);
            popup.EnableInClassList("ko-pop--revenge", isRevenge);

            var sticker = new VisualElement { pickingMode = PickingMode.Ignore };
            sticker.AddToClassList("ko-pop__sticker");
            sticker.style.rotate = new Rotate(tilt);
            popup.Add(sticker);

            if (word != null) {
                popup.AddToClassList($"ko-pop--{word}");
                sticker.Add(CreateOutlinedText(Strings.Get($"hud.{word}"), "ko-pop__text"));
                return popup;
            }

            if (isBoost) {
                popup.AddToClassList("ko-pop--power");
                popup.AddToClassList("ko-pop--boost");
                sticker.Add(CreateOutlinedText(Strings.Get("hud.boost_pop"), "ko-pop__text"));
                return popup;
            }

            // An item's sticker is the word alone, in the kind's colour.
            if (pickup != ItemKind.None) {
                popup.AddToClassList("ko-pop--power");
                popup.AddToClassList($"ko-pop--{PowerName(pickup)}");
                sticker.Add(CreateOutlinedText(Strings.Get($"hud.item_{PowerName(pickup)}"), "ko-pop__text"));
                return popup;
            }

            sticker.Add(CreateStar("ko-star--big"));

            // REVENGE! means more than a combo, so it wins; a combo grows its sticker, up to RAMPAGE! from the fourth.
            var isCombo = !isRevenge && combo >= 2;
            var key = isRevenge ? "hud.revenge_taken" : isCombo ? $"hud.combo_{Mathf.Min(combo, 4)}" : "hud.knockout";
            if (isCombo) {
                popup.AddToClassList("ko-pop--combo");
                popup.EnableInClassList("ko-pop--combo-big", combo >= 3);
            }

            sticker.Add(CreateOutlinedText(Strings.Get(key), "ko-pop__text"));
            return popup;
        }

        /// <summary>
        /// The kind's name as the strings and the classes spell it: mega, shockwave, shield, turbo, bomb, glove, bowling,
        /// balloon, oil.
        /// </summary>
        private static string PowerName(ItemKind kind) => kind.ToString().ToLowerInvariant();

        /// <summary>
        /// The badge of the ability the player's car runs: its icon and name, and a bar that drains with the time. It
        /// blinks over the last seconds, and a shield, which can also go with a hit, is gone as it does.
        /// </summary>
        private void RefreshPower()
        {
            var ability = ViewModel.Ability;
            if (ability != shownAbility) {
                foreach (var kind in TimedKinds) {
                    power.EnableInClassList($"hud__power--{PowerName(kind)}", kind == ability);
                    powerIcon.EnableInClassList($"hud__power-icon--{PowerName(kind)}", kind == ability);
                }

                shownAbility = ability;
                power.EnableInClassList(PowerShownClass, ability != ItemKind.None);
                if (ability != ItemKind.None) {
                    powerName.text = Strings.Get($"hud.item_{PowerName(ability)}");
                }
            }

            if (ability == ItemKind.None) {
                return;
            }

            powerFill.style.width = Length.Percent(ViewModel.AbilityShare * 100f);
            var isBlinking = ViewModel.AbilityTimeLeft < PowerBlinkTime
                && Mathf.Repeat(Time.unscaledTime * PowerBlinkRate, 1f) < 0.5f;
            power.style.opacity = isBlinking ? 0.45f : 1f;
        }

        /// <summary>
        /// The item slot: an empty slot is hidden; a box the player broke spins it through the items, on unscaled
        /// time for as long as the roll lasts (nothing skips it: the item cannot be used until it lands), and it lands
        /// with a pop on the item; used, it is empty again. Small with its E key on a desktop. The first item of a visit
        /// also says how to use it.
        /// </summary>
        private void RefreshItem()
        {
            var isTouch = controls.CheckTouchScreen();
            itemButton.EnableInClassList(ItemDesktopClass, !isTouch);
            itemHint.EnableInClassList(ItemHintDesktopClass, !isTouch);
            if (boostSlot.ClassListContains(SlotItemsClass) != isTouch) {
                boostSlot.EnableInClassList(SlotItemsClass, isTouch);
                PlaceBoost();
            }

            var held = ViewModel.HeldItem;
            if (ViewModel.IsItemRolling) {
                isItemShownRolling = true;
                itemButton.AddToClassList(ItemRollingClass);
                SetItemIcon(ItemKinds[(int)(Time.unscaledTime * ItemSpinRate) % ItemKinds.Length]);
            }
            else {
                if (isItemShownRolling) {
                    isItemShownRolling = false;
                    itemButton.RemoveFromClassList(ItemRollingClass);
                    if (held != ItemKind.None) {
                        Pulse(itemButton, ItemPopClass);
                        audioService.PlayUiClick();
                    }
                }

                SetItemIcon(held);
            }

            // An empty slot is hidden and lets a finger through to steer, as if the button were not there.
            var isEmpty = held == ItemKind.None;
            itemButton.EnableInClassList(ItemEmptyClass, isEmpty);
            itemButton.pickingMode = isEmpty ? PickingMode.Ignore : PickingMode.Position;
            RefreshItemHint(isTouch);
        }

        private void OnItemRolled(ItemKind kind)
        {
            Pulse(itemButton, ItemPopClass);
        }

        private void SetItemIcon(ItemKind kind)
        {
            if (kind == shownItem) {
                return;
            }

            if (shownItem != ItemKind.None) {
                itemIcon.RemoveFromClassList($"hud__item-icon--{PowerName(shownItem)}");
            }

            shownItem = kind;
            if (kind != ItemKind.None) {
                itemIcon.AddToClassList($"hud__item-icon--{PowerName(kind)}");
            }
        }

        /// <summary>
        /// TAP TO USE, or RIGHT CLICK with the E key cap on a desktop, over the slot the first time in a visit an item
        /// lands in it, the button pulsing with it, for a moment; the visit's controls remember it was shown.
        /// </summary>
        private void RefreshItemHint(bool isTouch)
        {
            var now = Time.unscaledTime;
            if (!isItemHintShown) {
                if (controls.HasShownItemHint || !ViewModel.IsItemReady || isItemShownRolling) {
                    return;
                }

                controls.HasShownItemHint = true;
                isItemHintShown = true;
                itemHintTime = now;
                nextItemPulse = now;
                var text = Strings.Get(isTouch ? "hud.tap_to_use" : "hud.right_click");
                itemHintText.text = text;
                itemHintFace.text = text;
                itemHint.AddToClassList(ItemHintShownClass);
            }

            if (now - itemHintTime >= ItemHintDuration || !ViewModel.IsItemReady) {
                HideItemHint();
                return;
            }

            if (now >= nextItemPulse) {
                nextItemPulse = now + ItemHintPulsePeriod;
                Pulse(itemButton, ItemHintPulseClass);
            }
        }

        private void HideItemHint()
        {
            if (!isItemHintShown) {
                return;
            }

            isItemHintShown = false;
            itemHint.RemoveFromClassList(ItemHintShownClass);
        }

        private void OnItemPointerDown(PointerDownEvent evt)
        {
            evt.StopPropagation();
            // As with boost, Enhanced Touch owns touch presses; only a mouse or a pen is passed on from here.
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch) {
                ViewModel.PressItem();
            }

            Pulse(itemButton, ItemPressedClass);
        }

        /// <summary>
        /// A level badge in its rank's colour, with its rank's ornament (<see cref="LevelBadge"/>). The REVENGE rival's
        /// (<paramref name="isRival"/>) is framed red with two red horns: the frame reads before the name does, in the
        /// HUD, the lobby and on the result alike. Under a crown the horns stand further out, so both read.
        /// </summary>
        internal static VisualElement CreateLevelBadge(int level, string className, bool isRival = false,
            bool isLabelled = false)
        {
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("level-badge");
            badge.AddToClassList(PlayerRank.ClassOf(level));
            badge.AddToClassList(className);
            var value = new Label(level.ToString()) { pickingMode = PickingMode.Ignore };
            value.AddToClassList("level-badge__value");
            badge.Add(value);
            // Beside a nick a bare number reads as a place: a tiny LV tag on the badge's edge says it is the level,
            // without widening the badge (LV inline made a three-digit badge twice as wide as the number).
            if (isLabelled) {
                badge.AddToClassList("level-badge--labelled");
                var prefix = new Label(Strings.Get("common.lv")) { pickingMode = PickingMode.Ignore };
                prefix.AddToClassList("level-badge__prefix");
                badge.Add(prefix);
            }
            if (isRival) {
                badge.AddToClassList("level-badge--rival");
                var isCrowned = LevelBadge.IsCrowned(level);
                foreach (var isLeft in new[] { true, false }) {
                    var horn = new VisualElement { pickingMode = PickingMode.Ignore };
                    horn.AddToClassList("level-badge__horn");
                    horn.AddToClassList(isLeft ? "level-badge__horn--left" : "level-badge__horn--right");
                    horn.EnableInClassList("level-badge__horn--crowned", isCrowned);
                    horn.generateVisualContent += isLeft ? DrawLeftHorn : DrawRightHorn;
                    badge.Add(horn);
                }
            }

            // After the horns, so the crown stands in front of them.
            LevelBadge.Decorate(badge);
            return badge;
        }

        /// <summary>
        /// A level badge in a fixed-width slot of a list row, centred, so the nicks after it start on one line whatever
        /// the number's digits, the rank's wings or the rival's horns.
        /// </summary>
        internal static VisualElement InLevelSlot(VisualElement badge, string slotClass)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList("level-slot");
            slot.AddToClassList(slotClass);
            if (badge != null) {
                slot.Add(badge);
            }

            return slot;
        }

        private static void DrawLeftHorn(MeshGenerationContext context) => DrawHorn(context, false);
        private static void DrawRightHorn(MeshGenerationContext context) => DrawHorn(context, true);

        // A little devil's horn: a broad root along the bottom, curving up and out to a point; red with a navy outline.
        private static void DrawHorn(MeshGenerationContext context, bool isMirrored)
        {
            var rect = context.visualElement.contentRect;
            PaintHorn(context.painter2D, new Rect(0f, 0f, rect.width, rect.height), isMirrored,
                Mathf.Max(3f, rect.width * 0.14f));
        }

        /// <summary>The rival's horn in a box: the REVENGE mark on a level badge and a revenge mission's sign.</summary>
        internal static void PaintHorn(Painter2D painter, Rect box, bool isMirrored, float lineWidth)
        {
            var w = box.width;
            var h = box.height;
            Vector2 At(float x, float y) => new(box.x + (isMirrored ? (1f - x) * w : x * w), box.y + y * h);
            painter.BeginPath();
            painter.MoveTo(At(0.3f, 1f));
            painter.LineTo(At(0.2f, 0.7f));
            painter.LineTo(At(0.12f, 0.4f));
            painter.LineTo(At(0.06f, 0.06f));
            painter.LineTo(At(0.4f, 0.28f));
            painter.LineTo(At(0.72f, 0.56f));
            painter.LineTo(At(0.98f, 1f));
            painter.ClosePath();
            painter.fillColor = new Color32(0xff, 0x4d, 0x4d, 0xff);
            painter.Fill();
            painter.strokeColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);
            painter.lineWidth = lineWidth;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }

        private static VisualElement CreateOutlinedText(string value, string textClass)
        {
            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = new Label(value) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("display");
                label.AddToClassList(textClass);
                if (isFace) {
                    label.AddToClassList("stroke__face");
                }

                text.Add(label);
            }

            return text;
        }

        /// <summary>
        /// Follows every bot with its visit handle, on the same panel projection as knockout stickers. Position is on
        /// the outer element so the inner sticker can spring and shrink without moving its world anchor.
        /// </summary>
        private void RefreshNames()
        {
            social?.Tick(IsShown, carNames);
            if (carNames == null) {
                BuildNames();
            }

            var camera = Camera.main;
            if (ViewModel.IsMatch) {
                ShowScoreboard();
                ShowShield(camera);
            }

            ShowStreak(camera);
            potato?.Tick(camera, contentTopInset, safeInsets.Left, safeInsets.Right);

            if (carNames == null || camera == null || nameLayer.panel == null) {
                return;
            }

            // Until GO! the handles stay faint: the countdown is the thing to watch.
            nameLayer.EnableInClassList(NamesWaitingClass, ViewModel.IsBeforeStart);
            var size = nameLayer.layout.size;
            if (size.x <= 0f || size.y <= 0f) {
                return;
            }

            var nameCount = 0;
            for (var index = 0; index < carNames.Length; index++) {
                var tag = carNames[index];
                if (tag == null || !ViewModel.IsCarIn(index)) {
                    continue;
                }

                if (!(tag.layout.width > 0f) || !(tag.layout.height > 0f)) {
                    continue;
                }

                var position = ViewModel.PositionOf(index);
                var inFront = camera.WorldToViewportPoint(position).z > 0f;
                tag.style.visibility = inFront ? Visibility.Visible : Visibility.Hidden;
                if (!inFront) {
                    continue;
                }

                var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(nameLayer.panel, position, camera);
                var point = nameLayer.WorldToLocal(panelPoint) + Vector2.down * NameLift;
                var screenY = point.y;
                // Leave room for the outline and the rival's pulse. Clear just the central pills, not a whole band.
                var halfWidth = tag.layout.width * 0.6f + NameMargin;
                var height = tag.layout.height * 1.2f + NameMargin;
                var left = halfWidth + safeInsets.Left;
                var top = height + contentTopInset;
                point.x = Mathf.Clamp(point.x, left, Mathf.Max(left, size.x - halfWidth - safeInsets.Right));
                point.y = Mathf.Clamp(point.y, top, Mathf.Max(top, size.y - NameMargin - safeInsets.Bottom));
                ClearPill(timePill, ref point, halfWidth, height);
                ClearPill(carsPill, ref point, halfWidth, height);
                ClearPill(settingsButton, ref point, halfWidth, height);
                if (knockoutBadge.ClassListContains(KnockoutShownClass)) {
                    ClearPill(knockoutBadge, ref point, halfWidth, height);
                }
                if (social.IsFeedVisible) {
                    ClearPill(social.Feed, ref point, halfWidth, height);
                }
                ClearScoreboard(nameLayer, ref point, halfWidth, height);

                ref var placement = ref namePlacements[index];
                placement.Anchor = point;
                placement.ScreenY = screenY;
                placement.Bounds = new Rect(point.x - halfWidth, point.y - height, halfWidth * 2f, height);
                // Insertion sort, nearest (lower on screen) first; equal heights keep their car-index order.
                var slot = nameCount++;
                while (slot > 0 && screenY > namePlacements[nameOrder[slot - 1]].ScreenY) {
                    nameOrder[slot] = nameOrder[slot - 1];
                    slot--;
                }

                nameOrder[slot] = index;
            }

            var now = Time.unscaledTime;
            SeparateNames(nameCount, Mathf.Max(0f, now - nameUpdateTime));
            nameUpdateTime = now;

            // The rival springs in with the HUD, over the kickoff: the countdown is when there is time to read it.
            if (!isRevengeIntroduced && IsShown && (ViewModel.HasBegun || ViewModel.IsBeforeStart)) {
                IntroduceRevenge();
            }
        }

        /// <summary>
        /// Places farther handles above already placed nearer ones. Only the extra lift is smoothed, so a handle
        /// still follows its car directly. Bounds reserve room for outlines and pulses; neither the lift cap nor the
        /// HUD's safe area is sacrificed when the crowd is too dense to fit.
        /// </summary>
        private void SeparateNames(int count, float elapsed)
        {
            var blend = 1f - Mathf.Exp(-elapsed / NameShiftTime);
            for (var slot = 0; slot < count; slot++) {
                var index = nameOrder[slot];
                var tag = carNames[index];
                ref var placement = ref namePlacements[index];
                var bounds = placement.Bounds;
                var maxLift = Mathf.Min(tag.layout.height * NameMaxLiftHeights,
                    bounds.yMin - Mathf.Max(NameMargin, contentTopInset));
                maxLift = LimitNameLift(timePill, bounds, maxLift);
                maxLift = LimitNameLift(carsPill, bounds, maxLift);
                maxLift = LimitNameLift(settingsButton, bounds, maxLift);
                if (knockoutBadge.ClassListContains(KnockoutShownClass)) {
                    maxLift = LimitNameLift(knockoutBadge, bounds, maxLift);
                }
                if (social.IsFeedVisible) {
                    maxLift = LimitNameLift(social.Feed, bounds, maxLift);
                }
                if (scoreboard != null && scoreboard.IsVisible) {
                    maxLift = LimitNameLift(scoreboard.Element, bounds, maxLift);
                }

                maxLift = Mathf.Max(0f, maxLift);
                var minY = bounds.y - maxLift;
                // A lift can run into a handle already checked above it, so revisit until clear or at the cap.
                for (var pass = 0; pass < slot; pass++) {
                    var previousY = bounds.y;
                    for (var nearer = 0; nearer < slot; nearer++) {
                        var other = namePlacements[nameOrder[nearer]].Bounds;
                        var spaced = other;
                        spaced.yMin -= NameGap;
                        spaced.yMax += NameGap;
                        if (bounds.Overlaps(spaced)) {
                            bounds.y = Mathf.Max(minY, other.yMin - NameGap - bounds.height);
                        }
                    }

                    if (bounds.y == previousY || bounds.y <= minY) {
                        break;
                    }
                }

                var targetLift = placement.Bounds.y - bounds.y;
                placement.Lift = Mathf.Min(maxLift, Mathf.Lerp(placement.Lift, targetLift, blend));
                placement.Bounds = bounds;
                tag.style.left = placement.Anchor.x;
                tag.style.top = placement.Anchor.y - placement.Lift;
            }
        }

        // A name below a HUD pill may rise only as far as its bottom edge, including the usual clearance.
        private float LimitNameLift(VisualElement pill, Rect bounds, float maxLift)
        {
            // A part of the bar the mode does not use (a match's cars pill) has no place to keep clear of.
            if (pill.resolvedStyle.display == DisplayStyle.None) {
                return maxLift;
            }

            var min = nameLayer.WorldToLocal(pill.worldBound.min) - Vector2.one * NameMargin;
            var max = nameLayer.WorldToLocal(pill.worldBound.max) + Vector2.one * NameMargin;
            if (bounds.xMax > min.x && bounds.xMin < max.x && bounds.yMin >= max.y) {
                return Mathf.Min(maxLift, bounds.yMin - max.y);
            }

            return maxLift;
        }

        private void ClearPill(VisualElement pill, ref Vector2 point, float halfWidth, float height)
        {
            if (pill.resolvedStyle.display == DisplayStyle.None) {
                return;
            }

            var min = nameLayer.WorldToLocal(pill.worldBound.min) - Vector2.one * NameMargin;
            var max = nameLayer.WorldToLocal(pill.worldBound.max) + Vector2.one * NameMargin;
            if (point.x + halfWidth > min.x && point.x - halfWidth < max.x &&
                point.y > min.y && point.y - height < max.y) {
                point.y = max.y + height;
            }
        }

        private void BuildNames()
        {
            if (ViewModel.CarCount == 0) {
                return;
            }

            carNames = new VisualElement[ViewModel.CarCount];
            namePlacements = new NamePlacement[carNames.Length];
            nameOrder = new int[carNames.Length];
            nameUpdateTime = Time.unscaledTime;
            VisualElement revengeName = null;
            for (var index = 0; index < carNames.Length; index++) {
                if (index == ViewModel.PlayerIndex || !ViewModel.IsCarIn(index)) {
                    continue;
                }

                var tag = CreateName(index);
                if (ViewModel.IsNemesis(index)) {
                    revengeName = tag;
                }

                carNames[index] = tag;
            }

            // Painting priority is independent of which car is closer; keep the whole rival sticker on top.
            revengeName?.BringToFront();
        }

        /// <summary>
        /// A bot's handle over its car, hidden until it is placed: its level badge and name, and over them REVENGE with
        /// the target for the marked rival, which springs in once it is introduced.
        /// </summary>
        private VisualElement CreateName(int index)
        {
            var tag = new VisualElement { pickingMode = PickingMode.Ignore };
            tag.AddToClassList("car-name");
            tag.style.visibility = Visibility.Hidden;
            var sticker = new VisualElement { pickingMode = PickingMode.Ignore };
            sticker.AddToClassList("car-name__sticker");
            tag.Add(sticker);
            // The rival has no caption of its own: its badge is framed red with horns and its name is red.
            var isRival = ViewModel.IsNemesis(index);
            if (isRival) {
                tag.AddToClassList(RevengeClass);
                tag.AddToClassList(NameEnterClass);
            }

            // The bot's level on a small badge in its rank's colour, before the handle, as a player's would be.
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("car-name__line");
            line.Add(CreateLevelBadge(ViewModel.LevelOf(index), "car-name__level", isRival, true));
            line.Add(CreateOutlinedText(ViewModel.NameOf(index), "car-name__text"));
            // A bot that joined on a win streak wears the player's crown and ×N after its name.
            var streakOf = ViewModel.StreakOf(index);
            if (streakOf > 0) {
                var crown = new VisualElement { pickingMode = PickingMode.Ignore };
                crown.AddToClassList("car-name__crown");
                crown.generateVisualContent += RoundOverMatch.DrawCrown;
                line.Add(crown);
                line.Add(CreateOutlinedText($"×{streakOf}", "car-name__streak"));
            }

            sticker.Add(line);
            // A tail under the sticker points at its car, so the handle is read as that car's in a crowd.
            var tail = new VisualElement { pickingMode = PickingMode.Ignore };
            tail.AddToClassList("car-name__tail");
            tail.generateVisualContent += DrawNameTail;
            sticker.Add(tail);
            // The visit's first round shows no handles: a new player has the cars, the floor and the edge to read.
            // The tag stays, empty, so what follows it (the bots' quips) still knows where the car is.
            if (ViewModel.IsFirstRound) {
                sticker.style.display = DisplayStyle.None;
            }
            nameLayer.Add(tag);
            return tag;
        }

        /// <summary>
        /// A car back in a match gets its handle again, and the rival its REVENGE at once: it was introduced already.
        /// </summary>
        private void OnCarBack(int index)
        {
            if (carNames == null || index < 0 || index >= carNames.Length || index == ViewModel.PlayerIndex
                || carNames[index] != null) {
                return;
            }

            var tag = CreateName(index);
            carNames[index] = tag;
            if (ViewModel.IsNemesis(index)) {
                SpringRevenge(tag);
            }
        }

        /// <summary>
        /// The match's rival changed: the handle of the car that lost the mark becomes a plain one, and the handle of the
        /// new rival gets REVENGE, springing in and pulsing as at the start of a round.
        /// </summary>
        private void OnNemesisChanged()
        {
            if (carNames == null) {
                return;
            }

            for (var index = 0; index < carNames.Length; index++) {
                var tag = carNames[index];
                if (tag == null || tag.ClassListContains(RevengeClass) == ViewModel.IsNemesis(index)) {
                    continue;
                }

                tag.RemoveFromHierarchy();
                tag = CreateName(index);
                carNames[index] = tag;
                if (ViewModel.IsNemesis(index)) {
                    SpringRevenge(tag);
                }
            }
        }

        /// <summary>
        /// Brings a new rival's handle in once it is laid out small, on top of the others, and pulses it twice.
        /// </summary>
        private void SpringRevenge(VisualElement tag)
        {
            tag.BringToFront();

            void OnLaidOut(GeometryChangedEvent evt)
            {
                tag.UnregisterCallback<GeometryChangedEvent>(OnLaidOut);
                tag.RemoveFromClassList(NameEnterClass);
            }

            tag.RegisterCallback<GeometryChangedEvent>(OnLaidOut);
            for (var beat = 0; beat < 2; beat++) {
                tag.schedule.Execute(() => Pulse(tag, NamePulseClass))
                    .StartingIn(RevengeEnterDuration + beat * RevengePulseInterval);
            }
        }

        private void IntroduceRevenge()
        {
            isRevengeIntroduced = true;
            for (var index = 0; index < carNames.Length; index++) {
                var tag = carNames[index];
                if (tag == null || !ViewModel.IsNemesis(index) || !ViewModel.IsCarIn(index)) {
                    continue;
                }

                tag.RemoveFromClassList(NameEnterClass);
                var carIndex = index;
                for (var beat = 0; beat < 2; beat++) {
                    tag.schedule.Execute(() => {
                        if (ViewModel.IsCarIn(carIndex)) {
                            Pulse(tag, NamePulseClass);
                        }
                    }).StartingIn(RevengeEnterDuration + beat * RevengePulseInterval);
                }
            }
        }

        // The knock-out mark: a jagged gold impact burst in a navy outline, drawn in code (DrawBurst). It was an
        // eight-point star of two turned squares, which on the HUD bar read as a gear; the five-point star is XP's.
        internal static VisualElement CreateStar(string sizeClass)
        {
            var star = new VisualElement { pickingMode = PickingMode.Ignore };
            star.AddToClassList("ko-star");
            star.AddToClassList(sizeClass);
            DrawAsBurst(star);
            return star;
        }

        /// <summary>Draws the knock-out burst on an element (one from UXML, too), in its <c>color</c>.</summary>
        internal static void DrawAsBurst(VisualElement star)
        {
            if (star == null) {
                return;
            }

            star.Clear();
            star.generateVisualContent -= DrawBurst;
            star.generateVisualContent += DrawBurst;
        }

        // The handle's tail: a small triangle pointing down at the car, in the sticker's backdrop (its color).
        private static void DrawNameTail(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.fillColor = context.visualElement.resolvedStyle.color;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x, r.y));
            p.LineTo(new Vector2(r.xMax, r.y));
            p.LineTo(new Vector2(r.center.x, r.yMax));
            p.ClosePath();
            p.Fill();
        }

        // Eleven spikes of uneven length round a centre, as a comic "bonk"; the fill is the element's colour.
        private static readonly float[] BurstReach = { 1f, 0.86f, 0.97f, 0.8f, 1f, 0.9f, 0.82f, 1f, 0.88f, 0.95f, 0.84f };

        private static void DrawBurst(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var r = element.contentRect;
            if (r.width <= 0f || r.height <= 0f) {
                return;
            }

            PaintBurst(context.painter2D, r.center, Mathf.Min(r.width, r.height) * 0.46f, element.resolvedStyle.color);
        }

        /// <summary>The knock-out burst's path, filled and outlined navy: the HUD's KO mark and a knock-out mission's sign.</summary>
        internal static void PaintBurst(Painter2D p, Vector2 centre, float outer, Color fill)
        {
            p.lineWidth = outer * 0.17f;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = CarColor.Silhouette;
            p.fillColor = fill;
            p.BeginPath();
            var spikes = BurstReach.Length;
            for (var i = 0; i < spikes * 2; i++) {
                var angle = (i * 180f / spikes - 90f + 8f) * Mathf.Deg2Rad;
                var reach = i % 2 == 0 ? outer * BurstReach[i / 2] : outer * 0.55f;
                var point = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
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

        /// <summary>
        /// Shows the boost button, the charge growing from its middle as the boost cools down, and pops it when the
        /// boost is ready again. Without a touch screen it is a smaller gauge with the key that boosts: a mouse player
        /// sees when the boost is back and what to press.
        /// </summary>
        private void RefreshBoost()
        {
            if (!ViewModel.IsBoostEnabled) {
                // No boosts: the button is not shown or pressed, and the start prompt and hints say nothing of it. The
                // slot still follows the steering hand, for the item button in it.
                if (!Root.ClassListContains(NoBoostClass)) {
                    Root.AddToClassList(NoBoostClass);
                    PlaceBoost();
                    controls.ReleaseBoostButton(boostButton);
                }

                FollowSteeringSide();
                return;
            }

            if (!isBoostShown) {
                isBoostShown = true;
                boostButton.AddToClassList(BoostShownClass);
            }

            boostButton.EnableInClassList(BoostDesktopClass, !controls.CheckTouchScreen());
            FollowSteeringSide();

            var readiness = ViewModel.BoostReadiness;
            boostCharge.style.scale = new Scale(new Vector3(readiness, readiness, 1f));

            var isReady = ViewModel.IsBoostReady;
            if (isReady != isBoostShownReady) {
                isBoostShownReady = isReady;
                boostButton.EnableInClassList(BoostReadyClass, isReady);
                if (isReady) {
                    Pulse(boostButton, BoostPopClass);
                }
            }
        }

        /// <summary>
        /// Moves the boost slot, and the item button in it, to the side opposite the finger that began steering: the
        /// slot shrinks, changes side and grows back.
        /// </summary>
        private void FollowSteeringSide()
        {
            if (!isBoostMoving && isBoostOnLeft != controls.IsBoostOnLeft) {
                isBoostMoving = true;
                boostSlot.AddToClassList(BoostMovingClass);
                boostSlot.schedule.Execute(() => {
                    // Use the latest drag's choice if another finger started steering while the button shrank.
                    isBoostOnLeft = controls.IsBoostOnLeft;
                    boostSlot.EnableInClassList(BoostLeftClass, isBoostOnLeft);
                    PlaceBoost();
                    boostSlot.RemoveFromClassList(BoostMovingClass);
                    isBoostMoving = false;
                }).StartingIn(BoostMoveDuration);
            }
        }

        /// <summary>
        /// Draws the joystick where the player's driver says a finger steers with it, following the finger every frame:
        /// the disc on its centre, which the finger drags along past its edge, and the knob under the finger. Once the
        /// finger lets go, both shrink away where they were.
        /// </summary>
        private void RefreshStick()
        {
            var isShown = controls.IsJoystickShown;
            if (isShown) {
                var panel = stick.panel;
                if (panel == null) {
                    return;
                }

                var center = ToStick(panel, controls.JoystickCenter);
                var radius = Vector2.Distance(center,
                    ToStick(panel, controls.JoystickCenter + Vector2.right * controls.JoystickRadius));
                var knob = ToStick(panel, controls.JoystickKnob);
                if (!safeInsets.Equals(default(SafeInsets))) {
                    // Translate the whole drawing together, preserving its direction and travel. Steering still uses
                    // the real finger; a disc near a cutout or home indicator must remain visible.
                    var reach = radius * Mathf.Max(StickBaseScale, 1f + StickKnobScale);
                    var size = stick.layout.size;
                    var safeCenter = new Vector2(
                        Mathf.Clamp(center.x, safeInsets.Left + reach,
                            Mathf.Max(safeInsets.Left + reach, size.x - safeInsets.Right - reach)),
                        Mathf.Clamp(center.y, safeInsets.Top + reach,
                            Mathf.Max(safeInsets.Top + reach, size.y - safeInsets.Bottom - reach)));
                    knob += safeCenter - center;
                    center = safeCenter;
                }
                Place(stickBase, center, radius * StickBaseScale);
                Place(stickKnob, knob, radius * StickKnobScale);
            }

            if (isShown != isStickShown) {
                isStickShown = isShown;
                stick.EnableInClassList(StickShownClass, isShown);
            }
        }

        // A point in screen pixels from the bottom-left corner, as the input reports it, in the joystick layer's own.
        private Vector2 ToStick(IPanel panel, Vector2 screenPosition)
        {
            var panelPoint = RuntimePanelUtils.ScreenToPanel(panel,
                new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            return stick.WorldToLocal(panelPoint);
        }

        private static void Place(VisualElement element, Vector2 center, float radius)
        {
            element.style.left = center.x - radius;
            element.style.top = center.y - radius;
            element.style.width = radius * 2f;
            element.style.height = radius * 2f;
        }

        /// <summary>
        /// Touch demonstrations yield to actual input. A held steering finger has already found the joystick, even
        /// at its dead centre, so idle starts after it lifts. Boosting counts only when it fires. Clocks are unscaled.
        /// </summary>
        private void RefreshTeaching()
        {
            var now = Time.unscaledTime;
            var elapsed = Mathf.Max(0f, now - teachingTime);
            teachingTime = now;
            var isTouch = IsShown && controls.CheckTouchScreen();
            var enabled = isTouch && ViewModel.IsFirstRound;
            // The visit's first rounds keep the ghost wherever no finger steers: it yields to a touch and comes back
            // once the finger lifts.
            var keepsGhost = isTouch && ViewModel.RoundNumber <= ViewModel.Controls.GhostStickRounds &&
                (ViewModel.IsRunning || ViewModel.IsBeforeStart);

            if (ViewModel.LastTouchTime > seenTouchTime) {
                seenTouchTime = ViewModel.LastTouchTime;
                hasStartTouch = true;
                isIdleHintShown = false;
                idleSince = now;
            }

            if (!enabled || !ViewModel.IsRunning || controls.IsJoystickShown) {
                idleSince = now;
                isIdleHintShown = false;
            }
            else if (ViewModel.LastSteerTime > idleSince) {
                idleSince = ViewModel.LastSteerTime;
                isIdleHintShown = false;
            }
            else if (!isIdleHintShown && idleHintCount < ViewModel.Controls.IdleHintMax &&
                now - idleSince >= ViewModel.Controls.IdleHintDelay) {
                isIdleHintShown = true;
                idleHintCount++;
            }

            var showGhost = !controls.IsJoystickShown &&
                (keepsGhost || (enabled && ((ViewModel.IsWaitingForPlayer && !hasStartTouch) || isIdleHintShown)));
            if (showGhost && !isGhostShown) {
                ghostTime = now;
                ghostPanelSize = Vector2.zero;
            }

            isGhostShown = showGhost;
            AnimateTeaching(ghost, ref ghostProgress, showGhost, elapsed);
            if (ghostProgress > 0f) {
                DrawGhost(now - ghostTime);
            }

            // The desktop gets the same first-round nudge over its gauge, in its own words; no finger to hint beside.
            var canHintBoost = IsShown && ViewModel.IsFirstRound && ViewModel.CanHintBoost;
            boostHint.EnableInClassList(BoostHintDesktopClass, !isTouch);
            if (!canHintBoost || now - boostHintTime >= BoostHintDuration) {
                isBoostHintShown = false;
            }

            if (canHintBoost && !isBoostHintShown && now >= nextBoostHintTime) {
                isBoostHintShown = true;
                boostHintTime = now;
                nextBoostHintTime = now + BoostHintInterval;
                hasTapHint = false;
            }

            AnimateTeaching(boostHint, ref boostHintProgress, isBoostHintShown, elapsed);
            boostButton.EnableInClassList(BoostHintPulseClass, isBoostHintShown &&
                (now - boostHintTime) % BoostHintPulsePeriod < BoostHintPulsePeriod * 0.5f);

            // If the hint starts while no finger steers, wait for one rather than spending its second off-screen.
            if (isBoostHintShown && controls.IsJoystickShown && !hasTapHint) {
                hasTapHint = true;
                tapHintTime = now;
            }

            var showTap = canHintBoost && isTouch && hasTapHint && controls.IsJoystickShown && now - tapHintTime < TapHintDuration;
            AnimateTeaching(tapHint, ref tapHintProgress, showTap, elapsed);
            if (showTap) {
                PlaceTapHint();
            }
        }

        private void DrawGhost(float age)
        {
            var size = Root.layout.size;
            var radius = Mathf.Min(GhostMaxRadius, ViewModel.Controls.JoystickRadius * Mathf.Min(size.x, size.y));
            // Keep the demonstration still as cars drive past; find a clear place again only on show or resize.
            if (size != ghostPanelSize) {
                ghostPanelSize = size;
                ghostCenter = controls.HasJoystickPlace && !ViewModel.IsFirstRound ? PlaceGhostAtThumb(size, radius) :
                    size.x >= size.y ? PlaceWideGhost(size, radius) :
                    new Vector2(size.x * 0.5f, size.y * GhostPortraitY);
                if (!safeInsets.Equals(default(SafeInsets))) {
                    var bottom = GhostReach(radius);
                    var left = safeInsets.Left + GhostHalfWidth + TeachingMargin;
                    var top = contentTopInset + GhostCaptionLift;
                    this.ghostCenter.x = Mathf.Clamp(ghostCenter.x, left,
                        Mathf.Max(left, size.x - safeInsets.Right - GhostHalfWidth - TeachingMargin));
                    this.ghostCenter.y = Mathf.Clamp(ghostCenter.y, top,
                        Mathf.Max(top, size.y - safeInsets.Bottom - bottom - TeachingMargin));
                }
            }

            ghost.style.left = ghostCenter.x;
            ghost.style.top = ghostCenter.y;
            var angle = age / GhostCycle * Mathf.PI * 2f;
            var knob = new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * (radius * GhostTravel);
            Place(ghostBase, Vector2.zero, radius * StickBaseScale);
            Place(ghostKnob, knob, radius * StickKnobScale);
            // The hand turns about its fingertip (the style sheet's transform-origin), so the tip stays on the knob while
            // the hand lies down and right. Opacity belongs to the joystick wrapper; the hand and caption stay opaque.
            var handWidth = radius * GhostHandWidthScale;
            var handHeight = handWidth * GhostHandAspect;
            ghostFinger.style.left = knob.x - handWidth * GhostHandTip.x;
            ghostFinger.style.top = knob.y - handHeight * GhostHandTip.y;
            ghostFinger.style.width = handWidth;
            ghostFinger.style.height = handHeight;
        }

        // How far below the joystick's centre the hand reaches at the lowest point of the knob's orbit.
        private static float GhostReach(float radius)
        {
            var width = radius * GhostHandWidthScale;
            var tilt = GhostFingerTilt * Mathf.Deg2Rad;
            return radius * GhostTravel + width * GhostHandAspect * (1f - GhostHandTip.y) * Mathf.Cos(tilt) +
                width * (1f - GhostHandTip.x) * Mathf.Abs(Mathf.Sin(tilt));
        }

        /// <summary>
        /// Where the player's thumb last steered, kept on screen with room for the finger: after the first round the
        /// ghost marks where the joystick comes up, on the side the player plays on.
        /// </summary>
        private Vector2 PlaceGhostAtThumb(Vector2 size, float radius)
        {
            var center = controls.JoystickCenter;
            var panelPoint = RuntimePanelUtils.ScreenToPanel(Root.panel, new Vector2(center.x, Screen.height - center.y));
            var point = Root.WorldToLocal(panelPoint);
            var bottom = GhostReach(radius);
            var reach = radius * StickBaseScale + TeachingMargin;
            point.x = Mathf.Clamp(point.x, reach, Mathf.Max(reach, size.x - reach));
            point.y = Mathf.Clamp(point.y, reach, Mathf.Max(reach, size.y - bottom - TeachingMargin));
            return point;
        }

        // In landscape, prefer the lower area just right of centre. Check nearby places against the projected live
        // cars and the boost slot, so the demonstration does not cover a car or the button in the starting roster.
        private Vector2 PlaceWideGhost(Vector2 size, float radius)
        {
            var preferred = new Vector2(size.x * GhostWideX, size.y * GhostWideY);
            var camera = Camera.main;
            if (camera == null || ghost.panel == null) {
                return preferred;
            }

            var best = preferred;
            var fewestOverlaps = int.MaxValue;
            // Reserve the hand below the lowest point of the orbit, keeping it on-screen.
            var bottom = GhostReach(radius);
            var buttonBounds = new Rect(Root.WorldToLocal(boostSlot.worldBound.min), boostSlot.worldBound.size);
            if (itemButton.ClassListContains(ItemShownClass)) {
                // The item button stands out of the slot, up and inward of BOOST: the ghost keeps off both.
                var itemBounds = new Rect(Root.WorldToLocal(itemButton.worldBound.min), itemButton.worldBound.size);
                buttonBounds = Rect.MinMaxRect(Mathf.Min(buttonBounds.xMin, itemBounds.xMin),
                    Mathf.Min(buttonBounds.yMin, itemBounds.yMin), Mathf.Max(buttonBounds.xMax, itemBounds.xMax),
                    Mathf.Max(buttonBounds.yMax, itemBounds.yMax));
            }
            for (var row = 0; row < 2; row++) {
                for (var column = 0; column < 4; column++) {
                    var point = preferred + new Vector2(column * radius * 2f, row * radius);
                    point.x = Mathf.Min(point.x, size.x - GhostHalfWidth - TeachingMargin);
                    point.y = Mathf.Min(point.y, size.y - bottom - TeachingMargin);
                    var bounds = Rect.MinMaxRect(point.x - GhostHalfWidth, point.y - GhostCaptionLift,
                        point.x + GhostHalfWidth, point.y + bottom);
                    var overlaps = bounds.Overlaps(buttonBounds) ? 1 : 0;
                    bounds.xMin -= GhostCarClearance;
                    bounds.xMax += GhostCarClearance;
                    bounds.yMin -= GhostCarClearance;
                    bounds.yMax += GhostCarClearance;
                    for (var index = 0; index < ViewModel.CarCount; index++) {
                        if (ViewModel.IsCarIn(index)) {
                            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(ghost.panel,
                                ViewModel.PositionOf(index), camera);
                            if (bounds.Contains(Root.WorldToLocal(panelPoint))) {
                                overlaps++;
                            }
                        }
                    }

                    if (overlaps < fewestOverlaps) {
                        best = point;
                        fewestOverlaps = overlaps;
                    }

                    if (overlaps == 0) {
                        return point;
                    }
                }
            }

            return best;
        }

        private void PlaceTapHint()
        {
            var point = Root.WorldToLocal(stick.LocalToWorld(ToStick(stick.panel, controls.JoystickKnob)));
            var size = Root.layout.size;
            var x = point.x < size.x * 0.5f ? point.x + TapHintGap : point.x - TapHintGap - TapHintWidth;
            tapHint.style.left = Mathf.Clamp(x, TeachingMargin, Mathf.Max(TeachingMargin,
                size.x - TapHintWidth - TeachingMargin));
            tapHint.style.top = Mathf.Clamp(point.y - TapHintLift, TeachingMargin, Mathf.Max(TeachingMargin,
                size.y - TapHintHeight - TeachingMargin));
        }

        // A spring on the way in and a shrink on the way out, on the unscaled HUD clock, independent of hit-stop.
        private static void AnimateTeaching(VisualElement element, ref float progress, bool shown, float elapsed,
            float opacity = 1f)
        {
            progress = Mathf.MoveTowards(progress, shown ? 1f : 0f,
                elapsed / (shown ? TeachingEnterTime : TeachingLeaveTime));
            var back = progress - 1f;
            var scale = shown ? 1f + 2.7f * back * back * back + 1.7f * back * back : progress;
            element.style.scale = new Scale(new Vector3(scale, scale, 1f));
            element.style.opacity = progress * opacity;
        }

        private void OnBoostPointerDown(PointerDownEvent evt)
        {
            evt.StopPropagation();
            // Enhanced Touch owns touch presses, including the button, so the UI must not send the same boost twice.
            if (evt.pointerType != UnityEngine.UIElements.PointerType.touch) {
                controls.PressBoost();
            }

            Pulse(boostButton, BoostPressedClass);
        }

        /// <summary>
        /// Stacks a chip in its colour for each car still on the arena, overlapping: the bots in their order, then the
        /// player's in front of them on the right, with the white ring of its marker on the arena. A car already out (a
        /// HUD opened late) gets none: the stack only ever shows the cars still in.
        /// </summary>
        private void BuildChips()
        {
            var carCount = ViewModel.CarCount;
            if (carCount == 0) {
                return;
            }

            chipSlots = new VisualElement[carCount];
            var playerIndex = ViewModel.PlayerIndex;
            for (var index = 0; index < carCount; index++) {
                if (index != playerIndex) {
                    AddChip(index, false);
                }
            }

            AddChip(playerIndex, true);
        }

        private void AddChip(int index, bool isPlayer)
        {
            if (!ViewModel.IsCarIn(index)) {
                return;
            }

            var slot = CreateChip(ViewModel.ColorOf(index), isPlayer);
            chipSlots[index] = slot;
            chipRow.Add(slot);
        }

        /// <summary>
        /// Takes the car's chip out of the stack: it hops up spinning, then falls as it shrinks away, while the chips
        /// after it close up; the count punches and the pill pops.
        /// </summary>
        private void OnCarOut(int index)
        {
            if (carNames != null && index >= 0 && index < carNames.Length && carNames[index] != null) {
                var tag = carNames[index];
                carNames[index] = null;
                tag.AddToClassList(NameLeaveClass);
                tag.schedule.Execute(tag.RemoveFromHierarchy).StartingIn(NameLeaveDuration);
            }

            // A match keeps the leader in the cars pill, not the cars still in.
            if (ViewModel.IsMatch) {
                return;
            }

            ShowCarsLeft();
            Pulse(carsLeftLabel, CountPunchClass);
            Pulse(carsPill, PopClass);

            if (chipSlots == null || index < 0 || index >= chipSlots.Length || chipSlots[index] == null) {
                return;
            }

            var slot = chipSlots[index];
            chipSlots[index] = null;
            var chip = slot.Q<VisualElement>(className: ChipClass);
            chip.AddToClassList(ChipHopClass);
            slot.schedule.Execute(() => {
                chip.RemoveFromClassList(ChipHopClass);
                chip.AddToClassList(ChipFallClass);
                slot.AddToClassList(ChipSlotClosingClass);
            }).StartingIn(ChipHopDuration);
            // Closed up, its place takes no room, so taking it away moves nothing.
            slot.schedule.Execute(slot.RemoveFromHierarchy).StartingIn(ChipLeaveDuration);
        }

        private void ShowCarsLeft()
        {
            var carsLeft = ViewModel.CarsLeft;
            if (carsLeft != shownCarsLeft) {
                shownCarsLeft = carsLeft;
                carsLeftLabel.text = carsLeft.ToString();
            }
        }

        // A car's chip: a round sticker in the car's colour with a little white car seen from above in it, all drawn
        // by ui_screens, so the build carries no image for it. The chip sits in a place of its own in the stack, which
        // narrows to nothing as the chip leaves, so the chips after it slide over while the chip keeps its round shape.
        private static VisualElement CreateChip(int colorId, bool isPlayer)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList(ChipSlotClass);

            slot.Add(CarColor.CreateChip(colorId, isPlayer));

            return slot;
        }

        // Scales the element up for a moment; taking the class off again lets its transition ease it back.
        private static void Pulse(VisualElement element, string pulseClass)
        {
            element.AddToClassList(pulseClass);
            element.schedule.Execute(() => element.RemoveFromClassList(pulseClass)).StartingIn(PulseDuration);
        }
    }
}
