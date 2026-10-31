using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where a wave of tiles is in its crumbling.
    /// </summary>
    public enum WaveState
    {
        Standing,
        Warning,
        Falling,
        Gone,
    }

    /// <summary>
    /// One block of the floor: what it looks like and where it lies. The floor makes an object of it that can shake and
    /// fall on its own. Its local +X points away from the middle of the arena, which is the way it drifts as it falls.
    /// </summary>
    public sealed class FloorTile
    {
        public readonly string Name;
        public readonly Mesh Mesh;
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        /// <summary>The wave of crumbling the tile belongs to; the waves go in the order they fall.</summary>
        public readonly int Wave;

        public FloorTile(string name, Mesh mesh, Vector3 position, Quaternion rotation, int wave)
        {
            this.Name = name;
            this.Mesh = mesh;
            this.Position = position;
            this.Rotation = rotation;
            this.Wave = wave;
        }
    }

    /// <summary>
    /// A wave of tiles that shake and fall together. For the effects it carries the band of distances from the middle its
    /// tiles lie in, which is what a round floor's rings are; a floor of another shape will say more.
    /// </summary>
    public readonly struct CrumbleWave
    {
        public readonly int Index;
        public readonly float Inner;
        public readonly float Outer;

        public CrumbleWave(int index, float inner, float outer)
        {
            this.Index = index;
            this.Inner = inner;
            this.Outer = outer;
        }
    }

    /// <summary>
    /// The floor as a set of tiles: what each looks like and which wave of crumbling it belongs to, the part that never
    /// crumbles, and the single collider of the floor that is left. A layout also keeps its <see cref="IArenaEdge"/> up to
    /// date as waves warn and fall. Each shape of arena has its own layout; <see cref="CircleLayout"/> is a disc inside
    /// rings counted from the edge.
    /// </summary>
    public abstract class ArenaLayout
    {
        protected readonly List<FloorTile> tiles = new();
        protected readonly List<Mesh> meshes = new();

        /// <summary>The tiles, wave by wave in the order the waves fall.</summary>
        public IReadOnlyList<FloorTile> Tiles => tiles;

        /// <summary>Every mesh the layout made, for the floor to release.</summary>
        public IReadOnlyList<Mesh> Meshes => meshes;

        /// <summary>How many waves the floor crumbles in; none when the edge does not crumble.</summary>
        public abstract int WaveCount { get; }

        /// <summary>The part of the floor that never crumbles, seen.</summary>
        public Mesh CoreMesh { get; protected set; }

        /// <summary>The one seamless collider of the floor still in place.</summary>
        public Mesh ColliderMesh { get; protected set; }

        public abstract CrumbleWave Wave(int wave);

        /// <summary>
        /// Whether a point is over the tiles of a wave, for the cars the wave takes down with it.
        /// </summary>
        public abstract bool Contains(int wave, Vector3 position);

        /// <summary>
        /// Takes in how far each wave has got: the edge follows the waves not yet shaking (safe) and not yet fallen
        /// (solid), and the collider the waves not yet fallen.
        /// </summary>
        /// <returns>True when <see cref="ColliderMesh"/> changed.</returns>
        public abstract bool Refresh(IReadOnlyList<WaveState> states);

        protected Mesh Keep(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }
    }
}
