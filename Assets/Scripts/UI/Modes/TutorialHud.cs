using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The tutorial on the HUD (<see cref="TutorialMode"/>): a gold arrow bobs over where the step is (the bot to push,
    /// the box, the last two bots) and a white sticker right over the arrow says what to do there, with the step's
    /// number on a gold disc (1 to 4); it pops whenever the step changes, and only the arrow bobs, never the words.
    /// The words stand at the place in the world, not at an edge
    /// of the screen: a new player looks at the arena, not at the HUD; an item to use is pointed at its button. The praise between steps and at
    /// the end, the sticker stands over the player's own car without an arrow; the praise and the last words are green.
    /// Both are kept on the screen, and the sticker goes under the place when the top of the screen has no room for it.
    /// The round's clock and the kill feed are hidden: the tutorial ends with its lesson, not on time.
    /// <para>
    /// The arrow is not a ring on the floor: a white ring is the player's own marker. Built once; a frame only sets
    /// styles, on real time, and nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class TutorialHud : IModeHud
    {
        private const int StepCount = 4;

        // Metres over the step's place the arrow's tip floats, by what it points at.
        // High enough that the arrow (it bobs only upwards) never covers what it points at.
        private const float CarLift = 3.2f;
        private const float BoxLift = 2.6f;

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
        // How far under a target's foot the tip of an arrow pointing up from below stands.
        private const float FlipGap = 40f;
        private const float BarRoom = 150f;
        private const float SideMargin = 16f;

        // The sticker's pop on a new step, and how long the last words stay.
        private const float PopScale = 1.18f;
        private const float PopSeconds = 0.35f;
        private const float DoneSeconds = 2.5f;

        private const string ShownClass = "hud__tutorial--shown";
        private const string DoneClass = "hud__tutorial--done";
        // Bigger at the item button: on the v0.19.0 playtest a phone player drove 76 s with the item and never found it.
        private const string AtButtonClass = "hud__tutorial--button";
        private const string CheerShownClass = "hud__tutorial-cheer--shown";
        private const string ArrowShownClass = "hud__tutorial-arrow--shown";

        private readonly HudViewModel model;
        private readonly TutorialMode tutorial;
        private readonly VisualElement layer;
        private readonly VisualElement banner;
        private readonly VisualElement sticker;
        private readonly Label[] textLabels;
        private readonly VisualElement hint;
        private readonly Label[] hintLabels;
        private readonly Func<bool> isTouch;
        private readonly Label[] stepLabels;
        private readonly VisualElement arrow;
        private readonly VisualElement bar;
        private readonly VisualElement itemButton;
        private readonly VisualElement feed;
        private readonly Action onLanguage;
        private readonly VisualElement cheer;
        private readonly Label[] cheerLabels;
        private readonly ConfettiBurst confetti;
        private readonly CancellationTokenSource lifetime = new();
        private TutorialStep shownStep = (TutorialStep)(-1);
        private TutorialPraise shownPraise;
        private TutorialPraise shownCheer = (TutorialPraise)(-1);
        private bool shownTouch;
        private float popAt = float.NegativeInfinity;
        private float doneAt = float.PositiveInfinity;

        /// <summary>The tutorial's HUD, in the tutorial round only.</summary>
        public static TutorialHud Create(VisualElement layer, VisualElement root, HudViewModel model, Func<bool> isTouch)
        {
            return model.Mode is TutorialMode { IsActive: true } tutorial && layer != null && root != null
                ? new TutorialHud(layer, root, model, tutorial, isTouch)
                : null;
        }

        private TutorialHud(VisualElement layer, VisualElement root, HudViewModel model, TutorialMode tutorial,
            Func<bool> isTouch)
        {
            this.model = model;
            this.tutorial = tutorial;
            this.layer = layer;
            this.isTouch = isTouch ?? (() => false);

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
            // The words, and under them, at the item button on a computer, which press uses it. How to drive is said
            // once, by the start hint in the middle of the screen.
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.AddToClassList("hud__tutorial-words");
            words.Add(StrokeText.Create("", null, out textLabels, "hud__tutorial-text"));
            hint = StrokeText.Create("", null, out hintLabels, "hud__tutorial-hint");
            hint.style.display = DisplayStyle.None;
            words.Add(hint);
            sticker.Add(words);
            banner.Add(sticker);
            // Off the screen until its first frame has a place: it measures itself there first. Moved by translate, not
            // left: an absolute element left near the right edge shrinks to the room left of it and wraps its words.
            banner.style.translate = new Translate(-10000f, 0f);
            layer.Add(banner);

            // The praise and the last words: big tilted gold words in the middle of the screen with confetti, as YOU
            // WIN! on the result, not a bubble over the car.
            cheer = StrokeText.Outlined("", "hud__tutorial-cheer", out cheerLabels);
            cheer.pickingMode = PickingMode.Ignore;
            layer.Add(cheer);
            confetti = new ConfettiBurst(layer);

            // No bar at all: the clock would count five minutes down for a lesson with no time limit, and the cars'
            // chips only crowd a screen whose one job is the next step.
            bar = root.Q<VisualElement>("hud-bar");
            itemButton = root.Q<VisualElement>("item-button");
            feed = root.Q<VisualElement>("feed");
            SetDisplay(bar, false);
            SetDisplay(feed, false);

            onLanguage = () => shownStep = (TutorialStep)(-1);
            shownTouch = this.isTouch();
            Strings.Changed += onLanguage;
        }

        /// <summary>The tutorial points at the item button itself and says how to press it: the shared hint stays off.</summary>
        public bool OwnsItemHint => true;

        public void Tick(Camera camera, float top, float left, float right)
        {
            var now = Time.unscaledTime;
            var step = tutorial.Step;
            var praise = tutorial.Praise;
            var touch = isTouch();
            if (step != shownStep || praise != shownPraise || touch != shownTouch) {
                if (shownStep >= TutorialStep.Push && (step != shownStep || praise != shownPraise)) {
                    popAt = now;
                }

                shownStep = step;
                shownPraise = praise;
                shownTouch = touch;
                Show(step, praise, touch);
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

            var isCheer = step == TutorialStep.Done || praise != TutorialPraise.None;
            cheer.EnableInClassList(CheerShownClass, isShown && isCheer);
            if (isCheer) {
                arrow.EnableInClassList(ArrowShownClass, false);
                banner.EnableInClassList(ShownClass, false);
                return;
            }

            var hasPlace = Place(camera, now, top, left, right);
            banner.EnableInClassList(ShownClass, isShown && hasPlace);
        }

        public void Dispose()
        {
            Strings.Changed -= onLanguage;
            lifetime.Cancel();
            lifetime.Dispose();
            cheer.RemoveFromHierarchy();
            SetDisplay(bar, true);
            SetDisplay(feed, true);
            banner.RemoveFromHierarchy();
            arrow.RemoveFromHierarchy();
        }

        private void Show(TutorialStep step, TutorialPraise praise, bool touch)
        {
            var isCheer = step == TutorialStep.Done || praise != TutorialPraise.None;
            banner.EnableInClassList(DoneClass, isCheer);
            var key = praise switch {
                TutorialPraise.Pushed => "tutorial.nice_push",
                TutorialPraise.Grown => "tutorial.huge",
                _ => step switch {
                    TutorialStep.Box => "tutorial.box",
                    TutorialStep.Use => "tutorial.use",
                    TutorialStep.Smash => "tutorial.smash",
                    TutorialStep.Done => "tutorial.done",
                    _ => "tutorial.push",
                },
            };
            StrokeText.SetText(textLabels, Strings.Get(key));
            // Again on a new language: Place sets it only when the hint shows or hides.
            StrokeText.SetText(hintLabels, Strings.Get("tutorial.use_keys"));
            if (isCheer) {
                var isNew = !cheer.ClassListContains(CheerShownClass) || praise != shownCheer;
                StrokeText.SetText(cheerLabels, Strings.Get(key));
                if (isNew && !lifetime.IsCancellationRequested) {
                    confetti.Play(cheer, lifetime.Token).SuppressCancellationThrow().Forget();
                }
            }

            shownCheer = isCheer ? praise : (TutorialPraise)(-1);

            var number = Mathf.Clamp((int)step, 1, StepCount);
            StrokeText.SetText(stepLabels, isCheer ? "!" : number.ToString());
        }

        /// <summary>
        /// The arrow over the step's place and the sticker right over the arrow; on the player's own car the sticker
        /// alone. False when the step has no place on the screen now.
        /// </summary>
        private bool Place(Camera camera, float now, float top, float left, float right)
        {
            // An item to use: the arrow points at the item button, where the press is, not at the car.
            var isAtButton = TryGetButton(out var point);
            banner.EnableInClassList(AtButtonClass, isAtButton);
            // At the button on a computer the sticker also says which press uses the item (the shared hint is off).
            var isKeyed = isAtButton && !isTouch();
            if (isKeyed != (hint.resolvedStyle.display == DisplayStyle.Flex)) {
                hint.style.display = isKeyed ? DisplayStyle.Flex : DisplayStyle.None;
                StrokeText.SetText(hintLabels, Strings.Get("tutorial.use_keys"));
            }
            var place = Vector3.zero;
            var isOwnCar = false;
            var lift = 0f;
            var hasPlace = isAtButton || TryGetPlace(out place, out isOwnCar, out lift) && camera != null
                && layer.panel != null;
            var isPointing = hasPlace && !isOwnCar && !model.IsFinished;
            arrow.EnableInClassList(ArrowShownClass, isPointing);
            if (!hasPlace) {
                return false;
            }

            if (!isAtButton) {
                var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, place, camera);
                point = layer.WorldToLocal(panelPoint);
            }

            var size = layer.layout.size;
            var minY = top + BarRoom;
            var bottom = point.y;
            // A place too near the top for the arrow over it (the bar above it, a wide screen): the arrow points up at
            // it from below, and the sticker stands under the arrow.
            var isFlipped = isPointing && point.y - ArrowHeight - BobHeight < minY;
            if (isFlipped && !isAtButton) {
                // From below the arrow points at the target's own foot, a little under it: from the lifted point over
                // the target it would cover the target.
                var foot = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, place - Vector3.up * lift, camera);
                point.y = layer.WorldToLocal(foot).y + FlipGap;
            }
            var below = point.y;
            if (isPointing) {
                var minX = left + ArrowMargin;
                var tipX = Mathf.Clamp(point.x, minX, Mathf.Max(minX, size.x - right - ArrowMargin));
                // Only the arrow bobs, away from the place; the sticker stands still over the arrow's highest point,
                // so its words are easy to read.
                var restY = Mathf.Clamp(point.y, isFlipped ? top : minY, Mathf.Max(minY, size.y - ArrowMargin));
                var bob = BobHeight * (0.5f + 0.5f * Mathf.Sin(now * BobRate * Mathf.PI * 2f));
                arrow.style.left = tipX;
                arrow.style.top = isFlipped ? restY + bob : restY - bob;
                arrow.style.rotate = new Rotate(isFlipped ? 180f : 0f);
                bottom = restY - BobHeight - ArrowHeight;
                below = restY + BobHeight + ArrowHeight;
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

            banner.style.translate = new Translate(x - half, y);
            return true;
        }

        /// <summary>
        /// Where the step is, in the world: the arrow's tip floats over it, or, on the player's own car
        /// (<paramref name="isOwnCar"/>), the sticker.
        /// </summary>
        private bool TryGetPlace(out Vector3 place, out bool isOwnCar, out float lift)
        {
            place = default;
            isOwnCar = false;
            lift = tutorial.Step == TutorialStep.Box || tutorial.Step == TutorialStep.Use
                || tutorial.Step == TutorialStep.Smash && model.PlayerCar != null
                && !model.PlayerCar.IsRunning(ItemKind.Mega) ? BoxLift : CarLift;
            var player = model.PlayerCar;
            if (tutorial.Praise != TutorialPraise.None) {
                return OverPlayer(player, out place, out isOwnCar);
            }

            switch (tutorial.Step) {
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

        /// <summary>The top middle of the item button, in the layer, while the step wants its item used.</summary>
        private bool TryGetButton(out Vector2 point)
        {
            point = default;
            var player = model.PlayerCar;
            var wantsUse = tutorial.Praise == TutorialPraise.None && player != null && !player.IsOut && player.HasItem
                && (tutorial.Step == TutorialStep.Use
                    || tutorial.Step == TutorialStep.Smash && !player.IsRunning(ItemKind.Mega));
            if (!wantsUse || itemButton == null || itemButton.panel == null
                || itemButton.resolvedStyle.display == DisplayStyle.None) {
                return false;
            }

            var bounds = itemButton.worldBound;
            if (bounds.width <= 0f || float.IsNaN(bounds.width)) {
                return false;
            }

            point = layer.WorldToLocal(new Vector2(bounds.center.x, bounds.yMin));
            return true;
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
