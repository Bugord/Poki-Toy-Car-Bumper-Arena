using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The world far below the floating arena: a ground plane in the theme's material, what the arena stands on (the
    /// pedestal) and the large solids of the ground (a table top, a rug), so a car that falls drops a long way and the
    /// arena reads as standing high above a place. The fog that melts all of it into the background is set by
    /// <see cref="ArenaCamera"/>, which knows the distance to the arena. Built once as the scene is; nothing is built for
    /// an arena without a theme or without a ground depth.
    /// <para>
    /// Pure scenery like <see cref="ArenaDecor"/>: no colliders and no shadows, nothing on the arena layer, so cars fall
    /// through all of it and the steering ray never meets it. The pedestal and the pieces are placed as the camera sees the
    /// arena, like the props. Nothing here is tied to a circle: a pedestal is a list of pieces, so another floor shape only
    /// needs its own list.
    /// </para>
    /// </summary>
    public sealed class ArenaWorld : IInitializable, IDisposable
    {
        // Sides of a round piece; a rim this smooth reads as a circle at the sizes of the pedestal.
        private const int Sides = 28;

        // Half the width of the ground disc, in metres: far past where the fog has hidden it, so its edge is never seen.
        private const float GroundRadius = 600f;

        // Thickness of the ground below its top face, in metres; it is never seen from the side.
        private const float GroundThickness = 1f;

        private readonly ArenaMap map;
        private readonly ArenaCamera arenaCamera;
        private readonly Transform parent;
        private readonly List<Mesh> meshes = new();

        public ArenaWorld(ArenaMap map, ArenaCamera arenaCamera, Transform parent)
        {
            this.map = map;
            this.arenaCamera = arenaCamera;
            this.parent = parent;
        }

        public void Initialize()
        {
            if (!map.HasTheme || map.Theme.GroundDepth <= 0f) {
                return;
            }

            var theme = map.Theme;
            var root = new GameObject("World").transform;
            root.SetParent(parent, false);

            var cylinder = Keep(BuildCylinder());
            var box = Keep(BuildBox());

            if (theme.GroundMaterial != null) {
                var ground = CreateSolid("Ground", cylinder, theme.GroundMaterial, root);
                ground.localPosition = new Vector3(0f, -theme.GroundDepth - GroundThickness, 0f);
                ground.localScale = new Vector3(GroundRadius * 2f, GroundThickness, GroundRadius * 2f);
            }

            var behind = Vector3.ProjectOnPlane(arenaCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, behind);
            foreach (var piece in theme.Pedestal) {
                Place(piece, cylinder, box, behind, right, root);
            }

            foreach (var piece in theme.GroundPieces) {
                Place(piece, cylinder, box, behind, right, root);
            }
        }

        public void Dispose()
        {
            foreach (var mesh in meshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            meshes.Clear();
        }

        private static void Place(ArenaCatalog.WorldPiece piece, Mesh cylinder, Mesh box, Vector3 behind, Vector3 right,
            Transform root)
        {
            var position = right * piece.Offset.x + behind * piece.Offset.y + Vector3.up * piece.BaseHeight;
            var rotation = Quaternion.Euler(0f, piece.Yaw, 0f);
            if (piece.Shape == ArenaCatalog.PieceShape.Model) {
                if (piece.Model == null) {
                    return;
                }

                ArenaDecor.Place(piece.Model, position, rotation, piece.Size.x, root);
                return;
            }

            if (piece.Material == null) {
                return;
            }

            var solid = CreateSolid(piece.Shape.ToString(), piece.Shape == ArenaCatalog.PieceShape.Box ? box : cylinder,
                piece.Material, root);
            solid.SetPositionAndRotation(position, rotation);
            solid.localScale = piece.Size;
        }

        private static Transform CreateSolid(string name, Mesh mesh, Material material, Transform root)
        {
            var solid = new GameObject(name);
            solid.transform.SetParent(root, false);
            solid.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = solid.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return solid.transform;
        }

        private Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }

        /// <summary>
        /// A cylinder of diameter 1 whose base is at the origin and whose height is 1, sides smooth, top flat. The base is
        /// left open: it is never seen, as the camera looks down on the world.
        /// </summary>
        private static Mesh BuildCylinder()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i <= Sides; i++) {
                var angle = i * Mathf.PI * 2f / Sides;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * 0.5f);
                vertices.Add(direction * 0.5f + Vector3.up);
                normals.Add(direction);
                normals.Add(direction);
            }

            for (var i = 0; i < Sides; i++) {
                var a = i * 2;
                triangles.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
            }

            var centre = vertices.Count;
            vertices.Add(Vector3.up);
            normals.Add(Vector3.up);
            var ring = vertices.Count;
            for (var i = 0; i < Sides; i++) {
                var angle = i * Mathf.PI * 2f / Sides;
                vertices.Add(new Vector3(Mathf.Cos(angle) * 0.5f, 1f, Mathf.Sin(angle) * 0.5f));
                normals.Add(Vector3.up);
            }

            for (var i = 0; i < Sides; i++) {
                triangles.AddRange(new[] { centre, ring + i, ring + (i + 1) % Sides });
            }

            return Assemble("World Cylinder", vertices, normals, triangles);
        }

        /// <summary>A box of width and depth 1 whose base is at the origin and whose height is 1.</summary>
        private static Mesh BuildBox()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            AddFace(vertices, normals, triangles, Vector3.up, Vector3.right, Vector3.forward);
            AddFace(vertices, normals, triangles, Vector3.down, Vector3.right, Vector3.back);
            AddFace(vertices, normals, triangles, Vector3.right, Vector3.forward, Vector3.up);
            AddFace(vertices, normals, triangles, Vector3.left, Vector3.back, Vector3.up);
            AddFace(vertices, normals, triangles, Vector3.forward, Vector3.left, Vector3.up);
            AddFace(vertices, normals, triangles, Vector3.back, Vector3.right, Vector3.up);
            return Assemble("World Box", vertices, normals, triangles);
        }

        // A square face of the unit box, spanned by u and v; the winding follows from which way u x v points.
        private static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 normal, Vector3 u, Vector3 v)
        {
            var origin = new Vector3(0f, 0.5f, 0f) + normal * 0.5f;
            var first = vertices.Count;
            vertices.Add(origin - u * 0.5f - v * 0.5f);
            vertices.Add(origin - u * 0.5f + v * 0.5f);
            vertices.Add(origin + u * 0.5f + v * 0.5f);
            vertices.Add(origin + u * 0.5f - v * 0.5f);
            for (var i = 0; i < 4; i++) {
                normals.Add(normal);
            }

            if (Vector3.Dot(Vector3.Cross(u, v), normal) > 0f) {
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            else {
                triangles.AddRange(new[] { first, first + 2, first + 1, first, first + 3, first + 2 });
            }
        }

        private static Mesh Assemble(string name, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            // The triangles above run counter-clockwise from outside; Unity draws a triangle's front when
            // (second - first) x (third - first) points out of it, and with these corners that is the reverse order.
            for (var i = 0; i < triangles.Count; i += 3) {
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
