using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    public sealed class HudView : FadingView<HudViewModel>
    {
        // The numbers move at most once a second, so a tenth of one keeps them in step without running every frame.
        private const long RefreshInterval = 100;

        // Seconds left from which the time pill turns red.
        private const int UrgentSeconds = 10;

        // How long a beat or a pop holds its bigger scale before the transition in ui_screens eases it back.
        private const long PulseDuration = 120;

        private const string UrgentClass = "hud__timer--urgent";
        private const string BeatClass = "hud__timer--beat";
        private const string PopClass = "hud__cars--pop";
        private const string PipClass = "pip";
        private const string PipOutClass = "pip--out";

        private static readonly string[] WheelClasses = {
            "pip__wheel--front-left", "pip__wheel--front-right", "pip__wheel--back-left", "pip__wheel--back-right",
        };

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

        protected override void OnAttached()
        {
            // The HUD lies over the game: nothing on it may take a click or a touch from the controls.
            Root.pickingMode = PickingMode.Ignore;

            timePill = Root.Q<VisualElement>("time-pill");
            timeLabel = Root.Q<Label>("time");
            timeFaceLabel = Root.Q<Label>("time-face");
            carsPlate = Root.Q<VisualElement>("cars-plate");
            pipRow = Root.Q<VisualElement>("pips");
            carsLabel = Root.Q<Label>("cars");

            Refresh();
            Root.schedule.Execute(Refresh).Every(RefreshInterval);
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
