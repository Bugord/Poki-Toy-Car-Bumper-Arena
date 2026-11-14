using System;
using System.Collections.Generic;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Records the round without retaining cars. Falls in one physics observation share a place, matching the
    /// player's existing result; survivors share first and are displayed in knockout order.
    /// </summary>
    public sealed class RoundStandings
    {
        private readonly int[] knockouts;
        private readonly int[] places;
        private readonly int[] outOrder;
        private int outCount;
        private int recordedCount;

        public RoundStandings(int carCount)
        {
            knockouts = new int[carCount];
            places = new int[carCount];
            outOrder = new int[carCount];
        }

        public void RecordOut(int index, int by)
        {
            if (outOrder[index] != 0) {
                return;
            }
            outOrder[index] = ++outCount;
            if (by != RoundResult.Nobody && by != index) {
                knockouts[by]++;
            }
        }

        /// <summary>Called after every car below the fall height has been processed, before resolving the round.</summary>
        public void CompleteFrame()
        {
            var place = places.Length == 1 ? 1 : Math.Max(2, places.Length - outCount + 1);
            for (var index = 0; index < places.Length; index++) {
                if (outOrder[index] > recordedCount) {
                    places[index] = place;
                }
            }
            recordedCount = outCount;
        }

        public IReadOnlyList<RoundStanding> Snapshot()
        {
            var entries = new RoundStanding[places.Length];
            for (var index = 0; index < entries.Length; index++) {
                entries[index] = new RoundStanding(index, places[index] == 0 ? 1 : places[index], knockouts[index],
                    outOrder[index]);
            }
            Array.Sort(entries, Compare);
            return Array.AsReadOnly(entries);
        }

        private static int Compare(RoundStanding a, RoundStanding b)
        {
            var order = a.Place.CompareTo(b.Place);
            if (order == 0) {
                order = b.Knockouts.CompareTo(a.Knockouts);
            }
            return order != 0 ? order : a.Index.CompareTo(b.Index);
        }
    }

    public readonly struct RoundStanding
    {
        public readonly int Index;
        public readonly int Place;
        public readonly int Knockouts;
        public readonly int OutOrder;

        /// <summary>
        /// The points of a timed round (<see cref="MatchScore.PointsOf"/>): the knock-outs in the long match; the
        /// knock-outs in a round of last car standing, which has no points.
        /// </summary>
        public readonly int Points;

        /// <summary>The car's team, from 0 (the player's); -1 without teams.</summary>
        public readonly int Team;

        public RoundStanding(int index, int place, int knockouts, int outOrder, int? points = null, int team = -1)
        {
            this.Points = points ?? knockouts;
            this.Team = team;
            this.Index = index;
            this.Place = place;
            this.Knockouts = knockouts;
            this.OutOrder = outOrder;
        }
    }
}
