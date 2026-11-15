using System;
using Bugord.App.Localization;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a match on the result screen: the winner's car turning on its podium in the menu's soft light, with a
    /// crowned name pill above it (level and name: the car itself shows its colour), and the results table, a row per car in place order. A phone held upright shows the
    /// top three and, apart, the player's own row; a wide screen every car, the table at the left of the podium.
    /// <para>
    /// After a round of the last car standing the podium stands alone in the middle, without the table, as big as the
    /// screen allows and always whole (PlacePicture): the player's own car with no name over it (the title says
    /// whose it is) and no crown either (a flat one reads oddly on the 3D car), and the car that pushed them off on a
    /// loss to one, its name red with
    /// the REVENGE horns and a red REVENGE NEXT ROUND tag under it.
    /// </para>
    /// <para>
    /// Everything springs in once, on the panel's scheduler (real time, so a hit-stop at the buzzer slows nothing), and
    /// nothing waits for it: NEXT MATCH works from the start.
    /// </para>
    /// </summary>
    internal sealed class RoundOverMatch : IDisposable
    {
        private const string MatchClass = "round-over--match";
        private const string PodiumClass = "round-over--podium";
        private const string EnterClass = "results--enter";
        private const string RowEnterClass = "results__row--enter";
        private const string RowPopClass = "results__row--pop";
        private const string RaysEnterClass = "podium__rays--enter";
        private const string WinnerEnterClass = "podium__winner--enter";
        private const string CrownEnterClass = "podium__crown--enter";
        // The wide columns' table stands this far in from the left (ui_screens: .results--wide). An event mode's tall
        // table is a little narrower there, so the podium beside it gets the width its car needs.
        private const float WideTableLeft = 80f;
        private const float TallTableWidth = 660f;
        private const float TallPodiumLeft = 780f;
        private const float WidePodiumTop = 20f;
        // Between the bottom of the teams' table and the podium under it: the crown over the pill reaches up into it.
        private const float TeamPodiumGap = 40f;
        // The podium goes under the table only when its car is this much bigger there: beside it, as designed, the
        // car stays whole above the bottom edge and next to the rows it won.
        private const float UnderBias = 1.25f;
        // The height a name pill takes over the car (its crown, the pill, its tag and the air to the roof), for the
        // choice between beside and under the table.
        private const float PillRoom = 210f;
        // The car and its pedestal fill about this much width per height of the podium picture (twice as wide as
        // high, its sides empty): the picture may run past its box, as its sides are see-through.
        private const float ContentAspect = 1.3f;
        // Over the pill the winner's crown (ui_screens: .podium__crown, 60 px over it); under its tag, the roof.
        private const float CrownRoom = 64f;
        private const float PillRoofGap = 12f;

        // The narrow form shows this many leaders, then the player's own row when it is lower.
        private const int CompactLeaders = 3;

        // When each part comes in after the screen shows, in milliseconds (the design's timings).
        private const long RaysAt = 100;
        private const long TableAt = 500;
        private const long FirstRowAt = 600;
        private const long RowStep = 70;
        private const long WinnerAt = 750;
        private const long CrownAt = 950;
        private const long YouPopAt = 1800;
        private const long YouPopAgainAt = 2150;
        private const long PopHold = 150;

        // The light turns once in this many seconds, as the menu's; the car's podium has its own, faster turn.
        private const float RaysTurnSeconds = 24f;
        // The level reward's sunburst.
        private const int RayCount = 12;
        // The light's span against the podium picture's width: past the pedestal, its faded tips lost in the sky.
        private const float RaysScale = 1.3f;
        // The light's middle down the picture: behind the car's body over the pedestal, where the menu's light sits.
        private const float RaysMiddle = 0.55f;

        private readonly VisualElement root;
        private readonly RoundOverViewModel model;
        private readonly VisualElement podium;
        private readonly VisualElement rays;
        private readonly VisualElement car;
        private readonly VisualElement winner;
        private readonly VisualElement crown;
        private readonly VisualElement table;
        private readonly VisualElement rows;
        private readonly Label winnerTag;
        private readonly Label rivalTag;
        private readonly Label subtitle;
        private readonly LiveText text = new();
        private VisualElement youRow;
        private IVisualElementScheduledItem turn;
        private bool isWide;
        private bool isPlayed;
        private float shownAt;

        private RoundOverMatch(VisualElement root, RoundOverViewModel model)
        {
            this.root = root;
            this.model = model;
            root.AddToClassList(model.IsTimed ? MatchClass : PodiumClass);
            podium = root.Q<VisualElement>("podium");
            rays = root.Q<VisualElement>("podium-rays");
            rays.generateVisualContent += MenuView.DrawLight;
            // The light goes to the back of the screen, under the title and the stickers, as the menu's under its tiles:
            // in the podium, after the title in the tree, it washed over the title's letters.
            var screen = root.Q<VisualElement>("round-over") ?? root;
            screen.Insert(0, rays);
            car = root.Q<VisualElement>("podium-car");
            winner = root.Q<VisualElement>("podium-winner");
            crown = root.Q<VisualElement>("podium-crown");
            crown.generateVisualContent += DrawCrown;
            table = root.Q<VisualElement>("results");
            rows = root.Q<VisualElement>("results-rows");
            subtitle = root.Q<Label>("subtitle");
            // An event mode counts points of its own, not knock-outs: the column says so.
            var pointsHead = root.Q<Label>(className: "results__title--ko");
            if (model.IsEvent && pointsHead != null) {
                var pointsKey = ModeLooks.Of(model.Kind)?.PointsKey ?? ModeLook.DefaultPointsKey;
                text.Set(pointsHead, () => Strings.Get(pointsKey));
            }
            winnerTag = root.Q<Label>("podium-winner-tag");
            rivalTag = root.Q<Label>("podium-rival-tag");
            podium.parent.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            podium.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            car.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            podium.RegisterCallback<GeometryChangedEvent>(PlacePicture);
            winner.RegisterCallback<GeometryChangedEvent>(PlacePicture);
            winnerTag.RegisterCallback<GeometryChangedEvent>(PlacePicture);
            rivalTag.RegisterCallback<GeometryChangedEvent>(PlacePicture);
            podium.parent.RegisterCallback<GeometryChangedEvent>(PlaceWidePodium);
            table.RegisterCallback<GeometryChangedEvent>(PlaceWidePodium);
            table.RegisterCallback<GeometryChangedEvent>(KeepSubtitle);

            if (model.Podium != null) {
                car.style.backgroundImage = Background.FromRenderTexture((RenderTexture)model.Podium);
            }

            BuildWinner();
            // In teams the podium shows the winning team's best car (the player's team's on a draw): MVP, not WINNER,
            // which a draw has none of.
            if (model.TeamPoints.Count == 2) {
                winnerTag.ClearBinding("text");
                text.Set(winnerTag, () => Strings.Get("result.mvp"));
            }

            Layout(false);

            table.AddToClassList(EnterClass);
            rays.AddToClassList(RaysEnterClass);
            winner.AddToClassList(WinnerEnterClass);
            crown.AddToClassList(CrownEnterClass);
        }

        /// <summary>The podium part of the result (with the table after a match), or null without a podium to show.</summary>
        public static RoundOverMatch Create(VisualElement root, RoundOverViewModel model) =>
            model.Standings.Count > 0 || model.Podium != null ? new RoundOverMatch(root, model) : null;

        /// <summary>Every car on a wide screen, the leaders and the player's own row on a narrow one.</summary>
        public void SetWide(bool wide)
        {
            if (wide != isWide) {
                Layout(wide);
            }
        }

        public void Play()
        {
            isPlayed = true;
            shownAt = Time.realtimeSinceStartup;
            turn = rays.schedule.Execute(Turn).Every(16);
            After(RaysAt, () => rays.RemoveFromClassList(RaysEnterClass));
            After(TableAt, () => table.RemoveFromClassList(EnterClass));
            var index = 0;
            foreach (var row in rows.Children()) {
                var each = row;
                After(FirstRowAt + RowStep * index++, () => each.RemoveFromClassList(RowEnterClass));
            }

            After(WinnerAt, () => winner.RemoveFromClassList(WinnerEnterClass));
            After(CrownAt, () => crown.RemoveFromClassList(CrownEnterClass));
            After(YouPopAt, PopYou);
            After(YouPopAgainAt, PopYou);
        }

        /// <summary>
        /// Lets go of the podium's texture: the arena that draws it unloads while this screen fades out under the curtain.
        /// </summary>
        public void ReleasePodium()
        {
            car.style.backgroundImage = StyleKeyword.None;
            turn?.Pause();
        }

        public void Dispose()
        {
            ReleasePodium();
            rays.generateVisualContent -= MenuView.DrawLight;
            crown.generateVisualContent -= DrawCrown;
            podium.parent.UnregisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            podium.UnregisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            car.UnregisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            podium.UnregisterCallback<GeometryChangedEvent>(PlacePicture);
            winner.UnregisterCallback<GeometryChangedEvent>(PlacePicture);
            winnerTag.UnregisterCallback<GeometryChangedEvent>(PlacePicture);
            rivalTag.UnregisterCallback<GeometryChangedEvent>(PlacePicture);
            podium.parent.UnregisterCallback<GeometryChangedEvent>(PlaceWidePodium);
            table.UnregisterCallback<GeometryChangedEvent>(PlaceWidePodium);
            table.UnregisterCallback<GeometryChangedEvent>(KeepSubtitle);
            text.Dispose();
        }

        /// <summary>
        /// On a phone held upright the table can be taller than the room left under the subtitle, and centred beside
        /// the podium it rises over it, the place showing under the table's top. The table's own row says the place, so
        /// the subtitle steps aside then; it keeps its room, so the table does not move.
        /// </summary>
        private void KeepSubtitle(GeometryChangedEvent _)
        {
            if (subtitle == null) {
                return;
            }

            // The letters, not the row: a table that rises a little above its row keeps the subtitle; it steps aside
            // only when the table's top reaches above its baseline (the label's box holds a descent under it). Layout
            // positions, not world bounds: the table grows in from its top, and its transition would move them.
            var isCovered = false;
            if (table.resolvedStyle.display != DisplayStyle.None && table.layout.y < -1f && table.parent != null
                && subtitle.parent != null) {
                var top = table.parent.ChangeCoordinatesTo(subtitle.parent, table.layout.position).y;
                isCovered = top < subtitle.layout.y + subtitle.layout.height * 0.8f;
            }

            subtitle.style.visibility = isCovered ? Visibility.Hidden : new StyleEnum<Visibility>(StyleKeyword.Null);
        }

        private void After(long delay, Action action) => root.schedule.Execute(action).StartingIn(delay);

        private void PopYou()
        {
            if (youRow == null) {
                return;
            }

            youRow.AddToClassList(RowPopClass);
            youRow.schedule.Execute(() => youRow.RemoveFromClassList(RowPopClass)).StartingIn(PopHold);
        }

        private void Turn()
        {
            rays.style.rotate = new Rotate((Time.realtimeSinceStartup - shownAt) / RaysTurnSeconds * 360f % 360f);
        }

        // The light follows the car's picture (twice as wide as high, fitted in the car's box), wherever the layout puts
        // it: the rival's car lower under its pill, the round's foot under the cards, the wide columns' tall box. The light
        // lies at the back of the screen, so the car's box is taken over to its parent's space whenever it, the podium or
        // the row holding it moves.
        private void OnPictureGeometry(GeometryChangedEvent evt)
        {
            var layer = rays.parent;
            if (layer == null || car.parent == null) {
                return;
            }

            var box = car.parent.ChangeCoordinatesTo(layer, car.layout);
            var width = Mathf.Min(box.width, box.height * 2f);
            if (float.IsNaN(width) || width <= 0f) {
                return;
            }

            var height = width * 0.5f;
            var middle = new Vector2(box.center.x, box.y + (box.height - height) * 0.5f + height * RaysMiddle);
            var span = width * RaysScale;
            rays.style.width = span;
            rays.style.height = span;
            // An absolute child stands inside its parent's border, the layout's coordinates from its outer edge.
            rays.style.left = middle.x - span * 0.5f - layer.resolvedStyle.borderLeftWidth;
            rays.style.top = middle.y - span * 0.5f - layer.resolvedStyle.borderTopWidth;
        }

        /// <summary>
        /// The car's picture in the podium, every layout alike: the whole car always shows, no card covers its foot,
        /// and a name pill stands over the roof (which the podium's camera keeps at WinnerPodium.RoofTop of the
        /// picture's height), never on the car. The picture is twice as wide as high but the car fills only the middle
        /// of it, so the podium's width limits the car, not the picture: in a narrow column beside the table its
        /// see-through sides run past the column (the table, later in the tree, is drawn over them). The pill and the
        /// car together stand in the middle of the podium's height.
        /// </summary>
        private void PlacePicture(GeometryChangedEvent evt)
        {
            var area = podium.contentRect;
            if (float.IsNaN(area.width) || area.width <= 0f || float.IsNaN(area.height) || area.height <= 0f) {
                return;
            }

            var hasPill = winner.resolvedStyle.display != DisplayStyle.None;
            var crownRoom = hasPill && model.IsPodiumWinner ? CrownRoom : 0f;
            var pill = hasPill ? PillHeight() : 0f;
            var gap = hasPill ? PillRoofGap : 0f;
            var over = crownRoom + pill + gap;
            // Without a pill the whole picture fits the height; with one its empty top over the roof tucks behind it.
            var height = hasPill ? (area.height - over) / (1f - WinnerPodium.RoofTop) : area.height;
            height = Mathf.Floor(Mathf.Max(0f, Mathf.Min(height, area.width / ContentAspect)));
            var used = hasPill ? over + height * (1f - WinnerPodium.RoofTop) : height;
            var offset = Mathf.Round(Mathf.Max(0f, (area.height - used) * 0.5f));
            var top = hasPill ? offset + over - height * WinnerPodium.RoofTop : offset;
            if (hasPill) {
                Place(v => winner.style.top = v, winner.resolvedStyle.top, offset + crownRoom);
            }

            car.style.right = StyleKeyword.Auto;
            car.style.bottom = StyleKeyword.Auto;
            Place(v => car.style.left = v, car.resolvedStyle.left, Mathf.Round((area.width - height * 2f) * 0.5f));
            Place(v => car.style.top = v, car.resolvedStyle.top, Mathf.Round(top));
            Place(v => car.style.width = v, car.resolvedStyle.width, height * 2f);
            Place(v => car.style.height = v, car.resolvedStyle.height, height);
        }

        // The pill and the tag hanging under it (WINNER, MVP or REVENGE NEXT ROUND), in its own layout: its entrance
        // scales it, which its layout does not see.
        private float PillHeight()
        {
            var height = winner.layout.height;
            foreach (var tag in new[] { winnerTag, rivalTag }) {
                if (tag.resolvedStyle.display != DisplayStyle.None && !float.IsNaN(tag.layout.yMax)) {
                    height = Mathf.Max(height, tag.layout.yMax);
                }
            }

            return float.IsNaN(height) ? 0f : height;
        }

        // A wide screen with an event mode's table (two teams, or seven cars by points): the table, a little narrower,
        // at the left and the podium beside it, as designed, unless under the table its car would be clearly bigger (a
        // 4:3 screen, whose left part is narrow): then the podium takes the whole width and the height left under it,
        // and the table moves to the middle over it. Kept until a value moves by more than snapping can, as the
        // columns, so the layout settles.
        private void PlaceWidePodium(GeometryChangedEvent evt)
        {
            if (!IsTallTable) {
                return;
            }

            if (!isWide) {
                table.style.left = StyleKeyword.Null;
                table.style.width = StyleKeyword.Null;
                podium.style.marginLeft = StyleKeyword.Null;
                podium.style.marginTop = StyleKeyword.Null;
                podium.style.minHeight = StyleKeyword.Null;
                return;
            }

            table.style.width = TallTableWidth;
            var area = podium.parent.contentRect.size;
            var box = table.layout;
            if (float.IsNaN(area.x) || area.x <= 0f || float.IsNaN(box.height) || box.height <= 0f) {
                return;
            }

            var beside = Picture(area.x - TallPodiumLeft, area.y - WidePodiumTop - PillRoom);
            var underTop = box.height + TeamPodiumGap;
            var under = Picture(area.x, area.y - underTop - PillRoom);
            var isUnder = under > beside * UnderBias;
            Place(v => table.style.left = v, table.resolvedStyle.left,
                isUnder ? Mathf.Round((area.x - TallTableWidth) * 0.5f) : WideTableLeft);
            Place(v => podium.style.marginLeft = v, podium.resolvedStyle.marginLeft, isUnder ? 0f : TallPodiumLeft);
            Place(v => podium.style.marginTop = v, podium.resolvedStyle.marginTop,
                isUnder ? Mathf.Round(underTop) : WidePodiumTop);
            // Its own height comes from the space left, not a minimum that could push the row taller.
            podium.style.minHeight = 0f;
        }

        // An event mode's table, of two teams or of every car by points: tall enough that the podium beside it is small.
        // The plain match keeps its own layout.
        private bool IsTallTable => model.TeamPoints.Count == 2 || model.IsTimed && model.IsEvent;

        // The height of the car's picture a box of this size holds: as high as the box, as wide as the car needs.
        private static float Picture(float width, float height) =>
            Mathf.Max(0f, Mathf.Min(width / ContentAspect, height));

        private static void Place(Action<StyleLength> set, float current, float value)
        {
            if (float.IsNaN(current) || Mathf.Abs(current - value) > 1.5f) {
                set(value);
            }
        }

        private void BuildWinner()
        {
            var index = model.PodiumCar;
            var isRival = model.IsPodiumRival;
            var name = root.Q<Label>("podium-name");
            // The rival's badge horned, as over the REVENGE bot on the arena, which this car is next round.
            var isMarked = isRival || (index != 0 && index == model.Nemesis);
            name.parent.Insert(name.parent.IndexOf(name),
                HudView.CreateLevelBadge(model.LevelOf(index), "podium__level", isMarked, true));

            name.EnableInClassList("podium__name--revenge", isMarked);
            text.Set(name, () => model.NameOf(index));
            crown.style.display = model.IsPodiumWinner ? DisplayStyle.Flex : DisplayStyle.None;
            podium.EnableInClassList("podium--rival", isRival);
            // A round's own car needs no name over it, and so no crown: the title already says it is the player's, and
            // the WINNER ribbon under it says they won.
            if (!model.IsTimed && !isRival) {
                winner.style.display = DisplayStyle.None;
            }

            root.Q<Label>("podium-winner-tag").style.display = model.IsPodiumWinner ? DisplayStyle.Flex : DisplayStyle.None;
            root.Q<Label>("podium-rival-tag").style.display = isRival ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // The rows again for the form: every car, or the leaders and, a little apart, the player when lower.
        private void Layout(bool wide)
        {
            isWide = wide;
            table.EnableInClassList("results--wide", wide);
            rows.Clear();
            youRow = null;
            // A round in teams: each team under its header with its points, every car of it, its best with MVP.
            if (model.TeamPoints.Count == 2) {
                table.AddToClassList("results--teams");
                LayoutTeams(wide);
                return;
            }

            var standings = model.Standings;
            var playerAt = 0;
            for (var i = 0; i < standings.Count; i++) {
                if (standings[i].Index == 0) {
                    playerAt = i;
                }
            }

            for (var i = 0; i < standings.Count; i++) {
                if (!wide && i >= CompactLeaders && i != playerAt) {
                    continue;
                }

                if (!wide && i == playerAt && i > CompactLeaders) {
                    var gap = new Label("···") { pickingMode = PickingMode.Ignore };
                    gap.AddToClassList("results__gap");
                    gap.EnableInClassList(RowEnterClass, !isPlayed);
                    rows.Add(gap);
                }

                rows.Add(CreateRow(standings[i]));
            }
        }

        // The player's team first, then the other; within a team in place order, so its best car leads it. The compact
        // form (a phone held upright, where every car of both teams would not fit over the cards) keeps each team's
        // header and its best car, and the player when not its team's best.
        private void LayoutTeams(bool wide)
        {
            var playerTeam = RoundTeams.Blue;
            foreach (var standing in model.Standings) {
                if (standing.Index == 0) {
                    playerTeam = standing.Team;
                }
            }

            for (var pass = 0; pass < 2; pass++) {
                var team = pass == 0 ? playerTeam : 1 - playerTeam;
                rows.Add(CreateTeamHeader(team));
                var rank = 0;
                foreach (var standing in model.Standings) {
                    if (standing.Team != team) {
                        continue;
                    }

                    rank++;
                    if (!wide && rank > 1 && standing.Index != 0) {
                        continue;
                    }

                    rows.Add(CreateRow(standing, team, rank, rank == 1 && standing.Points > 0));
                }
            }
        }

        private VisualElement CreateTeamHeader(int team)
        {
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("results__team");
            header.AddToClassList(team == RoundTeams.Red ? "results__team--red" : "results__team--blue");
            header.EnableInClassList(RowEnterClass, !isPlayed);
            // The team's name and points outlined as every title is (a stroke, not a USS outline, which smears small).
            var name = StrokeText.Create(string.Empty, "results__team-name", out var names, "display",
                "results__team-name-text");
            foreach (var label in names) {
                text.Set(label, () => Strings.Get(team == RoundTeams.Red ? "hud.team_red" : "hud.team_blue"));
            }

            header.Add(name);
            var points = model.TeamPoints[team];
            var other = model.TeamPoints[1 - team];
            if (points > other) {
                var tag = new Label { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("results__team-tag");
                text.Set(tag, () => Strings.Get("result.winner"));
                header.Add(tag);
            }

            var score = StrokeText.Create(points.ToString(), "results__team-points", out _, "display",
                "results__team-points-text");
            header.Add(score);
            return header;
        }

        private VisualElement CreateRow(RoundStanding standing, int team = RoundTeams.None, int teamRank = 0,
            bool isBest = false)
        {
            var index = standing.Index;
            var isPlayer = index == 0;
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList("results__row");
            // Rows laid out again for a turned screen after the entrance are simply there.
            row.EnableInClassList(RowEnterClass, !isPlayed);
            row.EnableInClassList("results__row--you", isPlayer);
            row.EnableInClassList("results__row--revenge", !isPlayer && index == model.Nemesis);

            var rank = new VisualElement { pickingMode = PickingMode.Ignore };
            rank.AddToClassList("results__rank");
            if (standing.Place <= 3) {
                rank.AddToClassList($"results__rank--{standing.Place}");
            }

            // In teams the circle is the team's colour with the car's rank within its team.
            if (team != RoundTeams.None) {
                rank.RemoveFromClassList($"results__rank--{standing.Place}");
                rank.AddToClassList(team == RoundTeams.Red ? "results__rank--red" : "results__rank--blue");
            }

            var place = new Label((team != RoundTeams.None ? teamRank : standing.Place).ToString())
                { pickingMode = PickingMode.Ignore };
            place.AddToClassList("results__rank-text");
            rank.Add(place);
            row.Add(rank);

            row.Add(HudView.InLevelSlot(HudView.CreateLevelBadge(model.LevelOf(index), "results__level",
                index != 0 && index == model.Nemesis, true), "results__level-slot"));

            var name = new Label { pickingMode = PickingMode.Ignore };
            name.AddToClassList("results__name");
            text.Set(name, () => model.NameOf(index));
            row.Add(name);
            // The team's best contributor, the most points on it.
            if (isBest) {
                var mvp = new Label { pickingMode = PickingMode.Ignore };
                mvp.AddToClassList("results__mvp");
                text.Set(mvp, () => Strings.Get("result.mvp"));
                row.Add(mvp);
            }

            // The column is the round's score: knock-outs in the match, the mode's points in a mode.
            var knockouts = new Label(standing.Points.ToString()) { pickingMode = PickingMode.Ignore };
            knockouts.AddToClassList("results__ko");
            knockouts.EnableInClassList("results__ko--zero", standing.Points == 0);
            row.Add(knockouts);
            if (isPlayer) {
                youRow = row;
            }

            return row;
        }

        /// <summary>
        /// Wedges of one colour from the middle, drawn as triangles so they stay sharp at any size (USS has no conic
        /// gradient): the golden rays behind the level reward and NEW LEAGUE!. The podium has the menu's soft light.
        /// </summary>
        internal static void DrawSunburst(MeshGenerationContext context, Color color)
        {
            var rect = context.visualElement.contentRect;
            var centre = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var painter = context.painter2D;
            painter.fillColor = color;
            var half = Mathf.PI / RayCount * 0.42f;
            for (var i = 0; i < RayCount; i++) {
                var angle = i * Mathf.PI * 2f / RayCount;
                painter.BeginPath();
                painter.MoveTo(centre);
                painter.LineTo(centre + new Vector2(Mathf.Cos(angle - half), Mathf.Sin(angle - half)) * radius);
                painter.LineTo(centre + new Vector2(Mathf.Cos(angle + half), Mathf.Sin(angle + half)) * radius);
                painter.ClosePath();
                painter.Fill();
            }
        }

        // A gold three-point crown with the navy outline of the stickers.
        /// <summary>A gold crown with a navy outline filling the element; the HUD's streak crown draws the same.</summary>
        internal static void DrawCrown(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            var w = rect.width;
            var h = rect.height;
            var painter = context.painter2D;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0.06f * w, 0.94f * h));
            painter.LineTo(new Vector2(0.06f * w, 0.26f * h));
            painter.LineTo(new Vector2(0.3f * w, 0.58f * h));
            painter.LineTo(new Vector2(0.5f * w, 0.06f * h));
            painter.LineTo(new Vector2(0.7f * w, 0.58f * h));
            painter.LineTo(new Vector2(0.94f * w, 0.26f * h));
            painter.LineTo(new Vector2(0.94f * w, 0.94f * h));
            painter.ClosePath();
            painter.fillColor = new Color32(0xff, 0xc9, 0x3c, 0xff);
            painter.Fill();
            painter.strokeColor = new Color32(0x1d, 0x2b, 0x4f, 0xff);
            painter.lineWidth = Mathf.Max(4f, w * 0.07f);
            painter.lineJoin = LineJoin.Round;
            painter.Stroke();
        }
    }
}
