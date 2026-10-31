using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// The floors that are not circles, worked out from the tuning alone: the tiles and their waves, the part that never
    /// crumbles, the field of distances to the nearest void, the starting contour and the bots' way round a pit. No scene
    /// is loaded, so these are quick and only the geometry is in question.
    /// </summary>
    public sealed class ArenaShapeTests
    {
        private static readonly string[] Shaped = { "tabletop", "candy", "playroom" };

        private static GameTuning LoadTuning()
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
#else
            throw new System.NotSupportedException("Editor tests only.");
#endif
        }

        private static GameTuning.ArenaSettings Arena(GameTuning tuning, string id)
        {
            return tuning.Maps.First(map => map.Id == id).Arena;
        }

        private static ArenaShape Shape(string id, out GridEdge edge, int seed = 7)
        {
            var shape = ArenaShape.Create(Arena(LoadTuning(), id), new System.Random(seed));
            Assert.That(shape, Is.Not.Null, $"{id} is not a tiled floor");
            edge = new GridEdge(shape);
            return shape;
        }

        private static IReadOnlyList<WaveState> All(int count, WaveState state)
        {
            return Enumerable.Repeat(state, count).ToArray();
        }

        [Test]
        public void SandboxIsStillACircle()
        {
            Assert.That(ArenaShape.Create(Arena(LoadTuning(), "sandbox"), new System.Random(1)), Is.Null);
        }

        [Test]
        public void EachArenaHasItsWavesAndTilesAndACore([Values("tabletop", "candy", "playroom")] string id,
            [Values(1, 2, 3)] int seed)
        {
            var settings = Arena(LoadTuning(), id);
            var shape = Shape(id, out var edge, seed);
            // The board crumbles in five waves (corners, then four strips); the others as many as the tuning says.
            var expected = id == "tabletop" ? 5 : settings.Waves;
            Assert.That(shape.WaveCount, Is.EqualTo(expected));
            var tilesIn = new int[shape.WaveCount];
            for (var g = 0; g < shape.GroupCount; g++) {
                tilesIn[shape.WaveOf(g)]++;
            }

            Assert.That(tilesIn, Has.All.GreaterThan(0), "Every wave has a tile.");
            var coreFaces = Enumerable.Range(0, shape.FaceCount).Count(face => shape.GroupOf(face) == ArenaShape.Core);
            Assert.That(coreFaces, Is.GreaterThan(0));
            Assert.That(shape.GroupCount, Is.LessThanOrEqualTo(70), "A tile is an object: keep the draw calls near the sandbox's.");

            // The tiles make the layout's objects and its meshes.
            var layout = new TiledLayout(shape, edge, 0.08f);
            try {
                Assert.That(layout.Tiles.Count, Is.EqualTo(shape.GroupCount));
                Assert.That(layout.WaveCount, Is.EqualTo(shape.WaveCount));
                Assert.That(layout.CoreMesh.triangles.Length, Is.GreaterThan(0));
                Assert.That(layout.ColliderMesh.triangles.Length, Is.GreaterThan(0));
                for (var w = 0; w < layout.WaveCount; w++) {
                    Assert.That(layout.Wave(w).Spots, Is.Not.Null.And.Not.Empty);
                }
            }
            finally {
                foreach (var mesh in layout.Meshes) {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void TheCoreSurvivesEveryWaveAndLeavesRoomForADuel([Values("tabletop", "candy", "playroom")] string id)
        {
            var shape = Shape(id, out var edge);
            var before = edge.DistanceToEdge(Vector3.zero);
            Assert.That(edge.Update(All(shape.WaveCount, WaveState.Gone)), Is.True);

            // A disc of 4 m radius fits in what is left: somewhere the safe floor is 4 m or more from every void.
            var deepest = 0f;
            Vector3 where = default;
            for (var x = -20f; x <= 20f; x += 0.25f) {
                for (var z = -25f; z <= 25f; z += 0.25f) {
                    var depth = edge.DistanceToEdge(new Vector3(x, 0f, z));
                    if (depth > deepest) {
                        deepest = depth;
                        where = new Vector3(x, 0f, z);
                    }
                }
            }

            TestContext.WriteLine($"{id}: deepest safe point after all waves {deepest:F2} m at {where}; at the middle before {before:F2}");
            Assert.That(deepest, Is.GreaterThanOrEqualTo(4f), $"{id}: no room for a disc of 4 m at the end");

            // The core is still solid, and the whole floor is not: the tiles are gone.
            Assert.That(edge.IsSolid(where), Is.True);
            Assert.That(edge.SolidExtent, Is.LessThan(shape.FullExtent));
            Assert.That(edge.SolidExtent, Is.EqualTo(edge.SafeExtent).Within(0.001f));
        }

        [Test]
        public void TheFieldIsPositiveOnTheFloorAndNegativeInTheVoids()
        {
            // Every tile and the core: the middle of each face is on the floor.
            foreach (var id in Shaped) {
                var shape = Shape(id, out var edge);
                for (var face = 0; face < shape.FaceCount; face++) {
                    var middle = Vector2.zero;
                    foreach (var corner in shape.Face(face)) {
                        middle += shape.Vertices[corner];
                    }

                    middle /= shape.Face(face).Length;
                    var point = new Vector3(middle.x, 0f, middle.y);
                    Assert.That(edge.DistanceToEdge(point), Is.GreaterThan(0f), $"{id}: face {face} is not floor");
                    Assert.That(edge.IsSolid(point), Is.True, $"{id}: face {face} is not solid");
                }

                Assert.That(edge.DistanceToEdge(new Vector3(40f, 0f, 0f)), Is.LessThan(-10f), $"{id}: far outside");
                Assert.That(edge.Inward(new Vector3(40f, 0f, 0f)).x, Is.LessThan(0f), $"{id}: the way back from outside");
            }

            // The hole of the donut and the pits of the stadium are gaps, whatever the camera sees.
            Shape("candy", out var donut);
            Assert.That(donut.DistanceToEdge(Vector3.zero), Is.LessThan(-1.5f), "the hole");
            Assert.That(donut.IsSolid(Vector3.zero), Is.False);
            Shape("playroom", out var stadium);
            Assert.That(stadium.DistanceToEdge(new Vector3(10f, 0f, 0f)), Is.LessThan(-1f), "the pit on the right");
            Assert.That(stadium.DistanceToEdge(new Vector3(-10f, 0f, 1f)), Is.LessThan(-1f), "the pit on the left");
            Assert.That(stadium.DistanceToEdge(new Vector3(0f, 0f, 0f)), Is.GreaterThan(7.5f), "the turntable is clear of the pits");
        }

        [Test]
        public void InwardPointsAwayFromTheNearestVoid()
        {
            Shape("candy", out var donut);
            var fromHole = donut.Inward(new Vector3(2.6f, 0f, 0f));
            Assert.That(fromHole.x, Is.GreaterThan(0.8f), "away from the hole, outward from the middle");
            var fromRim = donut.Inward(new Vector3(13.5f, 0f, 0f));
            Assert.That(fromRim.x, Is.LessThan(-0.8f), "away from the rim, towards the middle");

            Shape("playroom", out var stadium);
            Assert.That(stadium.Inward(new Vector3(8.1f, 0f, 0f)).x, Is.LessThan(-0.8f), "away from the right pit");
            Assert.That(stadium.Inward(new Vector3(-8.1f, 0f, 0f)).x, Is.GreaterThan(0.8f), "away from the left pit");
            Assert.That(stadium.Inward(new Vector3(0f, 0f, -20f)).z, Is.GreaterThan(0.8f), "away from the near rim");
            Assert.That(stadium.Inward(new Vector3(0f, 0f, 30f)).z, Is.LessThan(0f), "from beyond the grid");

            Shape("tabletop", out var board);
            Assert.That(board.Inward(new Vector3(9.5f, 0f, 9.5f)).magnitude, Is.EqualTo(1f).Within(0.01f));
            Assert.That(board.Inward(new Vector3(9.5f, 0f, 9.5f)).x, Is.LessThan(0f));
            Assert.That(board.Inward(new Vector3(9.5f, 0f, 9.5f)).z, Is.LessThan(0f));
        }

        [Test]
        public void AShakingTileIsVoidInTheSafeViewAndFloorInTheSolidView()
        {
            var shape = Shape("tabletop", out var edge);
            var layout = new TiledLayout(shape, edge, 0.08f);
            try {
                var spot = layout.Wave(0).Spots[0];
                var point = new Vector3(spot.x, 0f, spot.y);
                Assert.That(edge.DistanceToEdge(point), Is.GreaterThan(0f));
                var states = new WaveState[shape.WaveCount];
                states[0] = WaveState.Warning;
                Assert.That(layout.Refresh(states), Is.False, "the collider holds until the wave falls");
                Assert.That(edge.DistanceToEdge(point), Is.LessThan(0f), "no longer safe");
                Assert.That(edge.DistanceToSolidEdge(point), Is.GreaterThan(0f), "still solid");
                Assert.That(layout.Contains(0, point), Is.True);
                Assert.That(layout.Contains(1, point), Is.False);
                states[0] = WaveState.Falling;
                Assert.That(layout.Refresh(states), Is.True, "the collider changes when the wave falls");
                Assert.That(edge.DistanceToSolidEdge(point), Is.LessThan(0f));
                Assert.That(edge.IsSolid(point), Is.False);
            }
            finally {
                foreach (var mesh in layout.Meshes) {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void TheOutlineFollowsTheShape()
        {
            var shape = Shape("tabletop", out _);
            Assert.That(shape.RimRadius(Vector2.right), Is.EqualTo(10.5f).Within(0.01f));
            Assert.That(shape.RimRadius(new Vector2(1f, 1f)), Is.EqualTo(13.6f).Within(0.05f), "the rounded corner");
            shape = Shape("playroom", out _);
            Assert.That(shape.RimRadius(Vector2.right), Is.EqualTo(14.5f).Within(0.01f));
            Assert.That(shape.RimRadius(Vector2.up), Is.EqualTo(21.5f).Within(0.05f));
            Assert.That(shape.Voids.Count, Is.EqualTo(2), "two pits");
            shape = Shape("candy", out _);
            Assert.That(shape.Voids.Count, Is.EqualTo(1), "one hole");
        }

        [Test]
        public void TheCarsStartOnTheFloorClearOfPitsAndTheTurntable([Values(4, 7)] int count,
            [Values("tabletop", "candy", "playroom")] string id)
        {
            var tuning = LoadTuning();
            var shape = Shape(id, out var edge);
            var spots = new List<Vector3>();
            for (var i = 0; i < count; i++) {
                Assert.That(edge.TryGetSpawn(i, count, out var spot), Is.True);
                spots.Add(spot);
                // A car is 2.6 m long: it starts two metres or more from any void, so not at the lip of a pit.
                Assert.That(edge.DistanceToEdge(spot), Is.GreaterThanOrEqualTo(2f), $"{id}: car {i} starts at the edge");
                if (id == "playroom") {
                    Assert.That(spot.magnitude, Is.GreaterThan(tuning.Carousel.Radius + 1f), "off the turntable");
                }
            }

            Assert.That(spots[0].z, Is.LessThanOrEqualTo(spots.Min(spot => spot.z) + 0.01f), "the player is nearest the camera");
            for (var i = 0; i < spots.Count; i++) {
                for (var j = i + 1; j < spots.Count; j++) {
                    Assert.That(Vector3.Distance(spots[i], spots[j]), Is.GreaterThan(3f), $"{id}: cars {i} and {j} start together");
                }
            }
        }

        [Test]
        public void ARebuildOfTheFieldIsCheap([Values("tabletop", "candy", "playroom")] string id)
        {
            var shape = Shape(id, out var edge);
            var states = new WaveState[shape.WaveCount];
            // Warm up, then time a warning and a fall of every wave.
            states[0] = WaveState.Warning;
            edge.Update(states);
            states[0] = WaveState.Standing;
            edge.Update(states);
            var worst = 0f;
            for (var w = 0; w < shape.WaveCount; w++) {
                states[w] = WaveState.Warning;
                edge.Update(states);
                worst = Mathf.Max(worst, edge.LastRebuildMilliseconds);
                states[w] = WaveState.Gone;
                edge.Update(states);
                worst = Mathf.Max(worst, edge.LastRebuildMilliseconds);
            }

            TestContext.WriteLine($"{id}: worst rebuild of one grid {worst:F2} ms over {edge.Rebuilds} rebuilds");
            Assert.That(worst, Is.LessThan(25f));
        }

        [Test]
        public void ASolidChordRunsFromTheFarthestSolidPointEachWay()
        {
            var shape = Shape("tabletop", out var edge);
            edge.SolidChord(Vector3.zero, Vector3.right, out var behind, out var ahead);
            Assert.That(behind, Is.EqualTo(10.5f).Within(0.3f));
            Assert.That(ahead, Is.EqualTo(10.5f).Within(0.3f));
            // Off the middle, the line meets the rounded corner: shorter, and the same both ways.
            edge.SolidChord(new Vector3(0f, 0f, 9.5f), Vector3.right, out behind, out ahead);
            Assert.That(behind, Is.EqualTo(ahead).Within(0.3f));
            Assert.That(ahead, Is.LessThan(10.5f));
            Assert.That(shape.FullExtent, Is.EqualTo(13.6f).Within(0.05f));
        }

        [Test]
        public void ABotGoesRoundAPitAndNotRoundTheRim()
        {
            Shape("playroom", out var stadium);
            var side = 1f;
            var hold = 0f;
            // From the left of the middle to a car beyond the right pit, straight across the pit.
            var from = new Vector3(-8f, 0f, 0f);
            var aim = new Vector3(13f, 0f, 0f) - from;
            var bent = PitDetour.Bend(stadium, from, aim, 1.5f, 24f, 8, 0.5f, 1f, ref side, ref hold);
            Assert.That(Mathf.Abs(bent.normalized.z), Is.GreaterThan(0.5f), "the bot swerves along the rim");
            Assert.That(bent.magnitude, Is.EqualTo(aim.magnitude).Within(0.01f));
            Assert.That(hold, Is.GreaterThan(0f), "it keeps its side for a moment");

            // The side is kept while the hold lasts, even if the line moves a little.
            var kept = side;
            var again = PitDetour.Bend(stadium, from, aim + new Vector3(0f, 0f, 0.5f), 1.5f, 24f, 8, 0.5f, 1f, ref side,
                ref hold);
            Assert.That(side, Is.EqualTo(kept));
            Assert.That(Mathf.Sign(again.z), Is.EqualTo(Mathf.Sign(bent.z)));

            // On the floor of a donut the way across the hole bends as well.
            Shape("candy", out var donut);
            hold = 0f;
            var across = PitDetour.Bend(donut, new Vector3(-8f, 0f, 0f), new Vector3(16f, 0f, 0f), 1.5f, 24f, 8, 0.5f, 1f,
                ref side, ref hold);
            Assert.That(Mathf.Abs(across.normalized.z), Is.GreaterThan(0.3f));

            // A convex floor never bends the line: the bot still drives its target off the rim.
            Shape("tabletop", out var board);
            hold = 0f;
            var toRim = new Vector3(12f, 0f, 0f);
            var same = PitDetour.Bend(board, Vector3.zero, toRim, 1.5f, 24f, 8, 0.5f, 1f, ref side, ref hold);
            Assert.That(same, Is.EqualTo(toRim));
            hold = 0f;
            Assert.That(PitDetour.Bend(new CircleEdge(15f), Vector3.zero, toRim, 1.5f, 24f, 8, 0.5f, 1f, ref side, ref hold),
                Is.EqualTo(toRim));
        }
    }
}
