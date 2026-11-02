using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The result's goals beside the level card, all saved before the screen opens: the three missions (a done one
    /// ticks gold, shows its reward and turns into the mission that replaces it), the round's coins next to its XP with
    /// the player's coins counting up in the corner, and the win streak under the title (STREAK ×N, or STREAK LOST).
    /// Built in code into the screen's tree; the animation runs on unscaled time and stops with the screen.
    /// </summary>
    internal sealed class RoundOverMeta : IDisposable
    {
        private const int RowsDelay = 250;
        private const int RowStep = 90;
        private const int TickDelay = 500;
        private const int SwapDelay = 1500;
        private const int CoinsDelay = 450;
        private const float CountDuration = 0.8f;

        private const string RowEnterClass = "mission--enter";
        private const string RowDoneClass = "mission--done";
        private const string RowSwapClass = "mission--swap";

        private readonly RoundOverViewModel viewModel;
        private readonly VisualElement missions;
        private readonly List<Row> rows = new();
        private readonly Label coinCount;
        private readonly VisualElement coinGain;
        private readonly Label streak;
        private readonly LiveText text = new();
        private CancellationTokenSource animation;

        public RoundOverMeta(VisualElement root, RoundOverViewModel viewModel)
        {
            this.viewModel = viewModel;

            // The player's coins in the corner, left of the gear: what the garage's toppers are bought with.
            CoinPill = new VisualElement { name = "coin-pill", pickingMode = PickingMode.Ignore };
            CoinPill.AddToClassList("pill");
            CoinPill.AddToClassList("coin-pill");
            CoinPill.Add(Coin());
            coinCount = Text(viewModel.Coins.Before.ToString(), "coin-pill__count");
            viewModel.CarBought += OnCarBought;
            CoinPill.Add(coinCount);
            // Inside the screen's own element, which carries the style sheet.
            (root.Q<VisualElement>("round-over") ?? root).Add(CoinPill);

            // The round's coins beside its XP on the level card.
            coinGain = new VisualElement { pickingMode = PickingMode.Ignore };
            coinGain.AddToClassList("coin-gain");
            coinGain.Add(Coin());
            coinGain.Add(Text($"+{viewModel.Coins.Total}", "coin-gain__value"));
            var head = root.Q<VisualElement>(className: "level-card__head");
            head.Insert(Mathf.Min(2, head.childCount), coinGain);

            streak = Text("", "best");
            streak.AddToClassList("round-over__streak");
            var change = viewModel.Streak;
            // REVENGE! has its own sticker there and starts a streak by itself: one sticker under the title is enough.
            var isShown = !viewModel.RevengeTaken && (change.IsStreak || change.IsLost);
            streak.EnableInClassList("best--shown", isShown);
            streak.EnableInClassList("best--new", isShown && !change.IsLost);
            streak.EnableInClassList("round-over__streak--lost", change.IsLost);
            text.Set(streak, () => change.IsLost ? Strings.Get("result.streak_lost") :
                Strings.Format("result.streak", change.After));
            var revenge = root.Q<Label>("revenge");
            revenge.parent.Insert(revenge.parent.IndexOf(revenge) + 1, streak);

            missions = new VisualElement { name = "missions", pickingMode = PickingMode.Ignore };
            missions.AddToClassList("card");
            missions.AddToClassList("missions");
            var bottom = root.Q<VisualElement>(className: "round-over__bottom");
            bottom.parent.Insert(bottom.parent.IndexOf(bottom), missions);
            if (viewModel.Missions == null) {
                missions.style.display = DisplayStyle.None;
                return;
            }

            // On the screen's root beside round-over--landscape and --desktop, which the style sheet pairs it with.
            root.AddToClassList("round-over--meta");
            var title = Text("", "missions__title");
            text.Set(title, () => Strings.Get("mission.title"));
            missions.Add(title);
            foreach (var line in viewModel.Missions.Lines) {
                rows.Add(new Row(this, line));
            }

            // As wide as the level card it sits on, or the whole bottom row beside the buttons on a wide desktop screen.
            var card = root.Q<VisualElement>("level-card");
            var actions = root.Q<VisualElement>(className: "round-over__actions");
            // Beside the podium (landscape on a touch screen) the style sheet sizes it.
            bottom.RegisterCallback<GeometryChangedEvent>(_ => {
                var isRow = bottom.resolvedStyle.flexDirection == FlexDirection.Row;
                missions.style.width = missions.resolvedStyle.position == Position.Absolute
                    ? new StyleLength(StyleKeyword.Null)
                    : new StyleLength(isRow ? actions.layout.xMax - card.layout.xMin : card.layout.width);
            });
        }

        /// <summary>The coin counter in the screen's corner, which the view keeps out of the safe area.</summary>
        public VisualElement CoinPill { get; }

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
            viewModel.CarBought -= OnCarBought;
            text.Dispose();
        }

        private async UniTaskVoid Animate(CancellationToken token)
        {
            try {
                CountCoins(token).Forget();
                await UniTask.Delay(RowsDelay, ignoreTimeScale: true, cancellationToken: token);
                foreach (var row in rows) {
                    row.Root.RemoveFromClassList(RowEnterClass);
                    await UniTask.Delay(RowStep, ignoreTimeScale: true, cancellationToken: token);
                }

                await UniTask.Delay(TickDelay, ignoreTimeScale: true, cancellationToken: token);
                foreach (var row in rows) {
                    row.Tick();
                }

                await UniTask.Delay(SwapDelay, ignoreTimeScale: true, cancellationToken: token);
                foreach (var row in rows) {
                    row.Swap(token).Forget();
                }
            }
            catch (OperationCanceledException) {
            }
        }

        private void OnCarBought()
        {
            coinCount.text = viewModel.CoinsNow.ToString();
            CoinPill.AddToClassList("coin-pill--bump");
            CoinPill.schedule.Execute(() => CoinPill.RemoveFromClassList("coin-pill--bump")).StartingIn(150);
        }

        private async UniTaskVoid CountCoins(CancellationToken token)
        {
            var coins = viewModel.Coins;
            coinGain.AddToClassList("coin-gain--enter");
            if (await UniTask.Delay(CoinsDelay, ignoreTimeScale: true, cancellationToken: token)
                    .SuppressCancellationThrow()) {
                return;
            }

            coinGain.RemoveFromClassList("coin-gain--enter");
            var start = Time.unscaledTime;
            while (!token.IsCancellationRequested) {
                var share = Mathf.Clamp01((Time.unscaledTime - start) / CountDuration);
                // Towards the coins now: a car bought from a level's popup meanwhile is already taken off.
                coinCount.text = Mathf.RoundToInt(Mathf.Lerp(coins.Before, viewModel.CoinsNow, share)).ToString();
                if (share >= 1f) {
                    CoinPill.AddToClassList("coin-pill--bump");
                    CoinPill.schedule.Execute(() => CoinPill.RemoveFromClassList("coin-pill--bump")).StartingIn(150);
                    return;
                }

                if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) {
                    return;
                }
            }
        }

        /// <summary>A gold coin of elements: a disc with a navy ring and a lighter inner disc.</summary>
        public static VisualElement Coin()
        {
            var coin = new VisualElement { pickingMode = PickingMode.Ignore };
            coin.AddToClassList("coin");
            var inner = new VisualElement { pickingMode = PickingMode.Ignore };
            inner.AddToClassList("coin__inner");
            coin.Add(inner);
            return coin;
        }

        private static Label Text(string value, string className)
        {
            var label = new Label(value) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        /// <summary>One slot: the check, the task and its progress, or once done the reward and then the next task.</summary>
        private sealed class Row
        {
            private readonly RoundOverMeta owner;
            private readonly MissionLine line;
            private readonly Label task;
            private readonly Label progress;

            public Row(RoundOverMeta owner, MissionLine line)
            {
                this.owner = owner;
                this.line = line;
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("mission");
                Root.AddToClassList(RowEnterClass);
                var check = new VisualElement { pickingMode = PickingMode.Ignore };
                check.AddToClassList("mission__check");
                var mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList("mission__mark");
                check.Add(mark);
                Root.Add(check);
                task = Text("", "mission__task");
                Root.Add(task);
                progress = Text("", "mission__progress");
                Root.Add(progress);
                owner.missions.Add(Root);
                var definition = line.Definition;
                owner.text.Set(task, () => owner.viewModel.MissionText(definition));
                // The progress as it was before this round; Tick moves it to after.
                progress.text = $"{line.Before}/{definition.Target}";
            }

            public VisualElement Root { get; }

            public void Tick()
            {
                if (!line.IsDone) {
                    progress.text = $"{line.After}/{line.Definition.Target}";
                    return;
                }

                Root.AddToClassList(RowDoneClass);
                progress.text = $"+{owner.viewModel.MissionXp(line.Definition)} XP";
            }

            /// <summary>A done mission gives way to the one that replaces it.</summary>
            public async UniTaskVoid Swap(CancellationToken token)
            {
                if (!line.IsDone || line.Next == null) {
                    return;
                }

                Root.AddToClassList(RowSwapClass);
                if (await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow()) {
                    return;
                }

                var next = line.Next.Definition;
                owner.text.Set(task, () => owner.viewModel.MissionText(next));
                progress.text = $"{line.Next.Progress}/{next.Target}";
                Root.RemoveFromClassList(RowDoneClass);
                Root.RemoveFromClassList(RowSwapClass);
            }
        }
    }
}
