using System;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The arena this scene was built for: its numbers and, when the catalog has one for it, its look. Chosen once as
    /// the scene is loaded from <see cref="IArenaRotation.Current"/>, because the next round's scene is the next arena.
    /// </summary>
    public sealed class ArenaMap
    {
        public string Id { get; }
        public GameTuning.ArenaSettings Settings { get; }
        public GameTuning.ArenaMechanic Mechanic { get; }

        /// <summary>The look of the arena, or null when the catalog has no entry for it: the scene's own stays.</summary>
        public ArenaCatalog.Entry Theme { get; }

        /// <summary>
        /// The tiles and outline of the floor when it is not a circle, null when it is. Made with the map, as the props
        /// and the world below need the outline before the floor is built.
        /// </summary>
        public ArenaShape Shape { get; }

        public ArenaMap(GameTuning tuning, IArenaRotation rotation, ArenaCatalog catalog)
        {
            Id = rotation.Current;
            foreach (var map in tuning.Maps) {
                if (map.Id == Id) {
                    Settings = map.Arena;
                    Mechanic = map.Mechanic;
                    break;
                }
            }

            if (Settings == null) {
                throw new ArgumentException($"GameTuning.Maps has no arena '{Id}'.");
            }

            Theme = catalog.Find(Id);
            // The random numbers only decide the order tiles crumble in, so each round is a different crumbling.
            Shape = ArenaShape.Create(Settings, new System.Random(UnityEngine.Random.Range(int.MinValue, int.MaxValue)));
        }

        /// <summary>
        /// Distance from the middle to the rim of the whole floor along a flat direction: the radius for a circle.
        /// </summary>
        public float RimRadius(Vector3 direction)
        {
            return Shape == null ? Settings.Radius : Shape.RimRadius(new Vector2(direction.x, direction.z));
        }

        /// <summary>The floor material of the theme, or null to keep the scene's.</summary>
        public Material FloorMaterial => Theme != null ? Theme.FloorMaterial : null;

        /// <summary>
        /// Whether the theme restyles the scene. It is all or nothing, floor and background together, so an entry still
        /// waiting for its material leaves the scene as it is.
        /// </summary>
        public bool HasTheme => FloorMaterial != null;
    }
}
