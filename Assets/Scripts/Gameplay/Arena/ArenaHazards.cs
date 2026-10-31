using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Something moving on the floor that the bots should keep out of the way of (a crab, a top, a pencil, a train): what
    /// it threatens and where a bot standing in its way should drive to. A mechanic writes this state and the bots read
    /// it, so neither knows the other, as with <see cref="BallLane"/>.
    /// </summary>
    public interface IArenaHazard
    {
        /// <summary>
        /// Changes each time the hazard starts a new threat (a new pass, a new spot), so a bot can tell a new one from
        /// the one it already reacted to and take its reaction time again.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Whether a car at <paramref name="position"/> is in the hazard's way (within <paramref name="margin"/> more
        /// metres), and if so the flat heading out of it that keeps <paramref name="edgeMargin"/> from the edge.
        /// </summary>
        bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading);
    }

    /// <summary>
    /// The hazards of this arena's mechanic, for the bots, and the seed the mechanics draw their chance from:
    /// <see cref="Bots.BotDriver.Seed"/> sets it, so a headless simulation repeats the hazards with the bots' choices.
    /// Mechanics add their hazards when they are built; on an arena without one the list stays empty.
    /// </summary>
    public sealed class ArenaHazards
    {
        private readonly List<IArenaHazard> hazards = new();

        public int Seed { get; set; }

        public IReadOnlyList<IArenaHazard> All => hazards;

        public void Add(IArenaHazard hazard)
        {
            if (!hazards.Contains(hazard)) {
                hazards.Add(hazard);
            }
        }

        public void Remove(IArenaHazard hazard) => hazards.Remove(hazard);
    }
}
