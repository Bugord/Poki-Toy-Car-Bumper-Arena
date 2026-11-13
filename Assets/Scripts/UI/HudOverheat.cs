using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The OVERHEAT mode on the HUD: every bot's heat as a flame and a percent in its handle, white when cold, orange when
    /// warm and red, beating, when red-hot; the player's own heat bigger, on a sticker under the player's car; RED HOT!
    /// springing over a car whose heat just got there; and MELTDOWN! across the screen when the player knocks a red-hot
    /// car off. The heat itself is in the arena too (steam and flames); this gives it a number.
    /// <para>
    /// Made only in the mode (<see cref="Create"/> gives null otherwise). Built once (a handle rebuilt gets a new chip);
    /// a frame sets text only when a percent changes and styles otherwise, on real time; nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class HudOverheat : IModeHud, IModeNameHud
    {
        // Reference pixels under the player's car the middle of its heat sticker sits: below the car and its ring.
        private const float PlayerDrop = 92f;
        private const float Margin = 20f;
        // Reference pixels over a car RED HOT! springs at: over its handle.
        private const float HotLift = 190f;
        // How far under the car RED HOT! sits when there is no room above it.
        private const float HotDrop = 120f;
        private const float HotSpring = 0.28f;
        private const float HotHold = 1.1f;
        private const float HotLeave = 0.3f;
        private const int HotPool = 3;
        // Reference pixels under the top of the safe area MELTDOWN! sits at: under the bar and the kill feed.
        private const float MeltdownTop = 430f;
        private const float MeltdownTime = 1.6f;
        private const float BeatRate = 7f;

        private const string ColdClass = "car-heat--cold";
        private const string WarmClass = "car-heat--warm";
        private const string HotClass = "car-heat--hot";
        private const string ShownClass = "hud__heat--shown";

        private static readonly Color Navy = new(0.114f, 0.169f, 0.31f);

        private sealed class Chip
        {
            public VisualElement Root;
            public Label[] Labels;
            public int Shown = -1;
            public int Step = -1;
        }

        private sealed class Pop
        {
            public VisualElement Root;
            public VisualElement Sticker;
            public int Index = -1;
            public float Start = -10f;
        }

        private readonly HudViewModel model;
        private readonly OverheatMode mode;
        private readonly VisualElement layer;
        // The HUD's top bar: a pop over a car on the far edge stays under it rather than behind the clock.
        private readonly VisualElement bar;
        // The score table: in portrait it hangs under the bar's right end, over the far edge's cars.
        private readonly VisualElement scoreboard;
        private Chip[] chips;
        private readonly Chip player;
        private readonly VisualElement playerRoot;
        private readonly Pop[] pops = new Pop[HotPool];
        private readonly Label[][] popLabels = new Label[HotPool][];
        private readonly VisualElement meltdown;
        private readonly VisualElement meltdownSticker;
        private readonly Label[] meltdownLabels;
        private readonly Action onLanguage;
        private float meltdownStart = -10f;
        private int nextPop;

        /// <summary>The mode's HUD, made by its look (<see cref="OverheatLook"/>) in an overheat round only.</summary>
        public static HudOverheat Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.Mode is OverheatMode { IsActive: true } mode && layer != null
                ? new HudOverheat(layer, root, model, mode)
                : null;
        }

        private HudOverheat(VisualElement layer, VisualElement root, HudViewModel model, OverheatMode mode)
        {
            this.model = model;
            this.mode = mode;
            this.layer = layer;
            bar = root?.Q("hud-bar");
            scoreboard = root?.Q("scoreboard");
            chips = new Chip[Mathf.Max(1, model.CarCount)];

            playerRoot = new VisualElement { name = "player-heat", pickingMode = PickingMode.Ignore };
            playerRoot.AddToClassList("hud__heat");
            player = CreateChip("hud__heat-sticker", "hud__heat-flame", "hud__heat-text");
            playerRoot.Add(player.Root);
            layer.Add(playerRoot);

            for (var i = 0; i < HotPool; i++) {
                var pop = new Pop { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
                pop.Root.AddToClassList("hud__red-hot");
                pop.Sticker = new VisualElement { pickingMode = PickingMode.Ignore };
                pop.Sticker.AddToClassList("hud__red-hot-sticker");
                pop.Sticker.Add(StrokeText.Create(Strings.Get("hud.heat_hot"), null, out popLabels[i], "display",
                    "hud__red-hot-text"));
                pop.Root.Add(pop.Sticker);
                pop.Root.style.display = DisplayStyle.None;
                layer.Add(pop.Root);
                pops[i] = pop;
            }

            meltdown = new VisualElement { name = "meltdown", pickingMode = PickingMode.Ignore };
            meltdown.AddToClassList("hud__meltdown");
            meltdownSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            meltdownSticker.AddToClassList("hud__meltdown-sticker");
            meltdownSticker.Add(StrokeText.Create(Strings.Get("hud.meltdown"), null, out meltdownLabels, "display",
                "hud__meltdown-text"));
            meltdown.Add(meltdownSticker);
            meltdown.style.display = DisplayStyle.None;
            root.Add(meltdown);

            onLanguage = () => {
                for (var i = 0; i < HotPool; i++) {
                    StrokeText.SetText(popLabels[i], Strings.Get("hud.heat_hot"));
                }

                StrokeText.SetText(meltdownLabels, Strings.Get("hud.meltdown"));
            };
            Strings.Changed += onLanguage;
            mode.WentRedHot += OnRedHot;
            mode.Meltdown += OnMeltdown;
        }

        /// <summary>A bot's handle: its heat after the name, the latest chip kept for the car.</summary>
        public void MarkName(VisualElement line, int index)
        {
            if (index < 0) {
                return;
            }

            if (index >= chips.Length) {
                System.Array.Resize(ref chips, index + 1);
            }

            var chip = CreateChip("car-heat", "car-heat__flame", "car-heat__text");
            line.Add(chip.Root);
            chips[index] = chip;
            Show(chip, mode.HeatOf(index));
        }

        public void Tick(Camera camera, float top, float left, float right)
        {
            var time = Time.unscaledTime;
            for (var i = 0; i < chips.Length; i++) {
                if (chips[i] != null && chips[i].Root.panel != null) {
                    Show(chips[i], mode.HeatOf(i));
                    Beat(chips[i], time + i * 0.3f);
                }
            }

            PlacePlayer(camera, top, left, right, time);
            PlacePops(camera, top, left, right, time);
            PlaceMeltdown(top, time);
        }

        public void Dispose()
        {
            Strings.Changed -= onLanguage;
            mode.WentRedHot -= OnRedHot;
            mode.Meltdown -= OnMeltdown;
            playerRoot.RemoveFromHierarchy();
            foreach (var pop in pops) {
                pop.Root.RemoveFromHierarchy();
            }

            meltdown.RemoveFromHierarchy();
        }

        private void PlacePlayer(Camera camera, float top, float left, float right, float time)
        {
            var car = model.PlayerCar;
            var isShown = car != null && !car.IsOut && camera != null && layer.panel != null && model.HasBegun
                && !model.IsFinished;
            playerRoot.EnableInClassList(ShownClass, isShown);
            if (!isShown) {
                return;
            }

            Show(player, mode.HeatOf(model.PlayerIndex));
            Beat(player, time);
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
                var isShown = pop.Index >= 0 && pop.Index < model.CarCount && age < HotSpring + HotHold + HotLeave
                    && camera != null && layer.panel != null;
                pop.Root.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
                if (!isShown) {
                    continue;
                }

                // Springs in past full size, holds, then shrinks away as it rises: no fade over outlined letters.
                float scale;
                var rise = 0f;
                if (age < HotSpring) {
                    var t = age / HotSpring;
                    scale = 1f + 0.25f * Mathf.Sin(t * Mathf.PI) - (1f - t) * (1f - t);
                }
                else if (age < HotSpring + HotHold) {
                    scale = 1f;
                }
                else {
                    var t = (age - HotSpring - HotHold) / HotLeave;
                    scale = 1f - t;
                    rise = 60f * t;
                }

                pop.Sticker.style.scale = new Scale(new Vector3(Mathf.Max(0f, scale), Mathf.Max(0f, scale), 1f));
                var car = Project(camera, model.PositionOf(pop.Index));
                var point = car;
                point.y -= HotLift + rise;
                // Over a car on the far edge there is no room above it under the HUD bar, and pushed down the
                // sticker would sit on the car's own name: it goes under the car instead.
                var popTop = BelowBar(top);
                var half = new Vector2(pop.Sticker.layout.width, pop.Sticker.layout.height) * 0.5f;
                if (point.y - half.y - Margin < popTop || Covers(scoreboard, new Rect(point - half, half * 2f))) {
                    point.y = car.y + HotDrop - rise;
                }

                Clamp(ref point, pop.Sticker, popTop, left, right);
                pop.Root.style.left = point.x;
                pop.Root.style.top = point.y;
            }
        }

        private void PlaceMeltdown(float top, float time)
        {
            var age = time - meltdownStart;
            var isShown = age < MeltdownTime;
            meltdown.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown) {
                return;
            }

            meltdown.style.top = top + MeltdownTop;
            var scale = age < 0.25f
                ? 0.4f + 0.75f * (age / 0.25f) - 0.15f * Mathf.Pow(age / 0.25f, 2f)
                : age > MeltdownTime - 0.25f
                    ? Mathf.Max(0f, (MeltdownTime - age) / 0.25f)
                    : 1f + 0.05f * Mathf.Sin(age * 14f);
            meltdownSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        private void OnRedHot(int index)
        {
            var pop = pops[nextPop];
            nextPop = (nextPop + 1) % pops.Length;
            pop.Index = index;
            pop.Start = Time.unscaledTime;
            // Over the names in the layer: they are built after the pops, and a new handle would cover the sticker.
            pop.Root.BringToFront();
        }

        private void OnMeltdown(int index)
        {
            meltdownStart = Time.unscaledTime;
        }

        private Vector2 Project(Camera camera, Vector3 world)
        {
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, world, camera);
            return layer.WorldToLocal(panelPoint);
        }

        /// <summary>Where the space under the HUD's top bar starts, in the layer, or <paramref name="top"/> without one.</summary>
        private float BelowBar(float top)
        {
            if (bar == null || bar.panel == null || bar.resolvedStyle.display == DisplayStyle.None) {
                return top;
            }

            return Mathf.Max(top, layer.WorldToLocal(new Vector2(0f, bar.worldBound.yMax)).y);
        }

        /// <summary>Whether a shown <paramref name="element"/> lies over <paramref name="rect"/> of the layer.</summary>
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

        private void Show(Chip chip, float heat)
        {
            var percent = Mathf.RoundToInt(heat * 100f);
            if (percent != chip.Shown) {
                chip.Shown = percent;
                StrokeText.SetText(chip.Labels, $"{percent}%");
            }

            var step = heat >= mode.Settings.Hot ? 2 : heat >= mode.Settings.Warm ? 1 : 0;
            if (step != chip.Step) {
                chip.Step = step;
                chip.Root.EnableInClassList(ColdClass, step == 0);
                chip.Root.EnableInClassList(WarmClass, step == 1);
                chip.Root.EnableInClassList(HotClass, step == 2);
                if (step < 2) {
                    chip.Root.style.scale = StyleKeyword.Null;
                }
            }
        }

        private static void Beat(Chip chip, float time)
        {
            if (chip.Step < 2) {
                return;
            }

            var beat = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(time * BeatRate));
            chip.Root.style.scale = new Scale(new Vector3(beat, beat, 1f));
        }

        private static Chip CreateChip(string className, string flameClass, string textClass)
        {
            var chip = new Chip { Root = new VisualElement { pickingMode = PickingMode.Ignore } };
            chip.Root.AddToClassList(className);
            chip.Root.AddToClassList(ColdClass);
            var flame = new VisualElement { pickingMode = PickingMode.Ignore };
            flame.AddToClassList(flameClass);
            flame.generateVisualContent += DrawFlame;
            chip.Root.Add(flame);
            chip.Root.Add(StrokeText.Create("0%", null, out chip.Labels, "display", textClass));
            return chip;
        }

        /// <summary>A toy flame in the element's colour with a navy outline: a round belly, a pointed tip, a lick aside.</summary>
        private static void DrawFlame(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var box = element.contentRect;
            if (box.width < 1f || box.height < 1f) {
                return;
            }

            var painter = context.painter2D;
            var w = box.width;
            var h = box.height;
            var line = Mathf.Max(2f, h * 0.1f);
            var inset = line * 0.5f;
            Vector2 P(float x, float y) => new(box.x + inset + x * (w - line), box.y + inset + y * (h - line));
            painter.BeginPath();
            painter.MoveTo(P(0.5f, 0f));
            painter.BezierCurveTo(P(0.62f, 0.2f), P(0.95f, 0.38f), P(0.95f, 0.66f));
            painter.BezierCurveTo(P(0.95f, 0.88f), P(0.75f, 1f), P(0.5f, 1f));
            painter.BezierCurveTo(P(0.25f, 1f), P(0.05f, 0.88f), P(0.05f, 0.66f));
            painter.BezierCurveTo(P(0.05f, 0.48f), P(0.18f, 0.36f), P(0.28f, 0.3f));
            painter.BezierCurveTo(P(0.28f, 0.42f), P(0.34f, 0.5f), P(0.4f, 0.52f));
            painter.BezierCurveTo(P(0.36f, 0.32f), P(0.42f, 0.14f), P(0.5f, 0f));
            painter.ClosePath();
            painter.fillColor = element.resolvedStyle.color;
            painter.Fill();
            painter.strokeColor = Navy;
            painter.lineWidth = line;
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();

            // A pale heart low in the belly, so the flame reads at a glance.
            painter.BeginPath();
            painter.MoveTo(P(0.5f, 0.45f));
            painter.BezierCurveTo(P(0.62f, 0.6f), P(0.72f, 0.7f), P(0.7f, 0.8f));
            painter.BezierCurveTo(P(0.68f, 0.9f), P(0.6f, 0.94f), P(0.5f, 0.94f));
            painter.BezierCurveTo(P(0.4f, 0.94f), P(0.32f, 0.9f), P(0.3f, 0.8f));
            painter.BezierCurveTo(P(0.28f, 0.7f), P(0.38f, 0.6f), P(0.5f, 0.45f));
            painter.ClosePath();
            painter.fillColor = new Color(1f, 0.86f, 0.3f);
            painter.Fill();
        }
    }
}
