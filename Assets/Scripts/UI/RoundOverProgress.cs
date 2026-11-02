using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Replays an already saved reward in three steps: first a plain bar from the level the round started at to the
    /// next fills with the round's XP (LEVEL UP! and the badge bumps on each level crossed), then each level crossed
    /// that gives something (a car, an arena, a rank) shows it in a popup over the screen until a tap or a few seconds,
    /// and last the level track: a row of stops from the level the round started at to the next rewards and the next
    /// rank, which builds left to right empty, then fills, each stop it reaches turning gold. Its lifetime belongs to
    /// the result view, so NEXT MATCH can leave at any point, including halfway through a popup, without holding up
    /// the next round or touching a detached screen.
    /// </summary>
    internal sealed class RoundOverProgress : IDisposable
    {
        private const float FillDuration = 0.8f;
        private const float PopDuration = 0.35f;
        private const float GrowDuration = 0.15f;
        // After the screen's own fade, so the track builds on a settled card.
        private const int BuildDelay = 150;
        private const float BarFillDuration = 0.9f;
        // LEVEL UP! holds this long before the level's reward pops up.
        private const int LevelUpHold = 450;
        // The bar holds this long full of the round's XP before it gives way to the track.
        private const int BarHold = 600;
        private const float ShrinkDuration = 0.2f;
        // A reward popup closes on its own after this long, so a player who just watches is not stuck under it.
        private const float RewardAutoClose = 4f;
        private const float RewardFadeIn = 0.15f;
        private const float RewardRaysTurnSeconds = 10f;
        private static readonly Color RewardRaysColor = new(1f, 0.79f, 0.24f, 0.32f);

        private const string ReachedClass = "track__node--reached";
        private const string DoneClass = "track__node--done";

        private readonly VisualElement root;
        private readonly RoundOverViewModel viewModel;
        private readonly bool isTouch;
        private readonly VisualElement bar;
        private readonly Label barFrom;
        private readonly Label barTo;
        private readonly VisualElement barFill;
        private readonly VisualElement levelUp;
        private readonly VisualElement levelBadge;
        private readonly Label levelLabel;
        private readonly Label rankLabel;
        private readonly Label gain;
        private readonly Label total;
        private readonly List<Waypoint> stops = new();
        private readonly List<Segment> segments = new();
        private readonly List<CarPortrait> trackCars = new();
        private readonly LiveText text = new();
        private int shownLevel;
        private CancellationTokenSource animation;
        private string rankClass;

        public RoundOverProgress(VisualElement root, RoundOverViewModel viewModel, bool isTouch)
        {
            this.root = root;
            this.viewModel = viewModel;
            this.isTouch = isTouch;
            this.levelBadge = root.Q<VisualElement>("xp-level");
            this.levelLabel = root.Q<Label>("xp-level-value");
            this.rankLabel = root.Q<Label>("xp-rank");
            this.gain = root.Q<Label>("xp-gain");
            this.total = root.Q<Label>("xp-total");

            var reward = viewModel.XpGain;
            SetLevel(reward.Before.Level);
            text.Set(gain, () => Strings.Format("result.xp_gain", reward.Total));
            text.Set(total, () => Strings.Format("result.xp_total", reward.Before.Xp, reward.Before.Cost));
            text.Set(rankLabel, () => Strings.Format("result.rank_level", PlayerRank.NameOf(shownLevel), shownLevel));
            var track = root.Q<VisualElement>("xp-track");
            BuildTrack(track, reward.Before.Level);

            // The bar of the first step lies over the track, which is still empty then: nothing in the card moves.
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("xp-bar");
            barFrom = BarLevel(bar);
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("xp-bar__line");
            barFill = new VisualElement { pickingMode = PickingMode.Ignore };
            barFill.AddToClassList("xp-bar__fill");
            line.Add(barFill);
            bar.Insert(1, line);
            barTo = BarLevel(bar);
            levelUp = Outlined("", "xp-bar__level-up", out var levelUpLabels);
            foreach (var label in levelUpLabels) {
                text.Set(label, () => Strings.Get("result.level_up"));
            }
            bar.Add(levelUp);
            track.Add(bar);

            SetScale(levelBadge, 0f);
            SetScale(gain, 0f);
            SetScale(bar, 0f);
            SetScale(levelUp, 0f);
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

        public void Dispose()
        {
            Stop();
            text.Dispose();
        }

        public void RefreshColor()
        {
            foreach (var car in trackCars) {
                car.SetColor(viewModel.SelectedColor);
            }
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
                // The car itself in the player's colour, so the stop reads as the car it gives, not a placeholder.
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                portrait.AddToClassList("track__car");
                node.Add(portrait);
                var car = new CarPortrait(portrait);
                car.Set(reward.Car, viewModel.SelectedColor);
                trackCars.Add(car);
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
                var levelCaption = Text("", "track__caption-level");
                text.Set(levelCaption, () => GameText.Level(level));
                caption.Add(levelCaption);
                var nameCaption = Text("", "track__caption-name");
                text.Set(nameCaption, () => reward.IsRank ? Strings.Format("result.rank_stop", PlayerRank.NameOf(level))
                    : reward.Car != null ? GameText.Car(reward.Car) : GameText.Arena(reward.Arena));
                caption.Add(nameCaption);
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
            await FillBar(token);
            await Shrink(bar, token);
            bar.style.display = DisplayStyle.None;
            await Pop(stops[0].Node, token);
            for (var i = 0; i < segments.Count; i++) {
                await Grow(segments[i].Root, token);
                Pop(stops[i + 1].Node, token).Forget();
            }
            await UniTask.Delay((int)(PopDuration * 1000), ignoreTimeScale: true, cancellationToken: token);
            await FillLevels(token);
        }

        /// <summary>
        /// The first two steps: the bar from a level to the next fills with the round's XP, level by level; each level
        /// crossed bumps the badge, says LEVEL UP! and shows what it gives in a popup before the bar starts the next.
        /// </summary>
        private async UniTask FillBar(CancellationToken token)
        {
            var reward = viewModel.XpGain;
            var level = reward.Before.Level;
            var share = (float)reward.Before.Xp / reward.Before.Cost;
            SetBarLevels(level);
            SetBarFill(share);
            await Pop(bar, token);
            while (level < reward.After.Level) {
                await BarTo(share, 1f, token);
                level++;
                SetLevel(level);
                Pop(levelUp, token).Forget();
                await Bounce(levelBadge, token);
                await UniTask.Delay(LevelUpHold, ignoreTimeScale: true, cancellationToken: token);
                await ShowRewards(level, token);
                SetScale(levelUp, 0f);
                share = 0f;
                SetBarLevels(level);
                SetBarFill(0f);
            }

            text.Set(total, () => Strings.Format("result.xp_total", reward.After.Xp, reward.After.Cost));
            await BarTo(share, (float)reward.After.Xp / reward.After.Cost, token);
            await UniTask.Delay(BarHold, ignoreTimeScale: true, cancellationToken: token);
        }

        /// <summary>
        /// The last step: moves the track's position, a level and the share of it earned, level by level from before
        /// the round to after, turning the stops of each level crossed gold. The badge already shows the level reached.
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
                await Reach(level, token);
            }

            await Fill(from, level + (float)reward.After.Xp / reward.After.Cost, token);
        }

        /// <summary>The second step: a popup for each thing the level gives, one after another.</summary>
        private async UniTask ShowRewards(int level, CancellationToken token)
        {
            foreach (var car in viewModel.NewCarsAt(level)) {
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                new CarPortrait(portrait).Set(car, viewModel.SelectedColor);
                var isForSale = viewModel.IsForSale(car);
                await ShowReward(level, Strings.Get("result.new_car_tag"), GameText.Car(car),
                    isForSale ? null : Strings.Get("result.drives_next"), portrait, token, isForSale ? car : null);
            }

            foreach (var arena in viewModel.NewArenasAt(level)) {
                var disc = new VisualElement { pickingMode = PickingMode.Ignore };
                disc.style.backgroundColor = arena.Background;
                await ShowReward(level, Strings.Get("result.new_arena_tag"), GameText.Arena(arena),
                    Strings.Get("result.arena_next"), disc, token);
            }

            if (PlayerRank.TierOf(level) > PlayerRank.TierOf(level - 1)) {
                var badge = HudView.CreateLevelBadge(level, "level-badge--big");
                await ShowReward(level, Strings.Get("result.new_rank_tag"), PlayerRank.NameOf(level), null, badge, token);
            }
        }

        /// <summary>
        /// One reward over the whole screen, as in the design: dimmed, gold rays turning behind a white card with the
        /// level over it, a tag, the reward and its name. A tap anywhere or a few seconds close it; NEXT MATCH by key
        /// still works under it, and leaving the screen takes it away.
        /// </summary>
        /// <para>
        /// A car with a price (<paramref name="forSale"/>) is offered with its BUY button and price while the coins pay
        /// for it; the offer waits for the player instead of closing by itself, and a tap beside it leaves it for later.
        /// </para>
        private async UniTask ShowReward(int level, string tag, string name, string caption, VisualElement art,
            CancellationToken token, CarCatalog.Entry forSale = null)
        {
            var overlay = new VisualElement();
            overlay.AddToClassList("level-reward");
            var rays = new VisualElement { pickingMode = PickingMode.Ignore };
            rays.AddToClassList("level-reward__rays");
            rays.generateVisualContent += context => RoundOverMatch.DrawSunburst(context, RewardRaysColor);
            overlay.Add(rays);

            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("card");
            card.AddToClassList("level-reward__card");
            card.Add(Outlined(Strings.Format("result.level_title", level), "level-reward__title", out _));
            card.Add(Text(tag, "level-reward__tag"));
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.AddToClassList("level-reward__art");
            art.AddToClassList("level-reward__" + (art.ClassListContains("car-portrait") ? "car"
                : art.ClassListContains("level-badge") ? "rank" : "arena"));
            frame.Add(art);
            card.Add(frame);
            card.Add(Text(name, "level-reward__name"));
            if (caption != null) {
                var captionLabel = Text(caption, "caption");
                captionLabel.AddToClassList("level-reward__caption");
                card.Add(captionLabel);
            }

            var isOffer = forSale != null && viewModel.CanAfford(forSale);
            var closeAt = isOffer ? float.MaxValue : RewardAutoClose;
            var isClosed = false;
            if (isOffer) {
                var buy = new Button();
                buy.RemoveFromClassList(Button.ussClassName);
                buy.AddToClassList("pill");
                buy.AddToClassList("level-reward__buy");
                buy.Add(Outlined(Strings.Get("result.buy"), "level-reward__buy-label", out _));
                var price = new VisualElement { pickingMode = PickingMode.Ignore };
                price.AddToClassList("pill");
                price.AddToClassList("level-reward__price");
                price.Add(RoundOverMeta.Coin());
                price.Add(Text(viewModel.PriceOf(forSale).ToString(), "level-reward__price-value"));
                buy.Add(price);
                // The overlay closes on any press: the button keeps its own press.
                buy.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                buy.clicked += () => {
                    if (isClosed || !viewModel.BuyCar(forSale)) {
                        return;
                    }

                    buy.SetEnabled(false);
                    buy.style.display = DisplayStyle.None;
                    card.schedule.Execute(() => isClosed = true).StartingIn(900);
                };
                card.Add(buy);
                viewModel.CarOffered(forSale);
            }

            overlay.Add(card);
            overlay.Add(Text(Strings.Get(isTouch ? "result.tap_continue" : "result.click_continue"), "level-reward__hint"));
            overlay.RegisterCallback<PointerDownEvent>(evt => {
                evt.StopPropagation();
                isClosed = true;
            });

            overlay.style.opacity = 0f;
            SetScale(card, 0f);
            // Inside the screen's own element, which carries the style sheet.
            (root.Q<VisualElement>("round-over") ?? root).Add(overlay);
            try {
                var popping = Pop(card, token);
                var shownAt = Time.realtimeSinceStartup;
                while (!isClosed && Time.realtimeSinceStartup - shownAt < closeAt) {
                    var shown = Time.realtimeSinceStartup - shownAt;
                    rays.style.rotate = new Rotate(shown / RewardRaysTurnSeconds * 360f % 360f);
                    overlay.style.opacity = Mathf.Clamp01(shown / RewardFadeIn);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                await popping;
                await Shrink(card, token);
            }
            finally {
                overlay.RemoveFromHierarchy();
            }
        }

        private void SetBarLevels(int level)
        {
            barFrom.text = level.ToString();
            barTo.text = (level + 1).ToString();
            SetRank(barFrom.parent, level);
            SetRank(barTo.parent, level + 1);
        }

        private static void SetRank(VisualElement badge, int level)
        {
            foreach (var className in new List<string>(badge.GetClasses())) {
                if (className.StartsWith("level-badge--") && className != "level-badge--big") {
                    badge.RemoveFromClassList(className);
                }
            }
            badge.AddToClassList(PlayerRank.ClassOf(level));
        }

        private void SetBarFill(float share) => barFill.style.width = Length.Percent(Mathf.Clamp01(share) * 100f);

        private async UniTask BarTo(float from, float to, CancellationToken token)
        {
            for (var time = 0f; time < BarFillDuration; time += Time.unscaledDeltaTime) {
                var t = time / BarFillDuration;
                SetBarFill(Mathf.Lerp(from, to, 1f - Mathf.Pow(1f - t, 3f)));
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetBarFill(to);
        }

        private static Label BarLevel(VisualElement bar)
        {
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.AddToClassList("level-badge");
            badge.AddToClassList("level-badge--big");
            badge.AddToClassList("xp-bar__level");
            var value = Text("", "level-badge__value");
            badge.Add(value);
            bar.Add(badge);
            return value;
        }

        /// <summary>Text with the outline of the titles: the same text twice, the face over the outline.</summary>
        private static VisualElement Outlined(string value, string className, out Label[] labels)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList("stroke");
            element.AddToClassList(className);
            var outline = Text(value, "display");
            outline.AddToClassList(className + "-text");
            var face = Text(value, "display");
            face.AddToClassList(className + "-text");
            face.AddToClassList("stroke__face");
            element.Add(outline);
            element.Add(face);
            labels = new[] { outline, face };
            return element;
        }

        private static async UniTask Shrink(VisualElement element, CancellationToken token)
        {
            for (var time = 0f; time < ShrinkDuration; time += Time.unscaledDeltaTime) {
                SetScale(element, 1f - time / ShrinkDuration);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            SetScale(element, 0f);
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
        }

        private void SetLevel(int level)
        {
            levelLabel.text = level.ToString();
            shownLevel = level;
            text.Set(rankLabel, () => Strings.Format("result.rank_level", PlayerRank.NameOf(shownLevel), shownLevel));
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
