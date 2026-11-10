using System;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// The model of every topper, by its id: FBX files made in Blender (Tools/toppers), painted with the cars' atlas, so
    /// <see cref="CarTopper.Build"/> puts them on the cars' own material. The id is what the wallet saves and the
    /// analytics name, so a league's topper keeps its first id (ember, silver-torch, fire-crown, phoenix) whatever its
    /// model is now.
    /// </summary>
    [CreateAssetMenu(menuName = "Toy Car Arena/Topper Models", fileName = "data_topper_models")]
    public sealed class TopperModels : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            [Tooltip("The topper's id, as GameTuning.Toppers names it")]
            public string Id;

            [Tooltip("Its model: a root with a part named body and the moving parts beside it")]
            public GameObject Model;
        }

        [Tooltip("Every topper the game builds, on sale, exclusive and the leagues'")]
        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();

        /// <summary>The topper's model, or null for an unknown id.</summary>
        public GameObject Find(string id)
        {
            foreach (var entry in entries) {
                if (entry.Id == id) {
                    return entry.Model;
                }
            }

            return null;
        }

        /// <summary>Puts in the model of an id (the editor fills the asset with it).</summary>
        public void Set(string id, GameObject model)
        {
            for (var i = 0; i < entries.Length; i++) {
                if (entries[i].Id == id) {
                    entries[i].Model = model;
                    return;
                }
            }

            Array.Resize(ref entries, entries.Length + 1);
            entries[^1] = new Entry { Id = id, Model = model };
        }
    }
}
