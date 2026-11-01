using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToyCarArena.Gameplay;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ToyCarArena.Tests.PlayMode
{
    /// <summary>
    /// Checks the visible surface across separate meshes: matching UV edges must also meet in space, and only the
    /// arena's real perimeter may remain open. This catches gaps and mismatched ring chords that flat normals hide.
    /// </summary>
    public sealed class FloorSeamsTests
    {
        [Test]
        public void RingsAndCoreShareTheirTopEdges([Values(3.7f, 7f, 12f)] float blockLength)
        {
            var settings = Settings("sandbox");
            var layout = new CircleLayout(settings, new CircleEdge(settings.Radius), blockLength, 6);
            try {
                var border = TopBorder(layout, FloorUv.ForCircle(settings.Radius));
                Assert.That(border, Is.Not.Empty);
                foreach (var (from, to) in border) {
                    Assert.That(World(from, settings.Radius).magnitude, Is.EqualTo(settings.Radius).Within(0.00001f));
                    Assert.That(World(to, settings.Radius).magnitude, Is.EqualTo(settings.Radius).Within(0.00001f));
                }

                // Every internal edge must be paired even after an inner ring uses a different block count.
                Assert.That(layout.Tiles.Select(tile => tile.Wave).Distinct().Count(), Is.EqualTo(layout.WaveCount));
                CheckCircleColliders(layout, settings);
            }
            finally {
                Release(layout);
            }
        }

        [Test]
        public void TilesAndCoreLeaveOnlyTheRealOutlineOpen([Values("tabletop", "candy", "playroom")] string id)
        {
            var shape = ArenaShape.Create(Settings(id), new System.Random(7));
            var uv = FloorUv.For(shape);
            var layout = new TiledLayout(shape, new GridEdge(shape), uv);
            try {
                var expected = new HashSet<(Vector2 from, Vector2 to)>();
                for (var e = 0; e < shape.EdgeA.Length; e++) {
                    if (shape.EdgeFaceA[e] >= 0 && shape.EdgeFaceB[e] >= 0) {
                        continue;
                    }

                    var a = shape.Vertices[shape.EdgeA[e]];
                    var b = shape.Vertices[shape.EdgeB[e]];
                    expected.Add(Ordered(uv.Top(a.x, a.y), uv.Top(b.x, b.y)));
                }

                Assert.That(TopBorder(layout, uv), Is.EquivalentTo(expected), "no internal grooves or missing top faces");
            }
            finally {
                Release(layout);
            }
        }

        /// <summary>
        /// Uses exact UVs as shared edge keys, then checks transformed positions separately so a displaced mesh cannot
        /// pass just because its texture still says where it was intended to be.
        /// </summary>
        private static List<(Vector2 from, Vector2 to)> TopBorder(ArenaLayout layout, FloorUv uv)
        {
            var edges = new Dictionary<(Vector2 from, Vector2 to), int>();
            var positions = new Dictionary<Vector2, Vector3>();
            AddTop(layout.CoreMesh, Vector3.zero, Quaternion.identity, uv, edges, positions);
            foreach (var tile in layout.Tiles) {
                AddTop(tile.Mesh, tile.Position, tile.Rotation, uv, edges, positions);
            }

            Assert.That(edges.Values, Has.All.InRange(1, 2), "no overlapping top faces");
            return edges.Where(edge => edge.Value == 1).Select(edge => edge.Key).ToList();
        }

        private static void AddTop(Mesh mesh, Vector3 pivot, Quaternion rotation, FloorUv uv,
            Dictionary<(Vector2 from, Vector2 to), int> edges, Dictionary<Vector2, Vector3> positions)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var triangles = mesh.triangles;
            var tops = 0;
            for (var t = 0; t < triangles.Length; t += 3) {
                var first = triangles[t];
                if (normals[first].y < 0.99f) {
                    continue;
                }

                tops++;
                for (var k = 0; k < 3; k++) {
                    var a = triangles[t + k];
                    var b = triangles[t + (k + 1) % 3];
                    Assert.That(Vector3.Distance(rotation * normals[a], Vector3.up), Is.LessThan(0.000001f));
                    var world = pivot + rotation * vertices[a];
                    Assert.That(world.y, Is.EqualTo(0f).Within(0.000001f), "flush top");
                    Assert.That(Vector2.Distance(uv.Top(world.x, world.z), uvs[a]), Is.LessThan(0.000001f));
                    if (positions.TryGetValue(uvs[a], out var shared)) {
                        Assert.That(Vector3.Distance(world, shared), Is.LessThan(0.00001f), "shared top vertex");
                    }
                    else {
                        positions.Add(uvs[a], world);
                    }

                    var edge = Ordered(uvs[a], uvs[b]);
                    edges.TryGetValue(edge, out var count);
                    edges[edge] = count + 1;
                }
            }

            Assert.That(tops, Is.GreaterThan(0), mesh.name);
        }

        private static (Vector2 from, Vector2 to) Ordered(Vector2 a, Vector2 b)
        {
            return a.x < b.x || a.x == b.x && a.y < b.y ? (a, b) : (b, a);
        }

        private static Vector2 World(Vector2 uv, float radius)
        {
            return new Vector2((uv.x * 2f - 1f) * radius, (uv.y / FloorUv.TopRange * 2f - 1f) * radius);
        }

        /// <summary>The visual tessellation must never reach the physical disc or its original radius arithmetic.</summary>
        private static void CheckCircleColliders(CircleLayout layout, GameTuning.ArenaSettings settings)
        {
            var states = new WaveState[layout.WaveCount];
            var width = layout.WaveCount > 0 ? (settings.Radius - settings.MinRadius) / layout.WaveCount : 0f;
            for (var wave = 0; wave <= layout.WaveCount; wave++) {
                var radius = wave < layout.WaveCount ? settings.Radius - wave * width : settings.MinRadius;
                var vertices = layout.ColliderMesh.vertices;
                Assert.That(vertices.Length, Is.EqualTo(96 * 7));
                for (var i = 0; i < 96; i++) {
                    var angle = i * Mathf.PI * 2f / 96;
                    var expected = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    Assert.That(vertices[i * 7 + 1].Equals(expected), Is.True, "original collider vertices, bit for bit");
                }

                if (wave < layout.WaveCount) {
                    states[wave] = WaveState.Gone;
                    layout.Refresh(states);
                }
            }
        }

        private static GameTuning.ArenaSettings Settings(string id)
        {
#if UNITY_EDITOR
            var tuning = AssetDatabase.LoadAssetAtPath<GameTuning>("Assets/Data/Gameplay/data_game_tuning.asset");
            return tuning.Maps.First(map => map.Id == id).Arena;
#else
            throw new System.NotSupportedException("Editor tests only.");
#endif
        }

        private static void Release(ArenaLayout layout)
        {
            foreach (var mesh in layout.Meshes) {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
