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
    /// The daily gift's popup (macket MenuB_Daily), in the main menu and over a visit's first result: a tilted gold DAILY GIFT!, the days in a row on an
    /// orange tag, the calendar of seven days (coins rising day by day, the last one twice as wide with the mystery hat),
    /// what tomorrow brings, and CLAIM with CLAIM ×2 for a rewarded ad beside it. Upright the calendar is two rows of four
    /// cells, the last two wide; sideways it is one row (<c>menu--wide</c> in ui_menu). Days already taken this week are
    /// pale with a green tick, today's is gold-ringed and lifted. Once claimed it shows the claim and celebrates
    /// it (<see cref="RewardCelebration"/>); the menu then turns to the tasks, the result closes it. Opened again the
    /// same day the menu shows the tasks with CONTINUE. The screen it is on (<see cref="MenuView"/>,
    /// <see cref="ResultDaily"/>) owns the frame, the keys and when it opens and closes.
    /// <para>
    /// In the menu a GIFT | TASKS switch at the top (as the garage's) turns to the day's tasks: the first win of the
    /// day, which pays twice, and the three daily tasks (<see cref="DailyTasks"/>) with their progress and reward, under
    /// them when the next ones come. Over the result the popup is the gift alone, without the switch.
    /// </para>
    /// </summary>
    internal sealed class MenuDaily
    {
        private const string DoneClass = "menu__daily-day--done";
        private const string TodayClass = "menu__daily-day--today";
        private const string BigClass = "menu__daily-day--big";
        private const string HatClass = "menu__daily-day--hat";

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
            Action<bool> pressTab = null)
        {
            this.sheet = sheet;
            sheet.AddToClassList("menu__daily-sheet");

            // GIFT | TASKS: a white pill, the chosen half green, a red dot on GIFT while it waits.
            tabs = Element("menu__daily-tabs");
            tabs.Add(Element("menu__drop"));
            var tabsFace = Element("menu__daily-tabs-face");
            giftTab = Tab(() => pressTab?.Invoke(false), false, out giftTabLabels);
            giftDot = Element("menu__daily-tab-dot");
            giftTab.Add(giftDot);
            tasksTab = Tab(() => pressTab?.Invoke(true), true, out tasksTabLabels);
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
            var doubleLabel = StrokeText.Outlined(Strings.Get("daily.claim"), "menu__daily-label",
                out var doubleLabels);
            foreach (var label in doubleLabels) {
                label.AddToClassList("button__label");
            }

            FitText.Fit(doubleLabel, doubleLabels);
            doubleButton.Add(doubleLabel);
            DoubleLabels = doubleLabels;
            DoubleAmount = Amount(doubleButton, out var doubleAmounts);
            DoubleAmounts = doubleAmounts;
            var ad = Text("menu__daily-ad");
            ad.text = Strings.Get("result.ad");
            AdTag = ad;
            doubleButton.Add(ad);
            doubleButton.Add(KeyCap("X", "menu__daily-key"));
            claimRow.Add(doubleButton);

            claimButton = Button("button--green", "menu__daily-claim", pressClaim);
            var claimLabel = StrokeText.Outlined(Strings.Get("daily.claim"), "menu__daily-label",
                out var claimLabels);
            foreach (var label in claimLabels) {
                label.AddToClassList("button__label");
            }

            FitText.Fit(claimLabel, claimLabels);
            claimButton.Add(claimLabel);
            ClaimLabels = claimLabels;
            ClaimAmount = Amount(claimButton, out var claimAmounts);
            ClaimAmounts = claimAmounts;
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
        private VisualElement DoubleAmount { get; }
        private Label[] DoubleAmounts { get; }
        private VisualElement ClaimAmount { get; }
        private Label[] ClaimAmounts { get; }
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
        /// Shows the half chosen as it is now: the calendar as <paramref name="gift"/> has it (the days taken, today's,
        /// what is to come, and the buttons a gift still to claim or a taken one leaves), or the day's tasks from
        /// <paramref name="menu"/>. Without <paramref name="menu"/> (over the result) it is the calendar alone. Also
        /// redraws the words in the language shown.
        /// </summary>
        public void Fill(DailyGift gift, MenuViewModel menu = null)
        {
            // A popup with only one half shows it without the switch.
            var hasTasks = menu is { HasDailyTasks: true };
            var hasGift = gift.IsEnabled;
            IsTasks = hasGift ? IsTasks && hasTasks : hasTasks;
            tabs.style.display = hasGift && hasTasks ? DisplayStyle.Flex : DisplayStyle.None;
            giftTab.EnableInClassList(TabOnClass, !IsTasks);
            tasksTab.EnableInClassList(TabOnClass, IsTasks);
            giftDot.style.display = gift.CanClaim ? DisplayStyle.Flex : DisplayStyle.None;
            StrokeText.SetText(giftTabLabels, Strings.Get("daily.tab_gift"));
            if (hasTasks) {
                StrokeText.SetText(tasksTabLabels,
                    Strings.Format("daily.tab_tasks", menu.DailyTasksDone, menu.TodaysTasks.Count));
            }

            giftPage.style.display = IsTasks ? DisplayStyle.None : DisplayStyle.Flex;
            tasksPage.style.display = IsTasks ? DisplayStyle.Flex : DisplayStyle.None;
            ContinueLabel.text = Strings.Get("result.continue");
            if (IsTasks) {
                FillTasks(menu);
                continueButton.style.display = DisplayStyle.Flex;
                return;
            }

            var canClaim = gift.CanClaim;
            var today = gift.Slot;
            streak.text = Strings.Format("daily.streak", today + 1);
            if (cells.Length != gift.Days) {
                days.Clear();
                cells = new Cell[gift.Days];
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
                var hat = gift.HatOf(i);
                if (hat != null) {
                    cell.ShowGift(Strings.Get("daily.mystery"));
                }
                else {
                    cell.ShowCoins(gift.CoinsOf(i), PileOf(i, cells.Length));
                }
            }

            ShowTomorrow(gift, !canClaim);

            claimRow.style.display = canClaim ? DisplayStyle.Flex : DisplayStyle.None;
            doubleButton.style.display = gift.CanDouble ? DisplayStyle.Flex : DisplayStyle.None;
            continueButton.style.display = canClaim ? DisplayStyle.None : DisplayStyle.Flex;
            foreach (var label in DoubleLabels) {
                label.text = Strings.Get("daily.claim");
            }

            foreach (var label in ClaimLabels) {
                label.text = Strings.Get("daily.claim");
            }

            // The amounts on the buttons, CLAIM ×2's already doubled: what the ad is worth is plain before it plays.
            // A hat has no amount.
            var coins = gift.HatOf(today) == null ? gift.CoinsOf(today) : 0;
            SetAmount(ClaimAmount, ClaimAmounts, coins);
            SetAmount(DoubleAmount, DoubleAmounts, coins * gift.AdMultiplier);

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
                // As the missions popup's rows: the task's sign (gold with a tick once done), the task over its XP and
                // coins, and the count with its bar.
                var definition = task.Definition;
                var row = Element("menu__mission-row");
                row.AddToClassList("menu__daily-task");
                row.EnableInClassList("menu__daily-task--done", task.IsDone);
                var icon = MissionLook.IconOf(definition);
                icon.Set(definition, task.IsDone);
                row.Add(icon);
                var body = Element("menu__mission-row-body");
                var text = Text("menu__mission-row-task");
                text.text = model.MissionText(definition);
                body.Add(text);
                body.Add(MissionLook.Reward(model.DailyTaskXp(definition), model.DailyTaskCoins(definition)));
                row.Add(body);
                var progress = MissionLook.ProgressOf();
                progress.Set(task.IsDone ? task.Target : task.Progress, task.Target);
                row.Add(progress);
                taskList.Add(row);
            }

            // USS has no :last-child, so the last row drops its gap under it by a class.
            if (taskList.childCount > 0) {
                taskList[taskList.childCount - 1].AddToClassList("menu__mission-row--last");
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
            // As the garage's tabs: the face over its outline and drop (StrokeText).
            button.Add(StrokeText.Outlined("", "menu__daily-tab-label", out labels));
            return button;
        }

        /// <summary>
        /// Today's gift was taken: its cell turns done (with the hat drawn in it, if that was the gift), the buttons go,
        /// and the celebration plays over the popup's dim with the stamp over the cell and, for coins,
        /// the coins flying from it into <paramref name="coinCounter"/>.
        /// </summary>
        public void Celebrate(VisualElement overlay, DailyGift gift, DailyClaim claim, GarageStage stage,
            VisualElement coinCounter, CancellationToken token)
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
                cell.ShowCoins(claim.Coins, PileOf(claim.Slot, cells.Length));
            }

            // The buttons go, and the line under the calendar says what tomorrow brings; the menu closes the card once
            // the celebration is over.
            claimRow.style.display = DisplayStyle.None;
            ShowTomorrow(gift, true);
            // The stamp goes over the whole calendar: a cell is too narrow for it.
            var stamp = Strings.Get(claim.Hat != null ? "result.equipped" : "daily.claimed");
            RewardCelebration.Play(overlay, sheet, days, cell.Root, stamp, claim.Hat == null ? cell.Art : null,
                claim.Hat == null ? coinCounter : null, token);
        }

        /// <summary>The ad gave nothing: the card shakes and NO AD RIGHT NOW floats over CLAIM ×2.</summary>
        public void Refuse(VisualElement overlay, CancellationToken token) =>
            RewardCelebration.Refuse(overlay, sheet, doubleButton, Strings.Get("garage.no_ad"), token);

        private void ShowTomorrow(DailyGift gift, bool isShown)
        {
            hint.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
            var next = gift.NextSlot;
            hint.text = gift.HatOf(next) != null
                ? Strings.Get("daily.tomorrow_hat")
                : Strings.Format("daily.tomorrow", gift.CoinsOf(next));
        }

        // A coin and an outlined amount after a button's CLAIM.
        private static VisualElement Amount(VisualElement button, out Label[] labels)
        {
            var amount = Element("menu__daily-sum");
            var coin = RoundOverMeta.Coin();
            coin.AddToClassList("menu__daily-sum-coin");
            amount.Add(coin);
            amount.Add(StrokeText.Outlined("", "menu__daily-label", out labels));
            foreach (var label in labels) {
                label.AddToClassList("button__label");
            }

            button.Add(amount);
            return amount;
        }

        private static void SetAmount(VisualElement amount, Label[] labels, int coins)
        {
            amount.style.display = coins > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var label in labels) {
                label.text = coins.ToString();
            }
        }

        // The pile a day's coins are drawn as (1 to 4, sprite_icon_coins_1 to 4): the first day the smallest, the last coin days
        // (and a seventh day paid in coins) the biggest heap, the days between two sizes in between.
        private static int PileOf(int slot, int count)
        {
            if (slot <= 0) {
                return 1;
            }

            if (slot >= count - 2) {
                return 4;
            }

            var between = Math.Max(1, count - 3);
            return 2 + Math.Min(1, (slot - 1) * 2 / between);
        }

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

            public void ShowCoins(int coins, int size)
            {
                Root.RemoveFromClassList(HatClass);
                // A painted pile that grows with the amount (sprite_icon_coins_1 to 4); one picture, so a day taken
                // goes pale through the art's opacity like the gift.
                Art.Clear();
                var pile = Element("menu__daily-pile");
                pile.AddToClassList($"menu__daily-pile--{Mathf.Clamp(size, 1, 4)}");
                Art.Add(pile);
                amount.text = coins.ToString();
            }

            public void ShowGift(string text)
            {
                Root.RemoveFromClassList(HatClass);
                Art.Clear();
                // The gift of the menu's DAILY tile (sprite_icon_daily).
                Art.Add(Element("menu__daily-gift"));
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
