using System;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Modes;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The FOOTBALL mode on the HUD, on top of the shared team score (<see cref="HudTeams"/>): a tilted GOAL! in the
    /// scoring team's colour with the scorer's nick (OWN GOAL! for a ball the other team put in), with confetti when the
    /// player's team scores; a big red arrow from the player's car to the red goal at each kick-off, so the way to play
    /// needs no reading; and, while the ball is off the screen, a ball at the screen's edge pointing to it.
    /// <para>
    /// Made only in the mode (<see cref="Create"/> gives null otherwise). Built once; a frame only sets styles, on real
    /// time, and nothing here catches input.
    /// </para>
    /// </summary>
    internal sealed class HudFootball : IModeHud
    {
        // Reference pixels under the top of the safe area the GOAL! banner's middle sits at: under the bar and the
        // team score.
        private const float GoalTop = 520f;
        private const float PopTime = 0.28f;
        private const float HoldTime = 1.25f;
        private const float LeaveTime = 0.3f;
        private const float LeaveRise = 260f;

        // The arrow to the red goal: reference pixels from the player's car to the arrow's middle, its bob along the way.
        private const float ArrowReach = 330f;
        private const float ArrowBob = 26f;
        private const float ArrowRate = 6f;
        private const float ArrowWidth = 190f;
        private const float ArrowHeight = 150f;

        // The ball's pointer: its size and how far in from the screen's edges it keeps.
        private const float PointerSize = 96f;
        private const float PointerMargin = 34f;
        private const float BottomMargin = 340f;

        private static readonly Color Navy = new(0.114f, 0.169f, 0.31f);
        private static readonly Color Red = new(0.902f, 0.373f, 0.282f);

        private readonly HudViewModel model;
        private readonly FootballMode mode;
        private readonly VisualElement layer;
        private readonly VisualElement root;
        private readonly VisualElement goal;
        private readonly VisualElement goalSticker;
        private readonly Label[] goalLabels;
        private readonly VisualElement scorer;
        private readonly Label[] scorerLabels;
        private readonly VisualElement arrow;
        private readonly VisualElement pointer;
        private readonly VisualElement pointerTip;
        private readonly ConfettiBurst confetti;
        private readonly CancellationTokenSource lifetime = new();
        private readonly Action onLanguage;
        private float goalTime = float.NegativeInfinity;
        private bool isOwnGoal;

        /// <summary>The mode's HUD, made by its look (<see cref="FootballLook"/>) in a round of football only.</summary>
        public static HudFootball Create(VisualElement layer, VisualElement root, HudViewModel model)
        {
            return model.Mode is FootballMode { IsActive: true } football && layer != null
                ? new HudFootball(layer, root, model, football)
                : null;
        }

        private HudFootball(VisualElement layer, VisualElement root, HudViewModel model, FootballMode mode)
        {
            this.model = model;
            this.mode = mode;
            this.layer = layer;
            this.root = root;

            arrow = new VisualElement { name = "attack-arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("hud__attack");
            arrow.style.width = ArrowWidth;
            arrow.style.height = ArrowHeight;
            arrow.generateVisualContent += DrawArrow;
            layer.Add(arrow);

            pointer = new VisualElement { name = "ball-pointer", pickingMode = PickingMode.Ignore };
            pointer.AddToClassList("hud__ball-pointer");
            pointerTip = new VisualElement { pickingMode = PickingMode.Ignore };
            pointerTip.AddToClassList("hud__ball-pointer-tip");
            pointerTip.generateVisualContent += DrawTip;
            pointer.Add(pointerTip);
            var ball = new VisualElement { pickingMode = PickingMode.Ignore };
            ball.AddToClassList("hud__ball-pointer-ball");
            ball.generateVisualContent += DrawBall;
            pointer.Add(ball);
            layer.Add(pointer);

            goal = new VisualElement { name = "goal", pickingMode = PickingMode.Ignore };
            goal.AddToClassList("hud__goal");
            goalSticker = new VisualElement { pickingMode = PickingMode.Ignore };
            goalSticker.AddToClassList("hud__goal-sticker");
            var goalText = StrokeText.Create(Strings.Get("hud.football_goal"), null, out goalLabels, "display", "hud__goal-text");
            goalSticker.Add(goalText);
            goal.Add(goalSticker);
            scorer = new VisualElement { pickingMode = PickingMode.Ignore };
            scorer.AddToClassList("hud__goal-scorer");
            var scorerText = StrokeText.Create("", null, out scorerLabels, "display", "hud__goal-scorer-text");
            scorer.Add(scorerText);
            goal.Add(scorer);
            root.Add(goal);
            confetti = new ConfettiBurst(root);

            mode.Scored += OnScored;
            onLanguage = () => StrokeText.SetText(goalLabels, Strings.Get(isOwnGoal ? "hud.football_own_goal" : "hud.football_goal"));
            Strings.Changed += onLanguage;
        }

        public void Tick(Camera camera, float top, float left, float right)
        {
            var time = Time.unscaledTime;
            TickGoal(time, top);
            var size = layer.layout.size;
            if (camera == null || layer.panel == null || size.x <= 0f) {
                arrow.style.display = DisplayStyle.None;
                pointer.style.display = DisplayStyle.None;
                return;
            }

            TickArrow(camera, time);
            TickPointer(camera, size, top, left, right);
        }

        public void Dispose()
        {
            mode.Scored -= OnScored;
            Strings.Changed -= onLanguage;
            lifetime.Cancel();
            lifetime.Dispose();
            goal.RemoveFromHierarchy();
            arrow.RemoveFromHierarchy();
            pointer.RemoveFromHierarchy();
            root.Q("confetti")?.RemoveFromHierarchy();
        }

        private void OnScored(int team, Car author)
        {
            var player = model.PlayerCar;
            var ourTeam = player != null ? player.Team : RoundTeams.Blue;
            isOwnGoal = author == null && mode.Ball.LastTouch != null && mode.Ball.LastTouch.Team != team;
            StrokeText.SetText(goalLabels, Strings.Get(isOwnGoal ? "hud.football_own_goal" : "hud.football_goal"));
            var hasName = author != null;
            scorer.style.display = hasName ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasName) {
                StrokeText.SetText(scorerLabels, model.NameOf(mode.IndexOf(author)));
            }

            goal.EnableInClassList("hud__goal--blue", team == RoundTeams.Blue);
            goal.EnableInClassList("hud__goal--red", team == RoundTeams.Red);
            scorer.EnableInClassList("hud__goal-scorer--you", author != null && author.IsPlayer);
            goalTime = Time.unscaledTime;
            if (team == ourTeam) {
                confetti.Play(goalSticker, lifetime.Token).Forget();
            }
        }

        // Pops in with an overshoot and a tilt, holds, then rises away shrinking.
        private void TickGoal(float time, float top)
        {
            var age = time - goalTime;
            var isShown = age >= 0f && age < PopTime + HoldTime + LeaveTime;
            goal.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown) {
                return;
            }

            float scale;
            var rise = 0f;
            if (age < PopTime) {
                var t = age / PopTime;
                // Ease out back: past full size and back.
                const float back = 2.2f;
                var u = t - 1f;
                scale = 0.3f + 0.7f * (1f + (back + 1f) * u * u * u + back * u * u);
            }
            else if (age < PopTime + HoldTime) {
                scale = 1f + 0.04f * Mathf.Sin((age - PopTime) * 10f) * (1f - (age - PopTime) / HoldTime);
            }
            else {
                var t = (age - PopTime - HoldTime) / LeaveTime;
                scale = Mathf.Lerp(1f, 0.55f, t * t);
                rise = LeaveRise * t * t;
            }

            goal.style.top = top + GoalTop - rise;
            goalSticker.style.scale = new Scale(new Vector3(scale, scale, 1f));
            scorer.style.scale = new Scale(new Vector3(scale, scale, 1f));
        }

        // At each kick-off and for the first seconds of play, from the player's car towards the goal it attacks.
        private void TickArrow(Camera camera, float time)
        {
            var player = model.PlayerCar;
            var isShown = mode.IsKickingOff && player != null && !player.IsOut && !model.IsFinished && mode.Pitch != null;
            arrow.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isShown) {
                return;
            }

            var from = ToLayer(camera, player.Position);
            var to = ToLayer(camera, mode.TargetOf(player.Team));
            var way = to - from;
            if (way.sqrMagnitude < 1f) {
                way = Vector2.down;
            }

            way.Normalize();
            var reach = ArrowReach + ArrowBob * (0.5f + 0.5f * Mathf.Sin(time * ArrowRate));
            var middle = from + way * reach;
            arrow.style.left = middle.x - ArrowWidth * 0.5f;
            arrow.style.top = middle.y - ArrowHeight * 0.5f;
            arrow.style.rotate = new Rotate(Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg);
        }

        // The ball at the screen's edge, pointing at it, while it is off the screen.
        private void TickPointer(Camera camera, Vector2 size, float top, float left, float right)
        {
            var ball = mode.Pitch != null ? mode.BallPosition : Vector3.zero;
            var viewport = camera.WorldToViewportPoint(ball);
            var isOff = mode.Pitch != null && !model.IsFinished
                && (viewport.z < 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f);
            pointer.style.display = isOff ? DisplayStyle.Flex : DisplayStyle.None;
            if (!isOff) {
                return;
            }

            var point = ToLayer(camera, ball);
            if (viewport.z < 0f) {
                point = size - point;
            }

            var half = PointerSize * 0.5f;
            var minX = left + PointerMargin + half;
            var maxX = Mathf.Max(minX, size.x - right - PointerMargin - half);
            var minY = top + PointerMargin + half;
            var maxY = Mathf.Max(minY, size.y - BottomMargin - half);
            var clamped = new Vector2(Mathf.Clamp(point.x, minX, maxX), Mathf.Clamp(point.y, minY, maxY));
            pointer.style.left = clamped.x - half;
            pointer.style.top = clamped.y - half;
            var way = point - clamped;
            pointerTip.style.rotate = new Rotate(way.sqrMagnitude > 1f ? Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg : 90f);
        }

        private Vector2 ToLayer(Camera camera, Vector3 world)
        {
            var panelPoint = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, world, camera);
            return layer.WorldToLocal(panelPoint);
        }

        // A fat red arrow pointing right (the element turns it), navy-edged, with a navy drop under it.
        private static void DrawArrow(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var w = rect.width;
            var h = rect.height;
            var painter = context.painter2D;
            for (var pass = 0; pass < 2; pass++) {
                var drop = pass == 0 ? 9f : 0f;
                painter.BeginPath();
                painter.MoveTo(new Vector2(w * 0.06f, h * 0.34f + drop));
                painter.LineTo(new Vector2(w * 0.52f, h * 0.34f + drop));
                painter.LineTo(new Vector2(w * 0.52f, h * 0.08f + drop));
                painter.LineTo(new Vector2(w * 0.95f, h * 0.5f + drop));
                painter.LineTo(new Vector2(w * 0.52f, h * 0.92f + drop));
                painter.LineTo(new Vector2(w * 0.52f, h * 0.66f + drop));
                painter.LineTo(new Vector2(w * 0.06f, h * 0.66f + drop));
                painter.ClosePath();
                painter.fillColor = pass == 0 ? Navy : Red;
                painter.Fill();
                painter.lineWidth = 9f;
                painter.lineJoin = LineJoin.Round;
                painter.strokeColor = Navy;
                painter.Stroke();
            }
        }

        /// <summary>The icon of a car's points in football, its goals: the ball (<see cref="ModeLooks.DrawPoints"/>).</summary>
        public static void PaintPoints(MeshGenerationContext context) => DrawBall(context);

        // A white football with dark patches.
        private static void DrawBall(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var centre = rect.center;
            var line = Mathf.Max(2f, Mathf.Min(rect.width, rect.height) * 0.07f);
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f - line * 0.5f;
            var painter = context.painter2D;
            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.fillColor = Color.white;
            painter.Fill();
            Patch(painter, centre, radius * 0.3f, -90f);
            for (var i = 0; i < 5; i++) {
                var angle = (-90f + i * 72f) * Mathf.Deg2Rad;
                Patch(painter, centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * 0.86f), radius * 0.2f,
                    -90f + i * 72f + 180f);
            }

            painter.BeginPath();
            painter.Arc(centre, radius, 0f, 360f);
            painter.lineWidth = line;
            painter.strokeColor = Navy;
            painter.Stroke();
        }

        private static void Patch(Painter2D painter, Vector2 centre, float radius, float turn)
        {
            painter.BeginPath();
            for (var i = 0; i < 5; i++) {
                var angle = (turn + i * 72f) * Mathf.Deg2Rad;
                var corner = centre + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) {
                    painter.MoveTo(corner);
                }
                else {
                    painter.LineTo(corner);
                }
            }

            painter.ClosePath();
            painter.fillColor = Navy;
            painter.Fill();
        }

        // The pointer's tip: a navy triangle on the ball's rim, the element turned to the ball.
        private static void DrawTip(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var w = rect.width;
            var h = rect.height;
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(new Vector2(w, h * 0.5f));
            painter.LineTo(new Vector2(w * 0.78f, h * 0.3f));
            painter.LineTo(new Vector2(w * 0.78f, h * 0.7f));
            painter.ClosePath();
            painter.fillColor = Navy;
            painter.Fill();
        }
    }
}
