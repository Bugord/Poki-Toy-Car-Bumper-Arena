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

        /// <summary>The look of the arena, or null when the catalog has no entry for it: the scene's own stays.</summary>
        public ArenaCatalog.Entry Theme { get; }

        public ArenaMap(GameTuning tuning, IArenaRotation rotation, ArenaCatalog catalog)
        {
            Id = rotation.Current;
            foreach (var map in tuning.Maps) {
                if (map.Id == Id) {
                    Settings = map.Arena;
                    break;
                }
            }

            if (Settings == null) {
                throw new ArgumentException($"GameTuning.Maps has no arena '{Id}'.");
            }

            Theme = catalog.Find(Id);
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
