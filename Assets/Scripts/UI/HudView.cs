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

        private const string UrgentClass = "hud__pill--urgent";

        private VisualElement timePill;
        private Label timeLabel;
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
            carsLabel = Root.Q<Label>("cars");

            Refresh();
            Root.schedule.Execute(Refresh).Every(RefreshInterval);
        }

        private void Refresh()
        {
            // Up, so the pill reads 0:00 only when the time is really out.
            var seconds = Mathf.CeilToInt(ViewModel.TimeLeft);
            if (seconds != shownSeconds) {
                shownSeconds = seconds;
                timeLabel.text = $"{seconds / 60}:{seconds % 60:00}";
                timePill.EnableInClassList(UrgentClass, seconds <= UrgentSeconds);
            }

            var carsLeft = ViewModel.CarsLeft;
            var carCount = ViewModel.CarCount;
            if (carsLeft != shownCarsLeft || carCount != shownCarCount) {
                shownCarsLeft = carsLeft;
                shownCarCount = carCount;
                carsLabel.text = $"{carsLeft}/{carCount}";
            }
        }
    }
}
