using System;
using Random = UnityEngine.Random;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Assigns shuffled, distinct handles once per visit, so a rival is recognisable by name and colour next round.
    /// </summary>
    public sealed class SessionRoster : ISessionRoster
    {
        private readonly string[] names = (string[])BotNames.All.Clone();

        public SessionRoster()
        {
            for (var i = names.Length - 1; i > 0; i--) {
                var other = Random.Range(0, i + 1);
                (names[i], names[other]) = (names[other], names[i]);
            }
        }

        public string NameOf(int carIndex)
        {
            if (carIndex < 0 || carIndex > names.Length) {
                throw new ArgumentOutOfRangeException(nameof(carIndex));
            }

            return carIndex == 0 ? "YOU" : names[carIndex - 1];
        }
    }
}
