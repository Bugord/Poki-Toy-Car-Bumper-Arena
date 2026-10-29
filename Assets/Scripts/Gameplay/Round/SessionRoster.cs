using System;
using ToyCarArena.Gameplay.Cars;
using Random = UnityEngine.Random;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Assigns distinct handles and random cars once per visit, so a rival keeps its name, colour and model next round.
    /// </summary>
    public sealed class SessionRoster : ISessionRoster
    {
        private readonly string[] names = (string[])BotNames.All.Clone();
        private readonly CarCatalog catalog;
        private readonly CarCatalog.Entry[] cars = new CarCatalog.Entry[BotNames.All.Length];
        private bool assigned;

        public SessionRoster(CarCatalog catalog)
        {
            this.catalog = catalog;
        }

        private void Assign()
        {
            if (assigned) {
                return;
            }

            if (catalog.Cars.Count == 0) {
                throw new InvalidOperationException("SessionRoster needs a non-empty CarCatalog.");
            }

            for (var i = names.Length - 1; i > 0; i--) {
                var other = Random.Range(0, i + 1);
                (names[i], names[other]) = (names[other], names[i]);
            }

            for (var i = 0; i < cars.Length; i++) {
                cars[i] = catalog.Cars[Random.Range(0, catalog.Cars.Count)];
            }

            assigned = true;
        }

        public string NameOf(int carIndex)
        {
            if (carIndex < 0 || carIndex > names.Length) {
                throw new ArgumentOutOfRangeException(nameof(carIndex));
            }

            if (carIndex == 0) {
                return "YOU";
            }

            Assign();
            return names[carIndex - 1];
        }

        public CarCatalog.Entry CarOf(int carIndex)
        {
            if (carIndex < 1 || carIndex > cars.Length) {
                throw new ArgumentOutOfRangeException(nameof(carIndex));
            }

            Assign();
            return cars[carIndex - 1];
        }
    }
}
