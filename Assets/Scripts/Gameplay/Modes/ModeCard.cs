namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// What the picture on a mode's menu card shows, drawn once from the game's own models by the explicit test
    /// <c>ModeCardArt</c> into <c>Assets/Art/Sprites/UI/sprite_mode_&lt;id&gt;.png</c>: a car of the catalog in one of
    /// the paints, seen as the catalog's portraits are, and a prop of the mode riding on its roof (the hot potato's
    /// bomb), copied from the mode's round where it shows.
    /// </summary>
    public sealed class ModeCard
    {
        public ModeCard(string carId, int color, string prop = null, float propRadius = 0f)
        {
            CarId = carId;
            Color = color;
            Prop = prop;
            PropRadius = propRadius;
        }

        /// <summary>The catalog id of the car.</summary>
        public string CarId { get; }

        /// <summary>The car's paint (<see cref="Cars.CarPaint"/>).</summary>
        public int Color { get; }

        /// <summary>
        /// The name of the scene object, shown in the mode's round, put on the car's roof; null for the car alone (a
        /// stub's placeholder until the mode has a prop of its own).
        /// </summary>
        public string Prop { get; }

        /// <summary>Metres from the prop's pivot to its bottom, so it sits on the roof.</summary>
        public float PropRadius { get; }
    }
}
