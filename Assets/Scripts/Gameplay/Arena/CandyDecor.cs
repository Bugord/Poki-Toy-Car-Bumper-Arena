using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The materials of the Candy arena's patches, held by a component of the scene so they ship in the build (a material
    /// only built in code would leave its shader out of WebGL). Looks only: the numbers the gameplay reads are
    /// <see cref="GameTuning.CandySettings"/>.
    /// </summary>
    public sealed class CandyDecor : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("Navy rim around every patch; drawn first")]
        [SerializeField]
        private Material outline;

        [Tooltip("Amber-brown body of a caramel patch")]
        [SerializeField]
        private Material caramel;

        [Tooltip("Lighter glossy highlight on a caramel patch")]
        [SerializeField]
        private Material caramelShine;

        [Tooltip("Pale blue-white body of an icing patch")]
        [SerializeField]
        private Material icing;

        [Tooltip("First colour of the sprinkle dashes on icing")]
        [SerializeField]
        private Material sprinkleA;

        [Tooltip("Second colour of the sprinkle dashes on icing")]
        [SerializeField]
        private Material sprinkleB;

        [Header("Look")]
        [Tooltip("Height of the patches above the floor, in metres: above the floor, below the car shadows")]
        [Min(0f)]
        [SerializeField]
        private float lift = 0.008f;

        [Tooltip("Width of the navy rim, in metres")]
        [Min(0f)]
        [SerializeField]
        private float outlineWidth = 0.16f;

        [Tooltip("How much the rim of a patch bulges and dips, as a share of its radius")]
        [Range(0f, 0.2f)]
        [SerializeField]
        private float wobble = 0.09f;

        [Tooltip("Bulges around a patch")]
        [Range(3, 8)]
        [SerializeField]
        private int bumps = 5;

        [Tooltip("Sprinkle dashes on an icing patch")]
        [Range(0, 20)]
        [SerializeField]
        private int sprinkles = 10;

        public Material Outline => outline;
        public Material Caramel => caramel;
        public Material CaramelShine => caramelShine;
        public Material Icing => icing;
        public Material SprinkleA => sprinkleA;
        public Material SprinkleB => sprinkleB;
        public float Lift => lift;
        public float OutlineWidth => outlineWidth;
        public float Wobble => wobble;
        public int Bumps => bumps;
        public int Sprinkles => sprinkles;

        /// <summary>Whether every material is set; the scope checks it, as it does its other references.</summary>
        public bool IsComplete => outline != null && caramel != null && caramelShine != null && icing != null
            && sprinkleA != null && sprinkleB != null;
    }
}
