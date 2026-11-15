using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Modes;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The tutorial on the HUD (<see cref="TutorialMode"/>): under the bar a white sticker says what to do now, with the
    /// step's number on a gold disc (1 to 4), and pops whenever the step changes; a gold arrow bobs over where the step
    /// is (the point to drive to, the bot to push, the box, the last two bots), kept on the screen. The round's clock is
    /// hidden: the tutorial ends with its lesson, not on time. When the last bot is off the sticker turns green with the
    /// last words.
    /// <para>
    /// The arrow is not a ring on the floor: a white ring is the player's own marker. Built once; a frame only sets
    /// styles, on real time, and nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class TutorialHud : IModeHud
    {
        private const int StepCount = 4;

        // Where the sticker sits, clear of the arena's far edge, where the last two bots wait: in portrait right under
        // the bar (the kill feed there is hidden: the lesson needs none), in landscape at the bottom middle, between the
        // item button and the corner it is not in.
        private const float PortraitTop = 205f;
        private const float LandscapeBottom = 60f;

        // Metres over the step's place the arrow's tip floats, by what it points at.
        private const float PointLift = 0.6f;
        private const float CarLift = 2.2f;
        private const float BoxLift = 1.7f;

        // The arrow's bob: reference pixels up and down, cycles a second.
        private const float BobHeight = 22f;
        private const float BobRate = 2.4f;
        private const float ArrowMargin = 70f;

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
            root.Add(banner);

            arrow = new VisualElement { name = "tutorial-arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("hud__tutorial-arrow");
            arrow.generateVisualContent += PaintArrow;
            layer.Add(arrow);

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

            var size = banner.parent?.layout.size ?? Vector2.zero;
            if (size.x > size.y) {
                banner.style.top = StyleKeyword.Auto;
                banner.style.bottom = LandscapeBottom;
            }
            else {
                banner.style.top = top + PortraitTop;
                banner.style.bottom = StyleKeyword.Auto;
            }
            var isShown = step != TutorialStep.None && !model.IsFinished || step == TutorialStep.Done
                && now - doneAt < DoneSeconds;
            if (model.PlayerCar != null && model.PlayerCar.IsOut) {
                isShown = false;
            }

            banner.EnableInClassList(ShownClass, isShown);
            var pop = Mathf.Clamp01((now - popAt) / PopSeconds);
            var scale = 1f + (PopScale - 1f) * Mathf.Sin(pop * Mathf.PI) * (1f - pop * 0.5f);
            sticker.style.scale = new Scale(new Vector3(scale, scale, 1f));

            TickArrow(camera, now, top, left, right);
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
                TutorialStep.Done => "tutorial.done",
                _ => "tutorial.drive",
            };
            StrokeText.SetText(textLabels, Strings.Get(key));
            var number = Mathf.Clamp((int)step, 1, StepCount);
            StrokeText.SetText(stepLabels, isDone ? "!" : number.ToString());
        }

        private void TickArrow(Camera camera, float now, float top, float left, float right)
        {
            var isPointing = TryGetPlace(out var place) && camera != null && arrow.panel != null && !model.IsFinished;
            arrow.EnableInClassList(ArrowShownClass, isPointing);
            if (!isPointing) {
                return;
            }

            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(arrow.panel, place, camera);
            var point = layer.WorldToLocal(panelPoint);
            point.y -= BobHeight * (0.5f + 0.5f * Mathf.Sin(now * BobRate * Mathf.PI * 2f));
            var size = layer.layout.size;
            var minX = left + ArrowMargin;
            point.x = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - right - ArrowMargin));
            // Under the bar and, in portrait, the sticker under it.
            var minY = top + ArrowMargin + (size.x > size.y ? 230f : PortraitTop + 130f);
            point.y = Mathf.Clamp(point.y, minY, Mathf.Max(minY, size.y - ArrowMargin));
            arrow.style.left = point.x;
            arrow.style.top = point.y;
        }

        /// <summary>Where the step is, in the world, for the arrow: the tip floats over it.</summary>
        private bool TryGetPlace(out Vector3 place)
        {
            place = default;
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
                    var player = model.PlayerCar;
                    if (player != null && !player.HasItem) {
                        return TryGetBox(out place);
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
                default:
                    return false;
            }
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
