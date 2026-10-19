using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The round floor the cars drive on: a disc of <see cref="GameTuning.ArenaSettings.Radius"/> centred on the world
    /// origin, its top at height 0, built when the scene starts so the radius is tuned in one place with the camera's
    /// fit and the spawn circle. Its collider is the same mesh; a car past the rim has nothing under it and falls.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshCollider))]
    public sealed class ArenaFloor : MonoBehaviour
    {
        // Enough sides that the rim reads as a circle from the camera's distance.
        private const int Segments = 96;

        // Depth of the rim below the driving surface, in metres; only seen from the camera's angle.
        private const float Thickness = 0.5f;

        private GameTuning.ArenaSettings settings;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Arena;
        }

        private void Awake()
        {
            var mesh = Build(settings.Radius);
            GetComponent<MeshFilter>().sharedMesh = mesh;
            GetComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>
        /// The top as a fan around the centre and the rim as a band facing out, each with its own vertices so the edge
        /// between them stays sharp. UVs map the top straight down.
        /// </summary>
        private static Mesh Build(float radius)
        {
            var vertices = new Vector3[1 + Segments + 1 + (Segments + 1) * 2];
            var normals = new Vector3[vertices.Length];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[Segments * 3 + Segments * 6];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);

            const int topRing = 1;
            const int rimStart = topRing + Segments + 1;
            for (var i = 0; i <= Segments; i++) {
                var angle = i * Mathf.PI * 2f / Segments;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var rim = outward * radius;

                vertices[topRing + i] = rim;
                normals[topRing + i] = Vector3.up;
                uvs[topRing + i] = new Vector2(0.5f + outward.x * 0.5f, 0.5f + outward.z * 0.5f);

                var rimTop = rimStart + i * 2;
                vertices[rimTop] = rim;
                vertices[rimTop + 1] = rim + Vector3.down * Thickness;
                normals[rimTop] = outward;
                normals[rimTop + 1] = outward;
                uvs[rimTop] = new Vector2((float)i / Segments, 1f);
                uvs[rimTop + 1] = new Vector2((float)i / Segments, 0f);
            }

            var index = 0;
            for (var i = 0; i < Segments; i++) {
                // Clockwise seen from above, so the top faces up.
                triangles[index++] = 0;
                triangles[index++] = topRing + i + 1;
                triangles[index++] = topRing + i;

                var rimTop = rimStart + i * 2;
                var nextTop = rimTop + 2;
                triangles[index++] = rimTop;
                triangles[index++] = nextTop;
                triangles[index++] = rimTop + 1;
                triangles[index++] = rimTop + 1;
                triangles[index++] = nextTop;
                triangles[index++] = nextTop + 1;
            }

            var mesh = new Mesh { name = "Arena Floor" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
