using System;
using Bugord.App.Localization;
using Bugord.App.UI;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace ToyCarArena.UI
{
    /// <summary>Fixed pools for the elimination feed and bot speech. No elements, strings or delegates per frame.</summary>
    public sealed class HudSocial : IDisposable
    {
        private const float FeedLife = 3f;
        private const float RowHeight = 74f;
        private const float SpeechLife = 1.2f;
        private const float SpeechGap = 4f;
        private const float EventChance = 0.55f;
        private const float RecoveryDelay = 0.65f;
        // String table keys: the bots speak short gamer slang in the player's language.
        private static readonly string[] WinLines = { "social.gg", "social.ez", "social.bye", "social.nice" };
        private static readonly string[] OutLines = { "social.nooo", "social.oops", "social.gg", "social.hey" };
        private static readonly string[] CloseLines = { "social.phew", "social.close" };
        private static readonly string[] HelloLines = { "social.hi", "social.glhf", "social.lets_go" };
        private static readonly string[] RivalLines = { "social.again", "social.lol" };

        private readonly HudViewModel model;
        private readonly VisualElement root;
        private readonly VisualElement speechLayer;
        private readonly VisualElement stick;
        private readonly VisualElement stickDisc;
        private readonly VisualElement boost;
        private readonly VisualElement start;
        private readonly VisualElement ghost;
        private readonly VisualElement[] pills;
        private readonly HudScoreboard scoreboard;
        private readonly FeedRow[] feedRows = new FeedRow[3];
        private readonly Speech[] speeches = new Speech[2];
        private readonly float[] nextSpeech;
        private readonly float[] seenBoost;
        private readonly float[] seenHit;
        private readonly float[] recoveryAt;
        private readonly int[] greetingOrder;
        private bool greeted;
        private bool landscape;
        private int sequence;
        private float previousTime;

        public VisualElement Feed { get; }
        public bool IsFeedVisible { get; private set; }

        public HudSocial(VisualElement root, HudViewModel model, VisualElement stick, VisualElement boost,
            VisualElement timePill, VisualElement carsPill, VisualElement koPill, VisualElement settingsButton,
            HudScoreboard scoreboard = null)
        {
            this.scoreboard = scoreboard;
            this.root = root;
            this.model = model;
            this.stick = stick;
            stickDisc = root.Q<VisualElement>("stick-base");
            this.boost = boost;
            start = root.Q<VisualElement>("start");
            ghost = root.Q<VisualElement>("ghost");
            pills = new[] { timePill, carsPill, koPill, settingsButton };
            Feed = root.Q<VisualElement>("feed");
            speechLayer = root.Q<VisualElement>("emotion-layer");
            for (var i = 0; i < feedRows.Length; i++) {
                feedRows[i] = new FeedRow();
                Feed.Add(feedRows[i].Root);
            }
            for (var i = 0; i < speeches.Length; i++) {
                speeches[i] = new Speech();
                speechLayer.Add(speeches[i].Root);
            }
            nextSpeech = new float[model.CarCount];
            seenBoost = new float[model.CarCount];
            seenHit = new float[model.CarCount];
            recoveryAt = new float[model.CarCount];
            greetingOrder = new int[Mathf.Max(0, model.CarCount - 1)];
            for (var index = 0; index < model.CarCount; index++) {
                nextSpeech[index] = recoveryAt[index] = float.NegativeInfinity;
                seenBoost[index] = model.LastBoostOf(index);
                seenHit[index] = model.LastHitOf(index);
                if (index > 0) {
                    greetingOrder[index - 1] = index;
                }
            }
            previousTime = Time.unscaledTime;
            model.Eliminated += OnEliminated;
            PillShape.Keep(Feed);
            PillShape.Keep(speechLayer);
        }

        public void Dispose() => model.Eliminated -= OnEliminated;

        private void OnEliminated(int index, int by)
        {
            for (var i = 0; i < speeches.Length; i++) {
                if (speeches[i].Active && speeches[i].Index == index) {
                    var position = model.PositionOf(index);
                    position.y = 0f;
                    speeches[i].Position = position;
                    speeches[i].Frozen = true;
                }
            }
            FeedRow row = null;
            for (var i = 0; i < feedRows.Length; i++) {
                var candidate = feedRows[i];
                if (!candidate.Active) {
                    row = candidate;
                    break;
                }
                if (row == null || candidate.Sequence < row.Sequence) {
                    row = candidate;
                }
            }
            row.Show(model, index, by, ++sequence);
            if (by > 0 && model.IsCarIn(by)) {
                Say(by, index == 0 && model.IsNemesis(by) ? RivalLines : WinLines);
            }
            if (index > 0 && by == 0) {
                Say(index, OutLines);
            }
        }

        public void Tick(bool isShown, VisualElement[] names)
        {
            var now = Time.unscaledTime;
            var delta = Mathf.Max(0f, now - previousTime);
            previousTime = now;
            var wide = root.layout.width > root.layout.height;
            if (wide != landscape) {
                landscape = wide;
                Feed.EnableInClassList("feed--wide", wide);
            }
            TickFeed(now, delta, isShown);
            if (isShown && model.IsRunning) {
                if (!greeted) {
                    Greet();
                }
                WatchRecovery(now);
            }
            TickSpeech(now, names);
        }

        private void TickFeed(float now, float delta, bool isShown)
        {
            var count = 0;
            for (var i = 0; i < feedRows.Length; i++) {
                var row = feedRows[i];
                if (row.Active && now - row.Started >= FeedLife) {
                    row.Active = false;
                    row.Root.style.display = DisplayStyle.None;
                }
                if (row.Active) {
                    count++;
                }
            }
            Feed.style.height = count * RowHeight;
            // Touch controls can start anywhere. Do not paint over a finger that is steering through the feed.
            var touchesFeed = stick.ClassListContains("hud__stick--shown") && Feed.worldBound.Overlaps(stickDisc.worldBound);
            var teachesOverFeed = start.ClassListContains("hud__start--shown") ||
                (ghost.resolvedStyle.display != DisplayStyle.None && ghost.resolvedStyle.opacity > 0f &&
                    Feed.worldBound.Overlaps(ghost.worldBound));
            IsFeedVisible = count > 0 && isShown && !model.IsWaitingForPlayer && !touchesFeed && !teachesOverFeed;
            Feed.style.visibility = IsFeedVisible ? Visibility.Visible : Visibility.Hidden;
            for (var i = 0; i < feedRows.Length; i++) {
                var row = feedRows[i];
                if (!row.Active) {
                    continue;
                }
                var newer = 0;
                for (var other = 0; other < feedRows.Length; other++) {
                    if (feedRows[other].Active && feedRows[other].Sequence > row.Sequence) {
                        newer++;
                    }
                }
                row.Y = Mathf.Lerp(row.Y, newer * RowHeight, 1f - Mathf.Exp(-delta / 0.09f));
                row.Root.style.top = row.Y;
                var age = now - row.Started;
                var enter = Mathf.Clamp01(age / 0.18f);
                var leave = Mathf.Clamp01((FeedLife - age) / 0.25f);
                row.Root.style.scale = new Scale(new Vector3(Mathf.Min(1.06f, Pop(enter)), leave, 1f));
                row.Root.style.opacity = Mathf.Min(enter * 4f, leave);
            }
        }

        private void Greet()
        {
            greeted = true;
            for (var i = greetingOrder.Length - 1; i > 0; i--) {
                var other = Random.Range(0, i + 1);
                (greetingOrder[i], greetingOrder[other]) = (greetingOrder[other], greetingOrder[i]);
            }
            var count = Mathf.Min(greetingOrder.Length, Random.Range(0, 3));
            for (var i = 0; i < count; i++) {
                if (model.IsCarIn(greetingOrder[i])) {
                    Say(greetingOrder[i], HelloLines);
                }
            }
        }

        private void WatchRecovery(float now)
        {
            for (var index = 1; index < nextSpeech.Length; index++) {
                var boostTime = model.LastBoostOf(index);
                var hitTime = model.LastHitOf(index);
                var changed = boostTime > seenBoost[index] || hitTime > seenHit[index];
                seenBoost[index] = boostTime;
                seenHit[index] = hitTime;
                if (!model.IsCarIn(index)) {
                    recoveryAt[index] = float.NegativeInfinity;
                    continue;
                }
                if (changed && float.IsNegativeInfinity(recoveryAt[index]) && model.IsNearEdge(index)) {
                    recoveryAt[index] = now + RecoveryDelay;
                }
                if (!float.IsNegativeInfinity(recoveryAt[index]) && now >= recoveryAt[index]) {
                    recoveryAt[index] = float.NegativeInfinity;
                    if (model.HasRecovered(index)) {
                        Say(index, CloseLines);
                    }
                }
            }
        }

        private void Say(int index, string[] lines)
        {
            var now = Time.unscaledTime;
            if (now < nextSpeech[index]) {
                return;
            }
            Speech available = null;
            for (var i = 0; i < speeches.Length; i++) {
                if (!speeches[i].Active || now - speeches[i].Started >= SpeechLife) {
                    available = speeches[i];
                    break;
                }
            }
            if (available == null || Random.value >= EventChance) {
                return;
            }
            available.Index = index;
            available.Started = now;
            available.Active = true;
            available.Frozen = !model.IsCarIn(index);
            var position = model.PositionOf(index);
            position.y = 0f;
            available.Position = position;
            available.Text.SetText(Strings.Get(lines[Random.Range(0, lines.Length)]));
            available.Root.style.display = DisplayStyle.Flex;
            available.Root.style.visibility = Visibility.Hidden;
            nextSpeech[index] = now + SpeechGap;
        }

        private void TickSpeech(float now, VisualElement[] names)
        {
            var camera = Camera.main;
            for (var i = 0; i < speeches.Length; i++) {
                var speech = speeches[i];
                if (!speech.Active) {
                    continue;
                }
                var age = now - speech.Started;
                if (age >= SpeechLife) {
                    speech.Active = false;
                    speech.Root.style.display = DisplayStyle.None;
                    continue;
                }
                if (camera == null || root.panel == null) {
                    continue;
                }
                var position = speech.Frozen ? speech.Position : model.PositionOf(speech.Index);
                var point = root.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, position, camera));
                if (!speech.Frozen && names != null && names[speech.Index] != null) {
                    var name = names[speech.Index].worldBound;
                    point = root.WorldToLocal(new Vector2(name.center.x, name.yMin));
                }
                var width = 280f;
                var height = 90f;
                var rect = new Rect(Mathf.Clamp(point.x - width / 2f, 20f, Mathf.Max(20f, root.layout.width - width - 20f)),
                    Mathf.Clamp(point.y - height - 14f, 20f, Mathf.Max(20f, root.layout.height - height - 20f)), width, height);
                for (var pill = 0; pill < pills.Length; pill++) {
                    Clear(pills[pill], ref rect);
                }
                if (IsFeedVisible) {
                    Clear(Feed, ref rect);
                }
                if (names != null) {
                    for (var pass = 0; pass < names.Length; pass++) {
                        for (var index = 1; index < names.Length; index++) {
                            if (names[index] != null && model.IsCarIn(index)) {
                                var name = LocalBounds(names[index]);
                                if (rect.Overlaps(name)) {
                                    rect.y = name.yMin - height - 12f;
                                }
                            }
                        }
                    }
                }
                var visible = rect.y >= 20f && camera.WorldToViewportPoint(position).z > 0f;
                for (var pill = 0; pill < pills.Length; pill++) {
                    visible &= !Overlaps(pills[pill], rect);
                }
                visible &= !IsFeedVisible || !Overlaps(Feed, rect);
                // The scoreboard is a tall column at the side: a bubble over it is hidden rather than pushed far down.
                visible &= scoreboard == null || !scoreboard.IsVisible || !Overlaps(scoreboard.Element, rect);
                visible &= !stick.ClassListContains("hud__stick--shown") || !Overlaps(stickDisc, rect);
                visible &= !Overlaps(boost, rect);
                for (var other = 0; other < i; other++) {
                    if (speeches[other].Active && speeches[other].Root.resolvedStyle.visibility == Visibility.Visible) {
                        visible &= !Overlaps(speeches[other].Root, rect);
                    }
                }
                speech.Root.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
                speech.Root.style.left = rect.x;
                speech.Root.style.top = rect.y;
                var scale = Pop(Mathf.Clamp01(age / 0.18f)) * Mathf.Clamp01((SpeechLife - age) / 0.2f);
                speech.Root.style.scale = new Scale(Vector3.one * scale);
            }
        }

        private Rect LocalBounds(VisualElement element)
        {
            var min = root.WorldToLocal(element.worldBound.min);
            var max = root.WorldToLocal(element.worldBound.max);
            return new Rect(min, max - min);
        }

        private bool Overlaps(VisualElement element, Rect bounds) =>
            element.resolvedStyle.display != DisplayStyle.None && bounds.Overlaps(LocalBounds(element));

        private void Clear(VisualElement element, ref Rect bounds)
        {
            if (Overlaps(element, bounds)) {
                bounds.y = LocalBounds(element).yMax + 20f;
            }
        }

        private static float Pop(float t)
        {
            var shifted = t - 1f;
            return 1f + 2.70158f * shifted * shifted * shifted + 1.70158f * shifted * shifted;
        }

        private sealed class Speech
        {
            public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
            public readonly StickerText Text = new("");
            public bool Active;
            public bool Frozen;
            public int Index;
            public float Started;
            public Vector3 Position;

            public Speech()
            {
                Root.AddToClassList("emotion");
                Text.AddToClassList("emotion__text");
                Root.Add(Text);
                Root.style.display = DisplayStyle.None;
            }
        }

        private sealed class FeedRow
        {
            public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
            private readonly VisualElement firstChip = CarColor.CreateChip(0, false);
            private readonly VisualElement secondChip = CarColor.CreateChip(0, false);
            private readonly StickerText first = new("");
            private readonly StickerText second = new("");
            private readonly StickerText fell = new(Strings.Get("hud.fell_off"));
            private readonly VisualElement arrow = new() { pickingMode = PickingMode.Ignore };
            private int firstColor;
            private int secondColor;
            public bool Active;
            public int Sequence;
            public float Started;
            public float Y;

            public FeedRow()
            {
                Root.AddToClassList("feed__row");
                first.AddToClassList("feed__name");
                second.AddToClassList("feed__name");
                fell.AddToClassList("feed__fell");
                arrow.AddToClassList("feed__arrow");
                Root.Add(firstChip);
                Root.Add(first);
                Root.Add(arrow);
                Root.Add(secondChip);
                Root.Add(second);
                Root.Add(fell);
                Root.style.display = DisplayStyle.None;
            }

            public void Show(HudViewModel model, int index, int by, int sequence)
            {
                Active = true;
                Started = Time.unscaledTime;
                Sequence = sequence;
                Y = -RowHeight;
                var isKo = by != RoundResult.Nobody;
                Root.EnableInClassList("feed__row--you", index == 0 || by == 0);
                Recolor(firstChip, ref firstColor, model.ColorOf(isKo ? by : index));
                CarColor.MarkPlayer(firstChip, (isKo ? by : index) == 0);
                first.SetText(model.NameOf(isKo ? by : index));
                arrow.style.display = secondChip.style.display = second.style.display =
                    isKo ? DisplayStyle.Flex : DisplayStyle.None;
                fell.style.display = isKo ? DisplayStyle.None : DisplayStyle.Flex;
                if (isKo) {
                    Recolor(secondChip, ref secondColor, model.ColorOf(index));
                    CarColor.MarkPlayer(secondChip, index == 0);
                    second.SetText(model.NameOf(index));
                }
                Root.style.display = DisplayStyle.Flex;
                Root.style.opacity = 0f;
                Root.style.top = -RowHeight;
            }

            /// <summary>
            /// Only the chip carries the car's colour: a name in blue or purple ink is lost on the dark pill.
            /// </summary>
            private static void Recolor(VisualElement chip, ref int previous, int colorId)
            {
                chip.RemoveFromClassList(CarColor.ClassOf(previous));
                chip.AddToClassList(CarColor.ClassOf(colorId));
                previous = colorId;
            }
        }
    }
}
