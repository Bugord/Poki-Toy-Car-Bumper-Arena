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
        private const float RevealDuration = 0.3f;
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
            foreach (var stop in stops) {
                stop.Portrait?.SetColor(viewModel.SelectedColor);
            }
        }

        private void BuildTrack(VisualElement track, int from)
        {
            stops.Add(CreateStop(track, from, null, null, true));
            foreach (var trackStop in viewModel.TrackStops()) {
                var segment = new Segment(trackStop.IsRank);
                track.Add(segment.Root);
                segments.Add(segment);
                stops.Add(CreateStop(track, trackStop.Level, trackStop.Name, trackStop, false));
            }
        }

        private Waypoint CreateStop(VisualElement track, int level, string name, TrackStop reward, bool isCurrent)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("track__node");
            track.Add(node);
            CarPortrait portrait = null;
            if (isCurrent) {
                node.AddToClassList(ReachedClass);
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (reward.IsRank) {
                node.AddToClassList("track__node--rank");
                node.AddToClassList(PlayerRank.ClassOf(level));
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (reward.Car != null) {
                var image = new VisualElement { pickingMode = PickingMode.Ignore };
                image.AddToClassList("car-portrait");
                image.AddToClassList("track__car");
                node.Add(image);
                portrait = new CarPortrait(image);
                portrait.Set(reward.Car, viewModel.SelectedColor, true);
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

            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.AddToClassList("track__tick");
            var mark = new VisualElement { pickingMode = PickingMode.Ignore };
            mark.AddToClassList("track__tick-mark");
            tick.Add(mark);
            node.Add(tick);
            return new Waypoint(level, node, portrait, isCurrent);
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
                    if (stop.Portrait != null) {
                        celebrations.Add(Reveal(stop.Portrait, token));
                    }
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

        private static async UniTask Reveal(CarPortrait portrait, CancellationToken token)
        {
            for (var time = 0f; time < RevealDuration; time += Time.unscaledDeltaTime) {
                portrait.SetReveal(time / RevealDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            portrait.SetReveal(1f);
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
            public Waypoint(int level, VisualElement node, CarPortrait portrait, bool isReached)
            {
                Level = level;
                Node = node;
                Portrait = portrait;
                IsReached = isReached;
            }

            public int Level { get; }
            public VisualElement Node { get; }
            public CarPortrait Portrait { get; }
            public bool IsReached { get; set; }
        }

        /// <summary>The bar between two stops; the next-rank one is dotted, it is further than it looks.</summary>
        private sealed class Segment
        {
            private readonly VisualElement fill;

            public Segment(bool isDotted)
            {
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("track__segment");
                Root.EnableInClassList("track__segment--dotted", isDotted);
                fill = new VisualElement { pickingMode = PickingMode.Ignore };
                fill.AddToClassList("track__fill");
                Root.Add(fill);
            }

            public VisualElement Root { get; }

            public void SetFill(float value) => fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
        }
    }
}
