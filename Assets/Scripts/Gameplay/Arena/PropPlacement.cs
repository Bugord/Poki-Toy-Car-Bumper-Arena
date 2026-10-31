using System.Collections.Generic;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where the fixed props of a mechanic (jelly pads, the jack-in-the-box spots) lie: random places on the floor that
    /// are whole, clear of each other and of the cars' starting places. Drawn from a seeded <see cref="System.Random"/>,
    /// so a headless simulation puts them in the same places.
    /// </summary>
    public static class PropPlacement
    {
        private const int Attempts = 600;
        private const float MinClearance = 1.2f;

        /// <summary>
        /// Up to <paramref name="count"/> places at least <paramref name="edgeClearance"/> metres from the edge of the
        /// floor (which includes pits and holes), <paramref name="spacing"/> from one another and
        /// <paramref name="carClearance"/> from every car. If the floor is too crowded for that, the edge clearance is
        /// eased a little at a time before places are given up.
        /// </summary>
        public static List<Vector3> Scatter(System.Random random, IArenaEdge edge, IReadOnlyList<Car> cars, int count,
            float edgeClearance, float spacing, float carClearance)
        {
            var places = new List<Vector3>(count);
            var extent = edge.FullExtent;
            var clearance = edgeClearance;
            while (places.Count < count) {
                for (var attempt = 0; attempt < Attempts && places.Count < count; attempt++) {
                    var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var radius = Mathf.Sqrt((float)random.NextDouble()) * extent;
                    var point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    if (edge.DistanceToEdge(point) < clearance || IsCrowded(point, places, cars, spacing, carClearance)) {
                        continue;
                    }

                    places.Add(point);
                }

                if (places.Count >= count || clearance <= MinClearance) {
                    break;
                }

                clearance = Mathf.Max(MinClearance, clearance - 0.5f);
            }

            return places;
        }

        private static bool IsCrowded(Vector3 point, List<Vector3> places, IReadOnlyList<Car> cars, float spacing,
            float carClearance)
        {
            foreach (var place in places) {
                if ((place - point).sqrMagnitude < spacing * spacing) {
                    return true;
                }
            }

            for (var i = 0; i < cars.Count; i++) {
                var position = cars[i].Position;
                var flat = new Vector3(position.x - point.x, 0f, position.z - point.z);
                if (flat.sqrMagnitude < carClearance * carClearance) {
                    return true;
                }
            }

            return false;
        }
    }
}
