using UnityEngine;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// One pickup lying on the floor of a long match: what it gives and where it lies. <see cref="ArenaPickups"/> owns it
    /// and reuses it, so a bot that keeps hold of one must ask <see cref="IsLive"/> before it drives to it.
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

        /// <summary>What the pickup gives.</summary>
        public PickupKind Kind { get; internal set; }

        /// <summary>The spot on the floor under it, at the floor's height.</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Whether it left the floor because a car took it, not because it faded.</summary>
        public bool WasTaken { get; internal set; }

        /// <summary>Seconds since it appeared.</summary>
        public float Age { get; internal set; }

        /// <summary>Whether it is lying there to be taken: not yet taken, not faded away and not reused.</summary>
        public bool IsLive => State == Stage.Live;

        // The slot's look, kept by the arena that built it.
        internal Transform Root;
        internal Transform Hover;
        internal Transform[] Tokens;
        internal Transform Marker;
        internal MeshRenderer MarkerDisc;
        internal MeshRenderer MarkerRing;

        // Seconds into the pop-in or the shrink.
        internal float StageTime;
        internal float Phase;
    }
}
