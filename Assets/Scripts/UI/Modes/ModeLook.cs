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
    }

    /// <summary>
    /// How an event mode looks wherever the shared screens show it, by its id: its two colours (the event card's and
    /// tile's gradient, the lobby card), its card art (the USS class <see cref="ArtClass"/>, the picture
    /// <c>sprite_mode_&lt;id&gt;.png</c>), its words (<see cref="NameKey"/>, <see cref="HintKey"/>), its own HUD and the
    /// icon of its points on the score table. One per mode in <c>UI/Modes/&lt;Mode&gt;Look.cs</c>, listed in
    /// <see cref="ModeLooks"/>.
    /// </summary>
    internal sealed class ModeLook
    {
        private readonly Func<VisualElement, VisualElement, HudViewModel, IModeHud> createHud;

        /// <param name="light">The light end of the mode's gradient, at the top.</param>
        /// <param name="deep">The deep end, at the bottom: also the colour of the mode's name and plates.</param>
        /// <param name="createHud">Builds the mode's own HUD over the name layer and the HUD's root; null for none.</param>
        /// <param name="paintPoints">Paints the icon of the mode's points on an element; null for the knock-out burst.</param>
        public ModeLook(SpecialRound kind, Color light, Color deep,
            Func<VisualElement, VisualElement, HudViewModel, IModeHud> createHud = null,
            Action<MeshGenerationContext> paintPoints = null)
        {
            Kind = kind;
            Light = light;
            Deep = deep;
            this.createHud = createHud;
            PaintPoints = paintPoints;
        }

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

        /// <summary>The mode's own HUD, or null when it has none.</summary>
        public IModeHud CreateHud(VisualElement layer, VisualElement root, HudViewModel model) =>
            layer != null ? createHud?.Invoke(layer, root, model) : null;
    }
}
