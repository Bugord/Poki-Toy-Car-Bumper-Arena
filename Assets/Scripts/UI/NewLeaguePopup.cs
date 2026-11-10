using System;
using Bugord.App.Localization;
using Bugord.App.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// NEW LEAGUE!: the popup that greets a league the player has not seen, over the main menu (<see cref="MenuLeagues"/>)
    /// or over the result of the round that reached it (<see cref="RoundOverView"/>). The player's own car turns on
    /// the new league's pedestal in a framed piece of its sky (<see cref="GarageView.SkyFrame"/>), the league's emblem
    /// under it, the change of the badge, what the league gave (its sky, its pedestal, the badge's frame) and the next
    /// league, locked, as the goal; the menu's light turns behind it all. Styles in <c>ui_menu.uss</c>
    /// (<c>menu__league-*</c>).
    /// </summary>
    internal sealed class NewLeaguePopup
    {
        private const string OpenClass = "menu__popup--open";

        private readonly Action click;
        private readonly Action closed;
        private readonly VisualElement popup;

        /// <param name="host">Where it lives: the screen's element that carries <c>ui_menu.uss</c>.</param>
        /// <param name="click">The click sound of its button.</param>
        /// <param name="closed">Called once it closes.</param>
        public NewLeaguePopup(VisualElement host, Action click, Action closed)
        {
            this.click = click;
            this.closed = closed;
            popup = Element("menu__popup");
            popup.AddToClassList("menu__league-popup");
            popup.pickingMode = PickingMode.Position;
            popup.RegisterCallback<ClickEvent>(evt => {
                if (evt.target == popup || evt.target == Holder) {
                    Continue();
                }
            });
            var rays = Element("menu__league-light");
            rays.generateVisualContent += MenuView.DrawLight;
            rays.schedule.Execute(() =>
                rays.style.rotate = new Rotate(Time.unscaledTime / MenuView.RaysTurnSeconds * 360f % 360f)).Every(16);
            popup.Add(rays);
            Holder = Element("menu__league-holder");
            Card = Element("card");
            Card.AddToClassList("menu__league-card");
            Card.pickingMode = PickingMode.Position;
            Holder.Add(Card);
            popup.Add(Holder);
            host.Add(popup);
            popup.RegisterCallback<GeometryChangedEvent>(_ => Refit());
            Holder.RegisterCallback<GeometryChangedEvent>(_ => Refit());
        }

        public bool IsOpen => popup.ClassListContains(OpenClass);

        /// <summary>The card, to scale to the screen: <see cref="Fit"/>.</summary>
        public VisualElement Card { get; }

        // The card's holder: the screen's scale goes on it, so a pop of the card itself would not undo it.
        private VisualElement Holder { get; }

        // How far the card keeps from the screen's edges, and how far its tilted title rises over it.
        private const float Edge = 40f;
        private const float TitleRise = 130f;
        private float most = 1f;

        /// <summary>
        /// The most the card may grow (a phone on its side grows the menu's sheets); it shrinks by itself to whatever
        /// the screen leaves, and on a wide screen its picture is lower.
        /// </summary>
        public void Fit(float scale)
        {
            most = scale;
            Refit();
        }

        private void Refit()
        {
            var size = popup.layout.size;
            var own = Holder.layout.size;
            if (size.y <= 0f || own.y <= 0f) {
                return;
            }

            Card.EnableInClassList("menu__league-card--wide", size.x > size.y * 1.2f);
            var fit = Mathf.Min(most, Mathf.Min((size.y - 2f * Edge) / (own.y + TitleRise), (size.x - 2f * Edge) / own.x));
            Holder.style.scale = new Scale(new Vector3(fit, fit, 1f));
        }

        /// <summary>
        /// Opens the popup for the league <paramref name="level"/> is in. <paramref name="hero"/> is the player's 3D car
        /// on that league's pedestal (<see cref="GarageStage.ShowCar"/>); without it the flat pedestal stands in.
        /// </summary>
        public void Show(int level, bool isDesktop, Texture hero)
        {
            var tier = PlayerRank.TierOf(level);
            Card.Clear();

            var title = Element("menu__league-title");
            title.Add(MenuLeagues.Stroked("menu__league-title-text", Strings.Get("menu.new_league")));
            Card.Add(title);

            // The new look in place: the player's car on the league's pedestal under its sky, the league's emblem as a
            // medal on the frame's lower edge.
            var hero3D = Element("menu__league-stage");
            var frame = GarageView.SkyFrame(tier);
            frame.AddToClassList("menu__league-frame");
            var art = Element("garage__sheet-art");
            if (hero != null) {
                art.style.backgroundImage = new StyleBackground(Background.FromRenderTexture((RenderTexture)hero));
            }
            else {
                art.Add(MenuLeagues.Plinth(tier, "menu__league-frame-plinth"));
            }

            frame.Add(art);
            hero3D.Add(frame);
            hero3D.Add(MenuLeagues.Emblem(tier, "menu__league-medal"));
            Card.Add(hero3D);

            Card.Add(MenuLeagues.Stroked("menu__league-name", MenuLeagues.LeagueName(tier)));
            var change = Element("menu__league-change");
            change.Add(HudView.CreateLevelBadge(PlayerRank.StartOf(tier - 1), "menu__league-small"));
            change.Add(MenuLeagues.Icon("menu__league-arrow", MenuLeagues.DrawArrow));
            change.Add(HudView.CreateLevelBadge(level, "menu__league-big"));
            Card.Add(change);

            var caption = Text("menu__league-caption");
            caption.text = Strings.Get("menu.you_unlocked");
            Card.Add(caption);
            var gets = Element("menu__league-gets");
            var swatch = Element("menu__league-swatch");
            swatch.style.backgroundImage = new StyleBackground(LeagueLook.CardSkyOf(tier));
            gets.Add(Get(swatch, "menu.unlock_background"));
            gets.Add(Get(MenuLeagues.Plinth(tier, "menu__league-plinth"), "menu.unlock_plinth"));
            gets.Add(Get(HudView.CreateLevelBadge(PlayerRank.StartOf(tier), "menu__league-get-badge"),
                "menu.unlock_badge"));
            Card.Add(gets);

            // The league after this one, locked: the goal the climb leads to. The last league has none.
            var next = Element("menu__league-next");
            if (tier + 1 < PlayerRank.Count) {
                var emblem = MenuLeagues.Emblem(tier + 1, "menu__league-next-emblem");
                emblem.Add(MenuLeagues.Icon("menu__league-next-lock", MenuView.DrawLock));
                next.Add(emblem);
                var name = Text("menu__league-next-name");
                name.text = Strings.Format("menu.next_league", PlayerRank.NameOfTier(tier + 1),
                    PlayerRank.StartOf(tier + 1));
                next.Add(name);
            }
            else {
                next.AddToClassList("menu__league-next--top");
                var top = Text("menu__league-next-name");
                top.text = Strings.Get("menu.top_league");
                next.Add(top);
            }

            Card.Add(next);

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
            go.Add(StrokeText.Create(Strings.Get("result.continue"), null, out _, "button__label"));
            if (isDesktop) {
                var key = Element("key");
                key.AddToClassList("menu__league-key");
                var keyText = Text("key__text");
                keyText.text = Strings.Get("common.space");
                key.Add(keyText);
                go.Add(key);
            }

            Card.Add(go);
            PillShape.Keep(popup);
            popup.AddToClassList(OpenClass);
        }

        /// <summary>CONTINUE, Space, Enter, Esc or a press on the dim: what is behind, in its new colours.</summary>
        public void Continue()
        {
            if (!IsOpen) {
                return;
            }

            popup.RemoveFromClassList(OpenClass);
            closed();
        }

        /// <summary>Takes the popup off the screen for good (the result builds one for each league it greets).</summary>
        public void Remove() => popup.RemoveFromHierarchy();

        private static VisualElement Get(VisualElement picture, string key)
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

        private static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
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
