namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// How the player fired a boost, so touch gestures can be told apart in the first round's analytics.
    /// </summary>
    public enum BoostInputMethod
    {
        Button,
        Tap,
        Finger,
        Key,
        Mouse,
        Gamepad,
    }
}
