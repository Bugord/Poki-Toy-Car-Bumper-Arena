using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The materials and look of the Carousel arena's turntable, held by a component of the scene so they ship in the
    /// build (a material only built in code would leave its shader out of WebGL). Looks only: the numbers the gameplay
    /// reads are <see cref="GameTuning.CarouselSettings"/>.
    /// </summary>
    public sealed class CarouselDecor : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("Navy rim around the turntable; drawn first")]
        [SerializeField]
        private Material outline;

        [Tooltip("Colour of every other wedge")]
        [SerializeField]
        private Material wedgeA;

        [Tooltip("Colour of the wedges between")]
        [SerializeField]
        private Material wedgeB;

        [Tooltip("Arrows along the rim showing which way it turns")]
        [SerializeField]
        private Material arrow;

        [Tooltip("What the arrows flash in just before the turntable reverses")]
        [SerializeField]
        private Material arrowWarning;

        [Header("Look")]
        [Tooltip("Height of the turntable above the floor, in metres: above the floor, below the car shadows")]
        [Min(0f)]
        [SerializeField]
        private float lift = 0.008f;

        [Tooltip("Width of the navy rim, in metres")]
        [Min(0f)]
        [SerializeField]
        private float rimWidth = 0.22f;

        [Tooltip("Wedges of the pattern; even, so the two colours alternate all round")]
        [Range(4, 16)]
        [SerializeField]
        private int wedges = 8;

        [Tooltip("Arrows along the rim")]
        [Range(3, 16)]
        [SerializeField]
        private int arrows = 8;

        [Tooltip("Flashes per second of the arrows during the warning")]
        [Min(1f)]
        [SerializeField]
        private float flashRate = 8f;

        public Material Outline => outline;
        public Material WedgeA => wedgeA;
        public Material WedgeB => wedgeB;
        public Material Arrow => arrow;
        public Material ArrowWarning => arrowWarning;
        public float Lift => lift;
        public float RimWidth => rimWidth;
        public int Wedges => wedges & ~1;
        public int Arrows => arrows;
        public float FlashRate => flashRate;

        /// <summary>Whether every material is set; the scope checks it, as it does its other references.</summary>
        public bool IsComplete => outline != null && wedgeA != null && wedgeB != null && arrow != null
            && arrowWarning != null;
    }
}
