using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The result's goals beside the level card, all saved before the screen opens: the three missions (a done one
    /// ticks gold, shows its reward and turns into the mission that replaces it), the player's coins counting up in the
    /// corner (again after CLAIM ×2), and the win streak under the title (STREAK ×N, or STREAK LOST).
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
        // The daily gift's coins land in the corner about this long after its claim.
        private const int GiftDelay = 600;

        private const string RowEnterClass = "mission--enter";
        private const string RowDoneClass = "mission--done";
        private const string RowSwapClass = "mission--swap";

        private readonly RoundOverViewModel viewModel;
        private readonly VisualElement missions;
        private readonly List<Row> rows = new();
        private readonly Label coinCount;
        private int shownCoins;
        private CancellationTokenSource recount;
        private readonly Label streak;
        private readonly LiveText text = new();
        private CancellationTokenSource animation;

        public RoundOverMeta(VisualElement root, RoundOverViewModel viewModel)
        {
            this.viewModel = viewModel;

            // The player's coins in the corner, left of the gear: what the garage's toppers are bought with. The menu's
            // pill (its drop a layer of its own, its colour inside the border), as tall as the gear beside it.
            CoinPill = new VisualElement { name = "coin-pill", pickingMode = PickingMode.Ignore };
            CoinPill.AddToClassList("coin-pill");
            CoinPill.AddToClassList("menu__coins");
            CoinPill.Add(new VisualElement { pickingMode = PickingMode.Ignore });
            CoinPill[0].AddToClassList("menu__coins-drop");
            var face = MenuView.Face("menu__coins-face", "menu__coins-fill");
            face.pickingMode = PickingMode.Ignore;
            face[0].pickingMode = PickingMode.Ignore;
            face.Add(Coin());
            coinCount = Text(viewModel.Coins.Before.ToString(), "menu__coins-count");
            viewModel.CarBought += OnCarBought;
            face.Add(coinCount);
            CoinPill.Add(face);
            // Inside the screen's own element, which carries the style sheet.
            (root.Q<VisualElement>("round-over") ?? root).Add(CoinPill);

            shownCoins = viewModel.Coins.Before;

            streak = Text("", "best");
            streak.AddToClassList("round-over__streak");
            var change = viewModel.Streak;
            // REVENGE! has its own sticker there and starts a streak by itself: one sticker under the title is enough.
            // After a knock-out the podium already says REVENGE NEXT ROUND, which is what a lost streak leads to.
            var isShown = !viewModel.RevengeTaken &&
                (change.IsStreak || change.IsLost && !(viewModel.IsPodiumRival && viewModel.Podium != null));
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

        /// <summary>CLAIM ×2 paid: the corner counts up from what it shows to the coins now.</summary>
        public void Recount(CancellationToken token)
        {
            recount?.Cancel();
            recount?.Dispose();
            recount = CancellationTokenSource.CreateLinkedTokenSource(token);
            CountCoins(0, recount.Token).Forget();
        }

        /// <summary>
        /// The daily gift's coins, flown into the corner before the round's count: it counts them up once they land and
        /// the round's count goes on from there.
        /// </summary>
        public void CountGift(int coins, CancellationToken token)
        {
            recount?.Cancel();
            recount?.Dispose();
            recount = CancellationTokenSource.CreateLinkedTokenSource(token);
            CountCoins(GiftDelay, recount.Token, shownCoins + coins).Forget();
        }

        public void Dispose()
        {
            Stop();
            recount?.Cancel();
            recount?.Dispose();
            viewModel.CarBought -= OnCarBought;
            text.Dispose();
        }

        private async UniTaskVoid Animate(CancellationToken token)
        {
            try {
                CountCoins(CoinsDelay, token).Forget();
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
            shownCoins = viewModel.CoinsNow;
            coinCount.text = shownCoins.ToString();
            CoinPill.AddToClassList("coin-pill--bump");
            CoinPill.schedule.Execute(() => CoinPill.RemoveFromClassList("coin-pill--bump")).StartingIn(150);
        }

        private async UniTaskVoid CountCoins(int delay, CancellationToken token, int target = -1)
        {
            if (await UniTask.Delay(delay, ignoreTimeScale: true, cancellationToken: token)
                    .SuppressCancellationThrow()) {
                return;
            }

            var from = shownCoins;
            var start = Time.unscaledTime;
            while (!token.IsCancellationRequested) {
                var share = Mathf.Clamp01((Time.unscaledTime - start) / CountDuration);
                // Towards the coins now: a car bought from a level's popup meanwhile is already taken off.
                shownCoins = Mathf.RoundToInt(Mathf.Lerp(from, target < 0 ? viewModel.CoinsNow : target, share));
                coinCount.text = shownCoins.ToString();
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

        /// <summary>
        /// One slot: the task's sign, the task, its count with a thin bar (more than one step), and the coins it pays;
        /// once done the sign turns into a gold tick, the XP joins the coins, and then the next task takes the row.
        /// </summary>
        private sealed class Row
        {
            private readonly RoundOverMeta owner;
            private readonly MissionLine line;
            private readonly MissionLook.Icon icon;
            private readonly Label task;
            private readonly MissionLook.Progress progress;
            private readonly VisualElement reward;

            public Row(RoundOverMeta owner, MissionLine line)
            {
                this.owner = owner;
                this.line = line;
                Root = new VisualElement { pickingMode = PickingMode.Ignore };
                Root.AddToClassList("mission");
                Root.AddToClassList(RowEnterClass);
                var definition = line.Definition;
                icon = MissionLook.IconOf(definition);
                Root.Add(icon);
                task = Text("", "mission__task");
                Root.Add(task);
                progress = MissionLook.ProgressOf();
                Root.Add(progress);
                reward = new VisualElement { pickingMode = PickingMode.Ignore };
                reward.AddToClassList("mission__reward");
                Root.Add(reward);
                owner.missions.Add(Root);
                owner.text.Set(task, () => owner.viewModel.MissionText(definition));
                // The progress as it was before this round; Tick moves it to after.
                progress.Set(line.Before, definition.Target);
                ShowReward(definition, false);
            }

            public VisualElement Root { get; }

            public void Tick()
            {
                if (!line.IsDone) {
                    progress.Set(line.After, line.Definition.Target);
                    return;
                }

                Root.AddToClassList(RowDoneClass);
                icon.Set(line.Definition, true);
                progress.style.display = DisplayStyle.None;
                ShowReward(line.Definition, true);
            }

            // The coins a mission pays, and once it is done the XP it paid beside them.
            private void ShowReward(GameTuning.MissionDefinition definition, bool isDone)
            {
                reward.Clear();
                reward.Add(MissionLook.Reward(owner.viewModel.MissionXp(definition),
                    owner.viewModel.MissionCoins(definition), isDone));
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
                icon.Set(next, false);
                progress.Set(line.Next.Progress, next.Target);
                ShowReward(next, false);
                Root.RemoveFromClassList(RowDoneClass);
                Root.RemoveFromClassList(RowSwapClass);
            }
        }
    }
}
