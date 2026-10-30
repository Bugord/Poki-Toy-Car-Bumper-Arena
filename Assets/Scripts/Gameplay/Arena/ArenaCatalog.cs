using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// How each arena looks and what it is called. The numbers of a round on it, and the level that opens it, are in
    /// <see cref="GameTuning.Maps"/> under the same id.
    /// </summary>
    [CreateAssetMenu(fileName = "data_arena_catalog", menuName = "Bonk Arena/Arena Catalog")]
    public sealed class ArenaCatalog : ScriptableObject
    {
        [Tooltip("Arenas in rotation order; each id matches an entry of GameTuning.Maps")]
        [SerializeField]
        private Entry[] arenas = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Arenas => arenas;

        public Entry Find(string id)
        {
            foreach (var arena in arenas) {
                if (arena.Id == id) {
                    return arena;
                }
            }

            return null;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Permanent identity used by storage and analytics; changing it would lose that identity")]
            [SerializeField]
            private string id;

            [Tooltip("Uppercase name shown in the lobby")]
            [SerializeField]
            private string displayName;

            [Tooltip("Material of the floor; empty keeps the one the scene has")]
            [SerializeField]
            private Material floorMaterial;

            [Tooltip("Colour behind the arena; applied together with the floor material, so an entry without a material keeps the scene's sky")]
            [SerializeField]
            private Color background = new(0.53f, 0.81f, 0.92f, 1f);

            public string Id => id;
            public string DisplayName => displayName;
            public Material FloorMaterial => floorMaterial;
            public Color Background => background;
        }
    }
}
