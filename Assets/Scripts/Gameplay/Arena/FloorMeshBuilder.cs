using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Gathers faces with their own vertices, so every edge between two faces stays sharp, and winds each face so
    /// it faces its normal; the layouts then only have to list the corners in order round the face.
    /// </summary>
    internal sealed class FloorMeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Vector3> normals = new();
        private readonly List<int> triangles = new();

        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            // Unity draws a triangle whose corners go clockwise seen from the front, where this cross product points.
            var isFacing = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) >= 0f;
            triangles.Add(start);
            triangles.Add(isFacing ? start + 1 : start + 2);
            triangles.Add(isFacing ? start + 2 : start + 1);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normalA, Vector3 normalB,
            Vector3 normalC, Vector3 normalD)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normalA);
            normals.Add(normalB);
            normals.Add(normalC);
            normals.Add(normalD);
            var facing = normalA + normalB + normalC + normalD;
            var isFacing = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) >= 0f;
            if (isFacing) {
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
            else {
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);
                triangles.Add(start);
                triangles.Add(start + 3);
                triangles.Add(start + 2);
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}
