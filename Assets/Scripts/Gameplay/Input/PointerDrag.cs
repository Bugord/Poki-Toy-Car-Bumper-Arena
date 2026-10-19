using UnityEngine;

namespace ToyCarArena.Gameplay.Input
{
    /// <summary>
    /// A finger held on the screen, or the mouse with its left button held: where it came down and where it is now, in
    /// screen pixels from the bottom-left corner.
    /// </summary>
    public readonly struct PointerDrag
    {
        /// <summary>
        /// Different for every drag, so a reader can tell a new drag from the one it saw last frame.
        /// </summary>
        public readonly int Id;

        public readonly Vector2 Start;
        public readonly Vector2 Position;
        public readonly bool IsMouse;

        public PointerDrag(int id, Vector2 start, Vector2 position, bool isMouse)
        {
            this.Id = id;
            this.Start = start;
            this.Position = position;
            this.IsMouse = isMouse;
        }
    }
}
