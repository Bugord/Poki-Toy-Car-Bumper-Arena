using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// Merges scenery that never moves into one mesh per material, so a phone's frame does not pay a draw call (and an
    /// outline draw) for every prop of the world below the arena. A round drew about 200 objects a frame, and the props
    /// and the pedestal were the largest share: dozens of renderers, each drawn twice because the outline pass redraws
    /// every outlined renderer. On WebGL every draw is a round trip through JavaScript into GL, which a weak phone's CPU
    /// feels. Merged, the whole of a root costs one draw per material, and its outline one draw more: the outlined parts
    /// are merged again into a single hull drawn with the outline material itself (<see cref="OutlineFeature.HullMaterial"/>),
    /// which is what the outline pass would have drawn part by part.
    /// <para>
    /// The look does not change: the same meshes, materials, places and fog. What changes: the parts are no longer culled
    /// one by one (the world is almost all in view anyway), and the source meshes must be readable (Read/Write on the
    /// decor models). A part that cannot be merged (unreadable, mirrored, disabled) is left as it was.
    /// </para>
    /// </summary>
    public static class StaticMerge
    {
        /// <summary>Name of the child that holds the merged meshes, under the root given to <see cref="Merge"/>.</summary>
        public const string MergedName = "Merged";

        /// <summary>
        /// Merges every mesh renderer under <paramref name="root"/> into one child of it, <see cref="MergedName"/>, and
        /// takes the merged parts' renderers off; their game objects stay where they were. Returns the meshes it made,
        /// which are the caller's to destroy.
        /// </summary>
        public static List<Mesh> Merge(Transform root)
        {
            var made = new List<Mesh>();
            var byMaterial = new Dictionary<Material, List<CombineInstance>>();
            var hull = new List<CombineInstance>();
            var toRoot = root.worldToLocalMatrix;
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            foreach (var renderer in renderers) {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                var matrix = toRoot * renderer.transform.localToWorldMatrix;
                // A mirrored part would come out inside out: merging does not flip the triangles.
                if (mesh == null || !mesh.isReadable || !renderer.enabled || matrix.determinant <= 0f) {
                    continue;
                }

                var materials = renderer.sharedMaterials;
                var isOutlined = (renderer.renderingLayerMask & Outline.Layer) != 0;
                for (var i = 0; i < mesh.subMeshCount; i++) {
                    var material = materials.Length > 0 ? materials[Mathf.Min(i, materials.Length - 1)] : null;
                    if (material == null) {
                        continue;
                    }

                    var part = new CombineInstance { mesh = mesh, subMeshIndex = i, transform = matrix };
                    if (!byMaterial.TryGetValue(material, out var parts)) {
                        byMaterial[material] = parts = new List<CombineInstance>();
                    }

                    parts.Add(part);
                    if (isOutlined) {
                        hull.Add(part);
                    }
                }

                Object.Destroy(renderer);
                Object.Destroy(filter);
            }

            if (byMaterial.Count == 0) {
                return made;
            }

            var merged = new GameObject(MergedName).transform;
            merged.SetParent(root, false);

            // Without the outline material (a renderer without the feature), the merged parts keep the outline bit and
            // the outline pass draws them as before, one draw per material.
            var hullMaterial = OutlineFeature.HullMaterial;
            var isHullDrawn = hullMaterial != null && hull.Count > 0;
            foreach (var pair in byMaterial) {
                var renderer = Create(pair.Key.name, pair.Value, pair.Key, merged, made);
                if (!isHullDrawn && Contains(hull, pair.Value)) {
                    renderer.renderingLayerMask |= Outline.Layer;
                }
            }

            if (isHullDrawn) {
                Create("Outline", hull, hullMaterial, merged, made);
            }

            return made;
        }

        private static MeshRenderer Create(string name, List<CombineInstance> parts, Material material, Transform parent,
            List<Mesh> made)
        {
            var vertices = 0;
            foreach (var part in parts) {
                vertices += part.mesh.vertexCount;
            }

            var mesh = new Mesh { name = $"Merged {name}" };
            if (vertices > ushort.MaxValue) {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.CombineMeshes(parts.ToArray(), true, true, false);
            // Nothing reads it back: the CPU copy is only memory.
            mesh.UploadMeshData(true);
            made.Add(mesh);

            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = holder.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.renderingLayerMask &= ~Outline.Layer;
            return renderer;
        }

        private static bool Contains(List<CombineInstance> hull, List<CombineInstance> parts)
        {
            foreach (var part in parts) {
                foreach (var outlined in hull) {
                    if (outlined.mesh == part.mesh && outlined.subMeshIndex == part.subMeshIndex
                        && outlined.transform == part.transform) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
