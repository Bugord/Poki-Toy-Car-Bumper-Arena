using Bugord.App.Localization;
using ToyCarArena.Gameplay;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A car's speed, power and boost (<see cref="GameTuning.CarStatLine"/>) as rows of pips out of 5, each stat in its own
    /// colour (car-stats in ui_screens): the full one names each stat, the compact one on a garage card is the pips alone
    /// in the same colours, so the full one under the hero reads as their key.
    /// </summary>
    internal static class CarStatsView
    {
        private static readonly string[] Names = { "speed", "power", "boost" };

        public static VisualElement Create(bool isCompact, string className = null)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList("car-stats");
            root.EnableInClassList("car-stats--compact", isCompact);
            if (className != null) {
                root.AddToClassList(className);
            }

            foreach (var name in Names) {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.AddToClassList("car-stats__row");
                row.AddToClassList($"car-stats__row--{name}");
                if (!isCompact) {
                    var label = new Label { pickingMode = PickingMode.Ignore };
                    label.AddToClassList("car-stats__label");
                    row.Add(label);
                }

                var pips = new VisualElement { pickingMode = PickingMode.Ignore };
                pips.AddToClassList("car-stats__pips");
                for (var i = 0; i < GameTuning.CarStatSettings.Max; i++) {
                    var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                    pip.AddToClassList("car-stats__pip");
                    pips.Add(pip);
                }

                row.Add(pips);
                root.Add(row);
            }

            return root;
        }

        /// <summary>Fills the pips with <paramref name="stats"/> and names the stats in the language shown.</summary>
        public static void Set(VisualElement root, GameTuning.CarStatLine stats)
        {
            var values = new[] { stats.Speed, stats.Power, stats.Boost };
            for (var i = 0; i < Names.Length; i++) {
                var row = root[i];
                var label = row.Q<Label>(className: "car-stats__label");
                if (label != null) {
                    label.text = Strings.Get($"car.stat_{Names[i]}");
                }

                var pips = row.Q(className: "car-stats__pips");
                for (var pip = 0; pip < pips.childCount; pip++) {
                    pips[pip].EnableInClassList("car-stats__pip--on", pip < values[i]);
                }
            }
        }
    }
}
