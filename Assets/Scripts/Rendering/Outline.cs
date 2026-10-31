using UnityEngine;

namespace ToyCarArena.Rendering
{
    /// <summary>
    /// Opts solid 3D objects into the comic-book outline. The outline is a second pass of <see cref="OutlineFeature"/>
    /// that draws only the renderers carrying <see cref="Layer"/> in their rendering layer mask. A rendering layer is used
    /// rather than a physics layer: the physics layers steer the cars (the steering ray reads the Arena layer), and a
    /// rendering layer changes nothing but how an object is drawn.
    /// </summary>
    public static class Outline
    {
        /// <summary>
        /// The bit of the rendering layer mask that marks a renderer as outlined; bit 0 is URP's own default layer.
        /// </summary>
        public const uint Layer = 1u << 1;

        /// <summary>
        /// Outlines every renderer of the object and its children. Flat stickers (shadows, markers, patches, effects)
        /// are never passed here, because they carry their own outlines.
        /// </summary>
        public static void Apply(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)) {
                renderer.renderingLayerMask |= Layer;
            }
        }
    }
}
