using System;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToyCarArena.UI
{
    /// <summary>
    /// An event mode's own HUD: made by its look (<see cref="ModeLook.CreateHud"/>) when its round opens the HUD, ticked
    /// every frame after the cars' names are placed, disposed with the HUD.
    /// </summary>
    internal interface IModeHud : IDisposable
    {
        /// <param name="top">Reference pixels of the top inset: the bar, the Poki button and the safe area.</param>
        /// <param name="left">The safe area's left inset, in reference pixels.</param>
        /// <param name="right">The safe area's right inset, in reference pixels.</param>
        void Tick(Camera camera, float top, float left, float right);

        /// <summary>
        /// Whether the mode's HUD shows the item hint its own way (on the shared element): the HUD's one-time hint then
        /// keeps off it, so it neither pops the hint in nor takes it down on its own clock.
        /// </summary>
        bool OwnsItemHint => false;
    }

    /// <summary>
    /// A mode HUD with something of its own in each bot's handle over its car (OVERHEAT's heat): the HUD calls it for
    /// every handle it builds, again when a handle is rebuilt (a car back, a new rival), so the mode keeps the latest
    /// element per car.
    /// </summary>
    internal interface IModeNameHud
    {
        /// <summary>Adds the mode's part to the handle line of car <paramref name="index"/>.</summary>
        void MarkName(VisualElement line, int index);
    }

    /// <summary>
    /// How an event mode looks wherever the shared screens show it, by its id: its two colours (the event card's and
    /// tile's gradient, the lobby card), its card art (the USS class <see cref="ArtClass"/>, the picture
    /// <c>sprite_mode_&lt;id&gt;.png</c>), its words (<see cref="NameKey"/>, <see cref="HintKey"/>), its own HUD and the
    /// icon of its points on the score table and the header of their column (<see cref="PointsKey"/>), and whether the
    /// win streak's crowns stay off the round (<see cref="HidesStreakCrowns"/>). One per mode in
    /// <c>UI/Modes/&lt;Mode&gt;Look.cs</c>, listed in <see cref="ModeLooks"/>.
    /// </summary>
    internal sealed class ModeLook
    {
        private readonly Func<VisualElement, VisualElement, HudViewModel, IModeHud> createHud;

        /// <param name="light">The light end of the mode's gradient, at the top.</param>
        /// <param name="deep">The deep end, at the bottom: also the colour of the mode's name and plates.</param>
        /// <param name="createHud">Builds the mode's own HUD over the name layer and the HUD's root; null for none.</param>
        /// <param name="paintPoints">Paints the icon of the mode's points on an element; null for the knock-out burst.</param>
        /// <param name="pointsKey">The string key of the points column's header on the result; null for points.</param>
        /// <param name="hidesStreakCrowns">
        /// Whether the win streak's crowns (over the player's car and after the bots' names) stay off the round: a mode
        /// whose own thing is a crown would be mistaken for them.
        /// </param>
        public ModeLook(SpecialRound kind, Color light, Color deep,
            Func<VisualElement, VisualElement, HudViewModel, IModeHud> createHud = null,
            Action<MeshGenerationContext> paintPoints = null, string pointsKey = null, bool hidesStreakCrowns = false)
        {
            Kind = kind;
            Light = light;
            Deep = deep;
            this.createHud = createHud;
            PaintPoints = paintPoints;
            PointsKey = pointsKey ?? DefaultPointsKey;
            HidesStreakCrowns = hidesStreakCrowns;
        }

        /// <summary>The header of a column of points: PTS.</summary>
        public const string DefaultPointsKey = "result.points_short";

        public SpecialRound Kind { get; }

        public EventMode Mode => EventModes.ModeOf(Kind);

        /// <summary>The mode's id, as its strings, art and analytics spell it (<see cref="RoundRules.IdOf"/>).</summary>
        public string Id => RoundRules.IdOf(Kind);

        public Color Light { get; }
        public Color Deep { get; }

        /// <summary>The menu's name of the mode: <c>menu.&lt;id with underscores&gt;</c>.</summary>
        public string NameKey => "menu." + Id.Replace('-', '_');

        /// <summary>The menu's one line on the mode.</summary>
        public string HintKey => NameKey + "_hint";

        /// <summary>The USS class that puts the mode's card art on an element (<c>ui_menu.uss</c>).</summary>
        public string ArtClass => "mode-art--" + Id;

        public Action<MeshGenerationContext> PaintPoints { get; }

        /// <summary>The string key of the header over the mode's points on the result's table.</summary>
        public string PointsKey { get; }

        /// <summary>The win streak's crowns stay off the mode's round (the player's and the bots').</summary>
        public bool HidesStreakCrowns { get; }

        /// <summary>The mode's own HUD, or null when it has none.</summary>
        public IModeHud CreateHud(VisualElement layer, VisualElement root, HudViewModel model) =>
            layer != null ? createHud?.Invoke(layer, root, model) : null;
    }
}
