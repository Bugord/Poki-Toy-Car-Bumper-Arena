using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// A round in teams on the HUD (<see cref="ModeRules.Teams"/>), for every mode played in teams: the team score under
    /// the bar, BLUE N : M RED, the player's team on the left with a YOU tag; on each point the scoring side's number pops
    /// and a +N sticker of the team's colour springs up from under it into the number, so a point of either team is seen
    /// at a glance. The name tags over the cars mark teammates with a shield (<see cref="MarkName"/>), so allies and
    /// enemies differ by more than colour.
    /// <para>
    /// Made only in a round in teams (<see cref="Create"/> gives null otherwise), in place of the match's per-car
    /// scoreboard. Built once; the score changes on the round's event, a frame only moves the stickers on real time, and
    /// nothing here catches input.
    /// </para>
    /// </summary>
    public sealed class HudTeams : IDisposable
    {
        // Reference pixels under the safe top the score sits at: just under the bar.
        private const float Top = 176f;

        /// <summary>
        /// How far down the feed moves in portrait, where it sits left under the bar: past the score, its +N stickers
        /// and the end timer's note under them (in landscape the feed is in the top left corner, clear of them).
        /// </summary>
        public const float FeedDrop = 240f;

        // A changed number: up to this scale, easing back over the seconds.
        private const float PopScale = 1.45f;
        private const float PopSeconds = 0.35f;

        // The +N sticker: springs in under its side, holds, then flies up into the number and shrinks away.
        private const int Stickers = 4;
        private const float StickerIn = 0.22f;
        private const float StickerHold = 0.55f;
        private const float StickerOut = 0.3f;
        private const float StickerDrop = 120f;
        private const float StickerTilt = 7f;

        private const string ShownClass = "team-score--shown";
        private const string LeadClass = "team-score__side--lead";
        private const string AllyClass = "car-name--ally";
        private const string EnemyClass = "car-name--enemy";

        private static readonly string[] Numbers = CreateNumbers(100);

        private readonly HudViewModel model;
        private readonly VisualElement root;
        private readonly Side[] sides = new Side[2];
        private readonly Sticker[] stickers = new Sticker[Stickers];
        private readonly IVisualElementScheduledItem tick;
        private readonly Action onLanguage;
        // The HUD's top bar, which the end timer grows from its top centre in the last seconds: the score moves down
        // with its bottom edge, or the grown bar would cover it.
        private readonly VisualElement hudBar;
        private float barShift;
        private int nextSticker;

        /// <summary>The team HUD for a round in teams, null otherwise.</summary>
        public static HudTeams Create(VisualElement parent, HudViewModel model) =>
            model.Teams == 2 && parent != null ? new HudTeams(parent, model) : null;

        private HudTeams(VisualElement parent, HudViewModel model)
        {
            this.model = model;
            root = new VisualElement { name = "team-score", pickingMode = PickingMode.Ignore };
            root.AddToClassList("team-score");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("team-score__bar");
            root.Add(bar);

            var playerTeam = model.PlayerTeam;
            var otherTeam = playerTeam == RoundTeams.Blue ? RoundTeams.Red : RoundTeams.Blue;
            sides[0] = new Side(playerTeam, true);
            bar.Add(sides[0].Root);
            var colon = StrokeText.Create(":", "team-score__colon", out _, "display", "team-score__colon-text");
            bar.Add(colon);
            sides[1] = new Side(otherTeam, false);
            bar.Add(sides[1].Root);

            for (var i = 0; i < stickers.Length; i++) {
                stickers[i] = new Sticker();
                root.Add(stickers[i].Root);
            }

            parent.Add(root);
            hudBar = parent.Q<VisualElement>(className: "hud__bar");
            model.TeamScoreChanged += OnTeamScoreChanged;
            onLanguage = () => {
                foreach (var side in sides) {
                    side.SetName();
                }
            };
            Strings.Changed += onLanguage;
            Refresh();
            tick = root.schedule.Execute(Tick).Every(0);
        }

        /// <summary>The score's element, for the end timer's note and the name tags to keep clear of.</summary>
        public VisualElement Element => root;

        /// <summary>Puts the score under the bar, <paramref name="top"/> the safe top of the HUD.</summary>
        public void Place(float top)
        {
            root.style.top = Top + top;
        }

        /// <summary>
        /// Marks car <paramref name="index"/>'s name tag as a teammate's (a blue pill with a shield before the level) or
        /// an enemy's (a red pill), in a round in teams; nothing otherwise.
        /// </summary>
        public static void MarkName(VisualElement tag, VisualElement line, HudViewModel model, int index)
        {
            if (model.Teams <= 0) {
                return;
            }

            var isAlly = model.TeamOf(index) == model.PlayerTeam;
            tag.AddToClassList(isAlly ? AllyClass : EnemyClass);
            if (isAlly) {
                var shield = new VisualElement { pickingMode = PickingMode.Ignore };
                shield.AddToClassList("car-name__ally");
                shield.generateVisualContent += DrawShield;
                line.Insert(0, shield);
            }
        }

        public void Dispose()
        {
            tick.Pause();
            model.TeamScoreChanged -= OnTeamScoreChanged;
            Strings.Changed -= onLanguage;
            root.RemoveFromHierarchy();
        }

        private void OnTeamScoreChanged(int team, int change)
        {
            Refresh();
            var side = team == sides[0].Team ? sides[0] : sides[1];
            side.PopAt = Time.unscaledTime;
            if (change <= 0) {
                return;
            }

            var sticker = stickers[nextSticker];
            nextSticker = (nextSticker + 1) % stickers.Length;
            sticker.Show(team, side, side == sides[0], change, Time.unscaledTime);
        }

        private void Refresh()
        {
            var own = model.TeamPointsOf(sides[0].Team);
            var other = model.TeamPointsOf(sides[1].Team);
            sides[0].SetPoints(own, own > other);
            sides[1].SetPoints(other, other > own);
        }

        private void Tick()
        {
            root.EnableInClassList(ShownClass, model.HasBegun);
            FollowBar();
            var now = Time.unscaledTime;
            foreach (var side in sides) {
                var since = now - side.PopAt;
                var pop = since < PopSeconds ? 1f + (PopScale - 1f) * (1f - since / PopSeconds) * (1f - since / PopSeconds) : 1f;
                side.Number.style.scale = new Scale(new Vector3(pop, pop, 1f));
            }

            foreach (var sticker in stickers) {
                sticker.Tick(now, root);
            }
        }

        private void FollowBar()
        {
            if (hudBar == null) {
                return;
            }

            var height = hudBar.layout.height;
            var shift = float.IsNaN(height) ? 0f : height * Mathf.Max(0f, hudBar.resolvedStyle.scale.value.y - 1f);
            if (Mathf.Abs(shift - barShift) > 0.25f) {
                barShift = shift;
                root.style.translate = new Translate(0f, shift);
            }
        }

        /// <summary>A small white shield with a blue face and a white chevron: the teammate's mark on its name tag.</summary>
        private static void DrawShield(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var w = rect.width;
            var h = rect.height;
            if (w <= 0f || h <= 0f) {
                return;
            }

            var paint = context.painter2D;
            void Outline(float inset)
            {
                paint.BeginPath();
                paint.MoveTo(new Vector2(w * 0.5f, inset));
                paint.LineTo(new Vector2(w - inset, h * 0.16f + inset * 0.6f));
                paint.LineTo(new Vector2(w - inset, h * 0.5f));
                paint.BezierCurveTo(new Vector2(w - inset, h * 0.78f), new Vector2(w * 0.72f, h * 0.9f),
                    new Vector2(w * 0.5f, h - inset));
                paint.BezierCurveTo(new Vector2(w * 0.28f, h * 0.9f), new Vector2(inset, h * 0.78f),
                    new Vector2(inset, h * 0.5f));
                paint.LineTo(new Vector2(inset, h * 0.16f + inset * 0.6f));
                paint.ClosePath();
            }

            paint.fillColor = Navy;
            Outline(0f);
            paint.Fill();
            paint.fillColor = Color.white;
            Outline(w * 0.1f);
            paint.Fill();
            paint.fillColor = BlueFace;
            Outline(w * 0.2f);
            paint.Fill();

            paint.strokeColor = Color.white;
            paint.lineWidth = w * 0.13f;
            paint.lineCap = LineCap.Round;
            paint.lineJoin = LineJoin.Round;
            paint.BeginPath();
            paint.MoveTo(new Vector2(w * 0.32f, h * 0.56f));
            paint.LineTo(new Vector2(w * 0.5f, h * 0.38f));
            paint.LineTo(new Vector2(w * 0.68f, h * 0.56f));
            paint.Stroke();
        }

        private static readonly Color Navy = new(0.114f, 0.169f, 0.31f);
        private static readonly Color BlueFace = CarColor.TintOf(RoundTeams.PaintOf(RoundTeams.Blue));

        private static string[] CreateNumbers(int count)
        {
            var numbers = new string[count];
            for (var i = 0; i < count; i++) {
                numbers[i] = i.ToString();
            }

            return numbers;
        }

        private static string NumberOf(int value) => value >= 0 && value < Numbers.Length ? Numbers[value] : value.ToString();

        private static string TeamName(int team) => Strings.Get(team == RoundTeams.Red ? "hud.team_red" : "hud.team_blue");

        /// <summary>One team's half of the score: its name, its number and, for the player's team, YOU.</summary>
        private sealed class Side
        {
            private readonly Label[] names;
            private readonly Label[] numbers;
            private int shown = -1;

            public Side(int team, bool isPlayers)
            {
                Team = team;
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("team-score__side");
                Root.AddToClassList(team == RoundTeams.Red ? "team-score__side--red" : "team-score__side--blue");
                Root.AddToClassList(isPlayers ? "team-score__side--own" : "team-score__side--other");
                var name = StrokeText.Create(TeamName(team), "team-score__name", out names, "display",
                    "team-score__name-text");
                Number = StrokeText.Create("0", "team-score__number", out numbers, "display", "team-score__number-text");
                if (isPlayers) {
                    Root.Add(name);
                    Root.Add(Number);
                }
                else {
                    Root.Add(Number);
                    Root.Add(name);
                }

                if (isPlayers) {
                    var you = new Label(GameText.You) { pickingMode = PickingMode.Ignore };
                    you.AddToClassList("team-score__you");
                    Root.Add(you);
                    You = you;
                }
            }

            public int Team { get; }
            public VisualElement Root { get; }
            public VisualElement Number { get; }
            public Label You { get; }
            public float PopAt { get; set; } = float.NegativeInfinity;

            public void SetName()
            {
                foreach (var label in names) {
                    label.text = TeamName(Team);
                }

                if (You != null) {
                    You.text = GameText.You;
                }
            }

            public void SetPoints(int points, bool isLead)
            {
                Root.EnableInClassList(LeadClass, isLead);
                if (points == shown) {
                    return;
                }

                shown = points;
                foreach (var label in numbers) {
                    label.text = NumberOf(points);
                }
            }
        }

        /// <summary>A +N sticker of a team's colour that springs up under its side and flies into the number.</summary>
        private sealed class Sticker
        {
            private readonly Label[] labels;
            private float shownAt = float.NegativeInfinity;
            private bool isLeft;
            private bool isActive;
            private Side side;

            public Sticker()
            {
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("team-score__point");
                var text = StrokeText.Create("+1", "team-score__point-stroke", out labels, "display",
                    "team-score__point-text");
                Root.Add(text);
                Root.style.display = DisplayStyle.None;
            }

            public VisualElement Root { get; }

            public void Show(int team, Side under, bool left, int change, float now)
            {
                side = under;
                isLeft = left;
                isActive = true;
                shownAt = now;
                var value = "+" + NumberOf(change);
                foreach (var label in labels) {
                    label.text = value;
                }

                Root.EnableInClassList("team-score__point--red", team == RoundTeams.Red);
                Root.EnableInClassList("team-score__point--blue", team != RoundTeams.Red);
                Root.style.display = DisplayStyle.Flex;
            }

            public void Tick(float now, VisualElement parent)
            {
                if (!isActive) {
                    return;
                }

                var t = now - shownAt;
                if (t > StickerIn + StickerHold + StickerOut) {
                    isActive = false;
                    Root.style.display = DisplayStyle.None;
                    return;
                }

                float scale;
                float drop;
                if (t < StickerIn) {
                    var k = t / StickerIn;
                    // Springs in past its size and back.
                    scale = Mathf.LerpUnclamped(0.3f, 1f, 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f));
                    drop = StickerDrop;
                }
                else if (t < StickerIn + StickerHold) {
                    scale = 1f;
                    drop = StickerDrop;
                }
                else {
                    var k = (t - StickerIn - StickerHold) / StickerOut;
                    scale = Mathf.Lerp(1f, 0.35f, k * k);
                    drop = Mathf.Lerp(StickerDrop, 10f, k * k);
                }

                // Under the middle of its side, wherever the bar is.
                var bound = side.Root.worldBound;
                Root.style.left = parent.WorldToLocal(new Vector2(bound.center.x, bound.yMax)).x;
                Root.style.top = drop;
                Root.style.scale = new Scale(new Vector3(scale, scale, 1f));
                Root.style.rotate = new Rotate(isLeft ? -StickerTilt : StickerTilt);
            }
        }
    }
}
