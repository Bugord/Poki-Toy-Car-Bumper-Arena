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
    /// it (<see cref="RewardCelebration"/>), and the menu closes it after; opened again the same day it shows the
    /// calendar with CONTINUE. The screen it is on (<see cref="MenuView"/>, <see cref="ResultDaily"/>) owns the frame, the
    /// keys and when it opens and closes.
    /// </summary>
    internal sealed class MenuDaily
    {
        private const string DoneClass = "menu__daily-day--done";
        private const string TodayClass = "menu__daily-day--today";
        private const string BigClass = "menu__daily-day--big";
        private const string HatClass = "menu__daily-day--hat";
        private const string PileArtClass = "menu__daily-art--pile";

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Gold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color Red = new Color32(0xff, 0x4d, 0x4d, 0xff);

        private readonly VisualElement sheet;
        private readonly Label streak;
        private readonly VisualElement days;
        private readonly Label hint;
        private readonly VisualElement claimRow;
        private readonly Button doubleButton;
        private readonly Button claimButton;
        private readonly Button continueButton;
        private Cell[] cells = Array.Empty<Cell>();

        public MenuDaily(VisualElement sheet, Action pressDouble, Action pressClaim, Action pressContinue)
        {
            this.sheet = sheet;
            sheet.AddToClassList("menu__daily-sheet");

            streak = Text("menu__daily-streak");
            sheet.Add(streak);
            days = Element("menu__daily-days");
            sheet.Add(days);
            hint = Text("menu__daily-hint");
            sheet.Add(hint);

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
            sheet.Add(claimRow);

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

        /// <summary>The CLAIM ×2 button, which shakes when its ad came back empty.</summary>
        public VisualElement DoubleButton => doubleButton;

        /// <summary>
        /// Shows the calendar as <paramref name="gift"/> has it now: the days taken, today's, what is to come, and the
        /// buttons a gift still to claim or a taken one leaves. Also redraws the words in the language shown.
        /// </summary>
        public void Fill(DailyGift gift)
        {
            var canClaim = gift.CanClaim;
            var today = gift.Slot;
            streak.text = Strings.Format("daily.streak", gift.Streak);
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
            ContinueLabel.text = Strings.Get("result.continue");
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
            RewardCelebration.Play(overlay, sheet, days, stamp, claim.Hat == null ? cell.Art : null,
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

        // The pile a day's coins are drawn as (1 to 4, see CoinPile): the first day the smallest, the last coin days
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
            private CoinPile pile;

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
                // The pile draws itself pale on a day taken (CoinPile), not through the art's opacity: overlapping
                // coins under opacity would show each other's outlines through.
                Art.AddToClassList(PileArtClass);
                if (pile == null || pile.Element.parent != Art) {
                    Art.Clear();
                    pile = new CoinPile(Root);
                    Art.Add(pile.Element);
                }

                pile.Show(size);
                amount.text = coins.ToString();
            }

            public void ShowGift(string text)
            {
                Root.RemoveFromClassList(HatClass);
                Art.RemoveFromClassList(PileArtClass);
                pile = null;
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
                Art.RemoveFromClassList(PileArtClass);
                pile = null;
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

        /// <summary>
        /// A day's coins as a pile that grows with the amount: a short stack and a coin beside it on the first day,
        /// then two and three stacks, and a heap of stacks with a coin standing in front on the last coin days. Each
        /// coin lies flat, seen from above at an angle: a gold top with a pale disc, a darker gold edge, a navy
        /// outline, as the coins of <see cref="RoundOverMeta.Coin"/>. Every size is drawn at the same scale, so a
        /// bigger pile is bigger on screen. On a day taken it is drawn pale towards the cell's face.
        /// </summary>
        private sealed class CoinPile
        {
            // The design box the piles are laid out in, scaled to fit the element and stood on its bottom.
            private const float BoxWidth = 160f;
            private const float BoxHeight = 120f;
            private const float CoinX = 25f;
            private const float CoinY = 11f;
            private const float Thickness = 9f;
            private const float Line = 5f;
            private const float Kappa = 0.5523f;
            private const float PaleShare = 0.55f;

            private static readonly Color Face = new Color32(0xff, 0xc9, 0x3c, 0xff);
            private static readonly Color Light = new Color32(0xff, 0xe3, 0x9a, 0xff);
            private static readonly Color Edge = new Color32(0xe5, 0x9a, 0x12, 0xff);

            // Each pile's stacks back to front: centre x, the bottom of the lowest coin, how many coins.
            private static readonly Vector3[][] Stacks = {
                new[] { new Vector3(66f, 110f, 2f), new Vector3(100f, 117f, 1f) },
                new[] { new Vector3(58f, 106f, 3f), new Vector3(102f, 108f, 2f), new Vector3(80f, 118f, 1f) },
                new[] {
                    new Vector3(80f, 100f, 5f), new Vector3(46f, 109f, 3f), new Vector3(114f, 109f, 4f),
                    new Vector3(80f, 118f, 2f),
                },
                new[] {
                    new Vector3(102f, 96f, 7f), new Vector3(62f, 98f, 6f), new Vector3(34f, 108f, 3f),
                    new Vector3(128f, 107f, 4f), new Vector3(80f, 118f, 3f),
                },
            };

            // A coin standing up in front of the biggest piles: centre and radius (0 for none).
            private static readonly Vector3[] Standing = {
                Vector3.zero, Vector3.zero, Vector3.zero, new Vector3(126f, 96f, 20f),
            };

            private readonly VisualElement cell;
            private int size = 1;

            public CoinPile(VisualElement cell)
            {
                this.cell = cell;
                Element = new VisualElement { pickingMode = PickingMode.Ignore };
                Element.AddToClassList("menu__daily-pile");
                Element.generateVisualContent += Draw;
            }

            public VisualElement Element { get; }

            public void Show(int pileSize)
            {
                size = Mathf.Clamp(pileSize, 1, Stacks.Length);
                Element.MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                var r = Element.contentRect;
                var scale = Mathf.Min(r.width / BoxWidth, r.height / BoxHeight);
                if (scale <= 0f) {
                    return;
                }

                var origin = new Vector2(r.x + (r.width - BoxWidth * scale) * 0.5f, r.yMax - BoxHeight * scale);
                var isPale = cell.ClassListContains(DoneClass);
                var behind = cell.resolvedStyle.backgroundColor;
                Color Tint(Color colour) => isPale ? Color.Lerp(behind, colour, PaleShare) : colour;
                var navy = Tint(Navy);
                var face = Tint(Face);
                var light = Tint(Light);
                var edge = Tint(Edge);

                var p = context.painter2D;
                p.lineWidth = Line * scale;
                p.lineJoin = LineJoin.Round;
                p.strokeColor = navy;
                Vector2 At(float x, float y) => origin + new Vector2(x, y) * scale;

                foreach (var stack in Stacks[size - 1]) {
                    var count = (int)stack.z;
                    // The lowest coin's top sits a coin's edge and half its top above the stack's bottom.
                    var top = stack.y - Thickness - CoinY;
                    for (var i = 0; i < count; i++) {
                        Coin(stack.x, top - i * Thickness, i == count - 1);
                    }
                }

                var standing = Standing[size - 1];
                if (standing.z > 0f) {
                    Upright(standing.x, standing.y, standing.z);
                }

                // A coin lying flat with its top at (x, y): its silhouette in the edge's colour, then its top face
                // if no coin lies on it.
                void Coin(float x, float y, bool hasFace)
                {
                    p.fillColor = edge;
                    p.BeginPath();
                    p.MoveTo(At(x - CoinX, y));
                    p.LineTo(At(x - CoinX, y + Thickness));
                    p.BezierCurveTo(At(x - CoinX, y + Thickness + Kappa * CoinY),
                        At(x - Kappa * CoinX, y + Thickness + CoinY), At(x, y + Thickness + CoinY));
                    p.BezierCurveTo(At(x + Kappa * CoinX, y + Thickness + CoinY),
                        At(x + CoinX, y + Thickness + Kappa * CoinY), At(x + CoinX, y + Thickness));
                    p.LineTo(At(x + CoinX, y));
                    p.BezierCurveTo(At(x + CoinX, y - Kappa * CoinY), At(x + Kappa * CoinX, y - CoinY),
                        At(x, y - CoinY));
                    p.BezierCurveTo(At(x - Kappa * CoinX, y - CoinY), At(x - CoinX, y - Kappa * CoinY),
                        At(x - CoinX, y));
                    p.ClosePath();
                    p.Fill();
                    p.Stroke();
                    if (!hasFace) {
                        return;
                    }

                    Ellipse(x, y, CoinX, CoinY, face, true);
                    Ellipse(x, y, CoinX * 0.55f, CoinY * 0.5f, light, false);
                }

                // A coin standing up, facing the camera: a navy drop under it, the gold disc and its pale middle.
                void Upright(float x, float y, float radius)
                {
                    Ellipse(x, y + 3f, radius, radius, navy, true);
                    Ellipse(x, y, radius, radius, face, true);
                    Ellipse(x, y, radius * 0.42f, radius * 0.42f, light, false);
                }

                void Ellipse(float x, float y, float rx, float ry, Color fill, bool isOutlined)
                {
                    p.fillColor = fill;
                    p.BeginPath();
                    p.MoveTo(At(x + rx, y));
                    p.BezierCurveTo(At(x + rx, y + Kappa * ry), At(x + Kappa * rx, y + ry), At(x, y + ry));
                    p.BezierCurveTo(At(x - Kappa * rx, y + ry), At(x - rx, y + Kappa * ry), At(x - rx, y));
                    p.BezierCurveTo(At(x - rx, y - Kappa * ry), At(x - Kappa * rx, y - ry), At(x, y - ry));
                    p.BezierCurveTo(At(x + Kappa * rx, y - ry), At(x + rx, y - Kappa * ry), At(x + rx, y));
                    p.ClosePath();
                    p.Fill();
                    if (isOutlined) {
                        p.Stroke();
                    }
                }
            }
        }
    }
}
