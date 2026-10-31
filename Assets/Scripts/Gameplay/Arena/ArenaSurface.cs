using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The floor's feel as every layer of the arena's mechanics gives it together: the one <see cref="IArenaSurface"/> the
    /// cars ask. A mechanic that changes how the floor drives (the Candy patches, the Mounds' risen sand, a later jelly)
    /// adds itself as a layer; the answers multiply, so layers on the same spot stack, and a layer that is not there on this
    /// arena costs nothing. With no layer it is plain floor.
    /// </summary>
    public sealed class ArenaSurface : IArenaSurface
    {
        private readonly List<ISurfaceLayer> layers = new();

        public void Add(ISurfaceLayer layer)
        {
            if (!layers.Contains(layer)) {
                layers.Add(layer);
            }
        }

        public SurfaceFeel Sample(Vector3 position)
        {
            var feel = SurfaceFeel.Neutral;
            foreach (var layer in layers) {
                feel = feel.Combine(layer.Sample(position));
            }

            return feel;
        }
    }
}
