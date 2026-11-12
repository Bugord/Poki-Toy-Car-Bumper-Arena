using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The parts a mission is shown with on the menu's card, its popup and the result: a tile with a sign of what the
    /// mission asks (the KO burst for knock-outs, a cup for wins, the rival's horns for revenge, a checkered flag for
    /// rounds and so on) that turns gold with a tick once done, the count with a thin bar for a mission of more than one
    /// step, and the reward (the XP and the coins). A sign of the task rather than of its slot, so the icon says what to
    /// do; a sign instead of an empty circle, so a row does not read as a radio button to pick.
    /// </summary>
    internal static class MissionLook
    {
        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color Orange = new Color32(0xff, 0x9f, 0x1c, 0xff);
        private static readonly Color Purple = new Color32(0xb0, 0x6c, 0xff, 0xff);
        private static readonly Color Blue = new Color32(0x38, 0xb6, 0xff, 0xff);
        private static readonly Color Green = new Color32(0x5b, 0xd3, 0x4a, 0xff);
        private static readonly Color CarBlue = new Color32(0x5f, 0x74, 0xcb, 0xff);
        private static readonly Color CarRed = new Color32(0xe6, 0x5f, 0x48, 0xff);
        private static readonly Color Candy = new Color32(0xff, 0x7e, 0xb6, 0xff);

        /// <summary>The sign of a mission's task; without one (a card not filled yet) the tile stays blank.</summary>
        public static Icon IconOf(GameTuning.MissionDefinition definition) => new(definition);

        /// <summary>The count over a thin bar: <see cref="Progress.Set"/> hides both for a mission of one step.</summary>
        public static Progress ProgressOf() => new();

        /// <summary>+N XP with the XP star and the coins with a coin, side by side.</summary>
        public static VisualElement Reward(int xp, int coins, bool hasXp = true)
        {
            var reward = Element("mission-reward");
            if (hasXp) {
                var xpPart = Element("mission-reward__xp");
                var star = Element("mission-reward__star");
                star.generateVisualContent += MenuView.DrawXpStar;
                xpPart.Add(star);
                xpPart.Add(Text($"+{xp} XP", "mission-reward__xp-count"));
                reward.Add(xpPart);
            }

            var coinPart = Element("mission-reward__coins");
            coinPart.Add(RoundOverMeta.Coin());
            coinPart.Add(Text(coins.ToString(), "mission-reward__coin-count"));
            reward.Add(coinPart);
            return reward;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        /// <summary>The sign of the mission's task on a white tile, or a navy tick on gold once the mission is done.</summary>
        internal sealed class Icon : VisualElement
        {
            private readonly VisualElement sign;
            private MissionCounter? counter;
            private string map = "";
            private string mode = "";
            private bool isDone;

            public Icon(GameTuning.MissionDefinition definition)
            {
                counter = definition?.Counter;
                map = definition?.Map ?? "";
                mode = definition?.Mode ?? "";
                pickingMode = PickingMode.Ignore;
                AddToClassList("mission-icon");
                sign = Element("mission-icon__sign");
                sign.generateVisualContent += Draw;
                Add(sign);
            }

            public void Set(GameTuning.MissionDefinition definition, bool done)
            {
                var value = definition?.Counter;
                var valueMap = definition?.Map ?? "";
                var valueMode = definition?.Mode ?? "";
                if (counter == value && map == valueMap && mode == valueMode && isDone == done) {
                    return;
                }

                counter = value;
                map = valueMap;
                mode = valueMode;
                isDone = done;
                EnableInClassList("mission-icon--done", done);
                sign.MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                var r = sign.contentRect;
                if (float.IsNaN(r.width) || r.width <= 0f) {
                    return;
                }

                var p = context.painter2D;
                p.strokeColor = Navy;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                if (isDone) {
                    DrawTick(p, r);
                    return;
                }

                switch (counter) {
                    case null:
                        break;
                    case MissionCounter.Knockouts:
                        HudView.PaintBurst(p, At(r, 0.5f, 0.5f), r.width * 0.5f, Gold);
                        break;
                    case MissionCounter.ItemKnockouts:
                        HudView.PaintBurst(p, At(r, 0.42f, 0.42f), r.width * 0.42f, Gold);
                        DrawBomb(p, r, At(r, 0.62f, 0.68f), 0.25f);
                        break;
                    case MissionCounter.ItemsUsed:
                        DrawBomb(p, r, At(r, 0.44f, 0.6f), 0.34f);
                        break;
                    case MissionCounter.Boxes:
                        DrawBox(p, r);
                        break;
                    case MissionCounter.Wins:
                        DrawCup(p, r);
                        break;
                    case MissionCounter.FinalTwo:
                        DrawCar(p, r, new Rect(0.18f, 0f, 0.82f, 0.54f), CarRed);
                        DrawCar(p, r, new Rect(0f, 0.42f, 0.82f, 0.54f), CarBlue);
                        break;
                    case MissionCounter.Revenge:
                        DrawHorns(p, r);
                        break;
                    case MissionCounter.Boosts:
                        DrawBolt(p, r);
                        break;
                    case MissionCounter.ArenaRounds:
                        DrawArena(p, r, ColourOf(map));
                        break;
                    case MissionCounter.EventRounds:
                        DrawEvent(p, r, (ModeLooks.Of(EventModes.Parse(mode)) ?? HotPotatoLook.Look).Deep);
                        break;
                    default:
                        DrawFlag(p, r);
                        break;
                }
            }

            private static Vector2 At(Rect r, float x, float y) => new(r.x + x * r.width, r.y + y * r.height);

            // The arena's floor in its own colour, so three arena missions do not look alike.
            private static Color ColourOf(string map) => map switch {
                "tabletop" => Orange,
                "candy" => Candy,
                "playroom" => Blue,
                _ => Green,
            };

            private static void DrawTick(Painter2D p, Rect r)
            {
                p.lineWidth = r.width * 0.17f;
                p.BeginPath();
                p.MoveTo(At(r, 0.18f, 0.52f));
                p.LineTo(At(r, 0.42f, 0.76f));
                p.LineTo(At(r, 0.84f, 0.26f));
                p.Stroke();
            }

            // Items: a navy bomb with a white glint, its fuse and an orange spark, as on the item button.
            private static void DrawBomb(Painter2D p, Rect r, Vector2 centre, float size)
            {
                var radius = r.width * size;
                p.lineWidth = r.width * 0.08f;
                p.BeginPath();
                p.MoveTo(centre + new Vector2(radius * 0.55f, -radius * 0.75f));
                p.QuadraticCurveTo(centre + new Vector2(radius * 1.05f, -radius * 1.5f),
                    centre + new Vector2(radius * 1.15f, -radius * 1.1f));
                p.Stroke();
                p.fillColor = Navy;
                p.BeginPath();
                p.Arc(centre, radius, 0f, 360f);
                p.Fill();
                p.fillColor = Color.white;
                p.BeginPath();
                p.Arc(centre + new Vector2(-radius * 0.38f, -radius * 0.38f), radius * 0.2f, 0f, 360f);
                p.Fill();
                p.fillColor = Orange;
                p.lineWidth = r.width * 0.06f;
                p.BeginPath();
                p.Arc(centre + new Vector2(radius * 1.15f, -radius * 1.1f), Mathf.Max(radius * 0.32f, r.width * 0.08f), 0f,
                    360f);
                p.Fill();
                p.Stroke();
            }

            // Boxes: the floor's surprise box, purple with a white question mark.
            private static void DrawBox(Painter2D p, Rect r)
            {
                var line = r.width * 0.09f;
                p.lineWidth = line;
                p.fillColor = Purple;
                RoundedBox(p, At(r, 0.08f, 0.08f), At(r, 0.92f, 0.92f), r.width * 0.14f);
                p.Fill();
                p.Stroke();
                foreach (var (colour, width) in new[] { (Navy, r.width * 0.2f), (Color.white, r.width * 0.11f) }) {
                    p.strokeColor = colour;
                    p.lineWidth = width;
                    p.BeginPath();
                    p.Arc(At(r, 0.5f, 0.38f), r.width * 0.15f, 190f, 405f);
                    p.LineTo(At(r, 0.5f, 0.6f));
                    p.Stroke();
                    p.fillColor = colour;
                    p.BeginPath();
                    p.Arc(At(r, 0.5f, 0.76f), width * 0.55f, 0f, 360f);
                    p.Fill();
                }

                p.strokeColor = Navy;
            }

            // Wins: a gold cup with two handles on a stem and a base.
            private static void DrawCup(Painter2D p, Rect r)
            {
                var line = r.width * 0.085f;
                p.lineWidth = line;
                p.BeginPath();
                p.Arc(At(r, 0.2f, 0.3f), r.width * 0.13f, 90f, 270f);
                p.Stroke();
                p.BeginPath();
                p.Arc(At(r, 0.8f, 0.3f), r.width * 0.13f, -90f, 90f);
                p.Stroke();
                p.fillColor = Gold;
                p.BeginPath();
                p.MoveTo(At(r, 0.2f, 0.08f));
                p.LineTo(At(r, 0.8f, 0.08f));
                p.LineTo(At(r, 0.77f, 0.32f));
                p.Arc(At(r, 0.5f, 0.32f), r.width * 0.27f, 0f, 180f);
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.BeginPath();
                p.MoveTo(At(r, 0.42f, 0.58f));
                p.LineTo(At(r, 0.58f, 0.58f));
                p.LineTo(At(r, 0.58f, 0.76f));
                p.LineTo(At(r, 0.42f, 0.76f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                RoundedBox(p, At(r, 0.24f, 0.76f), At(r, 0.76f, 0.92f), r.width * 0.04f);
                p.Fill();
                p.Stroke();
            }

            // The final two: two cars side by side, one a step behind the other, as the HUD's chips of cars left.
            private static void DrawCar(Painter2D p, Rect r, Rect box, Color body)
            {
                var area = new Rect(r.x + box.x * r.width, r.y + box.y * r.height, box.width * r.width,
                    box.height * r.height);
                Vector2 Point(float x, float y) => new(area.x + x * area.width, area.y + y * area.height);
                p.lineWidth = r.width * 0.07f;
                p.fillColor = body;
                p.BeginPath();
                p.MoveTo(Point(0.04f, 0.78f));
                p.LineTo(Point(0.04f, 0.46f));
                p.LineTo(Point(0.22f, 0.4f));
                p.LineTo(Point(0.34f, 0.08f));
                p.LineTo(Point(0.7f, 0.08f));
                p.LineTo(Point(0.82f, 0.4f));
                p.LineTo(Point(0.96f, 0.46f));
                p.LineTo(Point(0.96f, 0.78f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath();
                p.MoveTo(Point(0.4f, 0.22f));
                p.LineTo(Point(0.64f, 0.22f));
                p.LineTo(Point(0.7f, 0.4f));
                p.LineTo(Point(0.34f, 0.4f));
                p.ClosePath();
                p.Fill();
                foreach (var x in new[] { 0.26f, 0.74f }) {
                    p.fillColor = Navy;
                    p.BeginPath();
                    p.Arc(Point(x, 0.8f), area.width * 0.15f, 0f, 360f);
                    p.Fill();
                    p.fillColor = Color.white;
                    p.BeginPath();
                    p.Arc(Point(x, 0.8f), area.width * 0.055f, 0f, 360f);
                    p.Fill();
                }
            }

            // Revenge: a navy head with angry eyes under the rival's two red horns, the mark the HUD, the lobby and the
            // result put on the one to pay back. The head makes the horns read as horns, not as a red letter.
            private static void DrawHorns(Painter2D p, Rect r)
            {
                var line = r.width * 0.075f;
                HudView.PaintHorn(p, new Rect(r.x + r.width * 0.04f, r.y, r.width * 0.38f, r.height * 0.56f), false,
                    line);
                HudView.PaintHorn(p, new Rect(r.x + r.width * 0.58f, r.y, r.width * 0.38f, r.height * 0.56f), true,
                    line);
                p.strokeColor = Navy;
                p.fillColor = Navy;
                p.BeginPath();
                p.Arc(At(r, 0.5f, 0.64f), r.width * 0.34f, 0f, 360f);
                p.Fill();
                p.fillColor = Color.white;
                foreach (var side in new[] { -1f, 1f }) {
                    // Each eye slants down toward the nose: a frown.
                    float X(float x) => 0.5f + side * x;
                    p.BeginPath();
                    p.MoveTo(At(r, X(0.06f), 0.6f));
                    p.LineTo(At(r, X(0.24f), 0.52f));
                    p.LineTo(At(r, X(0.22f), 0.68f));
                    p.LineTo(At(r, X(0.08f), 0.7f));
                    p.ClosePath();
                    p.Fill();
                }
            }

            // Boosts: an orange lightning bolt, the boost button's colour.
            private static void DrawBolt(Painter2D p, Rect r)
            {
                p.lineWidth = r.width * 0.085f;
                p.fillColor = Orange;
                p.BeginPath();
                p.MoveTo(At(r, 0.64f, 0.02f));
                p.LineTo(At(r, 0.1f, 0.6f));
                p.LineTo(At(r, 0.44f, 0.6f));
                p.LineTo(At(r, 0.32f, 0.98f));
                p.LineTo(At(r, 0.92f, 0.38f));
                p.LineTo(At(r, 0.56f, 0.38f));
                p.LineTo(At(r, 0.7f, 0.02f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            // The mode of the day: a disc in the mode's colour with EVENT's white bolt, as its tag in the menu.
            private static void DrawEvent(Painter2D p, Rect r, Color colour)
            {
                p.lineWidth = r.width * 0.085f;
                p.fillColor = colour;
                p.BeginPath();
                p.Arc(At(r, 0.5f, 0.5f), r.width * 0.44f, 0f, 360f);
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath();
                p.MoveTo(At(r, 0.58f, 0.18f));
                p.LineTo(At(r, 0.3f, 0.56f));
                p.LineTo(At(r, 0.48f, 0.56f));
                p.LineTo(At(r, 0.42f, 0.82f));
                p.LineTo(At(r, 0.72f, 0.44f));
                p.LineTo(At(r, 0.54f, 0.44f));
                p.LineTo(At(r, 0.62f, 0.18f));
                p.ClosePath();
                p.Fill();
                p.lineWidth = r.width * 0.06f;
                p.Stroke();
            }

            // An arena: a round floor seen from above at a slant, its rim white, in the arena's colour.
            private static void DrawArena(Painter2D p, Rect r, Color floor)
            {
                p.lineWidth = r.width * 0.085f;
                var top = At(r, 0.5f, 0.4f);
                var radiusX = r.width * 0.44f;
                var radiusY = r.height * 0.24f;
                var depth = r.height * 0.22f;
                p.fillColor = Color.Lerp(floor, Navy, 0.35f);
                p.BeginPath();
                p.MoveTo(top + new Vector2(-radiusX, 0f));
                p.LineTo(top + new Vector2(-radiusX, depth));
                Ellipse(p, top + new Vector2(0f, depth), radiusX, radiusY, 180f, 360f, false);
                p.LineTo(top + new Vector2(radiusX, 0f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.fillColor = Color.white;
                p.BeginPath();
                Ellipse(p, top, radiusX, radiusY, 0f, 360f, true);
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.fillColor = floor;
                p.BeginPath();
                Ellipse(p, top, radiusX * 0.72f, radiusY * 0.68f, 0f, 360f, true);
                p.ClosePath();
                p.Fill();
            }

            // Rounds: a checkered flag on its pole.
            private static void DrawFlag(Painter2D p, Rect r)
            {
                var line = r.width * 0.085f;
                const float left = 0.22f, right = 0.9f, top = 0.08f, bottom = 0.6f;
                p.fillColor = Color.white;
                p.lineWidth = line;
                p.BeginPath();
                p.MoveTo(At(r, left, top));
                p.LineTo(At(r, right, top));
                p.LineTo(At(r, right, bottom));
                p.LineTo(At(r, left, bottom));
                p.ClosePath();
                p.Fill();
                p.fillColor = Navy;
                const int columns = 3, rows = 2;
                var cellX = (right - left) / columns;
                var cellY = (bottom - top) / rows;
                for (var row = 0; row < rows; row++) {
                    for (var column = (row + 1) % 2; column < columns; column += 2) {
                        p.BeginPath();
                        p.MoveTo(At(r, left + column * cellX, top + row * cellY));
                        p.LineTo(At(r, left + (column + 1) * cellX, top + row * cellY));
                        p.LineTo(At(r, left + (column + 1) * cellX, top + (row + 1) * cellY));
                        p.LineTo(At(r, left + column * cellX, top + (row + 1) * cellY));
                        p.ClosePath();
                        p.Fill();
                    }
                }

                p.BeginPath();
                p.MoveTo(At(r, left, top));
                p.LineTo(At(r, right, top));
                p.LineTo(At(r, right, bottom));
                p.LineTo(At(r, left, bottom));
                p.ClosePath();
                p.Stroke();
                p.lineWidth = line * 1.2f;
                p.BeginPath();
                p.MoveTo(At(r, left - 0.06f, 0.04f));
                p.LineTo(At(r, left - 0.06f, 0.96f));
                p.Stroke();
            }

            private static void RoundedBox(Painter2D p, Vector2 min, Vector2 max, float radius)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(min.x + radius, min.y));
                p.ArcTo(new Vector2(max.x, min.y), new Vector2(max.x, max.y), radius);
                p.ArcTo(new Vector2(max.x, max.y), new Vector2(min.x, max.y), radius);
                p.ArcTo(new Vector2(min.x, max.y), new Vector2(min.x, min.y), radius);
                p.ArcTo(new Vector2(min.x, min.y), new Vector2(max.x, min.y), radius);
                p.ClosePath();
            }

            // Painter2D has only circular arcs: an ellipse (or part of one) as a line through points, angles clockwise.
            private static void Ellipse(Painter2D p, Vector2 centre, float radiusX, float radiusY, float from, float to,
                bool isNewPath)
            {
                const int steps = 32;
                for (var i = 0; i <= steps; i++) {
                    var angle = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                    var point = centre + new Vector2(Mathf.Cos(angle) * radiusX, -Mathf.Sin(angle) * radiusY);
                    if (i == 0 && isNewPath) {
                        p.MoveTo(point);
                    }
                    else {
                        p.LineTo(point);
                    }
                }
            }
        }

        /// <summary>The count (2/5) over a thin gold bar, shown only for a mission of more than one step.</summary>
        internal sealed class Progress : VisualElement
        {
            private readonly Label count;
            private readonly VisualElement fill;

            public Progress()
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList("mission-progress");
                count = Text("", "mission-progress__count");
                Add(count);
                var bar = Element("mission-progress__bar");
                fill = Element("mission-progress__fill");
                bar.Add(fill);
                Add(bar);
            }

            /// <summary>Shows progress of target; a one-step mission shows nothing (0/1 says no more than the task).</summary>
            public void Set(int progress, int target)
            {
                style.display = target > 1 ? DisplayStyle.Flex : DisplayStyle.None;
                count.text = $"{progress}/{target}";
                fill.style.width = Length.Percent(100f * Mathf.Clamp01((float)progress / Mathf.Max(1, target)));
            }
        }
    }
}
