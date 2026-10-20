using System.Collections.Generic;
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
        private const string PipClass = "pip";
        private const string PipOutClass = "pip--out";
        private const string BoostShownClass = "hud__boost--shown";
        private const string BoostReadyClass = "hud__boost--ready";
        private const string BoostPopClass = "hud__boost--pop";
        private const string BoostPressedClass = "hud__boost--pressed";
        private const string KnockoutShownClass = "hud__ko--shown";
        private const string KnockoutPopClass = "hud__ko--pop";
        private const string PopupStartClass = "ko-pop--start";
        private const string PopupLeaveClass = "ko-pop--leave";

        // The KNOCKOUT! sticker: how long it springs in and then holds before it flies off, and how long that takes
        // (the transitions of ko-pop in ui_screens), in milliseconds.
        private const long PopupEnterDuration = 300;
        private const long PopupHoldDuration = 800;
        private const long PopupLeaveDuration = 350;

        // How far the sticker's centre keeps from the sides of the screen, from the top (under the time pill, the car
        // plate and the KO badge) and from the bottom (over the boost button), in reference pixels, so it is always
        // whole and covers none of the HUD.
        private const float PopupSideMargin = 400f;
        private const float PopupTopMargin = 560f;
        private const float PopupBottomMargin = 480f;

        // The sticker sits this far above the point where the car went over the edge, in reference pixels, so the car
        // still shows under it.
        private const float PopupLift = 110f;

        // The tilt of the sticker, in degrees, every other one the other way so two at once do not look alike.
        private const float PopupTilt = 7f;

        private static readonly string[] WheelClasses = {
            "pip__wheel--front-left", "pip__wheel--front-right", "pip__wheel--back-left", "pip__wheel--back-right",
        };

        private readonly HudControls controls;
        private readonly List<VisualElement> pips = new();
        private VisualElement timePill;
        private Label timeLabel;
        private Label timeFaceLabel;
        private VisualElement carsPlate;
        private VisualElement pipRow;
        private Label carsLabel;
        private int shownSeconds = -1;
        private int shownCarsLeft = -1;
        private int shownCarCount = -1;
        private VisualElement boostButton;
        private VisualElement boostCharge;
        private bool isBoostShown;
        private bool isBoostShownReady;
        private VisualElement knockoutBadge;
        private Label knockoutLabel;
        private VisualElement popupLayer;
        private int shownKnockouts;
        private int popupCount;

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
            carsPlate = Root.Q<VisualElement>("cars-plate");
            pipRow = Root.Q<VisualElement>("pips");
            carsLabel = Root.Q<Label>("cars");
            boostButton = Root.Q<VisualElement>("boost-button");
            boostCharge = Root.Q<VisualElement>("boost-charge");
            knockoutBadge = Root.Q<VisualElement>("ko-badge");
            knockoutLabel = Root.Q<Label>("ko");
            popupLayer = Root.Q<VisualElement>("ko-layer");
            ViewModel.PlayerKnockout += ShowKnockout;

            // On the press, not the release: a boost is all about the moment.
            boostButton.RegisterCallback<PointerDownEvent>(OnBoostPointerDown);
            controls.SetBoostButton(boostButton);

            Refresh();
            RefreshBoost();
            Root.schedule.Execute(Refresh).Every(RefreshInterval);
            Root.schedule.Execute(RefreshBoost).Every(BoostRefreshInterval);
        }

        public override void Dispose()
        {
            ViewModel.PlayerKnockout -= ShowKnockout;

            if (boostButton != null) {
                boostButton.UnregisterCallback<PointerDownEvent>(OnBoostPointerDown);
                controls.ReleaseBoostButton(boostButton);
            }

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

            var carsLeft = ViewModel.CarsLeft;
            var carCount = ViewModel.CarCount;
            if (carsLeft != shownCarsLeft || carCount != shownCarCount) {
                // A car going out is news; the first count and a new round are not.
                if (carCount == shownCarCount && carsLeft < shownCarsLeft) {
                    Pulse(carsPlate, PopClass);
                }

                shownCarsLeft = carsLeft;
                shownCarCount = carCount;
                ShowCars(carsLeft, carCount);
            }

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

        private void OnBoostPointerDown(PointerDownEvent evt)
        {
            evt.StopPropagation();
            controls.PressBoost();
            Pulse(boostButton, BoostPressedClass);
        }

        private void ShowCars(int carsLeft, int carCount)
        {
            while (pips.Count < carCount) {
                var pip = CreatePip();
                pips.Add(pip);
                pipRow.Add(pip);
            }

            while (pips.Count > carCount) {
                pips[pips.Count - 1].RemoveFromHierarchy();
                pips.RemoveAt(pips.Count - 1);
            }

            // The cars still in come first, so the dim ones gather at the end as the round goes on.
            for (var i = 0; i < pips.Count; i++) {
                pips[i].EnableInClassList(PipOutClass, i >= carsLeft);
            }

            carsLabel.text = $"{carsLeft} LEFT";
        }

        // A little car seen from above: four wheels under a body with a windscreen, drawn by ui_screens alone, so the
        // build carries no image for it.
        private static VisualElement CreatePip()
        {
            var pip = new VisualElement { pickingMode = PickingMode.Ignore };
            pip.AddToClassList(PipClass);

            foreach (var wheelClass in WheelClasses) {
                var wheel = new VisualElement { pickingMode = PickingMode.Ignore };
                wheel.AddToClassList("pip__wheel");
                wheel.AddToClassList(wheelClass);
                pip.Add(wheel);
            }

            var body = new VisualElement { pickingMode = PickingMode.Ignore };
            body.AddToClassList("pip__body");
            var glass = new VisualElement { pickingMode = PickingMode.Ignore };
            glass.AddToClassList("pip__glass");
            body.Add(glass);
            pip.Add(body);

            return pip;
        }

        // Scales the element up for a moment; taking the class off again lets its transition ease it back.
        private static void Pulse(VisualElement element, string pulseClass)
        {
            element.AddToClassList(pulseClass);
            element.schedule.Execute(() => element.RemoveFromClassList(pulseClass)).StartingIn(PulseDuration);
        }
    }
}
