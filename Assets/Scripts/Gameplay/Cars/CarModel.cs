using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Shared geometry and atlas operations for arena models and editor portraits. Only called when a model is made.
    /// </summary>
    public static class CarModel
    {
        public static Vector2Int SwatchOf(Vector2 uv) => new(Mathf.FloorToInt(uv.x * 8f), Mathf.FloorToInt(uv.y * 4f));

        public static bool IsWheel(MeshFilter filter) =>
            filter.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0
            || filter.sharedMesh.name.IndexOf("wheel", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The largest concentration of paint UVs outside wheels is the body. Neutral atlas cells are details:
        /// windows (1,0), rubber (2,0), metal (4,0), lamps (6,1). They outnumber paint on some Toy Car Kit models.
        /// </summary>
        public static MeshFilter FindBody(GameObject model, out Vector2Int swatch)
        {
            MeshFilter body = null;
            swatch = default;
            var most = 0;
            var counts = new int[32];
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true)) {
                if (filter.sharedMesh == null || IsWheel(filter)) {
                    continue;
                }

                Array.Clear(counts, 0, counts.Length);
                foreach (var uv in filter.sharedMesh.uv) {
                    var cell = SwatchOf(uv);
                    var isPaint = cell.y == 1 && (cell.x >= 1 && cell.x <= 5 || cell.x == 7)
                        || cell == new Vector2Int(0, 2);
                    if (!isPaint) {
                        continue;
                    }

                    var count = ++counts[cell.y * 8 + cell.x];
                    if (count > most) {
                        most = count;
                        body = filter;
                        swatch = cell;
                    }
                }
            }

            if (body == null) {
                throw new InvalidOperationException($"No readable painted body found in {model.name}.");
            }

            return body;
        }

        /// <summary>
        /// Returns an owned clone; the caller destroys it. UVs outside the source paint cell never change.
        /// </summary>
        public static Mesh PaintBody(MeshFilter body, Vector2Int source, Vector2Int target)
        {
            var mesh = UnityEngine.Object.Instantiate(body.sharedMesh);
            mesh.name = body.sharedMesh.name;
            var uvs = mesh.uv;
            var shift = new Vector2((target.x - source.x) / 8f, (target.y - source.y) / 4f);
            for (var i = 0; i < uvs.Length; i++) {
                if (SwatchOf(uvs[i]) == source) {
                    uvs[i] += shift;
                }
            }

            mesh.uv = uvs;
            body.sharedMesh = mesh;
            return mesh;
        }

        public static void SetMaterial(GameObject model, Material material)
        {
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true)) {
                renderer.sharedMaterial = material;
            }
        }

        /// <summary>
        /// Tight geometry bounds in a chosen coordinate system, including child transforms and the FBX root scale.
        /// </summary>
        public static Bounds BoundsIn(GameObject model, Matrix4x4 worldToSpace)
        {
            var bounds = new Bounds();
            var started = false;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true)) {
                if (filter.sharedMesh == null) {
                    continue;
                }

                var matrix = worldToSpace * filter.transform.localToWorldMatrix;
                foreach (var vertex in filter.sharedMesh.vertices) {
                    var point = matrix.MultiplyPoint3x4(vertex);
                    if (!started) {
                        bounds = new Bounds(point, Vector3.zero);
                        started = true;
                    } else {
                        bounds.Encapsulate(point);
                    }
                }
            }

            if (!started) {
                throw new InvalidOperationException($"No readable geometry found in {model.name}.");
            }

            return bounds;
        }
    }
}
