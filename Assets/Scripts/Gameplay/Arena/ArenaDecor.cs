using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Large toy-world props around the rim of the floating arena, so each arena reads as a place (a mug and books on a
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
                var rotation = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(0f, item.Yaw, 0f);

                var instance = Object.Instantiate(item.Model, position, rotation, root);
                instance.transform.localScale = Vector3.one * item.Scale;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>()) {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
            }
        }
    }
}
