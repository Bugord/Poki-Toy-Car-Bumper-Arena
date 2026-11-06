using System;
using Bugord.App.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The leagues of the main menu (<see cref="MenuView"/>): the NEW LEAGUE! popup the first menu in a new league opens
    /// with, over its new sky and plinth, and the list of all five leagues a press on the level opens. A league is a
    /// <see cref="PlayerRank"/> tier; what it looks like is <see cref="LeagueLook"/>. Both are filled when they open, so
    /// they take the language and the level of that moment; neither can stay open under the settings.
    /// <para>
    /// Upright the list runs from LEGEND at the top down to ROOKIE, one row each; on a wide screen the leagues are five
    /// cards from ROOKIE at the left to LEGEND, with the bar to the next league under them. The design is MenuB_NewLeague
    /// and MenuB_Leagues in Claude Design.
    /// </para>
    /// </summary>
    internal sealed class MenuLeagues
    {
        private const string OpenClass = "menu__popup--open";
        private const string WideClass = "menu__leagues--wide";

        private static readonly Color Navy = new Color32(0x1d, 0x2b, 0x4f, 0xff);
        private static readonly Color Burst = new(1f, 0.79f, 0.24f, 0.45f);
        // The pedestal's colours that stay in every league, as the 3D one in the menu: its navy side, its gold band
        // and rim (MechanicArt's train_dark and train_yellow).
        private static readonly Color PlinthSide = new Color32(0x2c, 0x3a, 0x62, 0xff);
        private static readonly Color PlinthGold = new Color32(0xff, 0xc9, 0x3c, 0xff);
        private static readonly Color PlinthGoldDark = new Color32(0xe9, 0xa9, 0x1c, 0xff);
        // The light rays across every league's row and card, as the boards draw them.
        private static readonly Color Rays = new(1f, 1f, 1f, 0.2f);

        private readonly Action click;
        private readonly Action closed;
        private readonly VisualElement popup;
        private readonly VisualElement popupCard;
        private readonly VisualElement list;
        private readonly VisualElement listBody;
        private readonly VisualElement listItems;
        private readonly VisualElement listTrack;
        private readonly VisualElement listKey;
        private Label listTitle;
        private Label listTitleFace;
        private Label listHint;
        private int listLevel;
        private bool isWide;

        public MenuLeagues(VisualElement menu, Action click, Action closed)
        {
            this.click = click;
            this.closed = closed;

            popup = Element("menu__popup");
            popup.AddToClassList("menu__league-popup");
            popup.pickingMode = PickingMode.Position;
            popup.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == popup) {
                    Continue();
                }
            });
            popupCard = Element("card");
            popupCard.AddToClassList("menu__league-card");
            popupCard.pickingMode = PickingMode.Position;
            popup.Add(popupCard);
            menu.Add(popup);

            list = Element("menu__leagues");
            list.pickingMode = PickingMode.Position;
            var rays = Element("menu__leagues-rays");
            rays.generateVisualContent += MenuView.DrawLight;
            list.Add(rays);
            listBody = Element("menu__leagues-body");
            var head = Element("menu__leagues-head");
            var stroke = Element("stroke");
            listTitle = Text("display");
            listTitle.AddToClassList("menu__leagues-title");
            listTitleFace = Text("display");
            listTitleFace.AddToClassList("menu__leagues-title");
            listTitleFace.AddToClassList("stroke__face");
            stroke.Add(listTitle);
            stroke.Add(listTitleFace);
            stroke.AddToClassList("menu__leagues-title-box");
            head.Add(stroke);
            listKey = Element("key");
            listKey.AddToClassList("menu__leagues-key");
            var keyText = Text("key__text");
            keyText.text = "ESC";
            listKey.Add(keyText);
            head.Add(listKey);
            head.Add(MenuView.CloseSticker(() => {
                click();
                Close();
            }));
            listBody.Add(head);
            listItems = Element("menu__leagues-list");
            listBody.Add(listItems);
            listTrack = Element("menu__leagues-progress");
            listBody.Add(listTrack);
            var hintBox = Element("menu__leagues-hint");
            listHint = Text("menu__leagues-hint-text");
            hintBox.Add(listHint);
            listBody.Add(hintBox);
            list.Add(listBody);
            menu.Add(list);
        }

        public bool IsNewLeagueOpen => popup.ClassListContains(OpenClass);
        public bool IsListOpen => list.ClassListContains(OpenClass);

        /// <summary>The panel's top inset the list's head keeps, as the menu's top bar does.</summary>
        public float Top { get; set; } = 40f;

        // What the list needs from its top to its hint, upright and wide, before it shrinks.
        private const float ListHeight = 1700f;
        private const float WideListHeight = 1560f;

        // -- NEW LEAGUE! ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Opens the popup for the league <paramref name="level"/> is in: the badge in its new frame, the change from
        /// the league before, what the league gave (its sky, its plinth, the badge's frame) and the league after it.
        /// </summary>
        public void ShowNewLeague(int level, bool isDesktop)
        {
            var tier = PlayerRank.TierOf(level);
            popupCard.Clear();

            var title = Element("menu__league-title");
            title.Add(Stroked("menu__league-title-text", Strings.Get("menu.new_league")));
            popupCard.Add(title);

            var hero = Element("menu__league-hero");
            var burst = Element("menu__league-burst");
            burst.generateVisualContent += context => RoundOverMatch.DrawSunburst(context, Burst);
            hero.Add(burst);
            hero.Add(HudView.CreateLevelBadge(level, "menu__league-big"));
            popupCard.Add(hero);

            var change = Element("menu__league-change");
            change.Add(HudView.CreateLevelBadge(PlayerRank.StartOf(tier - 1), "menu__league-small"));
            change.Add(Icon("menu__league-arrow", DrawArrow));
            change.Add(HudView.CreateLevelBadge(PlayerRank.StartOf(tier), "menu__league-small"));
            popupCard.Add(change);

            popupCard.Add(Stroked("menu__league-name", LeagueName(tier)));
            var caption = Text("menu__league-caption");
            caption.text = Strings.Get("menu.you_unlocked");
            popupCard.Add(caption);

            var gets = Element("menu__league-gets");
            var swatch = Element("menu__league-swatch");
            swatch.style.backgroundImage = new StyleBackground(LeagueLook.CardSkyOf(tier));
            gets.Add(Get(swatch, "menu.unlock_background"));
            gets.Add(Get(Plinth(tier, "menu__league-plinth"), "menu.unlock_plinth"));
            gets.Add(Get(HudView.CreateLevelBadge(PlayerRank.StartOf(tier), "menu__league-get-badge"),
                "menu.unlock_badge"));
            popupCard.Add(gets);

            var next = Text("menu__league-next");
            next.text = tier + 1 < PlayerRank.Count
                ? Strings.Format("menu.next_league", PlayerRank.NameOfTier(tier + 1), PlayerRank.StartOf(tier + 1))
                : Strings.Get("menu.top_league");
            popupCard.Add(next);

            var go = new Button(() => {
                click();
                Continue();
            });
            go.RemoveFromClassList(Button.ussClassName);
            go.AddToClassList("button");
            go.AddToClassList("button--green");
            go.AddToClassList("button--big");
            go.AddToClassList("menu__league-continue");
            go.focusable = false;
            go.Add(Element("button__face"));
            var label = Element("stroke");
            foreach (var isFace in new[] { false, true }) {
                var text = Text("button__label");
                text.EnableInClassList("stroke__face", isFace);
                text.text = Strings.Get("result.continue");
                label.Add(text);
            }

            go.Add(label);
            if (isDesktop) {
                var key = Element("key");
                key.AddToClassList("menu__league-key");
                var keyText = Text("key__text");
                keyText.text = Strings.Get("common.space");
                key.Add(keyText);
                go.Add(key);
            }

            popupCard.Add(go);
            popup.AddToClassList(OpenClass);
        }

        /// <summary>CONTINUE, Space, Enter, Esc or a press on the dim: the menu behind, in its new colours.</summary>
        public void Continue()
        {
            if (!IsNewLeagueOpen) {
                return;
            }

            popup.RemoveFromClassList(OpenClass);
            closed();
        }

        private VisualElement Get(VisualElement picture, string key)
        {
            var tile = Element("menu__league-get");
            var pic = Element("menu__league-get-picture");
            pic.Add(picture);
            tile.Add(pic);
            var label = Text("menu__league-get-label");
            label.text = Strings.Get(key);
            tile.Add(label);
            return tile;
        }

        // -- The list ------------------------------------------------------------------------------------------------

        /// <summary>Opens the list of the leagues with the player's at <paramref name="level"/> marked.</summary>
        public void ShowList(int level, bool isDesktop)
        {
            listLevel = level;
            listKey.style.display = isDesktop ? DisplayStyle.Flex : DisplayStyle.None;
            list.style.backgroundImage = new StyleBackground(LeagueLook.CardSkyOf(PlayerRank.TierOf(level)));
            FillList();
            list.AddToClassList(OpenClass);
        }

        public void Close()
        {
            Continue();
            if (IsListOpen) {
                list.RemoveFromClassList(OpenClass);
                closed();
            }
        }

        /// <summary>Upright or wide, and the scale and the top the screen leaves; refills an open list that turned.</summary>
        public void Place(bool wide, float scale, float sheetScale, Vector2 size, float left, float right, float bottom)
        {
            var turned = wide != isWide;
            isWide = wide;
            list.EnableInClassList(WideClass, wide);
            // The list grows with the menu's touch scale from its top left, but never past the screen: its width is
            // what the sides leave, and it shrinks to fit when the screen is too short for every league.
            var height = size.y - Top - bottom;
            var fit = Mathf.Max(0.5f, Mathf.Min(scale, height / (wide ? WideListHeight : ListHeight)));
            listBody.style.left = left;
            listBody.style.top = Top;
            listBody.style.right = StyleKeyword.Auto;
            listBody.style.bottom = StyleKeyword.Auto;
            listBody.style.width = (size.x - left - right) / fit;
            listBody.style.height = height / fit;
            listBody.style.transformOrigin = new TransformOrigin(0f, 0f);
            listBody.style.scale = new Scale(new Vector3(fit, fit, 1f));
            popupCard.style.scale = new Scale(new Vector3(sheetScale, sheetScale, 1f));
            if (turned && IsListOpen) {
                FillList();
            }
        }

        private void FillList()
        {
            listTitle.text = listTitleFace.text = Strings.Get("menu.leagues");
            listHint.text = Strings.Get("menu.climb");
            listItems.Clear();
            listTrack.Clear();
            var current = PlayerRank.TierOf(listLevel);
            // Upright from the top league down; wide the row runs reversed, from rookie at the left.
            for (var tier = PlayerRank.Count - 1; tier >= 0; tier--) {
                listItems.Add(isWide ? Card(tier, current) : Row(tier, current));
            }

            if (isWide) {
                listTrack.Add(HudView.CreateLevelBadge(listLevel, "menu__leagues-progress-badge"));
                listTrack.Add(Track(current));
                if (current + 1 < PlayerRank.Count) {
                    listTrack.Add(HudView.CreateLevelBadge(PlayerRank.StartOf(current + 1),
                        "menu__leagues-progress-badge"));
                }
            }
        }

        // One league upright: its badge, its name and level (or, for the player's, the bar to the next), its plinth
        // and whether it is passed, the player's or still ahead.
        private VisualElement Row(int tier, int current)
        {
            var row = Item("menu__league-row", tier, current, LeagueLook.RowSkyOf(tier), new Vector2(0.18f, 0.5f),
                out var face);
            face.Add(Plate(tier == current ? listLevel : PlayerRank.StartOf(tier), "menu__league-row-badge"));
            var body = Element("menu__league-row-body");
            body.Add(Stroked("menu__league-row-name", PlayerRank.NameOfTier(tier)));
            if (tier == current) {
                body.Add(Track(current));
            }
            else {
                body.Add(FromLevel(tier));
                face.Add(body);
                face.Add(Plinth(tier, "menu__league-row-plinth"));
            }

            if (tier == current) {
                face.Add(body);
            }

            face.Add(Status(tier, current));
            return row;
        }

        // One league wide: a card, top down, its badge, plinth, name, level and state.
        private VisualElement Card(int tier, int current)
        {
            var card = Item("menu__league-card-item", tier, current, LeagueLook.CardSkyOf(tier), new Vector2(0.5f, 0.3f),
                out var face);
            face.Add(Plate(tier == current ? listLevel : PlayerRank.StartOf(tier), "menu__league-card-badge"));
            face.Add(Plinth(tier, "menu__league-card-plinth"));
            face.Add(Stroked("menu__league-card-name", PlayerRank.NameOfTier(tier)));
            face.Add(FromLevel(tier));
            face.Add(Element("menu__spacer"));
            face.Add(Status(tier, current));
            return card;
        }

        // A sticker in the league's sky with light rays from the given point (a share of its size): the navy drop
        // behind the face, and a gold ring round the player's league.
        private static VisualElement Item(string className, int tier, int current, Texture2D sky, Vector2 raysFrom,
            out VisualElement face)
        {
            var item = Element("menu__league-item");
            item.AddToClassList(className);
            item.EnableInClassList("menu__league-item--current", tier == current);
            item.EnableInClassList("menu__league-item--done", tier < current);
            if (tier == current) {
                item.Add(Element("menu__league-ring"));
            }

            item.Add(Element("menu__league-drop"));
            face = Element("menu__league-face");
            face.style.backgroundImage = new StyleBackground(sky);
            face.Add(Icon("menu__league-rays", context => DrawRays(context, raysFrom)));
            item.Add(face);
            return item;
        }

        // A level badge standing on its own navy drop, as the boards' plates do: the drop is a layer of its own, not a
        // thicker bottom border, so the number stays in the middle of the plate.
        private static VisualElement Plate(int level, string className)
        {
            var plate = Element("menu__league-plate");
            plate.AddToClassList(className + "-plate");
            plate.Add(Element("menu__league-plate-drop"));
            plate.Add(HudView.CreateLevelBadge(level, className));
            return plate;
        }

        // Stripes of light 8 degrees wide every 20 from a point, out past the corners; the face clips them.
        private static void DrawRays(MeshGenerationContext context, Vector2 from)
        {
            var r = context.visualElement.contentRect;
            if (r.width <= 0f || r.height <= 0f) {
                return;
            }

            var p = context.painter2D;
            var middle = new Vector2(r.x + r.width * from.x, r.y + r.height * from.y);
            var reach = new Vector2(r.width, r.height).magnitude;
            p.fillColor = Rays;
            for (var angle = 0f; angle < 360f; angle += 20f) {
                // From straight up, clockwise, as the boards' conic gradients: across, a row falls between stripes.
                var a = (angle - 90f) * Mathf.Deg2Rad;
                var b = (angle - 82f) * Mathf.Deg2Rad;
                p.BeginPath();
                p.MoveTo(middle);
                p.LineTo(middle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach);
                p.LineTo(middle + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * reach);
                p.ClosePath();
                p.Fill();
            }
        }

        private static VisualElement FromLevel(int tier)
        {
            var from = Text("menu__league-from");
            from.text = Strings.Format("menu.from_level", PlayerRank.StartOf(tier));
            return from;
        }

        // Passed: a green tick; the player's: YOU; ahead: a lock and the level it opens at.
        private static VisualElement Status(int tier, int current)
        {
            if (tier < current) {
                var done = Element("menu__league-done");
                done.Add(Icon("menu__league-tick", DrawTick));
                return done;
            }

            if (tier == current) {
                var you = Text("menu__league-you");
                you.text = Strings.Get("common.you");
                return you;
            }

            var locked = Element("menu__league-lock");
            locked.Add(Icon("menu__league-lock-icon", MenuView.DrawLock));
            var level = Text("menu__league-lock-text");
            level.text = Strings.Format("common.level_short", PlayerRank.StartOf(tier));
            locked.Add(level);
            return locked;
        }

        // The bar from the league's start to the next league's: LVL N at the left, the next league and its level at
        // the right; the top league's bar is full.
        private VisualElement Track(int tier)
        {
            var track = Element("menu__league-track");
            var fill = Element("menu__league-track-fill");
            fill.Add(Element("menu__league-track-shade"));
            track.Add(fill);
            var texts = Element("menu__league-track-text");
            var at = Text("menu__league-track-label");
            at.text = Strings.Format("common.level_short", listLevel);
            texts.Add(at);
            var next = Text("menu__league-track-label");
            float share;
            if (tier + 1 < PlayerRank.Count) {
                var start = PlayerRank.StartOf(tier);
                var end = PlayerRank.StartOf(tier + 1);
                share = Mathf.Clamp01((listLevel - start) / (float)(end - start));
                next.text = Strings.Format("menu.league_at", PlayerRank.NameOfTier(tier + 1), end);
            }
            else {
                share = 1f;
                next.text = Strings.Get("menu.top_league");
            }

            texts.Add(next);
            track.Add(texts);
            fill.style.width = Length.Percent(100f * share);
            return track;
        }

        // -- Pieces --------------------------------------------------------------------------------------------------

        /// <summary>A league's name with the word for league, as SILVER LEAGUE.</summary>
        public static string LeagueName(int tier) => Strings.Format("menu.league_name", PlayerRank.NameOfTier(tier));

        /// <summary>
        /// The league's plinth drawn small as the menu's 3D one: the navy side under a gold band, the gold rim, and
        /// inside it the top in the league's colour with its light middle. Only the top changes from league to league,
        /// as on the pedestal itself.
        /// </summary>
        private static VisualElement Plinth(int tier, string className)
        {
            var plinth = Element(className);
            plinth.generateVisualContent += context => DrawPlinth(context, tier);
            return plinth;
        }

        private static void DrawPlinth(MeshGenerationContext context, int tier)
        {
            var r = context.visualElement.contentRect;
            if (r.width <= 0f || r.height <= 0f) {
                return;
            }

            var p = context.painter2D;
            var line = Mathf.Max(2f, r.height * 0.075f);
            var rx = r.width / 2f - line / 2f;
            var ry = r.height * 0.3f - line / 2f;
            var cx = r.center.x;
            var topY = r.y + line / 2f + ry;
            var bottomY = r.yMax - line / 2f - ry;
            var band = (bottomY - topY) * 0.3f;
            p.lineWidth = line;
            p.strokeColor = Navy;
            p.lineJoin = LineJoin.Round;

            // The drum: navy, with the gold band round its top under the rim.
            p.fillColor = PlinthSide;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - rx, topY));
            p.LineTo(new Vector2(cx - rx, bottomY));
            HalfEllipse(p, cx, bottomY, rx, ry);
            p.LineTo(new Vector2(cx + rx, topY));
            p.ClosePath();
            p.Fill();
            p.fillColor = PlinthGoldDark;
            p.BeginPath();
            p.MoveTo(new Vector2(cx - rx, topY));
            p.LineTo(new Vector2(cx - rx, topY + band));
            HalfEllipse(p, cx, topY + band, rx, ry);
            p.LineTo(new Vector2(cx + rx, topY));
            p.ClosePath();
            p.Fill();
            p.BeginPath();
            p.MoveTo(new Vector2(cx - rx, topY));
            p.LineTo(new Vector2(cx - rx, bottomY));
            HalfEllipse(p, cx, bottomY, rx, ry);
            p.LineTo(new Vector2(cx + rx, topY));
            p.Stroke();

            // The gold rim, and the league's top sunk inside it, lit from the middle towards the back.
            p.fillColor = PlinthGold;
            p.BeginPath();
            Ellipse(p, cx, topY, rx, ry);
            p.Fill();
            p.Stroke();
            p.fillColor = LeagueLook.TopOf(tier);
            p.BeginPath();
            Ellipse(p, cx, topY + ry * 0.06f, rx * 0.8f, ry * 0.74f);
            p.Fill();
            p.fillColor = LeagueLook.TopLightOf(tier);
            p.BeginPath();
            Ellipse(p, cx, topY - ry * 0.06f, rx * 0.56f, ry * 0.46f);
            p.Fill();
            // A thin navy line inside the rim, where the top sinks below it.
            p.lineWidth = line * 0.45f;
            p.strokeColor = new Color(Navy.r, Navy.g, Navy.b, 0.35f);
            p.BeginPath();
            Ellipse(p, cx, topY + ry * 0.06f, rx * 0.8f, ry * 0.74f);
            p.Stroke();
        }

        // The lower half of an ellipse, from its left end to its right, by two quarter Béziers.
        private static void HalfEllipse(Painter2D p, float cx, float cy, float rx, float ry)
        {
            const float K = 0.5523f;
            p.BezierCurveTo(new Vector2(cx - rx, cy + ry * K), new Vector2(cx - rx * K, cy + ry), new Vector2(cx, cy + ry));
            p.BezierCurveTo(new Vector2(cx + rx * K, cy + ry), new Vector2(cx + rx, cy + ry * K), new Vector2(cx + rx, cy));
        }

        private static void Ellipse(Painter2D p, float cx, float cy, float rx, float ry)
        {
            const float K = 0.5523f;
            p.MoveTo(new Vector2(cx - rx, cy));
            HalfEllipse(p, cx, cy, rx, ry);
            p.BezierCurveTo(new Vector2(cx + rx, cy - ry * K), new Vector2(cx + rx * K, cy - ry), new Vector2(cx, cy - ry));
            p.BezierCurveTo(new Vector2(cx - rx * K, cy - ry), new Vector2(cx - rx, cy - ry * K), new Vector2(cx - rx, cy));
            p.ClosePath();
        }

        private static void DrawTick(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.strokeColor = Navy;
            p.lineWidth = r.width * 0.2f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + r.width * 0.16f, r.y + r.height * 0.52f));
            p.LineTo(new Vector2(r.x + r.width * 0.42f, r.y + r.height * 0.78f));
            p.LineTo(new Vector2(r.x + r.width * 0.86f, r.y + r.height * 0.24f));
            p.Stroke();
        }

        private static void DrawArrow(MeshGenerationContext context)
        {
            var r = context.visualElement.contentRect;
            var p = context.painter2D;
            p.strokeColor = Navy;
            p.lineWidth = r.height * 0.22f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(new Vector2(r.x + r.width * 0.1f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.86f, r.center.y));
            p.MoveTo(new Vector2(r.x + r.width * 0.55f, r.y + r.height * 0.18f));
            p.LineTo(new Vector2(r.x + r.width * 0.88f, r.center.y));
            p.LineTo(new Vector2(r.x + r.width * 0.55f, r.y + r.height * 0.82f));
            p.Stroke();
        }

        private static VisualElement Stroked(string className, string value)
        {
            var stroke = Element("stroke");
            foreach (var isFace in new[] { false, true }) {
                var label = Text("display");
                label.AddToClassList(className);
                label.EnableInClassList("stroke__face", isFace);
                label.text = value;
                stroke.Add(label);
            }

            return stroke;
        }

        private static VisualElement Icon(string className, Action<MeshGenerationContext> draw)
        {
            var icon = Element(className);
            icon.generateVisualContent += draw;
            return icon;
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
    }
}
