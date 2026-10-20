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
        private const string BoostReadyClass = "hud__boost--ready";
        private const string BoostPopClass = "hud__boost--pop";
        private const string BoostPressedClass = "hud__boost--pressed";
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

        // The start prompt bobs: each toggle eases it a little bigger or back (hud__start-prompt's transition), so a bob
        // takes twice this, as SMUP's TAP TO START.
        private const long StartBobInterval = 600;

        // How long the start prompt takes to flash away (hud__start--leave) before it is taken off the screen.
        private const long StartLeaveDuration = 200;

        // The joystick's disc and knob against how far the knob goes from the middle: the disc a little wider, so the
        // knob pushed all the way still mostly sits on it, and the knob a thumb's size.
        private const float StickBaseScale = 1.3f;
        private const float StickKnobScale = 0.55f;

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

        // The sticker sits this far above the point where the car went over the edge, in reference pixels, so the car
        // still shows under it.
        private const float PopupLift = 110f;

        // The tilt of the sticker, in degrees, every other one the other way so two at once do not look alike.
        private const float PopupTilt = 7f;

        private readonly HudControls controls;
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
        private VisualElement boostCharge;
        private bool isBoostShown;
        private bool isBoostShownReady;
        private VisualElement knockoutBadge;
        private Label knockoutLabel;
        private VisualElement popupLayer;
        private int shownKnockouts;
        private int popupCount;
        private VisualElement start;
        private IVisualElementScheduledItem startBob;
        private IVisualElementScheduledItem startWatch;
        private VisualElement stick;
        private VisualElement stickBase;
        private VisualElement stickKnob;
        private bool isStickShown;

        public HudView(HudControls controls)
        {
            this.controls = controls;
        }

        protected override void OnAttached()
        {
            // The HUD lies over the game: nothing on it may take a click or a touch from the controls but the boost
            // button, which tells the controls where it is so a finger on it does not steer.
            Root.pickingMode = PickingMode.Ignore;

            timePill = Root.Q<VisualElement>("time-pill");
            timeLabel = Root.Q<Label>("time");
            timeFaceLabel = Root.Q<Label>("time-face");
            carsPill = Root.Q<VisualElement>("cars-pill");
            chipRow = Root.Q<VisualElement>("chips");
            carsLeftLabel = Root.Q<Label>("cars-left");
            boostButton = Root.Q<VisualElement>("boost-button");
            boostCharge = Root.Q<VisualElement>("boost-charge");
            knockoutBadge = Root.Q<VisualElement>("ko-badge");
            knockoutLabel = Root.Q<Label>("ko");
            popupLayer = Root.Q<VisualElement>("ko-layer");
            start = Root.Q<VisualElement>("start");
            stick = Root.Q<VisualElement>("stick");
            stickBase = Root.Q<VisualElement>("stick-base");
            stickKnob = Root.Q<VisualElement>("stick-knob");
            ViewModel.PlayerKnockout += ShowKnockout;
            ViewModel.CarOut += OnCarOut;

            // On the press, not the release: a boost is all about the moment.
            boostButton.RegisterCallback<PointerDownEvent>(OnBoostPointerDown);
            controls.SetBoostButton(boostButton);

            if (ViewModel.IsWaitingForPlayer) {
                ShowStart();
            }

            Refresh();
            RefreshBoost();
            Root.schedule.Execute(Refresh).Every(RefreshInterval);
            Root.schedule.Execute(RefreshBoost).Every(BoostRefreshInterval);
            Root.schedule.Execute(RefreshStick).Every(BoostRefreshInterval);
        }

        public override void Dispose()
        {
            ViewModel.PlayerKnockout -= ShowKnockout;
            ViewModel.CarOut -= OnCarOut;

            if (boostButton != null) {
                boostButton.UnregisterCallback<PointerDownEvent>(OnBoostPointerDown);
                controls.ReleaseBoostButton(boostButton);
            }

            Root?.UnregisterCallback<GeometryChangedEvent>(OnResized);

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

            // The chips are built once the round has its cars; a car going out is shown as it happens (OnCarOut).
            if (chipSlots == null) {
                BuildChips();
            }

            ShowCarsLeft();

            // The badge stays away until the first knock-out, so the HUD starts with only the time and the cars.
            var knockouts = ViewModel.Knockouts;
            if (knockouts != shownKnockouts) {
                shownKnockouts = knockouts;
                knockoutLabel.text = $"KO {knockouts}";
                knockoutBadge.EnableInClassList(KnockoutShownClass, knockouts > 0);
                Pulse(knockoutBadge, KnockoutPopClass);
            }
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
            var text = isTouch ? "TAP TO START" : "CLICK TO START";
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

        private void OnResized(GeometryChangedEvent evt)
        {
            var size = Root.layout.size;
            start.EnableInClassList(StartWideClass, size.x >= size.y);
        }

        /// <summary>
        /// Pops a KNOCKOUT! sticker up over the point where the car the player knocked out went over the edge, kept
        /// clear of the screen's sides and the HUD: it springs in, holds, and flies up and away.
        /// </summary>
        private void ShowKnockout(Vector3 worldPosition)
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

            point.x = Mathf.Clamp(point.x, PopupSideMargin, Mathf.Max(PopupSideMargin, size.x - PopupSideMargin));
            point.y = Mathf.Clamp(point.y, PopupTopMargin, Mathf.Max(PopupTopMargin, size.y - PopupBottomMargin));

            var popup = CreatePopup(popupCount++ % 2 == 0 ? -PopupTilt : PopupTilt);
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
        private static VisualElement CreatePopup(float tilt)
        {
            var popup = new VisualElement { pickingMode = PickingMode.Ignore };
            popup.AddToClassList("ko-pop");
            popup.AddToClassList(PopupStartClass);

            var sticker = new VisualElement { pickingMode = PickingMode.Ignore };
            sticker.AddToClassList("ko-pop__sticker");
            sticker.style.rotate = new Rotate(tilt);
            popup.Add(sticker);

            sticker.Add(CreateStar("ko-star--big"));

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = new Label("KNOCKOUT!") { pickingMode = PickingMode.Ignore };
                label.AddToClassList("display");
                label.AddToClassList("ko-pop__text");
                if (isFace) {
                    label.AddToClassList("stroke__face");
                }

                text.Add(label);
            }

            sticker.Add(text);
            return popup;
        }

        // The knock-out star, eight points, built from two navy squares under two gold ones, each pair a plain one and
        // one turned by 45 degrees (ko-star in ui_screens), so the build carries no image for it.
        private static VisualElement CreateStar(string sizeClass)
        {
            var star = new VisualElement { pickingMode = PickingMode.Ignore };
            star.AddToClassList("ko-star");
            star.AddToClassList(sizeClass);
            foreach (var layer in new[] { "ko-star__back", "ko-star__front" }) {
                foreach (var isTurned in new[] { false, true }) {
                    var square = new VisualElement { pickingMode = PickingMode.Ignore };
                    square.AddToClassList(layer);
                    if (isTurned) {
                        square.AddToClassList("ko-star__turned");
                    }

                    star.Add(square);
                }
            }

            return star;
        }

        /// <summary>
        /// Shows the boost button once the screen is a touch screen, the charge growing from its middle as the boost
        /// cools down, and pops it when the boost is ready again.
        /// </summary>
        private void RefreshBoost()
        {
            if (!isBoostShown) {
                if (!controls.CheckTouchScreen()) {
                    return;
                }

                isBoostShown = true;
                boostButton.AddToClassList(BoostShownClass);
            }

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
                Place(stickBase, center, radius * StickBaseScale);
                Place(stickKnob, ToStick(panel, controls.JoystickKnob), radius * StickKnobScale);
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

        private void OnBoostPointerDown(PointerDownEvent evt)
        {
            evt.StopPropagation();
            controls.PressBoost();
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

            var slot = CreateChip(index, isPlayer);
            chipSlots[index] = slot;
            chipRow.Add(slot);
        }

        /// <summary>
        /// Takes the car's chip out of the stack: it hops up spinning, then falls as it shrinks away, while the chips
        /// after it close up; the count punches and the pill pops.
        /// </summary>
        private void OnCarOut(int index)
        {
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
        private static VisualElement CreateChip(int index, bool isPlayer)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList(ChipSlotClass);

            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList(ChipClass);
            chip.AddToClassList(CarColor.ClassOf(index));
            slot.Add(chip);

            if (isPlayer) {
                var ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList("hud__chip-ring");
                chip.Add(ring);
            }

            var car = new VisualElement { pickingMode = PickingMode.Ignore };
            car.AddToClassList("hud__chip-car");
            var glass = new VisualElement { pickingMode = PickingMode.Ignore };
            glass.AddToClassList("hud__chip-glass");
            car.Add(glass);
            chip.Add(car);

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
