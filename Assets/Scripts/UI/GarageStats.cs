using Bugord.App.Localization;
using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A car's stats as the garage shows them: each stat's name over five pips filled to its value in the stat's
    /// colour, and beside the name how much a car tried on has more or less than the chosen one. The garage's pill
    /// under the pedestal and its BUY window use it, and so does the result's new car popup, so a car reads the same
    /// everywhere. Styles in <c>ui_garage.uss</c> (<c>garage-stats*</c>).
    /// </summary>
    internal static class GarageStats
    {
        private static readonly string[] Names = { "speed", "power", "weight", "boost" };

        /// <summary>The empty pill with <paramref name="className"/>; <see cref="Set"/> fills it.</summary>
        public static VisualElement Build(string className)
        {
            var pill = Element("garage-stats");
            pill.AddToClassList(className);
            foreach (var name in Names) {
                var stat = Element("garage-stats__stat");
                stat.AddToClassList($"garage-stats__stat--{name}");
                var head = Element("garage-stats__head");
                head.Add(Text("garage-stats__label"));
                var diff = Element("garage-stats__diff");
                var arrow = Element("garage-stats__arrow");
                arrow.generateVisualContent += DrawDiffArrow;
                diff.Add(arrow);
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
        /// ▲N or ▼N beside its name. Without boosts in the build the boost stat is hidden: it would promise nothing.
        /// </summary>
        public static void Set(VisualElement pill, GameTuning.CarStatLine line, bool hasBoost,
            GameTuning.CarStatLine against = null)
        {
            var values = new[] { line.Speed, line.Power, line.Weight, line.Boost };
            var bases = against == null ? values : new[] { against.Speed, against.Power, against.Weight, against.Boost };
            for (var i = 0; i < Names.Length; i++) {
                var stat = pill[i];
                stat.style.display = Names[i] != "boost" || hasBoost ? DisplayStyle.Flex : DisplayStyle.None;
                stat.Q<Label>(className: "garage-stats__label").text = Strings.Get($"car.stat_{Names[i]}");
                var change = values[i] - bases[i];
                var diff = stat.Q<VisualElement>(className: "garage-stats__diff");
                diff.style.display = change != 0 ? DisplayStyle.Flex : DisplayStyle.None;
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

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        private static Label Text(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
