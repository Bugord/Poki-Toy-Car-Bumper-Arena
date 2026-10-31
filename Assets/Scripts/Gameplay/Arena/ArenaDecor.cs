using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Large toy-world props standing in the world below the floating arena (and a few round its rim), so each arena reads as a place (a mug and books on a
    /// table top, candy on a sweet one). Built once as the scene is, from the decor list of the arena's catalog entry;
    /// nothing is built for an arena without a theme. The props are only seen: no colliders, no shadows, so they never
    /// touch the cars or the steering ray, and they stay put while the floor crumbles.
    /// <para>
    /// Items are placed relative to the camera, because what hides the floor depends on where the camera looks from:
    /// angle 0 is straight behind the arena as the camera sees it, 90 to the right of the screen, 180 in front of it.
    /// </para>
    /// </summary>
    public sealed class ArenaDecor : IInitializable
    {
        private readonly ArenaMap map;
        private readonly ArenaCamera arenaCamera;
        private readonly Transform parent;

        public ArenaDecor(ArenaMap map, ArenaCamera arenaCamera, Transform parent)
        {
            this.map = map;
            this.arenaCamera = arenaCamera;
            this.parent = parent;
        }

        public void Initialize()
        {
            if (!map.HasTheme || map.Theme.Decor.Count == 0) {
                return;
            }

            var behind = Vector3.ProjectOnPlane(arenaCamera.transform.forward, Vector3.up).normalized;
            var root = new GameObject("Decor").transform;
            root.SetParent(parent, false);

            foreach (var item in map.Theme.Decor) {
                if (item.Model == null) {
                    continue;
                }

                var outward = Quaternion.Euler(0f, item.Angle, 0f) * behind;
                var position = outward * (map.Settings.Radius + item.Distance) + Vector3.up * item.Height;
                // Yaw 0 turns the prop's front to the arena's middle.
                var rotation = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0f, item.Yaw, 0f)
                    * Quaternion.Euler(item.Pitch, 0f, 0f);

                Place(item.Model, position, rotation, item.Scale, root);
            }
        }

        /// <summary>
        /// Stands a model at a point, turned about the middle of its footprint. Kenney's models do not agree where their
        /// origin is (the bear's is a corner), and a turn about a corner would swing the prop off its place.
        /// </summary>
        public static void Place(GameObject model, Vector3 position, Quaternion rotation, float scale, Transform parent)
        {
            var pivot = new GameObject(model.name).transform;
            pivot.SetParent(parent, false);
            pivot.position = position;
            var instance = Object.Instantiate(model, pivot);
            instance.transform.localScale = Vector3.one * scale;
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0) {
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) {
                    bounds.Encapsulate(renderer.bounds);
                }

                var centre = bounds.center - position;
                instance.transform.localPosition = new Vector3(-centre.x, 0f, -centre.z);
            }

            pivot.rotation = rotation;
            foreach (var renderer in renderers) {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
    }
}
