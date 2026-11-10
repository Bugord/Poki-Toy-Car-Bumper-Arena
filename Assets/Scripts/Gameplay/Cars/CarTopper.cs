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
        private static readonly Vector2Int Copper = new(5, 0);
        private static readonly Vector2Int Cream = new(0, 1);
        private static readonly Vector2Int Green = new(1, 1);
        private static readonly Vector2Int Gold = new(2, 1);
        private static readonly Vector2Int Orange = new(3, 1);
        private static readonly Vector2Int Red = new(4, 1);
        private static readonly Vector2Int Blue = new(5, 1);
        private static readonly Vector2Int Sky = new(6, 1);
        private static readonly Vector2Int Pink = new(0, 2);
        private static readonly Vector2Int Purple = new(7, 1);

        /// <summary>
        /// The toppers on sale this builds, for the bots that wear one now and then; the exclusive ones
        /// (<see cref="ExclusiveIds"/>) stay the players' who earned them.
        /// </summary>
        public static readonly IReadOnlyList<string> Ids = new[] {
            "top-hat", "party-hat", "cone", "antenna", "flag", "chef-hat", "horns", "cowboy-hat", "propeller",
            "viking-helmet", "duck", "fin", "halo", "rocket", "crown",
        };

        /// <summary>The toppers only a reward gives (the daily gift's last day), which no bot wears.</summary>
        public static readonly IReadOnlyList<string> ExclusiveIds = new[] { "wizard-hat" };

        /// <summary>
        /// The toppers the leagues give, one theme growing grander league by league: the champion's fire, from a copper
        /// ember to a phoenix. A bot wears one only when its level has reached the league.
        /// </summary>
        public static readonly IReadOnlyList<string> LeagueIds = new[] { "ember", "silver-torch", "fire-crown", "phoenix" };

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
        /// A topper with living parts (the leagues' flames) moves them itself (<see cref="TopperMotion"/> on its root).
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

            TopperMotion motion = null;

            // A flame: a teardrop standing on its foot (so it flickers from the foot up) with a smaller core of the
            // second colour set forward, so it shows in front of the outer flame instead of inside it.
            void Fire(Vector3 foot, float radius, float height, Vector2Int outer, Vector2Int core, float speed,
                float phase, float amount = 0.14f, Transform under = null, Vector3 euler = default)
            {
                motion ??= root.gameObject.AddComponent<TopperMotion>();
                var flame = new GameObject("Flame").transform;
                flame.SetParent(under ?? root, false);
                flame.localPosition = foot;
                flame.localRotation = Quaternion.Euler(euler);
                Part(FlameMesh(radius, height), outer, Vector3.zero, default, flame);
                Part(FlameMesh(radius * 0.62f, height * 0.62f), core, new Vector3(0f, 0f, radius * 0.45f), default,
                    flame);
                motion.AddFlame(flame, speed, phase, amount);
            }

            // A spark: a little ball that rises by drift over its period, shrinking out, again and again.
            void SparkAt(Vector3 from, float size, Vector2Int swatch, Vector3 drift, float period, float phase)
            {
                motion ??= root.gameObject.AddComponent<TopperMotion>();
                var spark = Part(ToyMeshes.Ellipsoid(Vector3.one, 8, 5), swatch, from);
                spark.localScale = Vector3.one * size;
                motion.AddSpark(spark, drift, period, phase);
            }

            switch (id) {
                case "top-hat":
                    // A gentleman's top hat: a wide black brim, a tall crown a little wider at the top, a red band.
                    Part(ToyMeshes.Cylinder(0.44f, 0.44f, 0.07f, 20), Dark, new Vector3(0f, 0.035f, 0f));
                    Part(ToyMeshes.Cylinder(0.26f, 0.3f, 0.56f, 20), Dark, new Vector3(0f, 0.35f, 0f));
                    Part(ToyMeshes.Cylinder(0.27f, 0.28f, 0.11f, 20), Red, new Vector3(0f, 0.14f, 0f));
                    break;
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
                case "chef-hat":
                    // A chef's toque: a white band under a big puffed top, a smaller puff on it.
                    Part(ToyMeshes.Cylinder(0.27f, 0.29f, 0.22f, 18), White, new Vector3(0f, 0.11f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.38f, 0.24f, 0.38f)), White, new Vector3(0f, 0.38f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.24f, 0.17f, 0.24f)), White, new Vector3(0.02f, 0.56f, -0.03f));
                    break;
                case "cowboy-hat":
                    // A straw cowboy hat: a wide brim curled up at the sides, a pinched crown and a red band.
                    Part(ToyMeshes.Cylinder(0.5f, 0.5f, 0.05f, 22), Cream, new Vector3(0f, 0.06f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.1f, 0.05f, 0.4f)), Cream, new Vector3(-0.46f, 0.14f, 0f),
                        new Vector3(0f, 0f, -38f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.1f, 0.05f, 0.4f)), Cream, new Vector3(0.46f, 0.14f, 0f),
                        new Vector3(0f, 0f, 38f));
                    Part(ToyMeshes.Cylinder(0.27f, 0.22f, 0.36f, 18), Cream, new Vector3(0f, 0.26f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.22f, 0.08f, 0.2f)), Cream, new Vector3(0f, 0.44f, 0f));
                    Part(ToyMeshes.Cylinder(0.28f, 0.27f, 0.08f, 18), Red, new Vector3(0f, 0.13f, 0f));
                    break;
                case "viking-helmet":
                    // A viking's helmet: a grey dome on a gold rim, a nose guard and two horns curving out and up.
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.34f, 0.34f, 0.34f), 18, 6, Mathf.PI * 0.5f), Grey, Vector3.zero);
                    Part(ToyMeshes.Cylinder(0.36f, 0.36f, 0.09f, 18), Gold, new Vector3(0f, 0.045f, 0f));
                    Part(ToyMeshes.Box(new Vector3(0.08f, 0.3f, 0.06f)), Gold, new Vector3(0f, 0.16f, 0.33f));
                    Part(ToyMeshes.Cylinder(0.12f, 0f, 0.5f, 10), Cream, new Vector3(-0.38f, 0.4f, 0f),
                        new Vector3(0f, 0f, 30f));
                    Part(ToyMeshes.Cylinder(0.12f, 0f, 0.5f, 10), Cream, new Vector3(0.38f, 0.4f, 0f),
                        new Vector3(0f, 0f, -30f));
                    break;
                case "duck":
                    // A rubber duck riding on the roof: a yellow body and head, an orange beak, dark eyes, a tail.
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.32f, 0.22f, 0.4f)), Gold, new Vector3(0f, 0.22f, -0.04f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.12f, 0.1f, 0.12f)), Gold, new Vector3(0f, 0.34f, -0.42f),
                        new Vector3(-30f, 0f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.2f), Gold, new Vector3(0f, 0.56f, 0.18f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.11f, 0.05f, 0.12f)), Orange, new Vector3(0f, 0.52f, 0.38f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.04f), Dark, new Vector3(-0.1f, 0.62f, 0.33f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.04f), Dark, new Vector3(0.1f, 0.62f, 0.33f));
                    break;
                case "halo":
                    // An angel's gold halo floating over two little white wings.
                    // Each wing is three feathers, each higher, shorter and swept further back, so the scalloped
                    // edge reads as a wing from the side too.
                    for (var side = -1; side <= 1; side += 2) {
                        for (var i = 0; i < 3; i++) {
                            Part(ToyMeshes.Ellipsoid(new Vector3(0.05f, 0.075f, 0.34f - 0.06f * i)), White,
                                new Vector3(side * (0.2f + 0.03f * i), 0.16f + 0.1f * i, -0.12f - 0.05f * i),
                                new Vector3(18f + 14f * i, -side * 45f, -side * 18f));
                        }
                    }

                    const int haloParts = 16;
                    const float haloRadius = 0.3f;
                    for (var i = 0; i < haloParts; i++) {
                        var angle = Mathf.PI * 2f * (i + 0.5f) / haloParts;
                        var tangent = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle));
                        var point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * haloRadius;
                        var length = 2f * haloRadius * Mathf.Sin(Mathf.PI / haloParts) + 0.04f;
                        Part(ToyMeshes.Cylinder(0.055f, 0.055f, length, 8), Gold, point + Vector3.up * 0.64f,
                            Quaternion.FromToRotation(Vector3.up, tangent).eulerAngles);
                    }

                    break;
                case "rocket":
                    // A toy rocket standing on its fins: a white body, a red nose, a sky window and an orange flame.
                    Part(ToyMeshes.Cylinder(0.13f, 0f, 0.16f, 12), Orange, new Vector3(0f, 0.08f, 0f), new Vector3(180f, 0f, 0f));
                    Part(ToyMeshes.Cylinder(0.2f, 0.2f, 0.56f, 16), White, new Vector3(0f, 0.44f, 0f));
                    Part(ToyMeshes.Cylinder(0.2f, 0f, 0.3f, 16), Red, new Vector3(0f, 0.87f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.09f, 0.09f, 0.04f)), Sky, new Vector3(0f, 0.56f, 0.19f));
                    for (var i = 0; i < 3; i++) {
                        var angle = Mathf.PI * 2f * i / 3f + Mathf.PI * 0.5f;
                        var outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        Part(ToyMeshes.Box(new Vector3(0.04f, 0.28f, 0.2f)), Red, outward * 0.24f + Vector3.up * 0.3f,
                            new Vector3(0f, 90f - angle * Mathf.Rad2Deg, 0f));
                    }

                    break;
                case "wizard-hat":
                    // A wizard's hat: a wide purple brim, a tall cone leaning back with its tip bent further, a gold
                    // band and gold stars, the daily gift's prize, so it reads as no hat of the shop.
                    Part(ToyMeshes.Cylinder(0.46f, 0.46f, 0.06f, 20), Purple, new Vector3(0f, 0.03f, 0f));
                    Part(ToyMeshes.Cylinder(0.27f, 0.12f, 0.58f, 16), Purple, new Vector3(0f, 0.32f, -0.03f),
                        new Vector3(-10f, 0f, 0f));
                    Part(ToyMeshes.Cylinder(0.12f, 0f, 0.36f, 12), Purple, new Vector3(0f, 0.73f, -0.16f),
                        new Vector3(-38f, 0f, 0f));
                    Part(ToyMeshes.Cylinder(0.28f, 0.27f, 0.09f, 16), Gold, new Vector3(0f, 0.1f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.09f), Gold, new Vector3(0f, 0.86f, -0.3f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.07f, 0.07f, 0.03f)), Gold, new Vector3(0.09f, 0.36f, 0.19f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.05f, 0.05f, 0.03f)), Gold, new Vector3(-0.1f, 0.5f, 0.12f));
                    break;
                case "ember":
                    // BRONZE: a copper bowl on a short foot with glowing coals in it and a small orange flame, the
                    // spark of the champion's fire.
                    Part(ToyMeshes.Cylinder(0.15f, 0.19f, 0.08f, 16), Copper, new Vector3(0f, 0.04f, 0f));
                    Part(ToyMeshes.Cylinder(0.19f, 0.32f, 0.18f, 18), Copper, new Vector3(0f, 0.17f, 0f));
                    Part(ToyMeshes.Cylinder(0.34f, 0.34f, 0.05f, 18), Copper, new Vector3(0f, 0.27f, 0f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.1f, 0.06f, 0.1f)), Red, new Vector3(-0.13f, 0.28f, 0.06f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.09f, 0.05f, 0.09f)), Orange, new Vector3(0.14f, 0.28f, -0.04f));
                    Fire(new Vector3(0f, 0.24f, 0f), 0.17f, 0.44f, Orange, Gold, 7f, 0f, 0.12f);
                    break;
                case "silver-torch":
                    // SILVER: a silver torch with a taller blue flame that flickers faster and throws a spark now and
                    // then.
                    Part(ToyMeshes.Cylinder(0.07f, 0.1f, 0.4f, 12), Grey, new Vector3(0f, 0.2f, 0f));
                    Part(ToyMeshes.Cylinder(0.11f, 0.11f, 0.06f, 12), White, new Vector3(0f, 0.12f, 0f));
                    Part(ToyMeshes.Cylinder(0.11f, 0.11f, 0.06f, 12), White, new Vector3(0f, 0.3f, 0f));
                    Part(ToyMeshes.Cylinder(0.12f, 0.23f, 0.16f, 16), White, new Vector3(0f, 0.47f, 0f));
                    Part(ToyMeshes.Cylinder(0.25f, 0.25f, 0.05f, 16), Grey, new Vector3(0f, 0.56f, 0f));
                    Fire(new Vector3(0f, 0.53f, 0f), 0.18f, 0.56f, Blue, Sky, 10f, 0f, 0.15f);
                    SparkAt(new Vector3(0.05f, 0.95f, 0.03f), 0.05f, Sky, new Vector3(0.12f, 0.45f, 0f), 1.4f, 0f);
                    break;
                case "fire-crown":
                    // GOLD: the crown of a champion, a flame on each of its five points, out of step with each other,
                    // and sparks rising all the time.
                    Part(ToyMeshes.Cylinder(0.3f, 0.34f, 0.22f, 10), Gold, new Vector3(0f, 0.11f, 0f));
                    for (var i = 0; i < 5; i++) {
                        var angle = Mathf.PI * 2f * i / 5f + Mathf.PI * 0.5f;
                        var point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f;
                        Part(ToyMeshes.Cylinder(0.08f, 0.03f, 0.13f, 6), Gold, point + Vector3.up * 0.28f);
                        Fire(point + Vector3.up * 0.32f, 0.085f, 0.26f, Orange, Gold, 9f + i, i * 1.3f, 0.18f);
                    }

                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.07f), Red, new Vector3(0f, 0.11f, 0.33f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.06f), Blue, new Vector3(0.33f, 0.11f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.06f), Purple, new Vector3(-0.33f, 0.11f, 0f));
                    SparkAt(new Vector3(0.18f, 0.6f, 0.1f), 0.045f, Gold, new Vector3(0.05f, 0.4f, 0f), 0.9f, 0f);
                    SparkAt(new Vector3(-0.15f, 0.6f, -0.1f), 0.04f, Orange, new Vector3(-0.04f, 0.45f, 0f), 1.1f, 0.4f);
                    SparkAt(new Vector3(0f, 0.6f, 0.2f), 0.04f, Gold, new Vector3(0f, 0.38f, 0.05f), 0.8f, 0.6f);
                    break;
                case "phoenix":
                    // LEGEND: a phoenix of fire with rainbow wings that beat, a flame crest, a tail of flames and a
                    // trail of sparks behind it.
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.17f, 0.15f, 0.27f)), Red, new Vector3(0f, 0.17f, -0.02f));
                    Part(ToyMeshes.Ellipsoid(new Vector3(0.13f, 0.11f, 0.17f)), Orange, new Vector3(0f, 0.22f, 0.1f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.12f), Orange, new Vector3(0f, 0.38f, 0.22f));
                    Part(ToyMeshes.Cylinder(0.05f, 0f, 0.13f, 8), Gold, new Vector3(0f, 0.36f, 0.37f), new Vector3(90f, 0f, 0f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.03f), Dark, new Vector3(-0.08f, 0.42f, 0.31f));
                    Part(ToyMeshes.Ellipsoid(Vector3.one * 0.03f), Dark, new Vector3(0.08f, 0.42f, 0.31f));
                    Fire(new Vector3(0f, 0.46f, 0.18f), 0.07f, 0.2f, Gold, Orange, 11f, 0.8f, 0.2f, null,
                        new Vector3(-20f, 0f, 0f));
                    // The tail: three tongues of flame leaning back and out.
                    for (var i = -1; i <= 1; i++) {
                        Fire(new Vector3(i * 0.06f, 0.18f, -0.24f), 0.09f - Mathf.Abs(i) * 0.015f,
                            0.38f - Mathf.Abs(i) * 0.08f, i == 0 ? Orange : Red, Gold, 8f + i, i * 1.7f, 0.16f, null,
                            new Vector3(-62f, i * 28f, 0f));
                    }

                    // The wings: a fan of rainbow feathers on a pivot at each shoulder, beating up and down.
                    var feathers = new[] { Pink, Purple, Blue, Sky, Gold };
                    for (var side = -1; side <= 1; side += 2) {
                        var wing = new GameObject("Wing").transform;
                        wing.SetParent(root, false);
                        wing.localPosition = new Vector3(side * 0.13f, 0.25f, 0.02f);
                        for (var i = 0; i < feathers.Length; i++) {
                            var length = 0.2f + 0.05f * i;
                            var spread = 70f - 14f * i;
                            var direction = Quaternion.Euler(0f, 0f, side * -spread) * (Vector3.right * side);
                            Part(ToyMeshes.Ellipsoid(new Vector3(length, 0.035f, 0.07f)), feathers[i],
                                direction * length * 0.85f + Vector3.back * (0.02f * i),
                                new Vector3(0f, side * 8f * i, side * -spread), wing);
                        }

                        motion ??= root.gameObject.AddComponent<TopperMotion>();
                        motion.AddWing(wing, Vector3.forward, 7f, side * 26f);
                    }

                    SparkAt(new Vector3(0f, 0.32f, -0.42f), 0.05f, Gold, new Vector3(0f, 0.3f, -0.25f), 0.9f, 0f);
                    SparkAt(new Vector3(0.08f, 0.3f, -0.4f), 0.04f, Orange, new Vector3(0.1f, 0.35f, -0.2f), 1.1f, 0.35f);
                    SparkAt(new Vector3(-0.08f, 0.3f, -0.4f), 0.04f, Pink, new Vector3(-0.1f, 0.32f, -0.22f), 1f, 0.7f);
                    break;
                default:
                    Object.Destroy(root.gameObject);
                    return null;
            }

            return root;
        }

        // A teardrop of flame standing on its foot at the origin: round below, drawn out to a point at the top.
        private static Mesh FlameMesh(float radius, float height) => ToyMeshes.Revolved(new[] {
            new Vector2(radius * 0.55f, 0f),
            new Vector2(radius * 0.95f, height * 0.18f),
            new Vector2(radius, height * 0.32f),
            new Vector2(radius * 0.82f, height * 0.52f),
            new Vector2(radius * 0.45f, height * 0.76f),
            new Vector2(0f, height),
        }, 12);

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
