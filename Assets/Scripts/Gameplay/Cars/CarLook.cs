using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// What tells the cars apart at a glance: every car gets its own body colour, and the player's car also gets a
    /// marker, a ring on the floor under it and an arrow floating above it. The round calls <see cref="Apply"/> once
    /// per car right after placing it, index 0 being the player and 1..N the bots.
    ///
    /// The body colour comes from the shared atlas, not from a tint: the atlas is a palette of flat swatches, and a
    /// <c>_BaseColor</c> multiply would darken the windows, bumpers and wheels along with the body. So the body's UVs
    /// move from the swatch the model was painted with to another one, and the other parts (their UVs sit on other
    /// swatches) stay as they are. The model must be readable for that. Everything here is looks only: no colliders,
    /// nothing on the arena layer, no numbers the gameplay reads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarLook : MonoBehaviour
    {
        private const int RingSegments = 48;

        [Header("Body")]
        [Tooltip("The mesh of the car's body, the one that is painted in a single swatch of the atlas")]
        [SerializeField]
        private MeshFilter body;

        [Tooltip("The swatch of each car's body by its index: the player first, then the bots. A car beyond the last " +
            "one starts over from the first bot's")]
        [SerializeField]
        private Vector2Int[] swatches =
        {
            new(1, 1), // green, the player
            new(4, 1), // red
            new(5, 1), // blue
            new(2, 1), // yellow
            new(7, 1), // purple
            new(3, 1), // orange
            new(0, 2), // pink
            new(0, 0), // dark
        };

        [Header("Player marker")]
        [Tooltip("Colour of the marker, an unlit material")]
        [SerializeField]
        private Material markerFill;

        [Tooltip("The marker's outline, an unlit dark material; the sticker look of the screens")]
        [SerializeField]
        private Material markerOutline;

        [Tooltip("Inner and outer radius of the ring on the floor, in metres")]
        [SerializeField]
        private Vector2 ringRadius = new(1.45f, 1.8f);

        [Tooltip("How much wider the outline is than the shape it frames, in metres")]
        [Min(0f)]
        [SerializeField]
        private float outlineWidth = 0.1f;

        [Tooltip("Height of the ring above the floor, in metres; just enough not to flicker with it")]
        [Min(0f)]
        [SerializeField]
        private float ringHeight = 0.03f;

        [Tooltip("Width and height of the arrow, in metres")]
        [SerializeField]
        private Vector2 arrowSize = new(1.5f, 1.7f);

        [Tooltip("Height of the arrow's tip above the car's bottom, in metres")]
        [Min(0f)]
        [SerializeField]
        private float arrowHeight = 2.7f;

        [Tooltip("How far the arrow bobs up and down, in metres, and how many times a second")]
        [SerializeField]
        private Vector2 arrowBob = new(0.2f, 1.5f);

        private Mesh ownBody;
        private GameObject model;
        private Car car;
        private Transform ring;
        private Transform arrow;
        private Transform viewpoint;
        private readonly System.Collections.Generic.List<Mesh> markerMeshes = new();

        /// <summary>
        /// Paints the car for its place in the round: index 0 is the player's, with the marker; 1..N are bots.
        /// </summary>
        public void Apply(int index, CarCatalog.Entry entry)
        {
            ReplaceModel(entry);
            ownBody = CarModel.PaintBody(CarModel.FindBody(model, out _), entry.BodySwatch, SwatchOf(index));
            SetMarker(index == 0);
        }

        // Shared by the catalog's portrait renderer; the palette stays authored on the prefab.
        public Vector2Int PlayerSwatch => swatches[0];
        public Material ModelMaterial => body.GetComponent<MeshRenderer>().sharedMaterial;
        public Quaternion ModelRotation => body.transform.localRotation;

        private void ReplaceModel(CarCatalog.Entry entry)
        {
            if (entry == null || entry.Model == null) {
                throw new MissingReferenceException("CarLook needs a catalog entry with a model. Run Tools > Cars > " +
                    "Update Catalog to populate the catalog.");
            }

            // The original racer remains a hidden reference, including its wheel hierarchy. Its geometry defines
            // the cosmetic length and floor level, without reading or changing any physics component.
            var reference = CarModel.BoundsIn(body.gameObject, transform.worldToLocalMatrix);
            body.gameObject.SetActive(false);
            if (ownBody != null) {
                Destroy(ownBody);
                ownBody = null;
            }

            if (model != null) {
                model.SetActive(false);
                Destroy(model);
            }

            model = new GameObject("Catalog Model");
            model.transform.SetParent(transform, false);
            model.transform.localRotation = ModelRotation;
            var instance = Instantiate(entry.Model, model.transform, false);
            CarModel.SetMaterial(instance, ModelMaterial);
            var bounds = CarModel.BoundsIn(model, transform.worldToLocalMatrix);
            if (bounds.size.z <= Mathf.Epsilon) {
                throw new System.InvalidOperationException($"Car model {entry.Id} has no length along Z.");
            }

            model.transform.localScale = Vector3.one * (reference.size.z / bounds.size.z);
            bounds = CarModel.BoundsIn(model, transform.worldToLocalMatrix);
            model.transform.localPosition = new Vector3(reference.center.x - bounds.center.x,
                reference.min.y - bounds.min.y, reference.center.z - bounds.center.z);
        }

        private void LateUpdate()
        {
            if (car != null && car.IsOut) {
                ring.gameObject.SetActive(false);
                arrow.gameObject.SetActive(false);
                return;
            }

            // The marker lives in the world, not on the car, so it stays level and under or over the car however the
            // car tilts or flies. The ring sinks with the car once it is below the floor.
            var position = transform.position;
            ring.position = new Vector3(position.x, Mathf.Min(ringHeight, position.y + ringHeight), position.z);

            var bob = Mathf.Sin(Time.time * arrowBob.y * Mathf.PI * 2f) * arrowBob.x;
            // The arrow faces the camera, so it reads as an arrow from the camera's angle whatever the screen's shape.
            arrow.SetPositionAndRotation(position + Vector3.up * (arrowHeight + bob),
                viewpoint != null ? viewpoint.rotation : Quaternion.identity);
        }

        private void OnDestroy()
        {
            if (ownBody != null) {
                Destroy(ownBody);
            }

            if (ring != null) {
                Destroy(ring.gameObject);
                Destroy(arrow.gameObject);
            }

            foreach (var mesh in markerMeshes) {
                Destroy(mesh);
            }
        }

        /// <summary>
        /// The swatch of the car with this index. Past the last one the bots start over from the first bot's, so no bot
        /// ever wears the player's colour (CarColor in the UI counts the same way).
        /// </summary>
        private Vector2Int SwatchOf(int index)
        {
            if (index < swatches.Length) {
                return swatches[Mathf.Max(0, index)];
            }

            var botSwatches = swatches.Length - 1;
            return botSwatches > 0 ? swatches[1 + (index - 1) % botSwatches] : swatches[0];
        }

        private void SetMarker(bool isPlayer)
        {
            if (!isPlayer) {
                enabled = false;
                return;
            }

            car = GetComponent<Car>();
            var main = Camera.main;
            viewpoint = main != null ? main.transform : null;
            // The outline sits a little behind its shape: below the ring, and further from the camera for the arrow.
            ring = BuildPart("Player Ring", BuildRing(ringRadius.x, ringRadius.y),
                BuildRing(ringRadius.x - outlineWidth, ringRadius.y + outlineWidth), Vector3.down * (ringHeight * 0.5f));
            arrow = BuildPart("Player Arrow", BuildArrow(arrowSize, 0f), BuildArrow(arrowSize, outlineWidth),
                Vector3.forward * 0.05f);
            enabled = true;
        }

        /// <summary>
        /// An object of the world holding the shape with its outline drawn behind it.
        /// </summary>
        private Transform BuildPart(string partName, Mesh shape, Mesh outline, Vector3 outlineOffset)
        {
            var root = new GameObject(partName);
            root.hideFlags = HideFlags.DontSave;
            // The arena layer is the floor cars steer by; a marker is no floor, so it takes the default layer.
            root.layer = 0;
            markerMeshes.Add(shape);
            markerMeshes.Add(outline);
            AddMesh(root.transform, "Shape", shape, markerFill, Vector3.zero);
            AddMesh(root.transform, "Outline", outline, markerOutline, outlineOffset);
            return root.transform;
        }

        private static void AddMesh(Transform parent, string meshName, Mesh mesh, Material material, Vector3 offset)
        {
            var part = new GameObject(meshName);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = offset;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var meshRenderer = part.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        /// <summary>
        /// A flat ring facing up, in the plane at height 0.
        /// </summary>
        private static Mesh BuildRing(float inner, float outer)
        {
            var vertices = new Vector3[RingSegments * 2];
            var triangles = new int[RingSegments * 6];
            for (var i = 0; i < RingSegments; i++) {
                var angle = i * Mathf.PI * 2f / RingSegments;
                var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices[i * 2] = outward * inner;
                vertices[i * 2 + 1] = outward * outer;

                var next = (i + 1) % RingSegments;
                var index = i * 6;
                // Clockwise seen from above, so the ring faces up.
                triangles[index] = i * 2;
                triangles[index + 1] = next * 2;
                triangles[index + 2] = i * 2 + 1;
                triangles[index + 3] = next * 2;
                triangles[index + 4] = next * 2 + 1;
                triangles[index + 5] = i * 2 + 1;
            }

            return Finish(new Mesh { name = "Player Ring" }, vertices, triangles);
        }

        /// <summary>
        /// A flat arrow pointing down, its tip at the origin, facing -Z; inflated by <paramref name="inflate"/> it is the
        /// outline that frames the arrow. The head and the shaft are convex, so each is inflated on its own.
        /// </summary>
        private static Mesh BuildArrow(Vector2 size, float inflate)
        {
            var halfWidth = size.x * 0.5f;
            var headHeight = size.y * 0.45f;
            var shaftHalfWidth = halfWidth * 0.4f;
            var head = new[] { new Vector2(0f, 0f), new Vector2(-halfWidth, headHeight), new Vector2(halfWidth, headHeight) };
            // The shaft starts inside the head, so the two have no seam between them.
            var shaft = new[]
            {
                new Vector2(-shaftHalfWidth, headHeight * 0.5f), new Vector2(-shaftHalfWidth, size.y),
                new Vector2(shaftHalfWidth, size.y), new Vector2(shaftHalfWidth, headHeight * 0.5f),
            };

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            AddPolygon(vertices, triangles, head, inflate);
            AddPolygon(vertices, triangles, shaft, inflate);
            return Finish(new Mesh { name = "Player Arrow" }, vertices.ToArray(), triangles.ToArray());
        }

        /// <summary>
        /// Adds a convex polygon moved outwards by <paramref name="inflate"/> on every side, as a fan wound clockwise
        /// seen from -Z, the side the camera is on.
        /// </summary>
        private static void AddPolygon(System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles, Vector2[] polygon, float inflate)
        {
            var count = polygon.Length;
            var area = 0f;
            for (var i = 0; i < count; i++) {
                var from = polygon[i];
                var to = polygon[(i + 1) % count];
                area += from.x * to.y - to.x * from.y;
            }

            // Counter-clockwise (positive area) polygons are turned round to wind clockwise.
            var points = (Vector2[])polygon.Clone();
            if (area > 0f) {
                System.Array.Reverse(points);
            }

            var first = vertices.Count;
            for (var i = 0; i < count; i++) {
                var previous = points[(i + count - 1) % count];
                var current = points[i];
                var next = points[(i + 1) % count];
                // Clockwise: the outside of an edge is on its left, and the corner moves along the bisector of the
                // two outside normals far enough for both offset edges to meet.
                var normalIn = Left(current - previous).normalized;
                var normalOut = Left(next - current).normalized;
                var bisector = (normalIn + normalOut).normalized;
                var corner = current + bisector * (inflate / Mathf.Max(0.2f, Vector2.Dot(bisector, normalIn)));
                vertices.Add(new Vector3(corner.x, corner.y, 0f));
            }

            for (var i = 1; i < count - 1; i++) {
                triangles.Add(first);
                triangles.Add(first + i);
                triangles.Add(first + i + 1);
            }
        }

        private static Vector2 Left(Vector2 direction) => new(-direction.y, direction.x);

        private static Mesh Finish(Mesh mesh, Vector3[] vertices, int[] triangles)
        {
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
