using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The camera over the arena. It keeps the angle it has in the scene and moves along its view, nearer or further,
    /// so the whole arena (<see cref="GameTuning.ArenaSettings.Radius"/> and the view margin) fits the screen in any
    /// shape: a phone held upright needs it much further than a wide window. Also turns screen points and screen
    /// directions into the arena's ground plane, for steering.
    /// <para>
    /// As the edge crumbles away it closes in on what is left of the floor (<see cref="ArenaFloor.SolidRadius"/>), so
    /// the last cars fighting on the small middle stay big enough to read. It eases in once a ring has fallen, not while
    /// it shakes, so a car on a shaking ring stays in view.
    /// </para>
    /// <para>
    /// It also shakes on a big hit (<see cref="Shake"/>): a small offset across the view on top of the fitted place,
    /// fading out, on the unscaled clock so it goes on through a hit-stop. Steering ignores the shake, so the car does
    /// not wobble with the picture.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCamera : MonoBehaviour
    {
        // Points on the arena's rim checked against the view; enough for a circle to within a few centimetres.
        private const int RimSamples = 32;

        // The arena is centred on the world origin, its floor at height 0.
        private static readonly Plane Ground = new(Vector3.up, Vector3.zero);

        [Header("Zoom")]
        [Tooltip("Share of the floor's shrinking the camera follows: 0 keeps the whole starting arena in view, 1 frames " +
            "only what is left of the floor")]
        [Range(0f, 1f)]
        [SerializeField]
        private float zoomShare = 0.85f;

        [Tooltip("Seconds the camera takes, about, to close in after a ring of the edge falls")]
        [Min(0.01f)]
        [SerializeField]
        private float zoomTime = 0.8f;

        [Header("Shake")]
        [Tooltip("Farthest the camera moves across its view at the strongest shake, in metres")]
        [Min(0f)]
        [SerializeField]
        private float shakeDistance = 1.2f;

        [Tooltip("How fast the camera jiggles while it shakes, in jolts per second")]
        [Min(0f)]
        [SerializeField]
        private float shakeFrequency = 22f;

        [Tooltip("How much of the strongest shake dies down per second")]
        [Min(0.01f)]
        [SerializeField]
        private float shakeDecay = 2.5f;

        private GameTuning.ArenaSettings settings;
        private ArenaFloor floor;
        private Camera view;
        private int fittedWidth;
        private int fittedHeight;
        // The distance along the view that fits an arena of radius 1; every distance in the fit grows with the
        // radius, so a closer or wider view is this times the radius in view.
        private float unitDistance;
        // The radius the camera frames now, easing to the target as the floor shrinks; 0 until the first frame.
        private float viewRadius;
        private float zoomSpeed;
        // How hard the camera shakes now, 0 to 1; the offset grows with its square, so small shakes stay small.
        private float shake;
        private Vector3 shakeOffset;
        private float shakeSeed;

        public Camera Camera => view;

        [Inject]
        private void Construct(GameTuning tuning, ArenaFloor floor)
        {
            this.settings = tuning.Arena;
            this.floor = floor;
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            shakeSeed = Random.Range(0f, 100f);
        }

        private void LateUpdate()
        {
            if (view.pixelWidth != fittedWidth || view.pixelHeight != fittedHeight) {
                fittedWidth = view.pixelWidth;
                fittedHeight = view.pixelHeight;
                unitDistance = FitDistance(1f);
            }

            var target = Mathf.Lerp(settings.Radius, floor.SolidRadius, zoomShare) + settings.ViewMargin;
            viewRadius = viewRadius > 0f
                ? Mathf.SmoothDamp(viewRadius, target, ref zoomSpeed, zoomTime)
                : target;
            transform.position = -transform.forward * (unitDistance * viewRadius) + Shaken();
        }

        /// <summary>
        /// Shakes the camera by <paramref name="amount"/>, from 0 (none) to 1 (the most it shakes); a shake on top of
        /// another adds to it, up to the most.
        /// </summary>
        public void Shake(float amount)
        {
            shake = Mathf.Clamp01(shake + Mathf.Max(0f, amount));
        }

        /// <summary>
        /// Where a screen point lands on the arena's floor; false when it points above the horizon.
        /// </summary>
        public bool TryScreenToGround(Vector2 screenPosition, out Vector3 point)
        {
            var ray = view.ScreenPointToRay(screenPosition);
            ray.origin -= shakeOffset;
            if (Ground.Raycast(ray, out var distance)) {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>
        /// A direction on the screen as one on the ground: up on the screen is away from the camera. Keeps the length.
        /// </summary>
        public Vector3 ScreenToGroundDirection(Vector2 screenDirection)
        {
            var forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            return right * screenDirection.x + forward * screenDirection.y;
        }

        /// <summary>
        /// The nearest distance from the arena's centre along the view at which every point of its rim is inside the
        /// view on all four sides.
        /// </summary>
        private float FitDistance(float radius)
        {
            var rotation = transform.rotation;
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var tanVertical = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanHorizontal = tanVertical * view.aspect;

            var distance = 0f;
            for (var i = 0; i < RimSamples; i++) {
                var angle = i * Mathf.PI * 2f / RimSamples;
                var point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // From the camera at the given distance, the point is this deep: its depth from the centre plus it.
                var depth = Vector3.Dot(point, forward);
                distance = Mathf.Max(distance,
                    Mathf.Abs(Vector3.Dot(point, right)) / tanHorizontal - depth,
                    Mathf.Abs(Vector3.Dot(point, up)) / tanVertical - depth);
            }

            return distance;
        }

        /// <summary>
        /// This frame's offset of the shake, across the view, and the shake dying down a step.
        /// </summary>
        private Vector3 Shaken()
        {
            if (shake <= 0f) {
                shakeOffset = Vector3.zero;
                return shakeOffset;
            }

            var time = Time.unscaledTime * shakeFrequency;
            var across = Mathf.PerlinNoise(shakeSeed, time) * 2f - 1f;
            var up = Mathf.PerlinNoise(shakeSeed + 37f, time) * 2f - 1f;
            shakeOffset = (transform.right * across + transform.up * up) * (shakeDistance * shake * shake);
            shake = Mathf.Max(0f, shake - shakeDecay * Time.unscaledDeltaTime);
            return shakeOffset;
        }
    }
}
