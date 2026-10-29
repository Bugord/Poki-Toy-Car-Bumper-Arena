namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The arena only needs the selected cosmetic identity, not the player's saved progression.
    /// </summary>
    public interface IPlayerCar
    {
        string SelectedCar { get; }
    }

    /// <summary>
    /// Standalone scenes and simulations have no garage or saved progression.
    /// </summary>
    public sealed class FirstPlayerCar : IPlayerCar
    {
        public FirstPlayerCar(CarCatalog catalog) => SelectedCar = catalog.Cars[0].Id;

        public string SelectedCar { get; }
    }
}
