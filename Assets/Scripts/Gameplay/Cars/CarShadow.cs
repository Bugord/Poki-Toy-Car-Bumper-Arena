using UnityEngine;

namespace ToyCarArena.Gameplay.Cars
{
    /// <summary>
    /// A soft dark spot on the floor under the car. The light's real shadows reach no further than a few dozen metres
    /// from the camera, and the camera stands further off to fit the whole arena, so without this the cars seem to
    /// hover. A spot costs a ray and a quad per car, which a weak phone can afford, and it tells a jump or a fall at a
    /// glance: it shrinks and pales as the car leaves the floor and fades out when there is no floor under it.
    ///
    /// Like the player's marker it lives in the world, not on the car, so it stays flat on the floor however the car
    /// tilts. It is an object with a renderer of its own and a copy of the material, as the marker is: drawn with
    /// Graphics.DrawMesh and a property block the spot did not show at all. Looks only: no colliders, nothing the
    /// gameplay reads.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarShadow : MonoBehaviour
    {
        private const int TextureSize = 64;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static Mesh quad;
        private static Texture2D spot;
        private static int arenaMask;

        [Tooltip("A transparent unlit material; its colour tints the spot and its alpha is the spot's opacity at the " +
            "centre")]
        [SerializeField]
        private Material material;

        [Tooltip("Size of the spot against the car's footprint")]
        [Min(0f)]
        [SerializeField]
        private float sizeScale = 1.7f;

        [Tooltip("Height above the floor, in metres, at which the spot is gone; it shrinks and pales on the way")]
        [Min(0.01f)]
        [SerializeField]
        private float fadeHeight = 3f;

        [Tooltip("Share of its size the spot keeps at the fade height")]
        [Range(0f, 1f)]
        [SerializeField]
        private float minScale = 0.5f;

        [Tooltip("Seconds the spot takes to fade out once there is no floor under the car, and back in")]
        [Min(0.01f)]
        [SerializeField]
        private float fadeTime = 0.15f;

        [Tooltip("Height of the spot above the floor, in metres; just enough not to flicker with it, below the " +
            "player's ring")]
        [Min(0f)]
        [SerializeField]
        private float lift = 0.015f;

        private Car car;
        private Vector2 footprint;
        private Vector3 floorPoint;
        private float visible;
        private Transform shape;
        private MeshRenderer shapeRenderer;
        private Material ownMaterial;

        /// <summary>
        /// What the spot's size is multiplied by while the car is grown (a mega item); 1 at its own size.
        /// </summary>
        public float Growth { get; set; } = 1f;

        /// <summary>
        /// Sizes the spot to the car's model, its width and length in metres.
        /// </summary>
        public void Fit(Vector2 size)
        {
            this.footprint = size * sizeScale;
        }

        private void Awake()
        {
            this.car = GetComponent<Car>();
            if (material == null) {
                Debug.LogWarning($"{name}: CarShadow has no material, the car is drawn without its shadow.", this);
                enabled = false;
                return;
            }

            if (arenaMask == 0) {
                arenaMask = LayerMask.GetMask("Arena");
            }

            // Built in code once and shared by every car, so the spot needs no texture asset. Unity's null check, not
            // C#'s: without a domain reload a mesh from the last Play Mode survives as a destroyed object.
            if (quad == null) {
                quad = BuildQuad();
            }

            if (spot == null) {
                spot = BuildSpot();
            }

            // Each car fades its own spot, so each has its own copy of the material.
            this.ownMaterial = new Material(material) { name = material.name + " (Spot)" };
            ownMaterial.SetTexture(BaseMapId, spot);
            var root = new GameObject("Car Shadow") { hideFlags = HideFlags.DontSave };
            // The arena layer is the floor cars steer by; a shadow is no floor, so it takes the default layer.
            root.layer = 0;
            root.AddComponent<MeshFilter>().sharedMesh = quad;
            this.shapeRenderer = root.AddComponent<MeshRenderer>();
            shapeRenderer.sharedMaterial = ownMaterial;
            shapeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shapeRenderer.receiveShadows = false;
            shapeRenderer.enabled = false;
            this.shape = root.transform;
        }

        private void OnDestroy()
        {
            if (shape != null) {
                Destroy(shape.gameObject);
            }

            if (ownMaterial != null) {
                Destroy(ownMaterial);
            }
        }

        private void LateUpdate()
        {
            if (car != null && car.IsOut) {
                shapeRenderer.enabled = false;
                return;
            }

            var position = transform.position;
            var height = fadeHeight;
            if (Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out var hit, fadeHeight + 0.5f, arenaMask,
                    QueryTriggerInteraction.Ignore)) {
                this.floorPoint = hit.point;
                height = Mathf.Max(0f, position.y - hit.point.y);
            }

            var target = height < fadeHeight ? 1f : 0f;
            this.visible = Mathf.MoveTowards(visible, target, Time.deltaTime / fadeTime);
            shapeRenderer.enabled = visible > 0f;
            if (visible <= 0f) {
                return;
            }

            var closeness = 1f - Mathf.Clamp01(height / fadeHeight);
            var size = footprint * (Growth * Mathf.Lerp(minScale, 1f, closeness));
            var color = material.color;
            color.a *= visible * closeness;
            ownMaterial.SetColor(BaseColorId, color);
            shape.SetPositionAndRotation(floorPoint + Vector3.up * lift, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            shape.localScale = new Vector3(size.x, 1f, size.y);
        }

        /// <summary>
        /// A unit square lying flat and facing up, centred on the origin.
        /// </summary>
        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "Car Shadow" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            // Clockwise seen from above, so the square faces up.
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A white disc whose alpha falls smoothly to nothing at the edge: the material's colour tints it, and the
        /// soft edge keeps the spot from reading as a hard sticker on the floor.
        /// </summary>
        private static Texture2D BuildSpot()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Car Shadow Spot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[TextureSize * TextureSize];
            var half = (TextureSize - 1) * 0.5f;
            for (var y = 0; y < TextureSize; y++) {
                for (var x = 0; x < TextureSize; x++) {
                    var distance = new Vector2((x - half) / half, (y - half) / half).magnitude;
                    var alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, distance));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
