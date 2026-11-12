using System;
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
    /// screen allows, its foot tucked under the cards below: the player's own car with no name over it (the title says
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
        private const string ColumnsClass = "round-over--columns";
        // The rival's pill sits this far over the car's roof: the REVENGE NEXT ROUND tag under it and a little air.
        private const float RivalPillGap = 76f;

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
            root.AddToClassList(model.IsMatch ? MatchClass : PodiumClass);
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
            podium.parent.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            podium.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            car.RegisterCallback<GeometryChangedEvent>(OnPictureGeometry);
            car.RegisterCallback<GeometryChangedEvent>(OnRivalGeometry);
            winner.RegisterCallback<GeometryChangedEvent>(OnRivalGeometry);

            if (model.Podium != null) {
                car.style.backgroundImage = Background.FromRenderTexture((RenderTexture)model.Podium);
            }

            BuildWinner();
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
            car.UnregisterCallback<GeometryChangedEvent>(OnRivalGeometry);
            winner.UnregisterCallback<GeometryChangedEvent>(OnRivalGeometry);
            text.Dispose();
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

        // In the wide columns the podium is tall and the picture (twice as wide as high) sits in its middle, so the
        // rival's pill at the podium's top hung far over the car: it stands on the car's roof instead, which the
        // podium's camera keeps at WinnerPodium.RoofTop of the picture's height.
        private void OnRivalGeometry(GeometryChangedEvent evt)
        {
            if (!model.IsPodiumRival) {
                return;
            }

            if (!root.ClassListContains(ColumnsClass)) {
                winner.style.top = StyleKeyword.Null;
                return;
            }

            var box = car.layout;
            var height = Mathf.Min(box.height, box.width * 0.5f);
            var roof = box.y + (box.height - height) * 0.5f + height * WinnerPodium.RoofTop;
            var top = Mathf.Round(Mathf.Max(0f, roof - winner.layout.height - RivalPillGap));
            if (Mathf.Abs(winner.resolvedStyle.top - top) > 1.5f) {
                winner.style.top = top;
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
            if (!model.IsMatch && !isRival) {
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

        private VisualElement CreateRow(RoundStanding standing)
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

            var place = new Label(standing.Place.ToString()) { pickingMode = PickingMode.Ignore };
            place.AddToClassList("results__rank-text");
            rank.Add(place);
            row.Add(rank);

            row.Add(HudView.InLevelSlot(HudView.CreateLevelBadge(model.LevelOf(index), "results__level",
                index != 0 && index == model.Nemesis, true), "results__level-slot"));

            var name = new Label { pickingMode = PickingMode.Ignore };
            name.AddToClassList("results__name");
            text.Set(name, () => model.NameOf(index));
            row.Add(name);

            var knockouts = new Label(standing.Knockouts.ToString()) { pickingMode = PickingMode.Ignore };
            knockouts.AddToClassList("results__ko");
            knockouts.EnableInClassList("results__ko--zero", standing.Knockouts == 0);
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
