using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Stable save and analytics identities, independent of the cars spawned in a round. Model and portrait references
    /// will belong to these entries when the garage gets its visuals.
    /// </summary>
    [CreateAssetMenu(fileName = "data_car_catalog", menuName = "Bonk Arena/Car Catalog")]
    public sealed class CarCatalog : ScriptableObject
    {
        [Header("Garage")]
        [Tooltip("Cars in display order; unlocks are derived from the player's level")]
        [SerializeField]
        private Entry[] cars = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Cars => cars;

        public Entry Find(string id)
        {
            foreach (var car in cars) {
                if (car.Id == id) {
                    return car;
                }
            }

            return null;
        }

        public Entry NextLocked(int level)
        {
            Entry next = null;
            foreach (var car in cars) {
                if (car.UnlockLevel > level && (next == null || car.UnlockLevel < next.UnlockLevel)) {
                    next = car;
                }
            }

            return next;
        }

        [Serializable]
        public sealed class Entry
        {
            [Tooltip("Permanent identity used by storage and analytics; changing it would lose that identity")]
            [SerializeField]
            private string id;

            [Tooltip("Uppercase name shown in the garage and on unlock stickers")]
            [SerializeField]
            private string displayName;

            [Tooltip("First player level at which this car is available")]
            [Min(1)]
            [SerializeField]
            private int unlockLevel = 1;

            // Future garage tasks add the model and portrait here, without changing the saved id.

            public string Id => id;
            public string DisplayName => displayName;
            public int UnlockLevel => unlockLevel;
        }
    }
}
