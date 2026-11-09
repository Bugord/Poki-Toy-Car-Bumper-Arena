using System;
using System.Collections.Generic;
using System.Threading;
using Bugord.App.Localization;
using Bugord.App.UI;
using Cysharp.Threading.Tasks;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// Replays an already saved reward in three steps: first a plain bar from the level the round started at to the
    /// next fills with the round's XP (LEVEL UP! and the badge bumps on each level crossed), then each level crossed
    /// that gives something (a car, an arena, a rank) shows it in a popup over the screen until a tap or a few seconds,
    /// and last the level track: a row of stops from the card's own level badge (so the level shows once) to the next
    /// rewards and the next rank, which builds left to right empty, then fills, each stop it reaches turning gold. The
    /// bar of the first step grows out of the same badge. Its lifetime belongs to
    /// the result view, so NEXT MATCH can leave at any point, including halfway through a popup, without holding up
    /// the next round or touching a detached screen.
    /// <para>
    /// CLAIM ×2 cuts the replay short (<see cref="Settle"/>: the card as it ends, a popup still due waits) and, once the
    /// ad has paid, plays on from there with the bonus (<see cref="Resume"/>): the bar again from the level reached,
    /// LEVEL UP! and popups for any level the bonus crosses, then the track on to the new total.
    /// </para>
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
        // The track's padding each side (.track in ui_screens), and how far a bar runs under a stop.
        private const float TrackInset = 30f;
        private const float BadgeTuck = 14f;
        // A reward popup closes on its own after this long, so a player who just watches is not stuck under it.
        private const float RewardAutoClose = 4f;
        private const float RewardFadeIn = 0.15f;
        // The menu's light behind a reward, turning as slowly as there.
        private const float RewardRaysTurnSeconds = 24f;
        // How far a reward's card keeps from the screen's edges, how far its tilted title rises over it, and how much
        // bigger it grows on a phone on its side, where the panel is tall in reference pixels but short in the hand.
        private const float RewardEdge = 40f;
        private const float RewardTitleRise = 140f;
        private const float RewardWideTouchScale = 1.3f;

        private const string ReachedClass = "track__node--reached";
        private const string DoneClass = "track__node--done";

        private readonly VisualElement root;
        private readonly RoundOverViewModel viewModel;
        private readonly bool isTouch;
        private NewLeaguePopup league;
        private readonly VisualElement bar;
        private readonly Label barTo;
        private readonly VisualElement barFill;
        private readonly VisualElement levelUp;
        private readonly VisualElement levelBadge;
        private readonly Label levelLabel;
        private readonly Label rankLabel;
        private readonly Label total;
        private readonly List<Waypoint> stops = new();
        private readonly List<Segment> segments = new();
        private readonly List<CarPortrait> trackCars = new();
        private readonly List<VisualElement> trackRows = new();
        private readonly VisualElement head;
        private VisualElement badgeSlot;
        private readonly LiveText text = new();
        private int shownLevel;
        // The levels whose popups have shown, and whether the gift's has, so a replay cut short shows what it skipped.
        private int rewardsShownTo;
        private bool isGiftShown;
        private int popupsShown;
        private XpLevel reached;
        private CancellationTokenSource animation;

        public RoundOverProgress(VisualElement root, RoundOverViewModel viewModel, bool isTouch)
        {
            this.root = root;
            this.viewModel = viewModel;
            this.isTouch = isTouch;
            this.levelBadge = root.Q<VisualElement>("xp-level");
            LevelBadge.Decorate(levelBadge);
            this.levelLabel = root.Q<Label>("xp-level-value");
            this.rankLabel = root.Q<Label>("xp-rank");
            // The rank is the badge's caption, under it as every stop's name is under the stop: over the track it
            // floated with nothing to belong to once the badge moved into the track.
            rankLabel.RemoveFromHierarchy();
            rankLabel.AddToClassList("level-card__rank--under");
            levelBadge.Add(rankLabel);
            this.total = root.Q<Label>("xp-total");

            var reward = viewModel.XpGain;
            rewardsShownTo = reward.Before.Level;
            reached = reward.After;
            SetLevel(reward.Before.Level);
            text.Set(total, () => Strings.Format("result.xp_total", reward.Before.Xp, reward.Before.Cost));
            text.Set(rankLabel, () => PlayerRank.NameOf(shownLevel));
            var track = root.Q<VisualElement>("xp-track");
            head = root.Q<VisualElement>(className: "level-card__head");
            BuildTrack(track, reward.Before.Level);

            // The bar of the first step lies over the track, which is still empty then: nothing in the card moves. It
            // goes between the bars and the stops, so the badge it grows out of is drawn over its end.
            bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList("xp-bar");
            // Only the next level at the bar's end: the card's own badge beside it already shows the level reached.
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("xp-bar__line");
            barFill = new VisualElement { pickingMode = PickingMode.Ignore };
            barFill.AddToClassList("xp-bar__fill");
            line.Add(barFill);
            bar.Add(line);
            barTo = BarLevel(bar);
            levelUp = Outlined("", "xp-bar__level-up", out var levelUpLabels);
            foreach (var label in levelUpLabels) {
                text.Set(label, () => Strings.Get("result.level_up"));
            }
            bar.Add(levelUp);
            track.Insert(1, bar);
            levelBadge.RegisterCallback<GeometryChangedEvent>(_ => FitToBadge());

            SetScale(levelBadge, 0f);
            SetScale(bar, 0f);
            SetScale(levelUp, 0f);
            for (var i = 1; i < stops.Count; i++) {
                SetScale(stops[i].Node, 0f);
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

        /// <summary>A reward popup is up: it takes the presses, so the keys under it wait too.</summary>
        public bool IsPopupOpen { get; private set; }

        /// <summary>NEW LEAGUE! is up: Space, Enter and Esc close it (<see cref="CloseLeague"/>), not NEXT MATCH.</summary>
        public bool IsLeagueOpen => league is { IsOpen: true };

        public void CloseLeague() => league?.Continue();

        /// <summary>A popup's button was pressed: the view plays its click.</summary>
        public event Action Clicked;

        /// <summary>A popup's reward was taken (bought or put on): the view plays its sound.</summary>
        public event Action RewardTaken;

        /// <summary>A popup's BUY &amp; EQUIP was pressed without the coins: the view plays its refusal.</summary>
        public event Action RewardRefused;

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
            trackRows.Add(bars);
            trackRows.Add(nodes);
            // The track starts at the card's own level badge, so the level the round started at shows once: the badge
            // is the first stop, and its slot in the bars' row follows its width (FitToBadge).
            badgeSlot = Slot(bars, false);
            levelBadge.RemoveFromHierarchy();
            levelBadge.AddToClassList("track__badge");
            nodes.Add(levelBadge);
            stops.Add(new Waypoint(from, levelBadge, true));
            var previous = from;
            var isCaptioned = false;
            foreach (var trackStop in viewModel.TrackStops()) {
                isCaptioned |= trackStop.Name != null;
                // Dotted only where the rank is further than one level: the bar to it would hardly move.
                var segment = new Segment(trackStop.IsRank && trackStop.Level - previous > 1);
                previous = trackStop.Level;
                bars.Add(segment.Root);
                segments.Add(segment);
                var gap = new VisualElement { pickingMode = PickingMode.Ignore };
                gap.AddToClassList("track__gap");
                nodes.Add(gap);
                Slot(bars, trackStop.IsRank);
                stops.Add(CreateStop(nodes, trackStop.Level, trackStop.Name, trackStop));
            }

            // Plain levels have no captions: no room kept under them.
            track.EnableInClassList("track--plain", !isCaptioned);
        }

        /// <summary>
        /// The badge's width sets its slot under the bars, where the first bar starts, the step's bar, which grows out
        /// of it, and the rank and XP line over the track, which starts beside it.
        /// </summary>
        private void FitToBadge()
        {
            var width = levelBadge.layout.width;
            if (float.IsNaN(width) || width <= 0f) {
                return;
            }

            badgeSlot.style.width = width;
            bar.style.left = TrackInset + width - BadgeTuck;
            head.style.paddingLeft = TrackInset + width;
        }

        private static VisualElement Row(VisualElement track, string className)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("track__row");
            row.AddToClassList(className);
            track.Add(row);
            return row;
        }

        private static VisualElement Slot(VisualElement bars, bool isRank)
        {
            var slot = new VisualElement { pickingMode = PickingMode.Ignore };
            slot.AddToClassList("track__slot");
            slot.EnableInClassList("track__slot--rank", isRank);
            bars.Add(slot);
            return slot;
        }

        private Waypoint CreateStop(VisualElement track, int level, string name, TrackStop reward)
        {
            var node = new VisualElement { pickingMode = PickingMode.Ignore };
            node.AddToClassList("track__node");
            track.Add(node);
            if (name == null) {
                node.Add(Text(level.ToString(), "track__level"));
            }
            else if (reward.IsRank) {
                node.AddToClassList("track__node--rank");
                node.AddToClassList(PlayerRank.ClassOf(level));
                node.Add(Text(level.ToString(), "track__level"));
                LevelBadge.Decorate(node);
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
            return new Waypoint(level, node, false);
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
            var reward = viewModel.XpGain;
            // The gift first: behind the bar and a new level's popup most players had pressed NEXT MATCH before it came.
            await ShowGift(token);
            await FillBar(reward.Before, reward.After, token);
            await ShowHatOffer(token);
            await Shrink(bar, token);
            bar.style.display = DisplayStyle.None;
            // The first stop is the card's badge, up since the start.
            for (var i = 0; i < segments.Count; i++) {
                await Grow(segments[i].Root, token);
                Pop(stops[i + 1].Node, token).Forget();
            }
            await UniTask.Delay((int)(PopDuration * 1000), ignoreTimeScale: true, cancellationToken: token);
            await FillLevels(reward.Before, reward.After, token);
        }

        /// <summary>
        /// Stops the replay and shows the card as the reward so far ends: the badge at the level reached, the track
        /// built and filled to it, the bar put away. Popups not shown yet wait for <see cref="Resume"/>.
        /// </summary>
        public void Settle()
        {
            Stop();
            SetScale(levelBadge, 1f);
            SetLevel(reached.Level);
            ShowTotal(reached);
            SetScale(levelUp, 0f);
            SetScale(bar, 0f);
            bar.style.display = DisplayStyle.None;
            foreach (var stop in stops) {
                SetScale(stop.Node, 1f);
                if (!stop.IsReached && stop.Level <= reached.Level) {
                    stop.IsReached = true;
                    stop.Node.AddToClassList(ReachedClass);
                    stop.Node.AddToClassList(DoneClass);
                }
            }

            foreach (var segment in segments) {
                segment.Root.style.scale = new Scale(Vector3.one);
            }

            SetPosition(PositionOf(reached));
        }

        /// <summary>
        /// After CLAIM ×2: the popups the cut replay skipped, then, with a <paramref name="bonus"/>, the bar from the
        /// level reached fills with it (LEVEL UP! and popups for the levels it crosses) and the track follows.
        /// </summary>
        public void Resume(XpGain? bonus, CancellationToken token)
        {
            Stop();
            animation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Continue(bonus, animation.Token).Forget();
        }

        private async UniTask Continue(XpGain? bonus, CancellationToken token)
        {
            for (var level = rewardsShownTo + 1; level <= reached.Level; level++) {
                await ShowRewards(level, token);
            }

            await ShowGift(token);
            await ShowHatOffer(token);
            if (bonus is not { } gain || gain.After.Level == 0) {
                return;
            }

            reached = gain.After;
            // The bar lies over the track, as at first: the track steps aside meanwhile.
            SetTrackShown(false);
            bar.style.display = DisplayStyle.Flex;
            try {
                await FillBar(gain.Before, gain.After, token);
                await Shrink(bar, token);
            }
            finally {
                bar.style.display = DisplayStyle.None;
                SetTrackShown(true);
            }

            await FillLevels(gain.Before, gain.After, token);
        }

        /// <summary>
        /// The first two steps: the bar from a level to the next fills with the XP, level by level; each level crossed
        /// bumps the badge, says LEVEL UP! and shows what it gives in a popup before the bar starts the next.
        /// </summary>
        private async UniTask FillBar(XpLevel from, XpLevel to, CancellationToken token)
        {
            var level = from.Level;
            var share = (float)from.Xp / from.Cost;
            SetBarLevels(level);
            SetBarFill(share);
            await Pop(bar, token);
            while (level < to.Level) {
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

            ShowTotal(to);
            await BarTo(share, (float)to.Xp / to.Cost, token);
            await UniTask.Delay(BarHold, ignoreTimeScale: true, cancellationToken: token);
        }

        /// <summary>Hides the track under the bar, all but its first stop: the card's badge the bar grows out of.</summary>
        private void SetTrackShown(bool isShown)
        {
            foreach (var row in trackRows) {
                foreach (var child in row.Children()) {
                    if (child != levelBadge) {
                        child.style.visibility = isShown ? Visibility.Visible : Visibility.Hidden;
                    }
                }
            }
        }

        private void ShowTotal(XpLevel level) =>
            text.Set(total, () => Strings.Format("result.xp_total", level.Xp, level.Cost));

        private static float PositionOf(XpLevel level) => level.Level + (float)level.Xp / level.Cost;

        /// <summary>
        /// The last step: moves the track's position, a level and the share of it earned, level by level, turning the
        /// stops of each level crossed gold. The badge already shows the level reached.
        /// </summary>
        private async UniTask FillLevels(XpLevel fromLevel, XpLevel to, CancellationToken token)
        {
            var level = fromLevel.Level;
            var from = PositionOf(fromLevel);
            while (level < to.Level) {
                await Fill(from, level + 1, token);
                level++;
                from = level;
                await Reach(level, token);
            }

            await Fill(from, PositionOf(to), token);
        }

        /// <summary>The second step: a popup for each thing the level gives, one after another.</summary>
        private async UniTask ShowRewards(int level, CancellationToken token)
        {
            if (level <= rewardsShownTo) {
                return;
            }

            foreach (var car in viewModel.NewCarsAt(level)) {
                if (!viewModel.IntroduceCar(car)) {
                    continue;
                }

                var isForSale = viewModel.IsForSale(car);
                // The stats as the garage's BUY window shows them, so a car reads the same wherever it is offered.
                var stats = GarageStats.Build("garage__sheet-stats");
                stats.AddToClassList("level-reward__stats");
                GarageStats.Set(stats, viewModel.StatsOf(car), viewModel.HasBoost);
                if (!isForSale) {
                    viewModel.CarIntroduced(car);
                }

                // A free car is the player's to take: EQUIP drives it next, CONTINUE keeps the car they have. It turns
                // in the player's colour and hat: the car they would drive.
                try {
                    await ShowReward(LevelTitle(level), Strings.Get("result.new_car_tag"), GameText.Car(car), null,
                        RewardArt(car, viewModel.WornTopper, null), token, isForSale ? car : null,
                        isForSale ? null : () => viewModel.EquipCar(car), stats);
                }
                finally {
                    viewModel.HideHero();
                }
            }

            // A new arena has no popup: the lobby names it with NEW before its round, and the track shows it ahead.
            // The level a league starts at greets it with NEW LEAGUE!, after what the level gave; the menu greets it
            // only when the result did not.
            if (level > 1 && PlayerRank.TierOf(level) > PlayerRank.TierOf(level - 1)) {
                await ShowNewLeague(token);
            }

            rewardsShownTo = level;
        }

        /// <summary>
        /// The one topper for coins the result ever offers (<see cref="RoundOverViewModel.HatOffer"/>), with its 3D
        /// model and BUY &amp; EQUIP, on a result that showed no other popup.
        /// </summary>
        private async UniTask ShowHatOffer(CancellationToken token)
        {
            if (popupsShown > 0) {
                return;
            }

            var hat = viewModel.HatOffer();
            if (hat == null) {
                return;
            }

            viewModel.HatOffered(hat);
            // Tried on: the player's own car wears it, so the offer shows what the coins would buy.
            try {
                await ShowReward(Strings.Get("result.hat_offer_title"), Strings.Get("result.new_hat_tag"),
                    GameText.Topper(hat), null, RewardArt(null, hat, hat), token, hatForSale: hat);
            }
            finally {
                viewModel.HideHero();
            }
        }

        private static string LevelTitle(int level) => Strings.Format("result.level_title", level);

        /// <summary>
        /// The topper an early win gave free (<see cref="RoundOverViewModel.Gift"/>): the same popup, the topper's own 3D
        /// model as its garage card shows it, and EQUIP to put it on at once. Owned either way.
        /// </summary>
        private async UniTask ShowGift(CancellationToken token)
        {
            var gift = viewModel.Gift;
            if (gift == null || isGiftShown) {
                return;
            }

            viewModel.GiftShown();
            try {
                await ShowReward(Strings.Get("result.gift_title"), Strings.Get("result.free_hat_tag"),
                    GameText.Topper(gift), null, RewardArt(null, gift, gift), token, null, viewModel.WearGift);
                isGiftShown = true;
            }
            finally {
                viewModel.HideHero();
            }
        }

        /// <summary>
        /// NEW LEAGUE! over the result (<see cref="NewLeaguePopup"/>), as the menu shows it, once for the league the
        /// player is in: the player's car on its pedestal, waiting for CONTINUE, a tap on the dim or a key.
        /// </summary>
        private async UniTask ShowNewLeague(CancellationToken token)
        {
            if (!viewModel.TakeNewLeague()) {
                return;
            }

            popupsShown++;
            var isClosed = false;
            league = new NewLeaguePopup(root.Q<VisualElement>("round-over") ?? root, () => Clicked?.Invoke(),
                () => isClosed = true);
            league.Fit(isTouch ? RewardWideTouchScale : 1f);
            league.Show(viewModel.PlayerLevel, !isTouch, viewModel.ShowCarHero(null, viewModel.WornTopper),
                viewModel.LeaguePrize);
            IsPopupOpen = true;
            try {
                while (!isClosed) {
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally {
                league.Remove();
                league = null;
                viewModel.HideHero();
                IsPopupOpen = false;
            }
        }

        /// <summary>
        /// A reward's picture: <paramref name="car"/> (null: the player's own) turning on the league's pedestal in
        /// <paramref name="topper"/>, as the garage's BUY window shows a car. Where the arena's stage is not there (tests)
        /// the car's portrait stands in, or the flat icon of <paramref name="hat"/>.
        /// </summary>
        private VisualElement RewardArt(CarCatalog.Entry car, string topper, string hat)
        {
            var art = new VisualElement { pickingMode = PickingMode.Ignore };
            art.AddToClassList("garage__sheet-art");
            var hero = viewModel.ShowCarHero(car, topper);
            if (hero != null) {
                art.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(hero));
            }
            else if (hat != null) {
                art.Add(TopperIcon.Create(hat, "level-reward__hat-icon"));
            }
            else {
                var portrait = new VisualElement { pickingMode = PickingMode.Ignore };
                portrait.AddToClassList("car-portrait");
                portrait.AddToClassList("garage__sheet-portrait");
                new CarPortrait(portrait).Set(car ?? viewModel.SelectedCar, viewModel.SelectedColor);
                art.Add(portrait);
            }

            return art;
        }

        // The card at its own size where it fits, bigger on a phone on its side (as the garage's window), smaller where
        // the screen is short. On the holder: the card's own scale pops and shrinks it.
        private void FitReward(VisualElement overlay, VisualElement holder)
        {
            var size = overlay.layout.size;
            var own = holder.layout.size;
            if (own.x <= 0f || own.y <= 0f || size.y <= 0f) {
                return;
            }

            var most = isTouch && size.x > size.y * 1.2f ? RewardWideTouchScale : 1f;
            var fit = Mathf.Min(most, Mathf.Min((size.y - 2f * RewardEdge) / (own.y + RewardTitleRise),
                (size.x - 2f * RewardEdge) / own.x));
            holder.style.scale = new Scale(new Vector3(fit, fit, 1f));
        }

        /// <summary>
        /// One reward over the whole screen: dimmed, the menu's light turning behind a white card with the level over it,
        /// a tag, the reward in a framed piece of the league's sky (<see cref="GarageView.SkyFrame"/>, as the garage's BUY
        /// window) and its name. A tap anywhere or a few seconds close it; NEXT MATCH by key
        /// still works under it, and leaving the screen takes it away.
        /// </summary>
        /// <para>
        /// A car with a price (<paramref name="forSale"/>) comes with BUY &amp; EQUIP and its price. While the coins pay
        /// for it the offer waits for the player instead of closing by itself, and a tap beside it leaves it for later;
        /// without them the button is grey, a press shakes the card and says how many coins are missing, and the popup
        /// closes by itself as any other.
        /// </para>
        /// <para>
        /// A gift or a free car (<paramref name="wear"/>) is offered with EQUIP, which puts it on, and waits for the
        /// player too. Either taken, the popup celebrates (<see cref="RewardCelebration"/>) before it closes.
        /// </para>
        /// <para>
        /// A popup with such a button also has CONTINUE under it, which closes it with nothing taken, in place of the
        /// hint to tap: a tap beside the card still does the same.
        /// </para>
        private async UniTask ShowReward(string title, string tag, string name, string caption, VisualElement art,
            CancellationToken token, CarCatalog.Entry forSale = null, System.Func<bool> wear = null,
            VisualElement details = null, string hatForSale = null)
        {
            popupsShown++;
            var overlay = new VisualElement();
            overlay.AddToClassList("level-reward");
            var rays = new VisualElement { pickingMode = PickingMode.Ignore };
            rays.AddToClassList("level-reward__rays");
            rays.generateVisualContent += MenuView.DrawLight;
            overlay.Add(rays);

            var card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.AddToClassList("card");
            card.AddToClassList("level-reward__card");
            card.Add(Outlined(title, "level-reward__title", out _));
            card.Add(Text(tag, "level-reward__tag"));
            var frame = GarageView.SkyFrame(viewModel.League);
            frame.AddToClassList("level-reward__frame");
            frame.Add(art);
            card.Add(frame);
            card.Add(Text(name, "level-reward__name"));
            if (details != null) {
                card.Add(details);
            }
            if (caption != null) {
                var captionLabel = Text(caption, "caption");
                captionLabel.AddToClassList("level-reward__caption");
                card.Add(captionLabel);
            }

            var isOffer = forSale != null && viewModel.CanAfford(forSale);
            var closeAt = isOffer || wear != null ? float.MaxValue : RewardAutoClose;
            var isClosed = false;
            var isTaken = false;
            if (forSale != null) {
                var buy = new Button();
                buy.RemoveFromClassList(Button.ussClassName);
                buy.AddToClassList("pill");
                buy.AddToClassList("level-reward__buy");
                buy.EnableInClassList("level-reward__buy--dear", !isOffer);
                buy.Add(FitOutlined(Strings.Get("result.buy_equip"), "level-reward__buy-label"));
                var price = new VisualElement { pickingMode = PickingMode.Ignore };
                price.AddToClassList("pill");
                price.AddToClassList("level-reward__price");
                price.Add(RoundOverMeta.Coin());
                price.Add(Text(viewModel.PriceOf(forSale).ToString(), "level-reward__price-value"));
                buy.Add(price);
                // The overlay closes on any press: the button keeps its own press.
                buy.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                buy.clicked += () => {
                    if (isClosed || isTaken) {
                        return;
                    }

                    if (!viewModel.BuyCar(forSale)) {
                        RewardRefused?.Invoke();
                        RewardCelebration.Refuse(overlay, card, buy,
                            Strings.Format("garage.need_more", viewModel.PriceOf(forSale) - viewModel.CoinsNow), token);
                        return;
                    }

                    isTaken = true;
                    // The coins fly from the price, so it is measured before the button gives way.
                    RewardCelebration.Play(overlay, card, art, Strings.Get("result.equipped"), price,
                        root.Q<VisualElement>("coin-pill"), token);
                    buy.SetEnabled(false);
                    buy.style.visibility = Visibility.Hidden;
                    RewardTaken?.Invoke();
                    card.schedule.Execute(() => isClosed = true)
                        .StartingIn((long)(RewardCelebration.Duration * 1000f));
                };
                card.Add(buy);
                if (isOffer) {
                    viewModel.CarOffered(forSale);
                }
            }

            if (hatForSale != null) {
                var buyHat = new Button();
                buyHat.RemoveFromClassList(Button.ussClassName);
                buyHat.AddToClassList("pill");
                buyHat.AddToClassList("level-reward__buy");
                buyHat.Add(FitOutlined(Strings.Get("result.buy_equip"), "level-reward__buy-label"));
                var hatPrice = new VisualElement { pickingMode = PickingMode.Ignore };
                hatPrice.AddToClassList("pill");
                hatPrice.AddToClassList("level-reward__price");
                hatPrice.Add(RoundOverMeta.Coin());
                hatPrice.Add(Text(viewModel.TopperPrice(hatForSale).ToString(), "level-reward__price-value"));
                buyHat.Add(hatPrice);
                buyHat.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                buyHat.clicked += () => {
                    if (isClosed || isTaken || !viewModel.BuyHat(hatForSale)) {
                        return;
                    }

                    isTaken = true;
                    RewardCelebration.Play(overlay, card, art, Strings.Get("result.equipped"), hatPrice,
                        root.Q<VisualElement>("coin-pill"), token);
                    buyHat.SetEnabled(false);
                    buyHat.style.visibility = Visibility.Hidden;
                    RewardTaken?.Invoke();
                    card.schedule.Execute(() => isClosed = true)
                        .StartingIn((long)(RewardCelebration.Duration * 1000f));
                };
                card.Add(buyHat);
                closeAt = float.MaxValue;
            }

            if (wear != null) {
                var button = new Button();
                button.RemoveFromClassList(Button.ussClassName);
                button.AddToClassList("pill");
                button.AddToClassList("level-reward__buy");
                button.AddToClassList("level-reward__wear");
                button.Add(FitOutlined(Strings.Get("garage.use"), "level-reward__buy-label"));
                button.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                button.clicked += () => {
                    if (isClosed || isTaken || !wear()) {
                        return;
                    }

                    isTaken = true;
                    RewardCelebration.Play(overlay, card, art, Strings.Get("result.equipped"), null, null, token);
                    button.SetEnabled(false);
                    button.style.visibility = Visibility.Hidden;
                    RewardTaken?.Invoke();
                    card.schedule.Execute(() => isClosed = true)
                        .StartingIn((long)(RewardCelebration.Duration * 1000f));
                };
                card.Add(button);
            }

            var hasChoice = forSale != null || wear != null || hatForSale != null;
            if (hasChoice) {
                var skip = new Button();
                skip.RemoveFromClassList(Button.ussClassName);
                skip.AddToClassList("pill");
                skip.AddToClassList("level-reward__skip");
                skip.Add(Text(Strings.Get("result.continue"), "level-reward__skip-label"));
                skip.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                skip.clicked += () => {
                    if (!isTaken) {
                        isClosed = true;
                    }
                };
                card.Add(skip);
            }

            var holder = new VisualElement { pickingMode = PickingMode.Ignore };
            holder.AddToClassList("level-reward__holder");
            holder.Add(card);
            if (!hasChoice) {
                holder.Add(Text(Strings.Get(isTouch ? "result.tap_continue" : "result.click_continue"),
                    "level-reward__hint"));
            }

            overlay.Add(holder);
            overlay.RegisterCallback<GeometryChangedEvent>(_ => FitReward(overlay, holder));
            holder.RegisterCallback<GeometryChangedEvent>(_ => FitReward(overlay, holder));
            overlay.RegisterCallback<PointerDownEvent>(evt => {
                evt.StopPropagation();
                // A tap mid-celebration waits for it: the popup closes on its own once it is over.
                if (!isTaken) {
                    isClosed = true;
                }
            });

            // Built after the screen's pills were kept round, so its own pills (the tag, BUY, EQUIP) ask again.
            PillShape.Keep(overlay);
            overlay.style.opacity = 0f;
            SetScale(card, 0f);
            // Inside the screen's own element, which carries the style sheet.
            (root.Q<VisualElement>("round-over") ?? root).Add(overlay);
            IsPopupOpen = true;
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
                IsPopupOpen = false;
            }
        }

        private void SetBarLevels(int level)
        {
            barTo.text = (level + 1).ToString();
            SetRank(barTo.parent, level + 1);
        }

        private static void SetRank(VisualElement badge, int level) => LevelBadge.SetRank(badge, level);

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
            LevelBadge.Decorate(badge);
            bar.Add(badge);
            return value;
        }

        /// <summary>Text with the outline of the titles: the same text twice, the face over the outline.</summary>
        // A button's outlined label, shrunk to the width the button leaves it (FitText).
        private static VisualElement FitOutlined(string value, string className)
        {
            var element = Outlined(value, className, out var labels);
            FitText.Fit(element, labels);
            return element;
        }

        internal static VisualElement Outlined(string value, string className, out Label[] labels)
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
            text.Set(rankLabel, () => PlayerRank.NameOf(shownLevel));
            LevelBadge.SetRank(levelBadge, level);
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
