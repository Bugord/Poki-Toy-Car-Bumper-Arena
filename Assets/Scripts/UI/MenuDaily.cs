using System;
using System.Threading;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The daily gift's popup in the main menu (macket MenuB_Daily): a tilted gold DAILY GIFT!, the days in a row on an
    /// orange tag, the calendar of seven days (coins rising day by day, the last one twice as wide with the mystery hat),
    /// what tomorrow brings, and CLAIM with CLAIM ×2 for a rewarded ad beside it. Upright the calendar is two rows of four
    /// cells, the last two wide; sideways it is one row (<c>menu--wide</c> in ui_menu). Days already taken this week are
    /// pale with a green tick, today's is gold-ringed and lifted. Once claimed it shows the claim and celebrates
    /// it (<see cref="RewardCelebration"/>), and the menu turns to the tasks after; opened again the same day it shows the
    /// tasks with CONTINUE. <see cref="MenuView"/> owns the frame, the keys and when it opens and closes.
    /// <para>
    /// A GIFT | TASKS switch at the top (as the garage's) turns to the day's tasks: the first win of the day, which pays
    /// twice, and the three daily tasks (<see cref="DailyTasks"/>) with their progress and reward, under them when the
    /// next ones come.
    /// </para>
    /// </summary>
    internal sealed class MenuDaily
    {
        private const string DoneClass = "menu__daily-day--done";
        private const string TodayClass = "menu__daily-day--today";
        private const string BigClass = "menu__daily-day--big";
        private const string HatClass = "menu__daily-day--hat";

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);

        private readonly VisualElement sheet;
        private readonly VisualElement tabs;
        private readonly Button giftTab;
        private readonly Button tasksTab;
        private readonly VisualElement giftDot;
        private readonly VisualElement giftPage;
        private readonly VisualElement tasksPage;
        private readonly VisualElement firstWin;
        private readonly Label firstWinBadge;
        private readonly Label firstWinTitle;
        private readonly Label firstWinHint;
        private readonly VisualElement taskList;
        private readonly Label tasksHint;
        private readonly Label[] giftTabLabels;
        private readonly Label[] tasksTabLabels;
        private readonly Label streak;
        private readonly VisualElement days;
        private readonly Label hint;
        private readonly VisualElement claimRow;
        private readonly Button doubleButton;
        private readonly Button claimButton;
        private readonly Button continueButton;
        private Cell[] cells = Array.Empty<Cell>();

        public MenuDaily(VisualElement sheet, Action pressDouble, Action pressClaim, Action pressContinue,
            Action<bool> pressTab)
        {
            this.sheet = sheet;
            sheet.AddToClassList("menu__daily-sheet");

            // GIFT | TASKS: a white pill, the chosen half green, a red dot on GIFT while it waits.
            tabs = Element("menu__daily-tabs");
            tabs.Add(Element("menu__drop"));
            var tabsFace = Element("menu__daily-tabs-face");
            giftTab = Tab(() => pressTab(false), false, out giftTabLabels);
            giftDot = Element("menu__daily-tab-dot");
            giftTab.Add(giftDot);
            tasksTab = Tab(() => pressTab(true), true, out tasksTabLabels);
            tabsFace.Add(giftTab);
            tabsFace.Add(tasksTab);
            tabs.Add(tabsFace);
            sheet.Add(tabs);

            giftPage = Element("menu__daily-page");
            sheet.Add(giftPage);
            streak = Text("menu__daily-streak");
            giftPage.Add(streak);
            days = Element("menu__daily-days");
            giftPage.Add(days);
            hint = Text("menu__daily-hint");
            giftPage.Add(hint);

            // The tasks: the day's first win on an orange card, then the three tasks as the missions' rows.
            tasksPage = Element("menu__daily-page");
            sheet.Add(tasksPage);
            firstWin = Element("menu__daily-first");
            firstWinBadge = Text("menu__daily-first-badge");
            firstWin.Add(firstWinBadge);
            var firstWinText = Element("menu__daily-first-text");
            firstWinTitle = Text("menu__daily-first-title");
            firstWinText.Add(firstWinTitle);
            firstWinHint = Text("menu__daily-first-hint");
            firstWinText.Add(firstWinHint);
            firstWin.Add(firstWinText);
            var firstWinTick = Element("menu__daily-tick");
            firstWinTick.Add(Element("mission__mark"));
            firstWin.Add(firstWinTick);
            tasksPage.Add(firstWin);
            taskList = Element("menu__daily-tasks");
            tasksPage.Add(taskList);
            tasksHint = Text("menu__daily-hint");
            tasksPage.Add(tasksHint);

            claimRow = Element("menu__daily-buttons");
            doubleButton = Button("button--purple", "menu__daily-double", pressDouble);
            doubleButton.Add(RoundOverRewards.PlaySign());
            var doubleLabel = RoundOverProgress.Outlined(Strings.Get("result.claim_x2"), "menu__daily-label",
                out var doubleLabels);
            foreach (var label in doubleLabels) {
                label.AddToClassList("button__label");
            }

            FitText.Fit(doubleLabel, doubleLabels);
            doubleButton.Add(doubleLabel);
            DoubleLabels = doubleLabels;
            var ad = Text("menu__daily-ad");
            ad.text = Strings.Get("result.ad");
            AdTag = ad;
            doubleButton.Add(ad);
            doubleButton.Add(KeyCap("X", "menu__daily-key"));
            claimRow.Add(doubleButton);

            claimButton = Button("button--green", "menu__daily-claim", pressClaim);
            var claimLabel = RoundOverProgress.Outlined(Strings.Get("daily.claim"), "menu__daily-label",
                out var claimLabels);
            foreach (var label in claimLabels) {
                label.AddToClassList("button__label");
            }

            FitText.Fit(claimLabel, claimLabels);
            claimButton.Add(claimLabel);
            ClaimLabels = claimLabels;
            SpaceKey = KeyCap(Strings.Get("common.space"), "menu__daily-key");
            claimButton.Add(SpaceKey);
            claimRow.Add(claimButton);
            giftPage.Add(claimRow);

            continueButton = Button("button--white", "menu__daily-continue", pressContinue);
            var continueLabel = Text("button__label");
            continueLabel.AddToClassList("menu__daily-continue-label");
            continueButton.Add(continueLabel);
            ContinueLabel = continueLabel;
            sheet.Add(continueButton);
        }

        private Label[] DoubleLabels { get; }
        private Label[] ClaimLabels { get; }
        private Label AdTag { get; }
        private VisualElement SpaceKey { get; }
        private Label ContinueLabel { get; }

        /// <summary>The popup's card, which the menu scales to the screen.</summary>
        public VisualElement Sheet => sheet;

        /// <summary>Whether the TASKS half shows, rather than the gift's calendar.</summary>
        public bool IsTasks { get; private set; }

        /// <summary>Turns to the tasks or the gift; <see cref="Fill"/> draws it.</summary>
        public void SetTab(bool isTasks) => IsTasks = isTasks;

        /// <summary>The CLAIM ×2 button, which shakes when its ad came back empty.</summary>
        public VisualElement DoubleButton => doubleButton;

        /// <summary>
        /// Shows the calendar as <paramref name="model"/> has it now: the days taken, today's, what is to come, and the
        /// buttons a gift still to claim or a taken one leaves. Also redraws the words in the language shown.
        /// </summary>
        public void Fill(MenuViewModel model)
        {
            // A build with only one half shows it without the switch.
            IsTasks = model.HasDailyGift ? IsTasks && model.HasDailyTasks : model.HasDailyTasks;
            tabs.style.display = model.HasDailyGift && model.HasDailyTasks ? DisplayStyle.Flex : DisplayStyle.None;
            giftTab.EnableInClassList(TabOnClass, !IsTasks);
            tasksTab.EnableInClassList(TabOnClass, IsTasks);
            giftDot.style.display = model.CanClaimDaily ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var label in giftTabLabels) {
                label.text = Strings.Get("daily.tab_gift");
            }

            foreach (var label in tasksTabLabels) {
                label.text = Strings.Format("daily.tab_tasks", model.DailyTasksDone, model.TodaysTasks.Count);
            }

            giftPage.style.display = IsTasks ? DisplayStyle.None : DisplayStyle.Flex;
            tasksPage.style.display = IsTasks ? DisplayStyle.Flex : DisplayStyle.None;
            ContinueLabel.text = Strings.Get("result.continue");
            if (IsTasks) {
                FillTasks(model);
                continueButton.style.display = DisplayStyle.Flex;
                return;
            }

            var canClaim = model.CanClaimDaily;
            var today = model.DailySlot;
            streak.text = Strings.Format("daily.streak", model.DailyStreak);
            if (cells.Length != model.DailyDays) {
                days.Clear();
                cells = new Cell[model.DailyDays];
                for (var i = 0; i < cells.Length; i++) {
                    cells[i] = new Cell(i == cells.Length - 1);
                    days.Add(cells[i].Root);
                }
            }

            for (var i = 0; i < cells.Length; i++) {
                var cell = cells[i];
                var isToday = i == today;
                var isDone = i < today || (isToday && !canClaim);
                cell.Root.EnableInClassList(TodayClass, isToday);
                cell.Root.EnableInClassList(DoneClass, isDone);
                cell.Caption.text = isToday ? Strings.Get("daily.today") : Strings.Format("daily.day", i + 1);
                var hat = model.DailyHatOf(i);
                if (hat != null) {
                    cell.ShowGift(Strings.Get("daily.mystery"));
                }
                else {
                    cell.ShowCoins(model.DailyCoinsOf(i), CoinsIn(i, cells.Length));
                }
            }

            var next = model.NextDailySlot;
            hint.text = model.DailyHatOf(next) != null
                ? Strings.Get("daily.tomorrow_hat")
                : Strings.Format("daily.tomorrow", model.DailyCoinsOf(next));

            claimRow.style.display = canClaim ? DisplayStyle.Flex : DisplayStyle.None;
            doubleButton.style.display = model.CanDoubleDaily ? DisplayStyle.Flex : DisplayStyle.None;
            continueButton.style.display = canClaim ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var label in DoubleLabels) {
                label.text = Strings.Get("result.claim_x2");
            }

            foreach (var label in ClaimLabels) {
                label.text = Strings.Get("daily.claim");
            }

            AdTag.text = Strings.Get("result.ad");
            SpaceKey.Q<Label>().text = Strings.Get("common.space");
        }

        // The first win of the day (still to come, or taken today) and the day's three tasks, each a row: the check,
        // the task, its progress (DONE! once done) and what it pays.
        private void FillTasks(MenuViewModel model)
        {
            firstWin.style.display = model.IsFirstWinOn ? DisplayStyle.Flex : DisplayStyle.None;
            var isWinTaken = !model.HasFirstWin;
            firstWin.EnableInClassList("menu__daily-first--done", isWinTaken);
            firstWinBadge.text = $"×{model.FirstWinMultiplier}";
            firstWinTitle.text = Strings.Get("daily.first_win");
            firstWinHint.text = isWinTaken ? Strings.Get("daily.first_win_done")
                : Strings.Format("daily.first_win_hint", model.FirstWinMultiplier);

            taskList.Clear();
            foreach (var task in model.TodaysTasks) {
                var definition = task.Definition;
                var row = Element("mission");
                row.AddToClassList("menu__daily-task");
                row.EnableInClassList("mission--done", task.IsDone);
                var check = Element("mission__check");
                check.Add(Element("mission__mark"));
                row.Add(check);
                var text = Text("mission__task");
                text.text = model.MissionText(definition);
                row.Add(text);
                var progress = Text("mission__progress");
                progress.text = task.IsDone ? Strings.Get("daily.task_done") : $"{task.Progress}/{task.Target}";
                row.Add(progress);
                var reward = Element("menu__daily-task-reward");
                var xp = Text("menu__daily-task-xp");
                xp.text = $"+{model.DailyTaskXp(definition)} XP";
                reward.Add(xp);
                var coins = Element("menu__reward");
                coins.Add(RoundOverMeta.Coin());
                var count = Text("menu__reward-count");
                count.text = model.DailyTaskCoins(definition).ToString();
                coins.Add(count);
                reward.Add(coins);
                row.Add(reward);
                taskList.Add(row);
            }

            tasksHint.text = Strings.Format("daily.tasks_next", TimeText(model.DailyTasksTimeLeft));
        }

        /// <summary>The time to midnight as the DAILY tile shows it: hours and minutes, or minutes under an hour.</summary>
        internal static string TimeText(TimeSpan left)
        {
            var hours = (int)left.TotalHours;
            var minutes = Mathf.Max(hours > 0 ? 0 : 1, left.Minutes);
            return hours > 0 ? Strings.Format("daily.time_hm", hours, minutes) : Strings.Format("daily.time_m", minutes);
        }

        private const string TabOnClass = "menu__daily-tab--on";

        private static Button Tab(Action pressed, bool isSplit, out Label[] labels)
        {
            var button = new Button(pressed);
            button.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName);
            button.AddToClassList("menu__daily-tab");
            // A navy line between the halves (USS has no :first-child).
            button.EnableInClassList("menu__daily-tab--split", isSplit);
            button.focusable = false;
            // Two layers, as the garage's tabs: the navy outline under a clean face.
            button.Add(RoundOverProgress.Outlined("", "menu__daily-tab-label", out labels));
            return button;
        }

        /// <summary>
        /// Today's gift was taken: its cell turns done (with the hat drawn in it, if that was the gift), the buttons go,
        /// and the celebration plays over the popup's dim with the stamp over the cell and, for coins,
        /// the coins flying from it into <paramref name="coinCounter"/>.
        /// </summary>
        public void Celebrate(VisualElement overlay, DailyClaim claim, GarageStage stage, VisualElement coinCounter,
            CancellationToken token)
        {
            if (claim.Slot < 0 || claim.Slot >= cells.Length) {
                return;
            }

            var cell = cells[claim.Slot];
            cell.Root.AddToClassList(DoneClass);
            if (claim.Hat != null) {
                cell.ShowHat(claim.Hat, stage, GameText.Topper(claim.Hat));
            }
            else {
                cell.ShowCoins(claim.Coins, CoinsIn(claim.Slot, cells.Length));
            }

            // The buttons go; the menu closes the card once the celebration is over.
            claimRow.style.display = DisplayStyle.None;
            // The stamp goes over the whole calendar: a cell is too narrow for it.
            var stamp = Strings.Get(claim.Hat != null ? "result.equipped" : "daily.claimed");
            RewardCelebration.Play(overlay, sheet, days, stamp, claim.Hat == null ? cell.Art : null,
                claim.Hat == null ? coinCounter : null, token);
        }

        /// <summary>The ad gave nothing: the card shakes and NO AD RIGHT NOW floats over CLAIM ×2.</summary>
        public void Refuse(VisualElement overlay, CancellationToken token) =>
            RewardCelebration.Refuse(overlay, sheet, doubleButton, Strings.Get("garage.no_ad"), token);

        // One coin on the first days, two in the middle, three at the end, as the macket stacks them.
        private static int CoinsIn(int slot, int count) => slot < 2 ? 1 : slot < Math.Min(4, count - 1) ? 2 : 3;

        private static Button Button(string colour, string className, Action pressed)
        {
            var button = new Button(pressed);
            button.RemoveFromClassList(UnityEngine.UIElements.Button.ussClassName);
            button.AddToClassList("button");
            button.AddToClassList(colour);
            button.AddToClassList(className);
            button.focusable = false;
            button.Add(Element("button__face"));
            return button;
        }

        private static VisualElement KeyCap(string key, string className)
        {
            var cap = Element("key");
            cap.AddToClassList(className);
            var keyText = Text("key__text");
            keyText.text = key;
            cap.Add(keyText);
            return cap;
        }

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            if (className != null) {
                element.AddToClassList(className);
            }

            return element;
        }

        private static Label Text(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        /// <summary>A gift box: a red box under a red lid, a gold bow and a navy ribbon, as on the DAILY tile.</summary>
        internal static void DrawGift(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            float X(float x) => r.x + x * r.width;
            float Y(float y) => r.y + y * r.height;
            void Box(Color fill, float x0, float y0, float x1, float y1)
            {
                p.fillColor = fill;
                p.strokeColor = Navy;
                p.lineWidth = r.height * 0.08f;
                p.lineJoin = LineJoin.Round;
                p.BeginPath();
                p.MoveTo(new Vector2(X(x0), Y(y0)));
                p.LineTo(new Vector2(X(x1), Y(y0)));
                p.LineTo(new Vector2(X(x1), Y(y1)));
                p.LineTo(new Vector2(X(x0), Y(y1)));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            // The bow: two gold loops over the lid, a bow tie of two rounded triangles.
            p.fillColor = Gold;
            p.strokeColor = Navy;
            p.lineWidth = r.height * 0.08f;
            p.lineJoin = LineJoin.Round;
            foreach (var side in new[] { -1f, 1f }) {
                p.BeginPath();
                p.MoveTo(new Vector2(X(0.5f), Y(0.34f)));
                p.LineTo(new Vector2(X(0.5f + side * 0.3f), Y(0.08f)));
                p.LineTo(new Vector2(X(0.5f + side * 0.34f), Y(0.34f)));
                p.ClosePath();
                p.Fill();
                p.Stroke();
            }

            Box(Red, 0.17f, 0.54f, 0.83f, 0.92f);
            Box(Red, 0.09f, 0.36f, 0.91f, 0.54f);
            p.strokeColor = Navy;
            p.lineWidth = r.height * 0.08f;
            p.BeginPath();
            p.MoveTo(new Vector2(X(0.5f), Y(0.36f)));
            p.LineTo(new Vector2(X(0.5f), Y(0.92f)));
            p.Stroke();
        }

        /// <summary>One day of the calendar: its caption, its art (coins, the gift box or the hat) and what it gives.</summary>
        private sealed class Cell
        {
            public readonly VisualElement Root;
            public readonly Label Caption;
            public readonly VisualElement Art;
            private readonly Label amount;

            public Cell(bool isBig)
            {
                Root = Element("menu__daily-day");
                Root.EnableInClassList(BigClass, isBig);
                Caption = Text("menu__daily-caption");
                Root.Add(Caption);
                Art = Element("menu__daily-art");
                Root.Add(Art);
                amount = Text("menu__daily-amount");
                Root.Add(amount);
                var tick = Element("menu__daily-tick");
                tick.Add(Element("mission__mark"));
                Root.Add(tick);
            }

            public void ShowCoins(int coins, int count)
            {
                Root.RemoveFromClassList(HatClass);
                Art.Clear();
                var stack = Element("menu__daily-stack");
                stack.AddToClassList($"menu__daily-stack--{count}");
                for (var i = 0; i < count; i++) {
                    var coin = RoundOverMeta.Coin();
                    coin.AddToClassList("menu__daily-coin");
                    coin.AddToClassList($"menu__daily-coin--{i}");
                    stack.Add(coin);
                }

                Art.Add(stack);
                amount.text = coins.ToString();
            }

            public void ShowGift(string text)
            {
                Root.RemoveFromClassList(HatClass);
                Art.Clear();
                var gift = Element("menu__daily-gift");
                gift.generateVisualContent += DrawGift;
                Art.Add(gift);
                amount.text = text;
            }

            public void ShowHat(string id, GarageStage stage, string name)
            {
                // A pale face and the hat at full strength: the purple hat would be lost on the purple day.
                Root.AddToClassList(HatClass);
                Art.Clear();
                VisualElement hat;
                var texture = stage?.ShowToppers(CarTopper.ExclusiveIds);
                if (texture != null) {
                    hat = new Image {
                        image = texture,
                        uv = stage.CellOf(id),
                        scaleMode = ScaleMode.ScaleToFit,
                        pickingMode = PickingMode.Ignore,
                    };
                    hat.AddToClassList("menu__daily-hat");
                }
                else {
                    hat = TopperIcon.Create(id, "menu__daily-hat");
                }

                Art.Add(hat);
                amount.text = name;
            }
        }
    }
}
