using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using UnityEngine;

namespace ToyCarArena.Gameplay.Round
{
    /// <summary>
    /// Where a car that fell in a match comes back: on the floor as it is now, not where the round started it. The
    /// start's spawn circle (or contour) lies on the outer waves, and once they have fallen a car put back there would
    /// drop into the void again and again. So the spot is picked from a grid over the floor, among the points at least
    /// <see cref="GameTuning.MatchSettings.SpawnMargin"/> inside its safe edge (<see cref="IArenaEdge.DistanceToEdge"/>:
    /// the rim, pits and holes alike, without the wave that already shakes), as far as it can be from the other cars
    /// still in play up to <see cref="GameTuning.MatchSettings.SpawnSpread"/>, and deeper inside the floor after that.
    /// If no point is that far inside, the deepest point there is will do. No randomness: the bots' seed alone replays
    /// a simulated match.
    /// </summary>
    public static class RespawnSpot
    {
        // Past the spread, a metre further from the edge outweighs this much of a metre further from the cars; the
        // spread itself always wins.
        private const float DepthWeight = 0.1f;

        public static Vector3 Find(IArenaEdge edge, GameTuning.MatchSettings settings, IReadOnlyList<Car> cars, Car except)
        {
            var step = Mathf.Max(0.25f, settings.SpawnGridStep);
            var extent = Mathf.Max(step, edge.FullExtent);
            var steps = Mathf.CeilToInt(extent / step);
            var best = Vector3.zero;
            var bestScore = float.NegativeInfinity;
            var deepest = Vector3.zero;
            var deepestDistance = float.NegativeInfinity;
            for (var row = -steps; row <= steps; row++) {
                for (var column = -steps; column <= steps; column++) {
                    var point = new Vector3(column * step, 0f, row * step);
                    var depth = edge.DistanceToEdge(point);
                    if (depth > deepestDistance) {
                        deepestDistance = depth;
                        deepest = point;
                    }

                    if (depth < settings.SpawnMargin) {
                        continue;
                    }

                    var clearance = Mathf.Min(settings.SpawnSpread, NearestCar(point, cars, except));
                    var score = clearance + depth * DepthWeight;
                    if (score > bestScore) {
                        bestScore = score;
                        best = point;
                    }
                }
            }

            return float.IsNegativeInfinity(bestScore) ? deepest : best;
        }

        private static float NearestCar(Vector3 point, IReadOnlyList<Car> cars, Car except)
        {
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                if (car == except || car.IsOut) {
                    continue;
                }

                var position = car.Position;
                var distance = new Vector2(position.x - point.x, position.z - point.z).magnitude;
                nearest = Mathf.Min(nearest, distance);
            }

            return nearest;
        }
    }
}
