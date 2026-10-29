using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UIKit.Runtime.Abstractions;

namespace ToyCarArena.UI
{
    /// <summary>
    /// The end of a round: how it went against the player's bests, its saved XP reward, and Play Again. The result is
    /// true once the player asks for another round, regardless of how much of the reward animation has played.
    /// </summary>
    public sealed class RoundOverViewModel : ResultViewModel<bool>, IPayloadReceiver<RoundRecord>
    {
        private readonly ISessionRoster roster;
        private readonly CarCatalog catalog;

        public RoundOverViewModel(ISessionRoster roster, CarCatalog catalog)
        {
            this.roster = roster;
            this.catalog = catalog;
        }

        public XpGain XpGain { get; private set; }

        public string NextCarText(int level)
        {
            var next = catalog.NextLocked(level);
            return next == null ? "ALL CARS UNLOCKED" : $"NEXT: {next.DisplayName}";
        }

        /// <summary>Only cars earned by this reward appear, even if storage is edited while the screen is open.</summary>
        public IEnumerable<CarCatalog.Entry> NewCarsAt(int level)
        {
            foreach (var id in XpGain.UnlockedCarIds) {
                var car = catalog.Find(id);
                if (car != null && car.UnlockLevel == level) {
                    yield return car;
                }
            }
        }

        public string KnockedOutByName => KnockedOutBy == RoundResult.Nobody ? "" : roster.NameOf(KnockedOutBy);

        public bool RevengeTaken { get; private set; }

        public RoundOutcome Outcome { get; private set; }

        /// <summary>
        /// The player's place, 1 being the winner.
        /// </summary>
        public int Place { get; private set; }

        /// <summary>
        /// How many cars started the round.
        /// </summary>
        public int CarCount { get; private set; }

        public float Seconds { get; private set; }

        /// <summary>
        /// Cars the player knocked out.
        /// </summary>
        public int Knockouts { get; private set; }

        /// <summary>
        /// The index of the car that knocked the player out, which tells its colour, or
        /// <see cref="RoundResult.Nobody"/>.
        /// </summary>
        public int KnockedOutBy { get; private set; }

        /// <summary>
        /// The best place before this round, 0 for none yet.
        /// </summary>
        public int BestPlace { get; private set; }

        /// <summary>
        /// The most knock-outs in a round before this one.
        /// </summary>
        public int BestKnockouts { get; private set; }

        public bool IsBestPlace { get; private set; }

        public bool IsBestKnockouts { get; private set; }

        public void SetPayload(RoundRecord payload)
        {
            var result = payload.Result;
            XpGain = payload.XpGain;
            Outcome = result.Outcome;
            Place = result.Place;
            CarCount = result.CarCount;
            Seconds = result.Seconds;
            Knockouts = result.Knockouts;
            KnockedOutBy = result.KnockedOutBy;
            RevengeTaken = result.RevengeTaken;
            BestPlace = payload.BestPlace;
            BestKnockouts = payload.BestKnockouts;
            IsBestPlace = payload.IsBestPlace;
            IsBestKnockouts = payload.IsBestKnockouts;
        }

        public void PlayAgain() => SetResult(true);
    }
}
