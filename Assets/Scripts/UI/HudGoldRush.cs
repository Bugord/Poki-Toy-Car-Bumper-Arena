using System;
using ToyCarArena.Gameplay.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The GOLD RUSH mode on the HUD: the coins every bot carries as a coin and a number in its handle (gold for the car
    /// in the lead), the player's own coins bigger on a sticker under the player's car that jumps with every coin taken,
    /// and a red −N springing over a car that just lost coins to a bump or a fall. The coins themselves are in the arena;
    /// this gives them a count.
    /// <para>
    /// Made only in the mode (<see cref="Create"/> gives null otherwise). Built once (a handle rebuilt gets a new chip);
    /// a frame sets text only when a count changes, on real time; nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class HudGoldRush : IModeHud, IModeNameHud
    {
        // Reference pixels under the player's car the middle of its coin sticker sits: below the car and its ring.
        private const float PlayerDrop = 92f;
        private const float Margin = 20f;
        // Reference pixels over a car its loss springs at: over its handle.
        private const float LossLift = 170f;
        private const float LossDrop = 120f;
        private const float LossSpring = 0.24f;
        private const float LossHold = 0.8f;
        private const float LossLeave = 0.3f;
        private const int LossPool = 4;
        // Seconds the player's sticker jumps for after a coin, and how far.
        private const float JumpTime = 0.22f;
        private const float JumpScale = 0.28f;

        private const string LeadClass = "car-coins--lead";
        private const string ShownClass = "hud__coins--shown";

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color PaleGold = new Color32(0xff, 0xe8, 0x8f, 0xff);

        private sealed class Chip
        {
            public VisualElement Root;
            public Label[] Labels;
            public int Shown = -1;
            public bool IsLead;
        }

        private sealed class Pop
        {
            public VisualElement Root;
            public VisualElement Sticker;
            public Label[] Labels;
            public int Index = -1;
            public float Start = -10f;
        }

        private readonly HudViewModel model;
        private readonly GoldRushMode mode;
        private readonly VisualElement layer;
        private readonly VisualElement bar;
        private readonly VisualElement scoreboard;
        private Chip[] chips;
        private readonly Chip player;
        private readonly VisualElement playerRoot;
        private readonly Pop[] pops = new Pop[LossPool];
        private float jumpStart = -10f;
        private int nextPop;

        /// <summary>The mode's HUD, made by its look (<see cref="GoldRushLook"/>) in a GOLD RUSH round only.</summary>
        public static HudGoldRush Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.Mode is GoldRushMode { IsActive: true } mode && layer != null
                ? new HudGoldRush(layer, root, model, mode)
                : null;
        }

        private HudGoldRush(VisualElement layer, VisualElement root, HudViewModel model, GoldRushMode mode)
        {
            this.model = model;
            this.mode = mode;
            this.layer = layer;
            bar = root?.Q("hud-bar");
            scoreboard = root?.Q("scoreboard");
            chips = new Chip[Mathf.Max(1, model.CarCount)];

            playerRoot = new VisualElement { name = "player-coins", pickingMode = PickingMode.Ignore };
            playerRoot.AddToClassList("hud__coins");
            player = CreateChip("hud__coins-sticker", "hud__coins-icon", "hud__coins-text");
            playerRoot.Add(player.Root);
            layer.Add(playerRoot);

            for (var i = 0; i < LossPool; i++) {
                var pop = new Pop { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
                pop.Root.AddToClassList("hud__coin-loss");
                pop.Sticker = new VisualElement { pickingMode = PickingMode.Ignore };
                pop.Sticker.AddToClassList("hud__coin-loss-sticker");
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("hud__coin-loss-icon");
                icon.generateVisualContent += DrawCoin;
                pop.Sticker.Add(StrokeText.Create("-1", null, out pop.Labels, "display", "hud__coin-loss-text"));
                pop.Sticker.Add(icon);
                pop.Root.Add(pop.Sticker);
                pop.Root.style.display = DisplayStyle.None;
                layer.Add(pop.Root);
                pops[i] = pop;
            }

            mode.CoinTaken += OnCoinTaken;
            mode.CoinsLost += OnCoinsLost;
        }

        /// <summary>A bot's handle: its coins after the name, the latest chip kept for the car.</summary>
        public void MarkName(VisualElement line, int index)
        {
            if (index < 0) {
                return;
            }

            if (index >= chips.Length) {
                Array.Resize(ref chips, index + 1);
            }

            var chip = CreateChip("car-coins", "car-coins__icon", "car-coins__text");
            line.Add(chip.Root);
            chips[index] = chip;
            Show(chip, model.ScoreOf(index), IsLeader(index));
        }

        public void Tick(Camera camera, float top, float left, float right)
        {
            for (var i = 0; i < chips.Length; i++) {
                if (chips[i] != null && chips[i].Root.panel != null) {
                    Show(chips[i], model.ScoreOf(i), IsLeader(i));
                }
            }

            var time = Time.unscaledTime;
            PlacePlayer(camera, top, left, right, time);
            PlacePops(camera, top, left, right, time);
        }

        public void Dispose()
        {
            mode.CoinTaken -= OnCoinTaken;
            mode.CoinsLost -= OnCoinsLost;
            playerRoot.RemoveFromHierarchy();
            foreach (var pop in pops) {
                pop.Root.RemoveFromHierarchy();
            }
        }

        /// <summary>
        /// A toy gold coin in the box: a gold disc with a navy rim, a paler face inside it and a shine on its upper left.
        /// The mode's points on the bar, the score table and the result's table, and the coin of the HUD's counters.
        /// </summary>
        internal static void DrawCoin(MeshGenerationContext context)
        {
            var box = context.visualElement.contentRect;
            if (box.width < 1f || box.height < 1f) {
                return;
            }

            var painter = context.painter2D;
            var size = Mathf.Min(box.width, box.height);
            var middle = box.center;
            var line = Mathf.Max(3f, size * 0.09f);
            var radius = size * 0.5f - line * 0.5f;
            painter.BeginPath();
            painter.Arc(middle, radius, 0f, 360f);
            painter.ClosePath();
            painter.fillColor = Gold;
            painter.Fill();
            painter.strokeColor = Navy;
            painter.lineWidth = line;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(middle, radius * 0.6f, 0f, 360f);
            painter.ClosePath();
            painter.fillColor = PaleGold;
            painter.Fill();
            painter.strokeColor = new Color32(0xe0, 0x9a, 0x10, 0xff);
            painter.lineWidth = Mathf.Max(1.5f, line * 0.45f);
            painter.Stroke();

            // A short bar on the face, as a stamped mark, so the disc reads as a coin and not a dot.
            painter.BeginPath();
            painter.MoveTo(middle + new Vector2(0f, -radius * 0.32f));
            painter.LineTo(middle + new Vector2(0f, radius * 0.32f));
            painter.strokeColor = new Color32(0xe0, 0x9a, 0x10, 0xff);
            painter.lineWidth = Mathf.Max(2f, line * 0.7f);
            painter.lineCap = LineCap.Round;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(middle, radius * 0.8f, 200f, 250f);
            painter.strokeColor = Color.white;
            painter.lineWidth = Mathf.Max(2f, line * 0.55f);
            painter.lineCap = LineCap.Round;
            painter.Stroke();
        }

        private bool IsLeader(int index) => index >= 0 && index == mode.LeaderIndex;

        private void PlacePlayer(Camera camera, float top, float left, float right, float time)
        {
            var car = model.PlayerCar;
            var isShown = car != null && !car.IsOut && camera != null && layer.panel != null && model.HasBegun
                && !model.IsFinished;
            playerRoot.EnableInClassList(ShownClass, isShown);
            if (!isShown) {
                return;
            }

            Show(player, model.ScoreOf(model.PlayerIndex), IsLeader(model.PlayerIndex));
            var age = time - jumpStart;
            var jump = age < JumpTime ? 1f + JumpScale * Mathf.Sin(age / JumpTime * Mathf.PI) : 1f;
            player.Root.style.scale = new Scale(new Vector3(jump, jump, 1f));
            var point = Project(camera, car.Position);
            point.y += PlayerDrop;
            Clamp(ref point, player.Root, top, left, right);
            playerRoot.style.left = point.x;
            playerRoot.style.top = point.y;
        }

        private void PlacePops(Camera camera, float top, float left, float right, float time)
        {
            foreach (var pop in pops) {
                var age = time - pop.Start;
                var isShown = pop.Index >= 0 && pop.Index < model.CarCount && age < LossSpring + LossHold + LossLeave
                    && camera != null && layer.panel != null;
                pop.Root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isShown) {
                    continue;
                }

                // Springs in past full size, holds, then shrinks away as it rises: no fade over outlined letters.
                float scale;
                var rise = 0f;
                if (age < LossSpring) {
                    var t = age / LossSpring;
                    scale = 1f + 0.25f * Mathf.Sin(t * Mathf.PI) - (1f - t) * (1f - t);
                }
                else if (age < LossSpring + LossHold) {
                    scale = 1f;
                }
                else {
                    var t = (age - LossSpring - LossHold) / LossLeave;
                    scale = 1f - t;
                    rise = 50f * t;
                }

                pop.Sticker.style.scale = new Scale(new Vector3(Mathf.Max(0f, scale), Mathf.Max(0f, scale), 1f));
                var car = Project(camera, model.PositionOf(pop.Index));
                var point = car;
                point.y -= LossLift + rise;
                // Over a car on the far edge there is no room above it under the bar: it goes under the car instead.
                var popTop = BelowBar(top);
                var half = new Vector2(pop.Sticker.layout.width, pop.Sticker.layout.height) * 0.5f;
                if (point.y - half.y - Margin < popTop || Covers(scoreboard, new Rect(point - half, half * 2f))) {
                    point.y = car.y + LossDrop - rise;
                }

                Clamp(ref point, pop.Sticker, popTop, left, right);
                pop.Root.style.left = point.x;
                pop.Root.style.top = point.y;
            }
        }

        private void OnCoinTaken(int index, Vector3 point)
        {
            if (index == model.PlayerIndex) {
                jumpStart = Time.unscaledTime;
            }
        }

        private void OnCoinsLost(int index, int amount, int by)
        {
            var pop = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Length;
            pop.Index = index;
            pop.Start = Time.unscaledTime;
            StrokeText.SetText(pop.Labels, "-" + amount);
            // Over the names in the layer: they are built after the pops, and a new handle would cover the sticker.
            pop.Root.BringToFront();
        }

        private Vector2 Project(Camera camera, Vector3 world)
        {
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, world, camera);
            return layer.WorldToLocal(panelPoint);
        }

        private float BelowBar(float top)
        {
            if (bar == null || bar.panel == null || bar.resolvedStyle.display == DisplayStyle.None) {
                return top;
            }

            return Mathf.Max(top, layer.WorldToLocal(new Vector2(0f, bar.worldBound.yMax)).y);
        }

        private bool Covers(VisualElement element, Rect rect)
        {
            if (element == null || element.panel == null || element.resolvedStyle.display == DisplayStyle.None
                || element.resolvedStyle.opacity <= 0f) {
                return false;
            }

            var bound = element.worldBound;
            var min = layer.WorldToLocal(bound.min);
            var max = layer.WorldToLocal(bound.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y).Overlaps(rect);
        }

        private void Clamp(ref Vector2 point, VisualElement element, float top, float left, float right)
        {
            var size = layer.layout.size;
            var halfWidth = element.layout.width * 0.5f + Margin;
            var halfHeight = element.layout.height * 0.5f + Margin;
            var minX = halfWidth + left;
            point.x = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - halfWidth - right));
            point.y = Mathf.Clamp(point.y, halfHeight + top, Mathf.Max(halfHeight + top, size.y - halfHeight));
        }

        private static void Show(Chip chip, int coins, bool isLead)
        {
            if (coins != chip.Shown) {
                chip.Shown = coins;
                StrokeText.SetText(chip.Labels, coins.ToString());
            }

            if (isLead != chip.IsLead) {
                chip.IsLead = isLead;
                chip.Root.EnableInClassList(LeadClass, isLead);
            }
        }

        private static Chip CreateChip(string className, string iconClass, string textClass)
        {
            var chip = new Chip { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
            chip.Root.AddToClassList(className);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(iconClass);
            icon.generateVisualContent += DrawCoin;
            chip.Root.Add(icon);
            chip.Root.Add(StrokeText.Create("0", null, out chip.Labels, "display", textClass));
            return chip;
        }
    }
}
