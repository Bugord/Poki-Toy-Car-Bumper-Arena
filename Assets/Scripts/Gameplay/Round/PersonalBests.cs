using Bugord.Core.Storage;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The player's best place and most knock-outs in a round, kept in the kit's storage (the browser's localStorage on
    /// the web), so a returning player still has them to beat. A target for the next round, not a progression: nothing
    /// unlocks with them.
    /// </summary>
    public sealed class PersonalBests
    {
        // Keys of the kit's storage, which already prefixes them with the game's own name.
        private const string PlaceKey = "best_place";
        private const string KnockoutsKey = "best_knockouts";

        private readonly IKeyValueStorage storage;

        public PersonalBests(IKeyValueStorage storage)
        {
            this.storage = storage;
        }

        /// <summary>
        /// The best place so far, 1 being a win; 0 before any round with a place.
        /// </summary>
        public int Place => storage.GetInt(PlaceKey);

        /// <summary>
        /// The most cars knocked out in one round so far.
        /// </summary>
        public int Knockouts => storage.GetInt(KnockoutsKey);

        /// <summary>
        /// Compares a round with the bests from before it, then keeps whatever it beat.
        /// </summary>
        public RoundRecord Record(RoundResult result)
        {
            var bestPlace = Place;
            var bestKnockouts = Knockouts;

            // A round of one car (the player alone) has no place to speak of.
            var hasPlace = result.CarCount > 1;
            var isBestPlace = hasPlace && bestPlace > 0 && result.Place < bestPlace;
            if (hasPlace && (bestPlace == 0 || result.Place < bestPlace)) {
                storage.SetInt(PlaceKey, result.Place);
            }

            var isBestKnockouts = result.Knockouts > bestKnockouts;
            if (isBestKnockouts) {
                storage.SetInt(KnockoutsKey, result.Knockouts);
            }

            return new RoundRecord(result, bestPlace, bestKnockouts, isBestPlace, isBestKnockouts);
        }
    }
}
