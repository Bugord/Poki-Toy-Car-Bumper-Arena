using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The live score of a long match on the HUD: a row per car with its place, colour chip, name and knock-outs, in the
    /// order of <see cref="MatchScore"/>. A wide screen shows every car in a column at its right side, beside the
    /// arena; a narrow one (a phone held upright) only the top three and the player's own row, under the settings gear.
    /// <para>
    /// The rows are built once and only moved: the order and the numbers change on the round's events (a fall, a car
    /// back, a new rival), never per frame, and a row is placed by its translate, so the new order is set at once and
    /// the slide is only the transition's. A car waiting to come back is dimmed with its seconds on its chip.
    /// </para>
    /// <para>
    /// Nothing on it takes a touch: a finger over it steers as anywhere else, and while the joystick is under it the
    /// view fades it, so it never paints over the thumb.
    /// </para>
    /// </summary>
    public sealed class HudScoreboard : IDisposable
    {
        // The distance from one row to the next, a row's height and its gap, for each form (as in ui_screens).
        private const float WidePitch = 84f;
        private const float CompactPitch = 64f;
        private const float RowGap = 8f;

        // The narrow form shows this many leaders, then, a little apart, the player's own row when it is lower.
        private const int CompactLeaders = 3;
        private const float PlayerGap = 14f;

        // How long a changed score holds its bigger scale before the transition eases it back, in milliseconds.
        private const long PopDuration = 120;

        private const string MatchClass = "scoreboard--match";
        private const string ShownClass = "scoreboard--shown";
        private const string FadedClass = "scoreboard--faded";
        private const string WideClass = "scoreboard--wide";
        private const string RowClass = "scoreboard__row";
        private const string RowYouClass = "scoreboard__row--you";
        private const string RowOutClass = "scoreboard__row--out";
        private const string RowHiddenClass = "scoreboard__row--hidden";
        private const string RowRevengeClass = "scoreboard__row--revenge";
        private const string RankClass = "scoreboard__rank";
        private const string KnockoutsPopClass = "scoreboard__ko--pop";

        private static readonly string[] RankClasses =
        {
            "scoreboard__rank--1", "scoreboard__rank--2", "scoreboard__rank--3",
        };

        // Numbers as text without a string per change; scores and seconds stay far below this.
        private static readonly string[] Numbers = CreateNumbers(100);

        private readonly HudViewModel model;
        private readonly VisualElement rowLayer;
        private readonly Row[] rows;
        private readonly int playerIndex;
        private bool isWide;
        private bool isShown;
        private bool isFaded;

        /// <summary>
        /// The board for the match the HUD shows, or null in a round of the last car standing, which has none.
        /// </summary>
        public static HudScoreboard Create(VisualElement container, HudViewModel model) =>
            model.IsTimed && model.CarCount > 0 ? new HudScoreboard(container, model) : null;

        private HudScoreboard(VisualElement container, HudViewModel model)
        {
            this.model = model;
            Element = container;
            playerIndex = model.PlayerIndex;
            container.pickingMode = PickingMode.Ignore;
            container.AddToClassList(MatchClass);

            // One star over the knock-out column says what the numbers count, as the star by the player's own on the bar.
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("scoreboard__header");
            // Over the column of points: the mode's own icon, the burst for knock-outs.
            var pointsIcon = HudView.CreateStar("scoreboard__star");
            ModeLooks.DrawPoints(pointsIcon, model.Mode?.Kind ?? SpecialRound.None);
            header.Add(pointsIcon);
            container.Add(header);

            rowLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            rowLayer.AddToClassList("scoreboard__rows");
            container.Add(rowLayer);

            rows = new Row[model.CarCount];
            for (var index = 0; index < rows.Length; index++) {
                rows[index] = new Row(model, index, index == playerIndex);
                rowLayer.Add(rows[index].Root);
            }

            model.CarOut += OnCarOut;
            model.Eliminated += OnEliminated;
            model.CarBack += OnCarBack;
            model.NemesisChanged += OnNemesisChanged;
            model.ScoreChanged += OnScoreChanged;
            // The settings open over the running match, so the language can change under the board: YOU follows it.
            Strings.Changed += OnLanguageChanged;
            OnNemesisChanged();
            Order(false);
        }

        /// <summary>The board's element, for the name tags, the speech and the stickers to keep clear of.</summary>
        public VisualElement Element { get; }

        /// <summary>Whether the board is on the screen now.</summary>
        public bool IsVisible => isShown;

        public bool IsWide => isWide;

        public void Dispose()
        {
            model.CarOut -= OnCarOut;
            model.Eliminated -= OnEliminated;
            model.CarBack -= OnCarBack;
            model.NemesisChanged -= OnNemesisChanged;
            model.ScoreChanged -= OnScoreChanged;
            Strings.Changed -= OnLanguageChanged;
        }

        /// <summary>
        /// The full column on a wide screen, the leaders and the player's own row on a narrow one.
        /// </summary>
        public void SetWide(bool wide)
        {
            if (wide == isWide) {
                return;
            }

            isWide = wide;
            Element.EnableInClassList(WideClass, wide);
            Order(false);
        }

        /// <summary>
        /// Shows the board while the match runs, faded while a finger steers or the joystick is taught under it.
        /// </summary>
        public void Show(bool shown, bool faded)
        {
            if (shown != isShown) {
                isShown = shown;
                Element.EnableInClassList(ShownClass, shown);
            }

            if (faded != isFaded) {
                isFaded = faded;
                Element.EnableInClassList(FadedClass, faded);
            }
        }

        /// <summary>
        /// The seconds on the chips of the cars waiting to come back; the view calls it on its tenth-of-a-second
        /// refresh, and a label changes only when its whole second does.
        /// </summary>
        public void RefreshCountdowns()
        {
            for (var index = 0; index < rows.Length; index++) {
                var row = rows[index];
                if (!row.IsOut) {
                    continue;
                }

                var left = model.RespawnTimeLeftOf(index);
                row.SetCountdown(left > 0f ? Mathf.CeilToInt(left) : 0);
            }
        }

        /// <summary>The row's slot from the top, 0 for the leader; the player's lower row in the narrow form is 3.</summary>
        public int SlotOf(int index) => rows[index].Slot;

        /// <summary>The place the row shows, 1 for the leader.</summary>
        public int PlaceShownOf(int index) => rows[index].Place;

        /// <summary>The knock-outs the row shows.</summary>
        public int KnockoutsShownOf(int index) => rows[index].Knockouts;

        /// <summary>Whether the row is on the board: always on a wide screen, the leaders and the player's otherwise.</summary>
        public bool IsRowShown(int index) => !rows[index].IsHidden;

        /// <summary>Whether the row is dimmed, its car waiting to come back.</summary>
        public bool IsRowWaiting(int index) => rows[index].IsOut;

        /// <summary>Whether the row's name is marked as the player's REVENGE target.</summary>
        public bool IsRowRevenge(int index) => rows[index].Root.ClassListContains(RowRevengeClass);

        private void OnCarOut(int index)
        {
            if (index < 0 || index >= rows.Length) {
                return;
            }

            rows[index].SetOut(true);
            rows[index].SetCountdown(Mathf.CeilToInt(model.RespawnTimeLeftOf(index)));
        }

        // The score is counted before the round tells of the fall, so it is current here; a fall of its own can move a
        // car too (fewer falls rank higher).
        private void OnEliminated(int index, int by) => Order(true);

        private void OnScoreChanged(int index) => Order(true);

        private void OnCarBack(int index)
        {
            if (index >= 0 && index < rows.Length) {
                rows[index].SetOut(false);
            }
        }

        private void OnLanguageChanged() => rows[playerIndex].SetName(model.NameOf(playerIndex));

        // The rival changes after the fall's events, so the red name follows the round's own event.
        private void OnNemesisChanged()
        {
            for (var index = 0; index < rows.Length; index++) {
                rows[index].Root.EnableInClassList(RowRevengeClass, index != playerIndex && model.IsNemesis(index));
            }
        }

        /// <summary>
        /// Puts every row at its place in the ranking, numbers it and pops a score that changed.
        /// </summary>
        private void Order(bool pop)
        {
            var pitch = isWide ? WidePitch : CompactPitch;
            var playerPlace = 0;
            for (var place = 0; place < rows.Length; place++) {
                if (model.IndexAtPlace(place) == playerIndex) {
                    playerPlace = place;
                }
            }

            var playerBelow = !isWide && playerPlace >= CompactLeaders;
            for (var place = 0; place < rows.Length; place++) {
                var index = model.IndexAtPlace(place);
                var row = rows[index];
                row.SetPlace(place + 1);
                row.SetKnockouts(model.ScoreOf(index), pop);

                if (isWide || place < CompactLeaders) {
                    row.Move(place, place * pitch, false);
                }
                else {
                    // Below the leaders, apart: the player's own row, its place on it; the others fade out there.
                    row.Move(CompactLeaders, CompactLeaders * pitch + PlayerGap, index != playerIndex);
                }
            }

            var shownRows = isWide ? rows.Length : Mathf.Min(rows.Length, CompactLeaders) + (playerBelow ? 1 : 0);
            rowLayer.style.height = shownRows * pitch - RowGap + (playerBelow ? PlayerGap : 0f);
        }

        private static string[] CreateNumbers(int count)
        {
            var numbers = new string[count];
            for (var number = 0; number < count; number++) {
                numbers[number] = number.ToString();
            }

            return numbers;
        }

        private static string NumberText(int number) =>
            number >= 0 && number < Numbers.Length ? Numbers[number] : number.ToString();

        private sealed class Row
        {
            public readonly VisualElement Root = new() { pickingMode = PickingMode.Ignore };
            private readonly VisualElement rankBadge = new() { pickingMode = PickingMode.Ignore };
            private readonly Label rank = new() { pickingMode = PickingMode.Ignore };
            private readonly Label countdown = new() { pickingMode = PickingMode.Ignore };
            private readonly StickerText knockouts = new("0");
            private readonly StickerText name;
            private readonly IVisualElementScheduledItem popEnd;
            private float shownY = float.NaN;
            private int shownSeconds = -1;

            public int Slot { get; private set; } = -1;
            public int Place { get; private set; }
            public int Knockouts { get; private set; } = -1;
            public bool IsHidden { get; private set; }
            public bool IsOut { get; private set; }

            public Row(HudViewModel model, int index, bool isPlayer)
            {
                Root.AddToClassList(RowClass);
                Root.EnableInClassList(RowYouClass, isPlayer);

                rankBadge.AddToClassList(RankClass);
                rank.AddToClassList("scoreboard__rank-text");
                rankBadge.Add(rank);
                Root.Add(rankBadge);

                // The chip in the car's colour, the player's with the white ring of its marker on the arena, and over it
                // the seconds until a fallen car comes back.
                var chipPlace = new VisualElement { pickingMode = PickingMode.Ignore };
                chipPlace.AddToClassList("scoreboard__chip");
                chipPlace.Add(CarColor.CreateChip(model.ColorOf(index), isPlayer));
                countdown.AddToClassList("scoreboard__countdown");
                chipPlace.Add(countdown);
                Root.Add(chipPlace);

                // The REVENGE target as the name tag over the car has it, so the mark is not the red alone.
                var target = new VisualElement { pickingMode = PickingMode.Ignore };
                target.AddToClassList("revenge-target");
                target.AddToClassList("scoreboard__target");
                var center = new VisualElement { pickingMode = PickingMode.Ignore };
                center.AddToClassList("revenge-target__center");
                target.Add(center);
                Root.Add(target);

                name = new StickerText(model.NameOf(index));
                name.AddToClassList("scoreboard__name");
                Root.Add(name);

                knockouts.AddToClassList("scoreboard__ko");
                Root.Add(knockouts);
                popEnd = knockouts.schedule.Execute(() => knockouts.RemoveFromClassList(KnockoutsPopClass));
                popEnd.Pause();
            }

            public void SetName(string text) => name.SetText(text);

            public void SetPlace(int place)
            {
                if (place == Place) {
                    return;
                }

                Place = place;
                rank.text = NumberText(place);
                for (var medal = 0; medal < RankClasses.Length; medal++) {
                    rankBadge.EnableInClassList(RankClasses[medal], medal == place - 1);
                }
            }

            public void SetKnockouts(int value, bool pop)
            {
                if (value == Knockouts) {
                    return;
                }

                var isFirst = Knockouts < 0;
                Knockouts = value;
                knockouts.SetText(NumberText(value));
                if (pop && !isFirst) {
                    knockouts.AddToClassList(KnockoutsPopClass);
                    popEnd.ExecuteLater(PopDuration);
                }
            }

            public void Move(int slot, float y, bool hidden)
            {
                Slot = slot;
                if (hidden != IsHidden) {
                    IsHidden = hidden;
                    Root.EnableInClassList(RowHiddenClass, hidden);
                }

                if (y != shownY) {
                    shownY = y;
                    Root.style.translate = new Translate(0f, y);
                }
            }

            public void SetOut(bool isOut)
            {
                IsOut = isOut;
                Root.EnableInClassList(RowOutClass, isOut);
                if (!isOut) {
                    shownSeconds = -1;
                }
            }

            public void SetCountdown(int seconds)
            {
                if (seconds == shownSeconds) {
                    return;
                }

                shownSeconds = seconds;
                countdown.text = seconds > 0 ? NumberText(seconds) : string.Empty;
            }
        }
    }
}
