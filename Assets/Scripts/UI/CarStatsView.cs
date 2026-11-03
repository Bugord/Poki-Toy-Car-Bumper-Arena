using Bugord.App.Localization;
using ToyCarArena.Gameplay;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A car's speed, power and boost (<see cref="GameTuning.CarStatLine"/>) as racing games show them: a row per stat,
    /// its name and a rounded bar filled to its value out of 5 in the stat's own colour, the steps marked across the
    /// bar (car-stats in ui_screens). The garage shows it under the car it looks at, the popup under a new car.
    /// </summary>
    internal static class CarStatsView
    {
        private static readonly string[] Names = { "speed", "power", "boost" };

        public static VisualElement Create(string className = null)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("car-stats");
            if (className != null) {
                root.AddToClassList(className);
            }

            foreach (var name in Names) {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("car-stats__row");
                row.AddToClassList($"car-stats__row--{name}");
                var label = new Label { pickingMode = PickingMode.Ignore };
                label.AddToClassList("car-stats__label");
                row.Add(label);

                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.AddToClassList("car-stats__bar");
                var fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("car-stats__fill");
                var shine = new VisualElement { pickingMode = PickingMode.Ignore };
                shine.AddToClassList("car-stats__shine");
                fill.Add(shine);
                bar.Add(fill);
                for (var step = 1; step < GameTuning.CarStatSettings.Max; step++) {
                    var tick = new VisualElement { pickingMode = PickingMode.Ignore };
                    tick.AddToClassList("car-stats__tick");
                    tick.style.left = Length.Percent(100f * step / GameTuning.CarStatSettings.Max);
                    bar.Add(tick);
                }

                row.Add(bar);
                root.Add(row);
            }

            return root;
        }

        /// <summary>
        /// Fills the bars with <paramref name="stats"/> and names the stats in the language shown. Without boosts in
        /// the build (<paramref name="hasBoost"/> false) the boost row is left out: it would promise nothing.
        /// </summary>
        public static void Set(VisualElement root, GameTuning.CarStatLine stats, bool hasBoost)
        {
            var values = new[] { stats.Speed, stats.Power, stats.Boost };
            for (var i = 0; i < Names.Length; i++) {
                var row = root[i];
                row.style.display = i < 2 || hasBoost ? DisplayStyle.Flex : DisplayStyle.None;
                row.Q<Label>(className: "car-stats__label").text = Strings.Get($"car.stat_{Names[i]}");
                row.Q(className: "car-stats__fill").style.width =
                    Length.Percent(100f * values[i] / GameTuning.CarStatSettings.Max);
            }
        }
    }
}
