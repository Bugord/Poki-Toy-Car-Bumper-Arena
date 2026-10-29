using System;
using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// Stable identities and cosmetic models, independent of the physics of cars spawned in a round.
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

            [SerializeField]
            private GameObject model;

            [Tooltip("Body paint in the shared 8 by 4 atlas, counted from the bottom left")]
            [SerializeField]
            private Vector2Int bodySwatch;

            [SerializeField]
            private Texture2D portrait;

            public string Id => id;
            public string DisplayName => displayName;
            public int UnlockLevel => unlockLevel;
            public GameObject Model => model;
            public Vector2Int BodySwatch => bodySwatch;
            public Texture2D Portrait => portrait;
        }
    }
}
