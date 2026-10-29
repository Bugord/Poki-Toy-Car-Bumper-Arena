namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>Stable colour IDs in prefab swatch and UI theme order, independent of a car's spawn index.</summary>
    public static class CarPaint
    {
        public const int Count = 8;
        private static readonly string[] Names = { "green", "red", "blue", "yellow", "purple", "orange", "pink", "dark" };

        public static bool IsValid(int id) => id >= 0 && id < Count;
        public static int OrGreen(int id) => IsValid(id) ? id : 0;
        public static string NameOf(int id) => Names[OrGreen(id)];
    }
}
