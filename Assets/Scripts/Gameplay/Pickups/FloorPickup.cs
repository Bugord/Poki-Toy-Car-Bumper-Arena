using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// One mystery box lying on the floor of a long match, and where. What it gives is rolled only when a car breaks it
    /// (<see cref="ItemRoll"/>). <see cref="ArenaPickups"/> owns it and reuses it, so a bot that keeps hold of one must ask
    /// <see cref="IsLive"/> before it drives to it.
    /// </summary>
    public sealed class FloorPickup
    {
        /// <summary>Where a slot is in its life: free, lying there to be taken, or shrinking away.</summary>
        internal enum Stage
        {
            Free,
            Live,
            Leaving,
        }

        internal Stage State;

        /// <summary>The spot on the floor under it, at the floor's height.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Whether it left the floor because a car broke it, not because it faded.</summary>
        public bool WasTaken { get; internal set; }

        /// <summary>
        /// The only car that may break it, the bots leave it lying (the item lesson's box, for the player); null for
        /// anyone.
        /// </summary>
        public Cars.Car Owner { get; internal set; }

        /// <summary>Whether <paramref name="car"/> may break it.</summary>
        public bool IsFor(Cars.Car car) => Owner == null || Owner == car;

        /// <summary>Seconds since it appeared.</summary>
        public float Age { get; internal set; }

        /// <summary>Whether it is lying there to be taken: not yet broken, not faded away and not reused.</summary>
        public bool IsLive => State == Stage.Live;

        // The slot's look, kept by the arena that built it.
        internal Transform Root;
        internal Transform Hover;
        internal MeshRenderer BoxRenderer;
        internal Transform Marker;
        internal MeshRenderer MarkerDisc;
        internal MeshRenderer MarkerRing;

        // Seconds into the pop-in or the shrink.
        internal float StageTime;
        internal float Phase;
    }
}
