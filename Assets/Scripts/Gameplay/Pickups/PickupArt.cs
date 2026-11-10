using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The look of the boxes and of the items that fly, built once in code like the arena objects' (<see cref="ToyMeshes"/>):
    /// a bright mystery box with a white "?" on each side and on top, a round bomb with a fuse over a red ring on the
    /// floor, a red boxing glove on a yellow arm, a bowling ball with its finger holes, a pink balloon on a string and a
    /// dark slick of oil with a rainbow sheen. Flat colours with the navy outline of the other toys, and soft
    /// markers on the floor. Nothing is imported: the materials are copies of ones <see cref="MechanicArt"/> already
    /// holds, tinted here, so the build carries no new asset. The item colours are also the HUD's. What it builds is freed with it.
    /// </summary>
    internal sealed class PickupArt
    {
        /// <summary>Metres above the floor the middle of a box floats, before it bobs.</summary>
        public const float HoverHeight = 1.25f;

        /// <summary>The radius of a bomb, in metres: it rests this high on the floor.</summary>
        public const float BombRadius = 0.5f;

        private const float BoxSize = 1.1f;
        private const float MarkerRadius = 1.5f;
        private const float DecalHeight = 0.012f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Navy = new(0.114f, 0.169f, 0.31f, 1f);
        private static readonly Color Gold = new(1f, 0.79f, 0.24f);
        private static readonly Color Red = new(1f, 0.3f, 0.3f);

        // The colours a box cycles through, the theme's brightest, so it reads as a lucky box from across the arena.
        private static readonly Color[] BoxColours =
        {
            new(1f, 0.79f, 0.24f), new(1f, 0.62f, 0.11f), new(1f, 0.42f, 0.62f), new(0.69f, 0.42f, 1f),
            new(0.22f, 0.71f, 1f), new(0.36f, 0.83f, 0.29f),
        };

        private readonly List<Object> owned = new();
        private readonly MaterialPropertyBlock block = new();
        private readonly Material white;
        private readonly Material navy;
        private readonly Material gold;
        private readonly Material red;
        private readonly Material yellow;
        private readonly Material bowling;
        private readonly Material pink;
        private readonly Material bombBody;
        private readonly Material metal;
        private readonly Material rope;
        private readonly Material orange;
        private readonly Material boxMaterial;
        private readonly Material markerMaterial;
        private readonly Mesh box;
        private readonly Mesh glyphs;
        private readonly Mesh sphere;
        private readonly Mesh cylinder;
        private readonly Mesh disc;
        private readonly Mesh ring;
        private readonly Mesh blob;

        public PickupArt(MechanicArt art)
        {
            var shine = art.Get("jelly_shine");
            white = Tinted(shine, "pickup_white", Color.white);
            navy = Tinted(shine, "pickup_navy", Navy);
            gold = Tinted(shine, "pickup_gold", Gold);
            red = Tinted(shine, "pickup_red", Red);
            yellow = Tinted(shine, "pickup_yellow", new Color(1f, 0.86f, 0.35f));
            bowling = Tinted(shine, "pickup_bowling", ColorOf(ItemKind.Bowling));
            pink = Tinted(shine, "pickup_pink", ColorOf(ItemKind.Balloon));
            // Lighter than the navy outline, so the ball shows as a ball inside it and not as a dark blot.
            bombBody = Tinted(shine, "pickup_bomb", new Color(0.25f, 0.31f, 0.5f));
            metal = Tinted(shine, "pickup_metal", new Color(0.74f, 0.78f, 0.86f));
            rope = Tinted(shine, "pickup_rope", new Color(0.96f, 0.8f, 0.55f));
            orange = Tinted(shine, "pickup_orange", new Color(1f, 0.56f, 0.16f));
            boxMaterial = Tinted(shine, "pickup_box", Gold);
            markerMaterial = art.Get("mound_ring");
            box = Own(ToyMeshes.Box(Vector3.one * BoxSize));
            glyphs = Own(BuildGlyphs(BoxSize * 0.5f));
            sphere = Own(ToyMeshes.Ellipsoid(Vector3.one, 16, 10));
            cylinder = Own(ToyMeshes.Cylinder(1f, 1f, 1f, 12));
            disc = Own(ToyMeshes.Disc(1f, 32));
            ring = Own(ToyMeshes.Ring(0.86f, 1f, 40));
            blob = Own(BuildBlob(48));
        }

        /// <summary>An item's colour: its HUD icon's and its sticker's.</summary>
        public static Color ColorOf(ItemKind kind)
        {
            return kind switch
            {
                ItemKind.Mega => new Color(0.37f, 0.45f, 0.8f),
                ItemKind.Shockwave => new Color(0.69f, 0.42f, 1f),
                ItemKind.Shield => new Color(0.22f, 0.71f, 1f),
                ItemKind.Turbo => new Color(1f, 0.79f, 0.24f),
                ItemKind.Bomb => Navy,
                ItemKind.Glove => Red,
                ItemKind.Bowling => new Color(0.24f, 0.77f, 0.63f),
                ItemKind.Balloon => new Color(1f, 0.36f, 0.63f),
                ItemKind.Oil => new Color(1f, 0.72f, 0.18f),
                _ => Color.white,
            };
        }

        /// <summary>
        /// A mystery box under <paramref name="hover"/>: a bright cube with a white "?" on its four sides and on top,
        /// outlined. Its colour cycles with <see cref="TintBox"/>.
        /// </summary>
        public MeshRenderer BuildBox(Transform hover)
        {
            var root = new GameObject("Box").transform;
            root.SetParent(hover, false);
            var cube = ToyMeshes.Part(root, "Cube", box, boxMaterial, Vector3.zero);
            ToyMeshes.Part(root, "Marks", glyphs, white, Vector3.zero);
            ToyMeshes.Outlined(root.gameObject);
            return cube.GetComponent<MeshRenderer>();
        }

        /// <summary>The soft marker on the floor under a box: a pale disc and a ring round it, tinted with <see cref="TintBox"/>.</summary>
        public void BuildMarker(FloorPickup slot)
        {
            slot.Marker = new GameObject("Marker").transform;
            slot.Marker.SetParent(slot.Root, false);
            var discPart = ToyMeshes.Decal(slot.Marker, "Disc", disc, markerMaterial, -4);
            discPart.transform.localPosition = Vector3.up * DecalHeight;
            discPart.transform.localScale = new Vector3(MarkerRadius, 1f, MarkerRadius);
            var ringPart = ToyMeshes.Decal(slot.Marker, "Ring", ring, markerMaterial, -3);
            ringPart.transform.localPosition = Vector3.up * (DecalHeight * 1.5f);
            ringPart.transform.localScale = new Vector3(MarkerRadius, 1f, MarkerRadius);
            slot.MarkerDisc = discPart;
            slot.MarkerRing = ringPart;
        }

        /// <summary>
        /// Colours a box and its marker at <paramref name="phase"/> seconds of its life: the cube slides through the
        /// bright colours, the marker's ring pulses.
        /// </summary>
        public void TintBox(FloorPickup slot, float phase)
        {
            var at = Mathf.Repeat(phase * 0.8f, BoxColours.Length);
            var from = (int)at;
            var colour = Color.Lerp(BoxColours[from], BoxColours[(from + 1) % BoxColours.Length], at - from);
            block.SetColor(ColorId, colour);
            slot.BoxRenderer.SetPropertyBlock(block);
            block.SetColor(ColorId, new Color(colour.r, colour.g, colour.b, 0.24f));
            slot.MarkerDisc.SetPropertyBlock(block);
            var pulse = 0.5f + 0.5f * Mathf.Sin(phase * 4f);
            block.SetColor(ColorId, new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0.55f, 0.95f, pulse)));
            slot.MarkerRing.SetPropertyBlock(block);
        }

        /// <summary>
        /// A bomb under <paramref name="parent"/>, drawn as a cartoon bomb reads at a glance: a slate ball lighter than
        /// its outline with a wide shine on top, a metal cap, a curled rope fuse and a spark burst at its tip
        /// (<see cref="ThrownBomb.Spark"/>, scaled as it fizzes), and a red ball over it that <see cref="ThrownBomb.Flash"/>
        /// switches on as it blinks. Its middle is its origin; it faces +Z.
        /// </summary>
        public void BuildBomb(Transform parent, ThrownBomb bomb)
        {
            var root = new GameObject("Bomb").transform;
            root.SetParent(parent, false);
            var r = BombRadius;
            ToyMeshes.Part(root, "Body", sphere, bombBody, Vector3.zero, default, Vector3.one * r);
            // Mostly on top: the camera looks down on the bomb from every side it rides on.
            var shineAt = new Vector3(-0.4f, 0.85f, -0.2f).normalized;
            var shinePart = ToyMeshes.Part(root, "Shine", sphere, white, shineAt * (r * 0.96f), default,
                new Vector3(r * 0.4f, r * 0.22f, r * 0.08f));
            shinePart.localRotation = Quaternion.LookRotation(shineAt, Vector3.right) * Quaternion.Euler(0f, 0f, 20f);
            ToyMeshes.Part(root, "Dot", sphere, white, new Vector3(-0.66f, 0.55f, -0.3f).normalized * (r * 0.98f), default,
                Vector3.one * (r * 0.09f));
            ToyMeshes.Part(root, "Cap", cylinder, metal, new Vector3(0f, r * 0.98f, 0f), default,
                new Vector3(r * 0.42f, r * 0.3f, r * 0.42f));
            // The fuse curls from the cap and out to one side, in three pieces of rope.
            var fuse = new[]
            {
                new Vector3(0f, r * 1.08f, 0f), new Vector3(r * 0.08f, r * 1.42f, 0f), new Vector3(r * 0.32f, r * 1.68f, 0f),
                new Vector3(r * 0.66f, r * 1.76f, 0f),
            };
            for (var i = 0; i < fuse.Length - 1; i++) {
                var along = fuse[i + 1] - fuse[i];
                var piece = ToyMeshes.Part(root, "Fuse", cylinder, rope, (fuse[i] + fuse[i + 1]) * 0.5f, default,
                    new Vector3(r * 0.12f, along.magnitude + r * 0.06f, r * 0.12f));
                piece.localRotation = Quaternion.FromToRotation(Vector3.up, along);
            }

            var flash = ToyMeshes.Part(root, "Flash", sphere, red, Vector3.zero, default, Vector3.one * (r * 1.04f));
            // The spark is a unit burst scaled by its owner: a gold ball crossed by four spindles, long and short in
            // turn, so it shows eight points.
            var spark = new GameObject("Spark").transform;
            spark.SetParent(root, false);
            spark.localPosition = fuse[fuse.Length - 1];
            spark.localScale = Vector3.one * 0.14f;
            for (var i = 0; i < 4; i++) {
                var length = i % 2 == 0 ? 2.2f : 1.6f;
                var ray = ToyMeshes.Part(spark, "Ray", sphere, i % 2 == 0 ? orange : gold, Vector3.zero, default,
                    new Vector3(0.34f, length, 0.34f));
                // In a plane tipped up toward a camera looking down, so the burst shows from above.
                ray.localRotation = Quaternion.Euler(30f, 0f, i * 45f + 10f);
            }

            ToyMeshes.Part(spark, "Core", sphere, gold, Vector3.zero, default, Vector3.one);
            ToyMeshes.Outlined(root.gameObject);
            flash.gameObject.SetActive(false);

            // The telegraph lies on the floor apart from the ball, so the ball can bounce and spin over it.
            var mark = new GameObject("Bomb Ring").transform;
            mark.SetParent(parent, false);
            var fill = ToyMeshes.Decal(mark, "Fill", disc, markerMaterial, -4);
            fill.transform.localPosition = Vector3.up * DecalHeight;
            var edge = ToyMeshes.Decal(mark, "Edge", ring, markerMaterial, -3);
            edge.transform.localPosition = Vector3.up * (DecalHeight * 1.5f);
            block.SetColor(ColorId, new Color(Red.r, Red.g, Red.b, 0.22f));
            fill.SetPropertyBlock(block);
            block.SetColor(ColorId, new Color(Red.r, Red.g, Red.b, 0.9f));
            edge.SetPropertyBlock(block);

            bomb.Root = root;
            bomb.Flash = flash.gameObject;
            bomb.Spark = spark;
            bomb.Mark = mark;
            root.gameObject.SetActive(false);
            mark.gameObject.SetActive(false);
        }

        /// <summary>
        /// A boxing glove under <paramref name="parent"/>: a red fist with a thumb and a white cuff facing +Z, and the
        /// yellow arm behind it that is stretched from the car's nose to the cuff.
        /// </summary>
        public void BuildGlove(Transform parent, GlovePunch glove)
        {
            var fist = new GameObject("Glove").transform;
            fist.SetParent(parent, false);
            ToyMeshes.Part(fist, "Fist", sphere, red, new Vector3(0f, 0f, 0.15f), default, new Vector3(0.6f, 0.55f, 0.62f));
            ToyMeshes.Part(fist, "Thumb", sphere, red, new Vector3(-0.5f, 0.08f, 0.05f), default, new Vector3(0.24f, 0.22f, 0.3f));
            ToyMeshes.Part(fist, "Shine", sphere, white, new Vector3(0.2f, 0.25f, 0.5f), default, Vector3.one * 0.12f);
            ToyMeshes.Part(fist, "Cuff", cylinder, white, new Vector3(0f, 0f, -0.42f), new Vector3(90f, 0f, 0f),
                new Vector3(0.44f, 0.3f, 0.44f));
            ToyMeshes.Outlined(fist.gameObject);

            // A unit long arm along +Z from its origin, stretched by its scale.
            var arm = new GameObject("Glove Arm").transform;
            arm.SetParent(parent, false);
            ToyMeshes.Part(arm, "Spring", cylinder, yellow, new Vector3(0f, 0f, 0.5f), new Vector3(90f, 0f, 0f),
                new Vector3(0.13f, 1f, 0.13f));
            ToyMeshes.Outlined(arm.gameObject);

            glove.Fist = fist;
            glove.Arm = arm;
            fist.gameObject.SetActive(false);
            arm.gameObject.SetActive(false);
        }

        /// <summary>
        /// A bowling ball of <paramref name="radius"/> under <paramref name="parent"/>: a teal ball with three navy
        /// finger holes and a shine, on a spinning part (<see cref="RolledBall.Spin"/>) under a root that faces its way.
        /// </summary>
        public void BuildBowlingBall(Transform parent, RolledBall ball, float radius)
        {
            var root = new GameObject("Bowling Ball").transform;
            root.SetParent(parent, false);
            var spin = new GameObject("Spin").transform;
            spin.SetParent(root, false);
            ToyMeshes.Part(spin, "Body", sphere, bowling, Vector3.zero, default, Vector3.one * radius);
            // The holes sit close together on one side, so the roll shows.
            var holes = new[] { new Vector3(-0.22f, 0.9f, 0.18f), new Vector3(0.22f, 0.9f, 0.18f), new Vector3(0f, 0.86f, -0.3f) };
            foreach (var hole in holes) {
                ToyMeshes.Part(spin, "Hole", sphere, navy, hole.normalized * (radius * 0.93f), default,
                    Vector3.one * (radius * 0.16f));
            }

            ToyMeshes.Part(root, "Shine", sphere, white, new Vector3(-0.35f, 0.45f, 0.35f) * radius, default,
                Vector3.one * (radius * 0.17f));
            ToyMeshes.Outlined(root.gameObject);
            ball.Root = root;
            ball.Spin = spin;
            root.gameObject.SetActive(false);
        }

        /// <summary>
        /// A balloon under <paramref name="parent"/>: a pink balloon with a knot and a shine whose bottom is the root,
        /// and a thin <paramref name="cord"/> hanging down from it, a unit long, stretched by its scale.
        /// </summary>
        public Transform BuildBalloon(Transform parent, out Transform cord)
        {
            var root = new GameObject("Balloon").transform;
            root.SetParent(parent, false);
            ToyMeshes.Part(root, "Body", sphere, pink, new Vector3(0f, 0.95f, 0f), default, new Vector3(0.8f, 0.95f, 0.8f));
            ToyMeshes.Part(root, "Knot", cylinder, pink, new Vector3(0f, 0.02f, 0f), default, new Vector3(0.16f, 0.12f, 0.16f));
            ToyMeshes.Part(root, "Shine", sphere, white, new Vector3(-0.3f, 1.35f, 0.42f), default,
                new Vector3(0.14f, 0.22f, 0.14f));
            ToyMeshes.Outlined(root.gameObject);
            cord = new GameObject("String").transform;
            cord.SetParent(root, false);
            ToyMeshes.Part(cord, "Line", cylinder, white, new Vector3(0f, -0.5f, 0f), default,
                new Vector3(0.035f, 1f, 0.035f));
            return root;
        }

        /// <summary>
        /// An oil slick under <paramref name="parent"/>, a unit across, scaled to its radius: a dark puddle with a wavy
        /// rim, a navy outline round it and a rainbow sheen on it, flat on the floor under the cars' shadows.
        /// </summary>
        public Transform BuildOilSlick(Transform parent)
        {
            var root = new GameObject("Oil Slick").transform;
            root.SetParent(parent, false);
            var rim = ToyMeshes.Decal(root, "Rim", blob, markerMaterial, -7);
            rim.transform.localPosition = Vector3.up * DecalHeight;
            rim.transform.localScale = Vector3.one * 1.08f;
            block.SetColor(ColorId, new Color(Navy.r, Navy.g, Navy.b, 1f));
            rim.SetPropertyBlock(block);
            var pool = ToyMeshes.Decal(root, "Pool", blob, markerMaterial, -6);
            pool.transform.localPosition = Vector3.up * (DecalHeight * 1.2f);
            block.SetColor(ColorId, new Color(0.16f, 0.13f, 0.22f, 1f));
            pool.SetPropertyBlock(block);
            var sheens = new[]
            {
                (new Vector3(-0.22f, 0f, 0.18f), 0.36f, new Color(0.55f, 0.35f, 1f, 0.55f)),
                (new Vector3(0.2f, 0f, -0.1f), 0.28f, new Color(0.25f, 0.85f, 0.9f, 0.5f)),
                (new Vector3(0.05f, 0f, 0.32f), 0.16f, new Color(1f, 0.85f, 0.35f, 0.6f)),
            };
            foreach (var (offset, size, colour) in sheens) {
                var sheen = ToyMeshes.Decal(root, "Sheen", blob, markerMaterial, -5);
                sheen.transform.localPosition = offset + Vector3.up * (DecalHeight * 1.4f);
                sheen.transform.localRotation = Quaternion.Euler(0f, offset.x * 200f, 0f);
                sheen.transform.localScale = Vector3.one * size;
                block.SetColor(ColorId, colour);
                sheen.SetPropertyBlock(block);
            }

            root.gameObject.SetActive(false);
            return root;
        }

        /// <summary>The renderer of the oily tyre tracks' one mesh, dark and half seen through, flat on the floor.</summary>
        public MeshRenderer BuildOilTracks(Transform parent, Mesh mesh)
        {
            var tracks = ToyMeshes.Decal(parent, "Oil Tracks", mesh, markerMaterial, -4);
            block.SetColor(ColorId, new Color(0.12f, 0.11f, 0.18f, 0.7f));
            tracks.SetPropertyBlock(block);
            return tracks;
        }

        public void Dispose()
        {
            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        /// <summary>
        /// The "?" marks of a box as one mesh: blocks of a pixel question mark standing a little proud of the four sides
        /// and the top of a cube of half size <paramref name="half"/>, so a box is two renderers however it turns.
        /// </summary>
        private static Mesh BuildGlyphs(float half)
        {
            // The mark on a 5 x 7 grid, from its middle, in cells: x, y, width, height.
            const float Cell = 0.11f;
            const float Depth = 0.05f;
            var blocks = new[]
            {
                new Vector4(0f, 3f, 3f, 1f), new Vector4(-2f, 2f, 1f, 1f), new Vector4(2f, 1.5f, 1f, 2f),
                new Vector4(0.5f, 0f, 2f, 1f), new Vector4(0f, -1f, 1f, 1f), new Vector4(0f, -3f, 1f, 1f),
            };
            var faces = new[]
            {
                Quaternion.identity, Quaternion.Euler(0f, 90f, 0f), Quaternion.Euler(0f, 180f, 0f),
                Quaternion.Euler(0f, 270f, 0f), Quaternion.Euler(-90f, 0f, 0f),
            };
            var parts = new List<CombineInstance>();
            var meshes = new List<Mesh>();
            foreach (var face in faces) {
                foreach (var b in blocks) {
                    var mesh = ToyMeshes.Box(new Vector3(b.z * Cell, b.w * Cell, Depth));
                    meshes.Add(mesh);
                    var local = new Vector3(b.x * Cell, b.y * Cell, half + Depth * 0.3f);
                    parts.Add(new CombineInstance
                    {
                        mesh = mesh,
                        transform = Matrix4x4.TRS(face * local, face, Vector3.one),
                    });
                }
            }

            var combined = new Mesh { name = "Box Marks" };
            combined.CombineMeshes(parts.ToArray(), true, true);
            foreach (var mesh in meshes) {
                Object.Destroy(mesh);
            }

            return combined;
        }

        /// <summary>A flat puddle a unit across, facing up, whose rim wobbles in and out a little, as poured oil does.</summary>
        private static Mesh BuildBlob(int sides)
        {
            var vertices = new Vector3[sides + 1];
            var triangles = new int[sides * 3];
            vertices[0] = Vector3.zero;
            for (var i = 0; i < sides; i++) {
                var angle = i * Mathf.PI * 2f / sides;
                var radius = 1f + 0.09f * Mathf.Sin(angle * 5f) + 0.05f * Mathf.Sin(angle * 3f + 1.3f);
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = (i + 1) % sides + 1;
                triangles[i * 3 + 2] = i + 1;
            }

            var mesh = new Mesh { name = "Oil Blob", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material Tinted(Material source, string name, Color colour)
        {
            var material = Own(new Material(source) { name = name });
            material.SetColor(ColorId, colour);
            return material;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
