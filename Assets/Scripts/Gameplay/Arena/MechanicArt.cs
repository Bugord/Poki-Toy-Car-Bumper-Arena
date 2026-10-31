using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The materials of the arena mechanics that build their look in code (a crab, a top, jelly pads, a train), by name,
    /// in one asset the Game scene's scope holds. One shared list keeps the scene from growing a component per mechanic;
    /// a mechanic asks for its materials by the names it adds here.
    /// </summary>
    [CreateAssetMenu(menuName = "Toy Car Arena/Mechanic Art", fileName = "data_mechanic_art")]
    public sealed class MechanicArt : ScriptableObject
    {
        [Serializable]
        public sealed class Item
        {
            [Tooltip("The name a mechanic asks for, e.g. crab_shell")]
            [SerializeField]
            private string key;

            [SerializeField]
            private Material material;

            public string Key => key;
            public Material Material => material;
        }

        [SerializeField]
        private List<Item> items = new();

        public IReadOnlyList<Item> Items => items;

        /// <summary>The material named <paramref name="key"/>; throws so a missing one is found in the editor, not on a phone.</summary>
        public Material Get(string key)
        {
            foreach (var item in items) {
                if (item.Key == key && item.Material != null) {
                    return item.Material;
                }
            }

            throw new KeyNotFoundException($"{nameof(MechanicArt)} '{name}' has no material '{key}'.");
        }
    }
}
