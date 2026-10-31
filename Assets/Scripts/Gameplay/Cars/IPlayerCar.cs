using System;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Cosmetic model and colour selection, independent of saved XP and unlock rules.
    /// </summary>
    public interface IPlayerCar
    {
        string SelectedCar { get; }
        int SelectedColor { get; }
        bool SelectColor(int id);
        event Action<int> ColorChanged;
    }

    /// <summary>
    /// Standalone scenes and simulations have no garage or saved progression.
    /// </summary>
    public sealed class FirstPlayerCar : IPlayerCar
    {
        public FirstPlayerCar(CarCatalog catalog) => SelectedCar = catalog.Cars[0].Id;

        public string SelectedCar { get; }
        public int SelectedColor => 0;
        public bool SelectColor(int id) => false;
        public event Action<int> ColorChanged { add { } remove { } }
    }
}
