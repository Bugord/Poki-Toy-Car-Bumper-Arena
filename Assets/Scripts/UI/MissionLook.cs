using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The parts a mission is shown with on the menu's card, its popup and the result: a tile with the slot's sign (a
    /// stopwatch for one round, rising bars for a total, a bulb for a lesson) that turns gold with a tick once done, the
    /// count with a thin bar for a mission of more than one step, and the reward (the XP and the coins). A slot's sign
    /// instead of an empty circle, so a row does not read as a radio button to pick.
    /// </summary>
    internal static class MissionLook
    {
        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color Green = new Color32(0x5b, 0xd3, 0x4a, 0xff);

        public static Icon IconOf(MissionSlot slot) => new(slot);

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

        /// <summary>The slot's sign on a white tile, or a navy tick on gold once the mission is done.</summary>
        internal sealed class Icon : VisualElement
        {
            private readonly VisualElement sign;
            private MissionSlot slot;
            private bool isDone;

            public Icon(MissionSlot slot)
            {
                this.slot = slot;
                pickingMode = PickingMode.Ignore;
                AddToClassList("mission-icon");
                sign = Element("mission-icon__sign");
                sign.generateVisualContent += Draw;
                Add(sign);
            }

            public void Set(MissionSlot value, bool done)
            {
                if (slot == value && isDone == done) {
                    return;
                }

                slot = value;
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

                switch (slot) {
                    case MissionSlot.Round:
                        DrawStopwatch(p, r);
                        break;
                    case MissionSlot.Total:
                        DrawBars(p, r);
                        break;
                    default:
                        DrawBulb(p, r);
                        break;
                }
            }

            private static Vector2 At(Rect r, float x, float y) => new(r.x + x * r.width, r.y + y * r.height);

            private static void DrawTick(Painter2D p, Rect r)
            {
                p.lineWidth = r.width * 0.17f;
                p.BeginPath();
                p.MoveTo(At(r, 0.18f, 0.52f));
                p.LineTo(At(r, 0.42f, 0.76f));
                p.LineTo(At(r, 0.84f, 0.26f));
                p.Stroke();
            }

            // One round: a stopwatch, its hand at a quarter past.
            private static void DrawStopwatch(Painter2D p, Rect r)
            {
                var line = r.width * 0.1f;
                p.lineWidth = line;
                var centre = At(r, 0.5f, 0.57f);
                var radius = r.width * 0.36f;
                p.fillColor = Navy;
                p.BeginPath();
                p.MoveTo(At(r, 0.4f, 0.04f));
                p.LineTo(At(r, 0.6f, 0.04f));
                p.LineTo(At(r, 0.6f, 0.2f));
                p.LineTo(At(r, 0.4f, 0.2f));
                p.ClosePath();
                p.Fill();
                p.fillColor = Color.white;
                p.BeginPath();
                p.Arc(centre, radius, 0f, 360f);
                p.Fill();
                p.Stroke();
                p.strokeColor = Red;
                p.BeginPath();
                p.MoveTo(centre);
                p.LineTo(centre + new Vector2(0f, -radius * 0.62f));
                p.MoveTo(centre);
                p.LineTo(centre + new Vector2(radius * 0.5f, 0f));
                p.Stroke();
                p.strokeColor = Navy;
            }

            // Over rounds and visits: three bars rising to the right.
            private static void DrawBars(Painter2D p, Rect r)
            {
                p.lineWidth = r.width * 0.08f;
                p.fillColor = Green;
                foreach (var (x, top) in new[] { (0.08f, 0.56f), (0.37f, 0.36f), (0.66f, 0.14f) }) {
                    p.BeginPath();
                    p.MoveTo(At(r, x, top));
                    p.LineTo(At(r, x + 0.26f, top));
                    p.LineTo(At(r, x + 0.26f, 0.9f));
                    p.LineTo(At(r, x, 0.9f));
                    p.ClosePath();
                    p.Fill();
                    p.Stroke();
                }
            }

            // A lesson: a bulb with its screw base.
            private static void DrawBulb(Painter2D p, Rect r)
            {
                p.lineWidth = r.width * 0.09f;
                p.fillColor = Gold;
                p.BeginPath();
                p.Arc(At(r, 0.5f, 0.38f), r.width * 0.3f, 150f, 390f);
                p.LineTo(At(r, 0.62f, 0.72f));
                p.LineTo(At(r, 0.38f, 0.72f));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                p.BeginPath();
                p.MoveTo(At(r, 0.4f, 0.84f));
                p.LineTo(At(r, 0.6f, 0.84f));
                p.MoveTo(At(r, 0.44f, 0.95f));
                p.LineTo(At(r, 0.56f, 0.95f));
                p.Stroke();
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
