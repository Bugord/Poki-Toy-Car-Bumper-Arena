using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// Which item a mystery box gives: drawn at random, weighted by how far the car trails the match's leader in
    /// knock-outs, so a car behind gets the items that knock cars off (shockwave, bomb, glove) more often and a leader
    /// the ones that hold its place (shield, heavy). A pure function of the dice and the score, so a test can count it.
    /// </summary>
    public static class ItemRoll
    {
        /// <summary>
        /// Knock-outs the car at <paramref name="index"/> trails the leader by; 0 level or leading, or without a score.
        /// </summary>
        public static int GapOf(MatchScore score, int index)
        {
            if (score == null || index < 0 || index >= score.Count) {
                return 0;
            }

            return Mathf.Max(0, score.KnockoutsOf(score.Leader) - score.KnockoutsOf(index));
        }

        /// <summary>
        /// How far behind the car is for the roll, 0 (level) to 1 (<see cref="GameTuning.PickupSettings.TrailingGap"/>
        /// knock-outs behind or more).
        /// </summary>
        public static float TrailingOf(MatchScore score, int index, GameTuning.PickupSettings settings)
        {
            return Mathf.Clamp01(GapOf(score, index) / Mathf.Max(1f, settings.TrailingGap));
        }

        /// <summary>
        /// Draws an item for a car <paramref name="trailing"/> of the way behind (see <see cref="TrailingOf"/>), one
        /// number from <paramref name="random"/>.
        /// </summary>
        public static ItemKind Choose(System.Random random, GameTuning.PickupSettings settings, float trailing)
        {
            var total = 0f;
            for (var kind = ItemKind.Heavy; kind <= ItemKind.Glove; kind++) {
                total += settings.WeightOf(kind, trailing);
            }

            if (total <= 0f) {
                return ItemKind.Heavy;
            }

            var roll = (float)random.NextDouble() * total;
            var last = ItemKind.Heavy;
            for (var kind = ItemKind.Heavy; kind <= ItemKind.Glove; kind++) {
                var weight = settings.WeightOf(kind, trailing);
                if (weight <= 0f) {
                    continue;
                }

                last = kind;
                roll -= weight;
                if (roll < 0f) {
                    return kind;
                }
            }

            // Rounding can leave a sliver of the roll: the last kind that can come up takes it, never one weighted out.
            return last;
        }
    }
}
