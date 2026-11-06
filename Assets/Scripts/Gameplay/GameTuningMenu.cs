using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The main menu, apart from the main file: which visit opens on it. A first visit goes straight into the arena, as
    /// before, because a new player decides in the first seconds whether to stay; a returning player opens on the menu,
    /// with their car, missions and the way into the garage. Nothing here touches the physics or the bots.
    /// </summary>
    public sealed partial class GameTuning
    {
        [Header("Menu")]
        [Tooltip("The main menu returning players open on")]
        [SerializeField]
        private MenuSettings menu = new();

        public MenuSettings Menu => menu;

        [Serializable]
        public sealed class MenuSettings
        {
            [Tooltip("The game opens on the main menu: off, every visit goes straight into the arena")]
            [SerializeField]
            private bool enabled = true;

            [Tooltip("The first visit (Visits, counted on this device) that opens on the menu; the visits before it go "
                + "straight into the arena and wait there for the first input")]
            [Min(1)]
            [SerializeField]
            private int fromVisit = 2;

            public bool IsEnabled => enabled;
            public int FromVisit => fromVisit;

            /// <summary>Whether visit <paramref name="visit"/> (from 1) opens on the menu.</summary>
            public bool OpensOn(int visit) => enabled && visit >= fromVisit;
        }
    }
}
