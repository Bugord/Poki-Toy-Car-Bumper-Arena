using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The toppers the garage sells, built in code from chunky primitives on the cars' own atlas material: every part
    /// is painted one flat swatch of the palette through its UVs, so a topper needs no asset of its own and takes the
    /// outline like the car under it. <see cref="Build"/> makes one standing on the origin with its front along +Z; the
    /// ids are those of <see cref="GameTuning.TopperSettings"/>.
    /// </summary>
    public static class CarTopper
    {
        // Swatches of the toy car atlas (8 by 4, x from the left, y from the bottom).
        private static readonly Vector2Int White = new(4, 0);
        private static readonly Vector2Int Dark = new(0, 0);
        private static readonly Vector2Int Grey = new(3, 0);
        private static readonly Vector2Int Cream = new(0, 1);
        private static readonly Vector2Int Green = new(1, 1);
        private static readonly Vector2Int Gold = new(2, 1);
        private static readonly Vector2Int Orange = new(3, 1);
        private static readonly Vector2Int Red = new(4, 1);
        private static readonly Vector2Int Blue = new(5, 1);
        private static readonly Vector2Int Sky = new(6, 1);
        private static readonly Vector2Int Pink = new(0, 2);

        /// <summary>Every topper this builds, for the bots that wear one now and then.</summary>
        public static readonly IReadOnlyList<string> Ids = new[] {
            "party-hat", "cone", "antenna", "flag", "horns", "propeller", "fin", "crown",
        };

        /// <summary>
        /// Where on the roof the topper stands, from the roof's middle in metres: a pole stands at the back corner, the
        /// rest in the middle.
        /// </summary>
        public static Vector2 FootOf(string id) => id switch {
            "antenna" => new Vector2(0.3f, -0.45f),
            "flag" => new Vector2(-0.3f, -0.45f),
            _ => Vector2.zero,
        };

        /// <summary>
        /// How far forward of the model's highest part the roof of this catalog car is, in metres: the drag racer's
        /// highest part is the air intake behind its driver, the vintage car's the windscreen ahead of its seat.
        /// </summary>
        public static float RoofShiftOf(string carId) => carId switch {
            "drag-racer" => 0.55f,
            "vintage" => -0.25f,
            _ => 0f,
        };

        /// <summary>
        /// Builds the topper under <paramref name="parent"/>; the meshes it makes go to <paramref name="meshes"/> for
        /// the owner to destroy. <paramref name="spinner"/> is the part that turns (the propeller's blades), or null.
        /// Null for an unknown id.
        /// </summary>
        public static Transform Build(string id, Transform parent, Material material, List<Mesh> meshes,
            out Transform spinner)
        {
            spinner = null;
            if (string.IsNullOrEmpty(id)) {
                return null;
            }

            var root = new GameObject($"Topper {id}").transform;
            root.SetParent(parent, false);

            Transform Part(Mesh mesh, Vector2Int swatch, Vector3 position, Vector3 euler = default, Transform under = null)
            {
                Paint(mesh, swatch);
                meshes.Add(mesh);
                return ToyMeshes.Part(under ?? root, mesh.name, mesh, material, position, euler);
            }

            switch (id) {
                case "party-hat":
                    Part(ToyMeshes.Cylinder(0.3f, 0f, 0.7f, 14), Pink, new Vector3(0f, 0.35f, 0f), new Vector3(-8f, 0f, 0f));
                    Part(ToyMeshes.Cylinder(0.25f, 0.18f, 0.12f, 14), Gold, new Vector3(0f, 0.2f, 0.02f), new Vector3(-8f, 0f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.12f), Gold, new Vector3(0f, 0.72f, 0.1f));
                    break;
                case "cone":
                    Part(ToyMeshes.Box(new Vector3(0.6f, 0.08f, 0.6f)), Orange, new Vector3(0f, 0.04f, 0f));
                    Part(ToyMeshes.Cylinder(0.24f, 0.05f, 0.62f, 14), Orange, new Vector3(0f, 0.39f, 0f));
                    Part(ToyMeshes.Cylinder(0.18f, 0.14f, 0.14f, 14), White, new Vector3(0f, 0.38f, 0f));
                    break;
                case "antenna":
                    Part(ToyMeshes.Cylinder(0.08f, 0.08f, 0.08f, 10), Dark, new Vector3(0f, 0.04f, 0f));
                    Part(ToyMeshes.Cylinder(0.035f, 0.035f, 1f, 8), Grey, new Vector3(0f, 0.55f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.14f), Red, new Vector3(0f, 1.08f, 0f));
                    break;
                case "flag":
                    Part(ToyMeshes.Cylinder(0.035f, 0.035f, 1.1f, 8), White, new Vector3(0f, 0.55f, 0f));
                    Part(ToyMeshes.Box(new Vector3(0.05f, 0.36f, 0.56f)), Red, new Vector3(0f, 0.9f, -0.3f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.075f), Gold, new Vector3(0f, 1.12f, 0f));
                    break;
                case "horns":
                    Part(ToyMeshes.Cylinder(0.13f, 0f, 0.5f, 10), Cream, new Vector3(-0.3f, 0.22f, 0.08f), new Vector3(0f, 0f, 28f));
                    Part(ToyMeshes.Cylinder(0.13f, 0f, 0.5f, 10), Cream, new Vector3(0.3f, 0.22f, 0.08f), new Vector3(0f, 0f, -28f));
                    break;
                case "propeller":
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.34f, 0.28f, 0.34f), 18, 6, Mathf.PI * 0.5f), Blue, Vector3.zero);
                    Part(ToyMeshes.Cylinder(0.05f, 0.05f, 0.2f, 8), Gold, new Vector3(0f, 0.36f, 0f));
                    spinner = new GameObject("Blades").transform;
                    spinner.SetParent(root, false);
                    spinner.localPosition = new Vector3(0f, 0.48f, 0f);
                    Part(ToyMeshes.Box(new Vector3(1.1f, 0.05f, 0.16f)), Red, Vector3.zero, default, spinner);
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.08f), Gold, Vector3.zero, default, spinner);
                    break;
                case "fin":
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.07f, 0.6f, 0.5f), 14, 8, Mathf.PI * 0.5f), Sky,
                        new Vector3(0f, 0f, -0.05f), new Vector3(-22f, 0f, 0f));
                    break;
                case "crown":
                    Part(ToyMeshes.Cylinder(0.3f, 0.34f, 0.22f, 10), Gold, new Vector3(0f, 0.11f, 0f));
                    for (var i = 0; i < 5; i++) {
                        var angle = Mathf.PI * 2f * i / 5f + Mathf.PI * 0.5f;
                        var point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f;
                        Part(ToyMeshes.Cylinder(0.08f, 0f, 0.22f, 6), Gold, point + Vector3.up * 0.33f);
                    }

                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.07f), Red, new Vector3(0f, 0.11f, 0.33f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.06f), Blue, new Vector3(0.33f, 0.11f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.06f), Green, new Vector3(-0.33f, 0.11f, 0f));
                    break;
                default:
                    Object.Destroy(root.gameObject);
                    return null;
            }

            return root;
        }

        // Every vertex on the middle of one swatch: the whole part in that flat colour of the palette.
        private static void Paint(Mesh mesh, Vector2Int swatch)
        {
            var uv = new Vector2((swatch.x + 0.5f) / 8f, (swatch.y + 0.5f) / 4f);
            var uvs = new Vector2[mesh.vertexCount];
            for (var i = 0; i < uvs.Length; i++) {
                uvs[i] = uv;
            }

            mesh.uv = uvs;
        }
    }
}
