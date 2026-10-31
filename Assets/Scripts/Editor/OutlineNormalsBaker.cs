using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ToyCarArena.Editor
{
    /// <summary>
    /// Bakes smoothed normals into TEXCOORD3 of every model under Assets/Art/Models, for the outline shader. The Kenney
    /// models are low-poly with hard normals (a vertex per face corner, each with the face's normal), so an outline pushed
    /// along them would tear open at every corner. Here the normals of all vertices at one position are averaged, weighted
    /// by the corner angle, and the hull moves along that average instead. The lit look keeps the hard normals.
    /// Runs at import, so the models need no Read/Write.
    /// </summary>
    public sealed class OutlineNormalsBaker : AssetPostprocessor
    {
        private const string Folder = "Assets/Art/Models/";
        private const float Grid = 10000f;

        // Raise to bake the models again.
        public override uint GetVersion() => 1;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.StartsWith(Folder)) {
                return;
            }

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) {
                Bake(filter.sharedMesh);
            }

            foreach (var skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                Bake(skinned.sharedMesh);
            }
        }

        private static void Bake(Mesh mesh)
        {
            if (mesh == null) {
                return;
            }

            var vertices = mesh.vertices;
            var sums = new Dictionary<Vector3Int, Vector3>();
            for (var sub = 0; sub < mesh.subMeshCount; sub++) {
                var triangles = mesh.GetTriangles(sub);
                for (var i = 0; i + 2 < triangles.Length; i += 3) {
                    var a = triangles[i];
                    var b = triangles[i + 1];
                    var c = triangles[i + 2];
                    var face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                    if (face.sqrMagnitude < 1e-12f) {
                        continue;
                    }

                    face.Normalize();
                    AddCorner(sums, vertices, a, b, c, face);
                    AddCorner(sums, vertices, b, c, a, face);
                    AddCorner(sums, vertices, c, a, b, face);
                }
            }

            var normals = new List<Vector3>(vertices.Length);
            for (var i = 0; i < vertices.Length; i++) {
                normals.Add(sums.TryGetValue(Key(vertices[i]), out var sum) ? sum.normalized : Vector3.zero);
            }

            mesh.SetUVs(3, normals);
        }

        private static void AddCorner(Dictionary<Vector3Int, Vector3> sums, Vector3[] vertices, int corner, int next,
            int previous, Vector3 face)
        {
            var angle = Vector3.Angle(vertices[next] - vertices[corner], vertices[previous] - vertices[corner]) * Mathf.Deg2Rad;
            var key = Key(vertices[corner]);
            sums.TryGetValue(key, out var sum);
            sums[key] = sum + face * angle;
        }

        private static Vector3Int Key(Vector3 position) =>
            new(Mathf.RoundToInt(position.x * Grid), Mathf.RoundToInt(position.y * Grid), Mathf.RoundToInt(position.z * Grid));
    }
}
