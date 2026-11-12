using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The toppers on the cars' roofs: models made in Blender (<see cref="TopperModels"/>) on the cars' own atlas
    /// material, so they take the outline like the car under it. <see cref="Build"/> makes one standing on the origin
    /// with its front along +Z; the ids are those of <see cref="GameTuning.TopperSettings"/>.
    /// </summary>
    public static class CarTopper
    {
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
        /// The toppers the leagues give, one theme growing grander league by league: helmets, a Spartan's in bronze, a
        /// knight's in silver, a samurai's in gold and an astronaut's for the legends. The ids are those of the fire the
        /// leagues gave first (saves and analytics name them). A bot wears one only when its level has reached the league.
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
        /// Builds the topper under <paramref name="parent"/> from its model (<see cref="TopperModels"/>), every part on
        /// <paramref name="material"/>; the meshes stay the model's. <paramref name="spinner"/> is the part that turns
        /// (the propeller's blades), or null. A topper with other living parts (a swaying aerial, a plume, an orbit) moves
        /// them itself (<see cref="TopperMotion"/> on its root). Null for no id or one without a model.
        /// </summary>
        public static Transform Build(string id, Transform parent, Material material, TopperModels models,
            out Transform spinner)
        {
            spinner = null;
            var source = string.IsNullOrEmpty(id) || models == null ? null : models.Find(id);
            if (source == null) {
                return null;
            }

            // The model under a root of its own: the import keeps a turn on the model (on its part, for a model of one),
            // and the callers place and scale the root.
            var root = new GameObject($"Topper {id}").transform;
            root.SetParent(parent, false);
            Object.Instantiate(source, root, false);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            TopperMotion motion = null;
            TopperMotion Motion() => motion ??= root.gameObject.AddComponent<TopperMotion>();

            foreach (var part in root.GetComponentsInChildren<Transform>(true)) {
                switch (part.name) {
                    case "blades":
                        spinner = part;
                        break;
                    case "sway":
                        // The aerial's rod on its spring: two slow wobbles out of step, so it never just ticks.
                        Motion().AddSway(part, Vector3.right, 3.1f, 6f, 0f);
                        Motion().AddSway(part, Vector3.forward, 2.3f, 5f, 1.3f);
                        break;
                    case "crest":
                        Motion().AddSway(part, Vector3.forward, 2.4f, 3f, 0f);
                        break;
                    case "plume":
                        Motion().AddSway(part, Vector3.right, 2.7f, 7f, 0f);
                        Motion().AddSway(part, Vector3.forward, 1.9f, 6f, 0.9f);
                        break;
                    case "blink":
                        Motion().AddPulse(part, 4f, 0.25f);
                        break;
                    case "orbit":
                        // The ring was tipped in Blender; it turns round its own axis.
                        Motion().AddSpin(part, Vector3.up, 45f);
                        break;
                }
            }

            return root;
        }
    }
}
