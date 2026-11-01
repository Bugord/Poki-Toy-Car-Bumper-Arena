using System;
using System.Collections.Generic;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// The score of a long match, by car index, without keeping the cars: knock-outs (the points), falls, how many of
    /// them were knock-outs by a car, and when each car reached its points. The ranking is the most knock-outs, then
    /// the fewest falls (a fall costs no points, only time), then whoever reached the points first; cars level on all
    /// three go by index, so the places are always 1..N, one each.
    /// </summary>
    public sealed class MatchScore
    {
        private readonly int[] knockouts;
        private readonly int[] falls;
        private readonly int[] knockedOff;
        private readonly float[] scoredAt;

        // The indices best first, kept sorted as the score changes, so the HUD can read the leader every frame.
        private readonly int[] order;

        public MatchScore(int carCount)
        {
            knockouts = new int[carCount];
            falls = new int[carCount];
            knockedOff = new int[carCount];
            scoredAt = new float[carCount];
            order = new int[carCount];
            for (var index = 0; index < carCount; index++) {
                order[index] = index;
            }
        }

        public int Count => order.Length;

        /// <summary>
        /// The car first in the ranking; the player's (0) while nobody has scored.
        /// </summary>
        public int Leader => order.Length > 0 ? order[0] : RoundResult.Nobody;

        /// <summary>
        /// The car at a place in the ranking, from 0 for the leader: the HUD's scoreboard reads the order without
        /// a snapshot, so a score change costs it no allocation.
        /// </summary>
        public int IndexAt(int place) => order[place];

        public int KnockoutsOf(int index) => knockouts[index];

        public int FallsOf(int index) => falls[index];

        /// <summary>
        /// The falls of the car that another car gets the credit for.
        /// </summary>
        public int KnockedOffOf(int index) => knockedOff[index];

        /// <summary>
        /// Counts a fall of <paramref name="index"/>, and a point for <paramref name="by"/> when a car gets the credit
        /// (<see cref="RoundResult.Nobody"/> for a fall of its own), at <paramref name="time"/> into the match.
        /// </summary>
        public void RecordFall(int index, int by, float time)
        {
            falls[index]++;
            if (by != RoundResult.Nobody && by != index) {
                knockouts[by]++;
                knockedOff[index]++;
                scoredAt[by] = time;
            }

            // Insertion sort: a handful of cars, and no allocation.
            for (var i = 1; i < order.Length; i++) {
                var current = order[i];
                var slot = i;
                while (slot > 0 && Compare(current, order[slot - 1]) < 0) {
                    order[slot] = order[slot - 1];
                    slot--;
                }

                order[slot] = current;
            }
        }

        /// <summary>
        /// The car's place, 1 for the leader.
        /// </summary>
        public int PlaceOf(int index)
        {
            return Array.IndexOf(order, index) + 1;
        }

        /// <summary>
        /// The standings in place order; a match has no order of going out, so that is left at 0.
        /// </summary>
        public IReadOnlyList<RoundStanding> Snapshot()
        {
            var entries = new RoundStanding[order.Length];
            for (var place = 0; place < order.Length; place++) {
                var index = order[place];
                entries[place] = new RoundStanding(index, place + 1, knockouts[index], 0);
            }

            return Array.AsReadOnly(entries);
        }

        private int Compare(int a, int b)
        {
            var result = knockouts[b].CompareTo(knockouts[a]);
            if (result == 0) {
                result = falls[a].CompareTo(falls[b]);
            }

            if (result == 0) {
                result = scoredAt[a].CompareTo(scoredAt[b]);
            }

            return result != 0 ? result : a.CompareTo(b);
        }
    }
}
