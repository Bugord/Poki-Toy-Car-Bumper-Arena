using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// Opts solid 3D objects into the comic-book outline. The outline is a second pass of <see cref="OutlineFeature"/>
    /// that draws only the renderers carrying <see cref="Layer"/> in their rendering layer mask. A rendering layer is used
    /// rather than a physics layer: the physics layers steer the cars (the steering ray reads the Arena layer), and a
    /// rendering layer changes nothing but how an object is drawn.
    /// </summary>
    public static class Outline
    {
        /// <summary>
        /// The bit of the rendering layer mask that marks a renderer as outlined; bit 0 is URP's own default layer.
        /// </summary>
        public const uint Layer = 1u << 1;

        /// <summary>
        /// Outlines every renderer of the object and its children. Flat stickers (shadows, markers, patches, effects)
        /// are never passed here, because they carry their own outlines.
        /// </summary>
        public static void Apply(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                renderer.renderingLayerMask |= Layer;
            }
        }

        /// <summary>
        /// Bakes into TEXCOORD3 of a mesh built in code the smoothed normals the hull follows, as
        /// <c>OutlineNormalsBaker</c> does for the imported models: every vertex gets the sum of the distinct face normals
        /// met at its place, so a hard corner pushes its hull out along the corner and the line does not tear there.
        /// Distinct normals rather than one per triangle, so a top cut into many triangles does not outweigh its walls.
        /// </summary>
        public static void BakeNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var sums = new Dictionary<Vector3Int, Vector3>();
            var seen = new HashSet<(Vector3Int, Vector3Int)>();
            for (var i = 0; i < vertices.Length; i++) {
                var place = Vector3Int.RoundToInt(vertices[i] * 1000f);
                if (!seen.Add((place, Vector3Int.RoundToInt(normals[i] * 1000f)))) {
                    continue;
                }

                sums[place] = sums.TryGetValue(place, out var sum) ? sum + normals[i] : normals[i];
            }

            var smoothed = new Vector3[vertices.Length];
            for (var i = 0; i < vertices.Length; i++) {
                var sum = sums[Vector3Int.RoundToInt(vertices[i] * 1000f)];
                smoothed[i] = sum.sqrMagnitude > 1e-6f ? sum.normalized : normals[i];
            }

            mesh.SetUVs(3, smoothed);
        }

        /// <summary>
        /// Takes the outline off every renderer of the object and its children, for a moment it is drawn see-through:
        /// the hull is opaque and would wrap the object in a solid shell.
        /// </summary>
        public static void Remove(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                renderer.renderingLayerMask &= ~Layer;
            }
        }
    }
}
