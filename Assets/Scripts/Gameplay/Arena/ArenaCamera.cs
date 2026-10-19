using UnityEngine;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The fixed camera over the arena. It keeps the angle it has in the scene and moves along its view, nearer or
    /// further, so the whole arena (<see cref="GameTuning.ArenaSettings.Radius"/> and the view margin) fits the screen
    /// in any shape: a phone held upright needs it much further than a wide window. Also turns screen points and
    /// screen directions into the arena's ground plane, for steering.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class ArenaCamera : MonoBehaviour
    {
        // Points on the arena's rim checked against the view; enough for a circle to within a few centimetres.
        private const int RimSamples = 32;

        // The arena is centred on the world origin, its floor at height 0.
        private static readonly Plane Ground = new(Vector3.up, Vector3.zero);

        private GameTuning.ArenaSettings settings;
        private Camera view;
        private int fittedWidth;
        private int fittedHeight;
        private float fittedRadius;

        public Camera Camera => view;

        [Inject]
        private void Construct(GameTuning tuning)
        {
            this.settings = tuning.Arena;
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            var radius = settings.Radius + settings.ViewMargin;
            if (view.pixelWidth != fittedWidth || view.pixelHeight != fittedHeight || radius != fittedRadius) {
                fittedWidth = view.pixelWidth;
                fittedHeight = view.pixelHeight;
                fittedRadius = radius;
                Fit(radius);
            }
        }

        /// <summary>
        /// Where a screen point lands on the arena's floor; false when it points above the horizon.
        /// </summary>
        public bool TryScreenToGround(Vector2 screenPosition, out Vector3 point)
        {
            var ray = view.ScreenPointToRay(screenPosition);
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
        private void Fit(float radius)
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

            transform.position = -forward * distance;
        }
    }
}
