using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The tutorial on the HUD (<see cref="TutorialMode"/>): a gold arrow bobs over where the step is (the point to
    /// drive to, the bot to push, the box, the last two bots) and a white sticker right over the arrow says what to do
    /// there, with the step's number on a gold disc (1 to 5); it pops whenever the step changes. The words stand at the
    /// place in the world, not at an edge of the screen: a new player looks at the arena, not at the HUD. Using the item
    /// and at the end, the sticker stands over the player's own car without an arrow. Both are kept on the screen, and
    /// the sticker goes under the place when the top of the screen has no room for it. The round's clock and the kill
    /// feed are hidden: the tutorial ends with its lesson, not on time. When the last bot is off the sticker turns green
    /// with the last words.
    /// <para>
    /// The arrow is not a ring on the floor: a white ring is the player's own marker. Built once; a frame only sets
    /// styles, on real time, and nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class TutorialHud : IModeHud
    {
        private const int StepCount = 5;

        // Metres over the step's place the arrow's tip floats, by what it points at.
        private const float PointLift = 0.6f;
        private const float CarLift = 2.2f;
        private const float BoxLift = 1.7f;

        // Metres over the player's own car the sticker's bottom stands.
        private const float OwnCarLift = 3.4f;

        // The arrow's bob: reference pixels up and down, cycles a second.
        private const float BobHeight = 22f;
        private const float BobRate = 2.4f;
        private const float ArrowMargin = 70f;

        // Reference pixels: the arrow's height (as in the style sheet), the gap from the arrow to the sticker, how far
        // under the place a sticker with no room above stands, the room the bar takes at the top and the sides' margin.
        private const float ArrowHeight = 112f;
        private const float StickerGap = 6f;
        private const float BelowGap = 110f;
        private const float BarRoom = 150f;
        private const float SideMargin = 16f;

        // The sticker's pop on a new step, and how long the last words stay.
        private const float PopScale = 1.18f;
        private const float PopSeconds = 0.35f;
        private const float DoneSeconds = 2.5f;

        private const string ShownClass = "hud__tutorial--shown";
        private const string DoneClass = "hud__tutorial--done";
        private const string ArrowShownClass = "hud__tutorial-arrow--shown";

        private readonly HudViewModel model;
        private readonly TutorialMode tutorial;
        private readonly VisualElement layer;
        private readonly VisualElement banner;
        private readonly VisualElement sticker;
        private readonly Label[] textLabels;
        private readonly Label[] stepLabels;
        private readonly VisualElement arrow;
        private readonly VisualElement timePill;
        private readonly VisualElement carsSep;
        private readonly VisualElement feed;
        private readonly Action onLanguage;
        private TutorialStep shownStep = (TutorialStep)(-1);
        private float popAt = float.NegativeInfinity;
        private float doneAt = float.PositiveInfinity;

        /// <summary>The tutorial's HUD, in the tutorial round only.</summary>
        public static TutorialHud Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.Mode is TutorialMode { IsActive: true } tutorial && layer != null && root != null
                ? new TutorialHud(layer, root, model, tutorial)
                : null;
        }

        private TutorialHud(VisualElement layer, VisualElement root, HudViewModel model, TutorialMode tutorial)
        {
            this.model = model;
            this.tutorial = tutorial;
            this.layer = layer;

            arrow = new VisualElement { name = "tutorial-arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("hud__tutorial-arrow");
            arrow.generateVisualContent += PaintArrow;
            // Turned round its tip when it points up from under a place by the top.
            arrow.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f));
            layer.Add(arrow);

            banner = new VisualElement { name = "tutorial", pickingMode = PickingMode.Ignore };
            banner.AddToClassList("hud__tutorial");
            sticker = new VisualElement { pickingMode = PickingMode.Ignore };
            sticker.AddToClassList("hud__tutorial-sticker");
            var disc = new VisualElement { pickingMode = PickingMode.Ignore };
            disc.AddToClassList("hud__tutorial-step");
            disc.Add(StrokeText.Create("1", null, out stepLabels, "display", "hud__tutorial-step-text"));
            sticker.Add(disc);
            sticker.Add(StrokeText.Create("", null, out textLabels, "hud__tutorial-text"));
            banner.Add(sticker);
            // Off the screen until its first frame has a place: it measures itself there first.
            banner.style.left = -10000f;
            layer.Add(banner);

            // The clock would count five minutes down: the lesson has no time limit, so the bar shows only the cars.
            timePill = root.Q<VisualElement>("time-pill");
            carsSep = root.Q<VisualElement>("cars-pill")?.Q<VisualElement>(className: "hud__sep");
            feed = root.Q<VisualElement>("feed");
            SetDisplay(timePill, false);
            SetDisplay(carsSep, false);
            SetDisplay(feed, false);

            onLanguage = () => shownStep = (TutorialStep)(-1);
            Strings.Changed += onLanguage;
        }

        public void Tick(Camera camera, float top, float left, float right)
        {
            var now = Time.unscaledTime;
            var step = tutorial.Step;
            if (step != shownStep) {
                if (shownStep >= TutorialStep.Drive) {
                    popAt = now;
                }

                shownStep = step;
                Show(step);
                if (step == TutorialStep.Done) {
                    doneAt = now;
                }
            }

            var isShown = step != TutorialStep.None && !model.IsFinished || step == TutorialStep.Done
                && now - doneAt < DoneSeconds;
            if (model.PlayerCar != null && model.PlayerCar.IsOut) {
                isShown = false;
            }

            var pop = Mathf.Clamp01((now - popAt) / PopSeconds);
            var scale = 1f + (PopScale - 1f) * Mathf.Sin(pop * Mathf.PI) * (1f - pop * 0.5f);
            sticker.style.scale = new Scale(new Vector3(scale, scale, 1f));

            var hasPlace = Place(camera, now, top, left, right);
            banner.EnableInClassList(ShownClass, isShown && hasPlace);
        }

        public void Dispose()
        {
            Strings.Changed -= onLanguage;
            SetDisplay(timePill, true);
            SetDisplay(carsSep, true);
            SetDisplay(feed, true);
            banner.RemoveFromHierarchy();
            arrow.RemoveFromHierarchy();
        }

        private void Show(TutorialStep step)
        {
            var isDone = step == TutorialStep.Done;
            banner.EnableInClassList(DoneClass, isDone);
            var key = step switch {
                TutorialStep.Push => "tutorial.push",
                TutorialStep.Box => "tutorial.box",
                TutorialStep.Use => "tutorial.use",
                TutorialStep.Smash => "tutorial.smash",
                TutorialStep.Done => "tutorial.done",
                _ => "tutorial.drive",
            };
            StrokeText.SetText(textLabels, Strings.Get(key));
            var number = Mathf.Clamp((int)step, 1, StepCount);
            StrokeText.SetText(stepLabels, isDone ? "!" : number.ToString());
        }

        /// <summary>
        /// The arrow over the step's place and the sticker right over the arrow; on the player's own car the sticker
        /// alone. False when the step has no place on the screen now.
        /// </summary>
        private bool Place(Camera camera, float now, float top, float left, float right)
        {
            var hasPlace = TryGetPlace(out var place, out var isOwnCar) && camera != null && layer.panel != null;
            var isPointing = hasPlace && !isOwnCar && !model.IsFinished;
            arrow.EnableInClassList(ArrowShownClass, isPointing);
            if (!hasPlace) {
                return false;
            }

            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, place, camera);
            var point = layer.WorldToLocal(panelPoint);
            var size = layer.layout.size;
            var minY = top + BarRoom;
            var bottom = point.y;
            // A place too near the top for the arrow over it (the bar above it, a wide screen): the arrow points up at
            // it from below, and the sticker stands under the arrow.
            var isFlipped = isPointing && point.y - ArrowHeight - BobHeight < minY;
            var below = point.y;
            if (isPointing) {
                var minX = left + ArrowMargin;
                var tipX = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - right - ArrowMargin));
                var bob = BobHeight * (0.5f + 0.5f * Mathf.Sin(now * BobRate * Mathf.PI * 2f));
                var tipY = isFlipped ? point.y + bob : point.y - bob;
                tipY = Mathf.Clamp(tipY, isFlipped ? top : minY, Mathf.Max(minY, size.y - ArrowMargin));
                arrow.style.left = tipX;
                arrow.style.top = tipY;
                arrow.style.rotate = new Rotate(isFlipped ? 180f : 0f);
                bottom = tipY - ArrowHeight;
                below = tipY + ArrowHeight;
            }

            // The sticker's bottom middle over the arrow or the car, its sides on the screen; with no room above it
            // stands under the place instead.
            var bubble = banner.layout.size;
            var half = bubble.x * 0.5f;
            var lowX = left + half + SideMargin;
            var x = Mathf.Clamp(point.x, lowX, Mathf.Max(lowX, size.x - right - half - SideMargin));
            var y = bottom - StickerGap - bubble.y;
            if (isFlipped) {
                y = Mathf.Min(below + StickerGap, size.y - bubble.y - SideMargin);
            }
            else if (y < minY) {
                y = Mathf.Min(point.y + BelowGap, size.y - bubble.y - SideMargin);
            }

            banner.style.left = x - half;
            banner.style.top = y;
            return true;
        }

        /// <summary>
        /// Where the step is, in the world: the arrow's tip floats over it, or, on the player's own car
        /// (<paramref name="isOwnCar"/>), the sticker.
        /// </summary>
        private bool TryGetPlace(out Vector3 place, out bool isOwnCar)
        {
            place = default;
            isOwnCar = false;
            var player = model.PlayerCar;
            switch (tutorial.Step) {
                case TutorialStep.Drive:
                    place = tutorial.DrivePoint + Vector3.up * PointLift;
                    return true;
                case TutorialStep.Push:
                    if (tutorial.Target == null || tutorial.Target.IsOut) {
                        return false;
                    }

                    place = tutorial.Target.Position + Vector3.up * CarLift;
                    return true;
                case TutorialStep.Box:
                    return TryGetBox(out place);
                case TutorialStep.Use:
                    return player != null && !player.HasItem ? TryGetBox(out place) : OverPlayer(player, out place, out isOwnCar);
                case TutorialStep.Smash:
                    // Grown, at the last two; a mega lost to a fall means the box again (or the item to use).
                    if (player != null && !player.IsRunning(ItemKind.Mega)) {
                        return player.HasItem ? OverPlayer(player, out place, out isOwnCar) : TryGetBox(out place);
                    }

                    var middle = Vector3.zero;
                    var count = 0;
                    foreach (var car in tutorial.Pair) {
                        if (!car.IsOut) {
                            middle += car.Position;
                            count++;
                        }
                    }

                    if (count == 0) {
                        return false;
                    }

                    place = middle / count + Vector3.up * CarLift;
                    return true;
                case TutorialStep.Done:
                    return OverPlayer(player, out place, out isOwnCar);
                default:
                    return false;
            }
        }

        private static bool OverPlayer(Car player, out Vector3 place, out bool isOwnCar)
        {
            isOwnCar = true;
            place = player != null ? player.Position + Vector3.up * OwnCarLift : default;
            return player != null && !player.IsOut;
        }

        private bool TryGetBox(out Vector3 place)
        {
            var box = tutorial.Box;
            place = box != null && box.IsLive ? box.Position + Vector3.up * BoxLift : default;
            return box != null && box.IsLive;
        }

        /// <summary>A gold arrow pointing down with a navy outline, its tip at the element's bottom middle.</summary>
        private static void PaintArrow(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var w = rect.width;
            var h = rect.height;
            var painter = context.painter2D;
            var points = new[] {
                new Vector2(w * 0.5f, h), new Vector2(w, h * 0.46f), new Vector2(w * 0.7f, h * 0.46f),
                new Vector2(w * 0.7f, 0f), new Vector2(w * 0.3f, 0f), new Vector2(w * 0.3f, h * 0.46f),
                new Vector2(0f, h * 0.46f),
            };
            painter.lineJoin = LineJoin.Round;
            painter.lineWidth = w * 0.12f;
            painter.strokeColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);
            painter.fillColor = new Color32(0xff, 0xc9, 0x3c, 0xff);
            painter.BeginPath();
            painter.MoveTo(points[0]);
            for (var i = 1; i < points.Length; i++) {
                painter.LineTo(points[i]);
            }

            painter.ClosePath();
            painter.Stroke();
            painter.Fill();
        }

        private static void SetDisplay(VisualElement element, bool isShown)
        {
            if (element != null) {
                element.style.display = isShown ? new StyleEnum<DisplayStyle>(StyleKeyword.Null) : DisplayStyle.None;
            }
        }
    }
}
