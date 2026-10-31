using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Replays an already saved reward on the level track: the level badge, +XP, and a row of stops from the level the
    /// round started at to the next rewards and the next rank. The track first builds left to right empty, then fills;
    /// each stop it reaches turns gold. Its lifetime belongs to the result view, so NEXT MATCH can leave at any point,
    /// including halfway through several level-ups, without holding up the next round or touching a detached screen.
    /// </summary>
    internal sealed class RoundOverProgress : IDisposable
    {
        private const float FillDuration = 0.8f;
        private const float PopDuration = 0.35f;
        private const float GrowDuration = 0.15f;
        // After the screen's own fade, so the track builds on a settled card.
        private const int BuildDelay = 150;
        // How long a new car celebrates before the fill goes on; its sticker stays afterwards.
        private const int UnlockHold = 600;

        private const string ReachedClass = "track__node--reached";
        private const string DoneClass = "track__node--done";

        private readonly RoundOverViewModel viewModel;
        private readonly VisualElement levelBadge;
        private readonly Label levelLabel;
        private readonly Label rankLabel;
        private readonly Label gain;
        private readonly Label total;
        private readonly VisualElement newCar;
        private readonly Label newCarName;
        private readonly CarPortrait newCarPortrait;
        private readonly Label ready;
        private readonly List<Waypoint> stops = new();
        private readonly List<Segment> segments = new();
        private CancellationTokenSource animation;
        private string rankClass;

        public RoundOverProgress(VisualElement root, RoundOverViewModel viewModel)
        {
            this.viewModel = viewModel;
            this.levelBadge = root.Q<VisualElement>("xp-level");
            this.levelLabel = root.Q<Label>("xp-level-value");
            this.rankLabel = root.Q<Label>("xp-rank");
            this.gain = root.Q<Label>("xp-gain");
            this.total = root.Q<Label>("xp-total");
            this.newCar = root.Q<VisualElement>("xp-new-car");
            this.newCarName = root.Q<Label>("xp-new-car-name");
            this.newCarPortrait = new CarPortrait(root.Q<VisualElement>("xp-new-car-portrait"));
            this.ready = root.Q<Label>("xp-ready");

            var reward = viewModel.XpGain;
            SetLevel(reward.Before.Level);
            gain.text = $"+{reward.Total} XP";
            total.text = $"{reward.Before.Xp} / {reward.Before.Cost} XP";
            BuildTrack(root.Q<VisualElement>("xp-track"), reward.Before.Level);

            SetScale(levelBadge, 0f);
            SetScale(gain, 0f);
            SetScale(newCar, 0f);
            ready.style.visibility = Visibility.Hidden;
            foreach (var stop in stops) {
                SetScale(stop.Node, 0f);
            }
            foreach (var segment in segments) {
                segment.Root.style.scale = new Scale(new Vector3(0f, 1f, 1f));
                segment.SetFill(0f);
            }
        }

        public void Play(CancellationToken token)
        {
            Stop();
            animation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Animate(animation.Token).Forget();
        }

        public void Stop()
        {
            animation?.Cancel();
            animation?.Dispose();
            animation = null;
        }

        public void Dispose() => Stop();

        public void RefreshColor()
        {
            newCarPortrait.SetColor(viewModel.SelectedColor);
        }

        /// <summary>
        /// Two rows laid out alike: the bars underneath, with an empty slot the size of each stop, and the stops over
        /// them with an empty gap for each bar. Each bar runs under both its stops, as in the design; in one row the
        /// stop before a bar would be drawn under it.
        /// </summary>
        private void BuildTrack(VisualElement track, int from)
        {
            var bars = Row(track, "track__row");
            var nodes = Row(track, "track__row--nodes");
            Slot(bars, false);
            stops.Add(CreateStop(nodes, from, null, null, true));
            var previous = from;
            foreach (var trackStop in viewModel.TrackStops()) {
                // Dotted only where the rank is further than one level: the bar to it would hardly move.
                var segment = new Segment(trackStop.IsRank && trackStop.Level - previous > 1);
                previous = trackStop.Level;
                bars.Add(segment.Root);
                segments.Add(segment);
                var gap = new VisualElement { pickingMode = PickingMode.Ignore };
                gap.AddToClassList("track__gap");
                nodes.Add(gap);
                Slot(bars, trackStop.IsRank);
                stops.Add(CreateStop(nodes, trackStop.Level, trackStop.Name, trackStop, false));
            }
        }

        private static VisualElement Row(VisualElement track, string className)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("track__row");
            row.AddToClassList(className);
            track.Add(row);
            return row;
        }

        private static void Slot(VisualElement bars, bool isRank)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList("track__slot");
            slot.EnableInClassList("track__slot--rank", isRank);
            bars.Add(slot);
        }

        private Waypoint CreateStop(VisualElement track, int level, string name, TrackStop reward, bool isCurrent)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("track__node");
            track.Add(node);
            if (isCurrent) {
                node.AddToClassList(ReachedClass);
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (name == null) {
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (reward.IsRank) {
                node.AddToClassList("track__node--rank");
                node.AddToClassList(PlayerRank.ClassOf(level));
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (reward.Car != null) {
                // A plain silhouette, as in the design: the car itself is the NEW CAR! sticker's to show.
                var silhouette = new VisualElement { pickingMode = PickingMode.Ignore };
                silhouette.AddToClassList("track__car");
                node.Add(silhouette);
            }
            else {
                var map = new VisualElement { pickingMode = PickingMode.Ignore };
                map.AddToClassList("track__map");
                map.style.backgroundColor = reward.Arena.Background;
                node.Add(map);
            }

            if (name != null) {
                var caption = new VisualElement { pickingMode = PickingMode.Ignore };
                caption.AddToClassList("track__caption");
                caption.Add(Text($"LVL {level}", "track__caption-level"));
                caption.Add(Text(name, "track__caption-name"));
                node.Add(caption);
            }

            // Reached, the whole stop turns into a gold disc with a navy tick over what it showed.
            var reach = new VisualElement { pickingMode = PickingMode.Ignore };
            reach.AddToClassList("track__reach");
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("track__tick-mark");
            reach.Add(mark);
            node.Add(reach);
            return new Waypoint(level, node, isCurrent);
        }

        private static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        private async UniTask Animate(CancellationToken token)
        {
            await UniTask.Delay(BuildDelay, ignoreTimeScale: true, cancellationToken: token);
            Pop(levelBadge, token).Forget();
            Pop(gain, token).Forget();
            await Pop(stops[0].Node, token);
            for (var i = 0; i < segments.Count; i++) {
                await Grow(segments[i].Root, token);
                Pop(stops[i + 1].Node, token).Forget();
            }
            await UniTask.Delay((int)(PopDuration * 1000), ignoreTimeScale: true, cancellationToken: token);
            await FillLevels(token);
        }

        /// <summary>
        /// Moves the track's position, a level and the share of it earned, level by level from before the round to
        /// after: each level crossed bumps the badge, turns its stops gold and shows its new cars.
        /// </summary>
        private async UniTask FillLevels(CancellationToken token)
        {
            var reward = viewModel.XpGain;
            var level = reward.Before.Level;
            var from = level + (float)reward.Before.Xp / reward.Before.Cost;
            while (level < reward.After.Level) {
                await Fill(from, level + 1, token);
                level++;
                from = level;
                SetLevel(level);
                await UniTask.WhenAll(Bounce(levelBadge, token), Reach(level, token));
            }

            total.text = $"{reward.After.Xp} / {reward.After.Cost} XP";
            await Fill(from, level + (float)reward.After.Xp / reward.After.Cost, token);
        }

        private async UniTask Reach(int level, CancellationToken token)
        {
            var celebrations = new List<UniTask>();
            foreach (var stop in stops) {
                if (stop.Level == level && !stop.IsReached) {
                    stop.IsReached = true;
                    stop.Node.AddToClassList(ReachedClass);
                    stop.Node.AddToClassList(DoneClass);
                    celebrations.Add(Bounce(stop.Node, token));
                }
            }
            await UniTask.WhenAll(celebrations);

            foreach (var car in viewModel.NewCarsAt(level)) {
                newCarName.text = $"NEW CAR! {car.DisplayName}";
                newCarPortrait.Set(car, viewModel.SelectedColor);
                ready.style.visibility = Visibility.Visible;
                await Pop(newCar, token);
                await UniTask.Delay(UnlockHold, ignoreTimeScale: true, cancellationToken: token);
            }
        }

        private void SetLevel(int level)
        {
            levelLabel.text = level.ToString();
            rankLabel.text = $"{PlayerRank.NameOf(level)} · LEVEL {level}";
            if (rankClass != null) {
                levelBadge.RemoveFromClassList(rankClass);
            }
            rankClass = PlayerRank.ClassOf(level);
            levelBadge.AddToClassList(rankClass);
        }

        /// <summary>Each segment fills by how far the position is between the levels of the stops at its ends.</summary>
        private void SetPosition(float position)
        {
            for (var i = 0; i < segments.Count; i++) {
                float a = stops[i].Level;
                float b = stops[i + 1].Level;
                segments[i].SetFill((position - a) / (b - a));
            }
        }

        private async UniTask Fill(float from, float to, CancellationToken token)
        {
            for (var time = 0f; time < FillDuration; time += Time.unscaledDeltaTime) {
                var t = time / FillDuration;
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                SetPosition(Mathf.Lerp(from, to, ease));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetPosition(to);
        }

        private static async UniTask Grow(VisualElement segment, CancellationToken token)
        {
            for (var time = 0f; time < GrowDuration; time += Time.unscaledDeltaTime) {
                var t = time / GrowDuration;
                segment.style.scale = new Scale(new Vector3(1f - (1f - t) * (1f - t), 1f, 1f));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            segment.style.scale = new Scale(Vector3.one);
        }

        private static async UniTask Bounce(VisualElement element, CancellationToken token)
        {
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                SetScale(element, 1f + 0.22f * Mathf.Sin(Mathf.PI * time / PopDuration));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(element, 1f);
        }

        /// <summary>The same 0.35s ease-out-back spring as the stickers, on unscaled time.</summary>
        private static async UniTask Pop(VisualElement sticker, CancellationToken token)
        {
            SetScale(sticker, 0f);
            for (var time = 0f; time < PopDuration; time += Time.unscaledDeltaTime) {
                var t = time / PopDuration - 1f;
                var scale = 1f + 2.70158f * t * t * t + 1.70158f * t * t;
                SetScale(sticker, scale);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(sticker, 1f);
        }

        private static void SetScale(VisualElement element, float value) =>
            element.style.scale = new Scale(new Vector3(value, value, 1f));

        private sealed class Waypoint
        {
            public Waypoint(int level, VisualElement node, bool isReached)
            {
                Level = level;
                Node = node;
                IsReached = isReached;
            }

            public int Level { get; }
            public VisualElement Node { get; }
            public bool IsReached { get; set; }
        }

        /// <summary>
        /// The bar between two stops, tucked under the next stop. The one to the next rank is a dotted line instead:
        /// it is further than it looks, and nothing fills it.
        /// </summary>
        private sealed class Segment
        {
            private const int Dots = 4;

            private readonly VisualElement fill;

            public Segment(bool isDotted)
            {
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("track__segment");
                Root.EnableInClassList("track__segment--dotted", isDotted);
                if (isDotted) {
                    // USS has no dotted border, so the dots are elements, in a row of their own: padding on the bar
                    // would add to its share of the width and pull the two rows of the track apart.
                    var dots = new VisualElement { pickingMode = PickingMode.Ignore };
                    dots.AddToClassList("track__dots");
                    for (var i = 0; i < Dots; i++) {
                        var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                        dot.AddToClassList("track__dot");
                        dots.Add(dot);
                    }
                    Root.Add(dots);
                    return;
                }

                // The fill runs over the visible part only, between the two stops the bar is tucked under.
                var area = new VisualElement { pickingMode = PickingMode.Ignore };
                area.AddToClassList("track__fill-area");
                fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("track__fill");
                area.Add(fill);
                Root.Add(area);
            }

            public VisualElement Root { get; }

            public void SetFill(float value)
            {
                if (fill != null) {
                    fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
                }
            }
        }
    }
}
