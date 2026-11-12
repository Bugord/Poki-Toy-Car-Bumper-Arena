using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Rendering
{
    /// <summary>The screens a player can be on, for <see cref="ShownScreens"/>.</summary>
    public enum ShownScreen
    {
        None,
        Round,
        Result,
        Curtain,
        Menu,
        Garage,
        Ad,
    }

    /// <summary>
    /// The screens shown now, so the arena's camera can stop drawing the 3D while an opaque screen covers it (the
    /// curtain with its lobby, the menu, the garage) and the frame rate can be counted by screen. A weak phone's frames
    /// are mostly filling pixels, and the arena loaded behind the menu and the lobby filled them for nothing.
    /// </summary>
    /// <remarks>
    /// Screens come and go out of order (the result closes under the curtain that came down over it, the HUD fades in
    /// under the curtain that is still up), so each one is removed by its own entry, and the screen the player is on is
    /// an ad first, then the curtain, then the newest one shown. Static, as the arena's camera lives in the Game scene and
    /// the screens in the app's; a screen removes its entry when it hides or is disposed, and tests clear the lot.
    /// </remarks>
    public static class ShownScreens
    {
        private sealed class Entry
        {
            public ShownScreen Screen;
            public bool CoversArena;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static int covering;

        /// <summary>Whether an opaque screen lies over the whole arena, so drawing it shows nowhere.</summary>
        public static bool IsArenaCovered => covering > 0;

        /// <summary>The screen the player is on: an ad, else the curtain, else the newest one shown.</summary>
        public static ShownScreen Current
        {
            get {
                var current = ShownScreen.None;
                foreach (var entry in entries) {
                    if (entry.Screen == ShownScreen.Ad) {
                        return ShownScreen.Ad;
                    }

                    if (current != ShownScreen.Curtain) {
                        current = entry.Screen;
                    }
                }

                return current;
            }
        }

        /// <summary>
        /// Notes <paramref name="screen"/> as shown; the returned entry goes back to <see cref="Remove"/> when it hides.
        /// </summary>
        public static object Add(ShownScreen screen, bool coversArena)
        {
            var entry = new Entry { Screen = screen, CoversArena = coversArena };
            entries.Add(entry);
            if (coversArena) {
                covering++;
            }

            return entry;
        }

        /// <summary>Forgets an entry of <see cref="Add"/>; null or one already removed does nothing.</summary>
        public static void Remove(object entry)
        {
            if (entry is Entry shown && entries.Remove(shown) && shown.CoversArena) {
                covering--;
            }
        }

        /// <summary>Forgets every screen, for a test that tears its app down without hiding them.</summary>
        public static void Clear()
        {
            entries.Clear();
            covering = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlay() => Clear();
    }
}
