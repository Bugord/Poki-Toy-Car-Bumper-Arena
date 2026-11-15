using System;
using UnityEngine;

namespace ToyCarArena.Gameplay
{
    /// <summary>
    /// The main menu, apart from the main file: who opens on it. A player who has never played goes straight into the arena,
    /// because a new player decides in the first seconds whether to stay; one who has played opens on the menu,
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

            public bool IsEnabled => enabled;

            /// <summary>
            /// Whether the game opens on the menu: for a player who has played before (<paramref name="hasPlayed"/>),
            /// on any visit. One who never has goes straight into the arena, whatever the visit's number: a first visit
            /// left before the first round ended is still a new player.
            /// </summary>
            public bool OpensFor(bool hasPlayed) => enabled && hasPlayed;
        }
    }
}
