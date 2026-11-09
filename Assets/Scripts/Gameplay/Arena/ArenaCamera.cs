using System.Collections.Generic;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The camera over the arena. It keeps the angle it has in the scene and moves along its view, nearer or further.
    /// It stands <see cref="closeUp"/> times nearer than the distance at which the whole arena
    /// (<see cref="IArenaEdge.FullExtent"/> and the view margin) fits the screen, so the cars read big on a phone held
    /// upright as well as on a wide window, and it follows the player's car across the arena: never past the framed
    /// floor's rim and a little of the void beyond, so the edge nearest the player is always on the screen. Also turns
    /// screen points and screen directions into the arena's ground plane, for steering.
    /// <para>
    /// As the edge crumbles away it closes in on what is left of the floor (<see cref="IArenaEdge.SolidExtent"/>): once
    /// that fits nearer than the close-up, the camera frames all of it, so the last cars fighting on the small middle stay
    /// big enough to read and nothing of the floor is out of view. It eases in once a ring has fallen, not while it
    /// shakes, so a car on a shaking ring stays in view.
    /// </para>
    /// <para>
    /// The camera follows the player only from GO and while their car is in play: before the round begins it keeps the
    /// car where it stood on the screen with the whole arena in view, around which the start screen's HUD is laid out;
    /// once the car is out it stays where it was (over the result too), and when a match brings the car back somewhere
    /// else it pans there.
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
        // A reach along the ground for a view edge that looks above the horizon.
        private const float Horizon = 10000f;

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

        [Header("Close-up")]
        [Tooltip("How many times nearer the camera stands than the distance that fits the whole arena: the cars read that " +
            "much bigger, and the camera follows the player. 1 frames the whole arena and stays over its middle")]
        [Min(1f)]
        [SerializeField]
        private float closeUp = 1.5f;

        [Tooltip("Metres of the void past the framed floor's rim the camera may show as it follows the player to the edge")]
        [Min(0f)]
        [SerializeField]
        private float rimView = 1f;

        [Tooltip("Share of the way from the player's car to the arena's middle the camera aims at: more shows more of the " +
            "arena, and of the cars in it, around a player near the edge")]
        [Range(0f, 1f)]
        [SerializeField]
        private float centrePull = 0.3f;

        [Tooltip("Seconds ahead along the player's velocity the camera aims, so it shows more of where the car is going")]
        [Min(0f)]
        [SerializeField]
        private float lead = 0.25f;

        [Tooltip("Farthest the camera aims ahead of the player's car, in metres")]
        [Min(0f)]
        [SerializeField]
        private float maxLead = 3f;

        [Tooltip("Share of the screen's height at the top the followed floor's far rim stays below: the HUD's bar and the " +
            "names over the cars cover it")]
        [Range(0f, 0.4f)]
        [SerializeField]
        private float topInset = 0.12f;

        [Tooltip("Seconds the camera takes, about, to follow the player's car")]
        [Min(0.01f)]
        [SerializeField]
        private float panTime = 0.35f;

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

        private ArenaMap map;
        private GameTuning.ArenaSettings settings;
        private IArenaEdge edge;
        private ArenaRound round;
        private Camera view;
        private int fittedWidth;
        private int fittedHeight;
        // The distance along the view that fits an arena of radius 1; every distance in the fit of a circle grows with
        // its radius.
        private float unitDistance;
        // The distance from the camera's aim along its view now, easing to the target as the floor shrinks; 0 until the
        // first frame.
        private float distance;
        private float zoomSpeed;
        // The point on the floor the camera looks at now, easing after the player, and the one it last aimed at.
        private Vector3 focus;
        private Vector3 focusSpeed;
        private Vector3 aim;
        private bool hasFocus;
        // How hard the camera shakes now, 0 to 1; the offset grows with its square, so small shakes stay small.
        private float shake;
        private Vector3 shakeOffset;
        private float shakeSeed;
        private bool hasFog;

        // On a floor that is not a circle the fit is done on its real outline: the whole floor's, kept from the start, and
        // the solid floor's, fitted again each time a wave falls. A circle keeps the fit on a unit radius.
        private Vector3[] fullOutline;
        private float fullDistance;
        private float solidDistance;
        // The outlines' bounds across the view (x) and along the ground away from the camera (y), for the follow.
        private Rect fullBounds;
        private Rect solidBounds;
        private bool isRefitNeeded;

        public Camera Camera => view;

        [Inject]
        private void Construct(ArenaMap map, IArenaEdge edge, ArenaRound round)
        {
            this.map = map;
            this.settings = map.Settings;
            this.edge = edge;
            this.round = round;
        }

        private void Awake()
        {
            view = GetComponent<Camera>();
            if (map.HasTheme) {
                view.backgroundColor = map.Theme.Background;
            }

            hasFog = map.HasTheme && map.Theme.GroundDepth > 0f;
            ApplyFog();

            shakeSeed = Random.Range(0f, 100f);
            edge.Changed += OnEdgeChanged;
        }

        private void OnDestroy()
        {
            if (edge != null) {
                edge.Changed -= OnEdgeChanged;
            }
        }

        private void OnEdgeChanged()
        {
            isRefitNeeded = true;
        }

        private void OnEnable()
        {
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void LateUpdate()
        {
            var outline = edge.Outline(true);
            float target;
            float whole;
            Rect bounds;
            if (outline == null) {
                if (view.pixelWidth != fittedWidth || view.pixelHeight != fittedHeight) {
                    fittedWidth = view.pixelWidth;
                    fittedHeight = view.pixelHeight;
                    unitDistance = FitDistance(1f);
                }

                var radius = Mathf.Lerp(edge.FullExtent, edge.SolidExtent, zoomShare) + settings.ViewMargin;
                target = unitDistance * radius;
                whole = unitDistance * (edge.FullExtent + settings.ViewMargin);
                bounds = Rect.MinMaxRect(-radius, -radius, radius, radius);
            }
            else {
                if (fullOutline == null) {
                    fullOutline = new Vector3[outline.Count];
                    for (var i = 0; i < fullOutline.Length; i++) {
                        fullOutline[i] = outline[i];
                    }

                    fullBounds = BoundsOf(fullOutline);
                    isRefitNeeded = true;
                }

                if (isRefitNeeded) {
                    solidBounds = BoundsOf(outline);
                }

                if (view.pixelWidth != fittedWidth || view.pixelHeight != fittedHeight || isRefitNeeded) {
                    fittedWidth = view.pixelWidth;
                    fittedHeight = view.pixelHeight;
                    isRefitNeeded = false;
                    fullDistance = FitDistance(fullOutline);
                    solidDistance = FitDistance(outline);
                }

                // The margin is metres of floor; it grows the fit by the share it is of the floor's extent.
                var extent = Mathf.Max(1f, Mathf.Lerp(edge.FullExtent, edge.SolidExtent, zoomShare));
                target = Mathf.Lerp(fullDistance, solidDistance, zoomShare) * (1f + settings.ViewMargin / extent);
                whole = fullDistance * (1f + settings.ViewMargin / Mathf.Max(1f, edge.FullExtent));
                bounds = Lerp(fullBounds, solidBounds, zoomShare, settings.ViewMargin);
            }

            // Nearer than the whole arena's fit by the close-up, until what is left of the floor fits nearer still.
            target = Mathf.Min(target, whole / closeUp);
            distance = distance > 0f
                ? Mathf.SmoothDamp(distance, target, ref zoomSpeed, zoomTime)
                : target;

            var focusTarget = Clamp(Aim(), Grow(bounds, rimView));
            if (hasFocus) {
                focus = Vector3.SmoothDamp(focus, focusTarget, ref focusSpeed, panTime);
            }
            else {
                focus = focusTarget;
                hasFocus = true;
            }

            var forward = transform.forward;
            transform.position = focus - forward * distance + Shaken();
            if (hasFog) {
                // Fog counts from the camera, and the camera's distance to the arena changes with the screen's shape, the
                // zoom and the follow, so it is anchored to the arena's middle: the arena itself stays clear whatever the
                // view.
                var middle = distance - Vector3.Dot(focus, forward);
                RenderSettings.fogStartDistance = middle + map.Theme.FogStart;
                RenderSettings.fogEndDistance = middle + map.Theme.FogEnd;
            }
        }

        /// <summary>
        /// The point on the floor the camera would look at: a little ahead of the player's car and pulled towards the
        /// arena's middle while the car is in play in a begun round, before GO where the car keeps its spot on the screen
        /// of the whole arena's view, the middle before the cars are placed, else the last one.
        /// </summary>
        private Vector3 Aim()
        {
            var player = round?.Player;
            if (closeUp <= 1f || player == null || !round.IsPlaced) {
                // The menu's arena has no cars yet: the middle.
                aim = Vector3.zero;
            }
            else if (!round.HasBegun) {
                // Until GO the player's car stands about where it stood on the screen with the whole arena in view, a third
                // of the way from its spot towards the middle at a close-up of 1.5: the start screen's HUD (TAP TO START,
                // the ghost stick, the countdown) is laid out around that spot and would cover the car anywhere else.
                var point = player.Position * (1f - 1f / closeUp);
                aim = new Vector3(point.x, 0f, point.z);
            }
            else if (!player.IsOut) {
                var ahead = Vector3.ClampMagnitude(player.Movement.Velocity * lead, maxLead);
                var point = Vector3.Lerp(player.Position + ahead, Vector3.zero, centrePull);
                aim = new Vector3(point.x, 0f, point.z);
            }

            return aim;
        }

        /// <summary>
        /// Moves <paramref name="point"/> as little as it takes for the view from it at the present distance to stay
        /// inside <paramref name="bounds"/> across and along the ground; where the view is wider than the bounds, as
        /// little as it takes for the bounds to stay inside the view.
        /// </summary>
        private Vector3 Clamp(Vector3 point, Rect bounds)
        {
            GroundAxes(out var right, out var ahead);
            var tanVertical = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var halfWidth = distance * tanVertical * view.aspect;
            var near = Reach(-tanVertical, ahead);
            // The far rim stops below the HUD at the top, not at the screen's edge, where it would hide a car on it.
            var far = Reach(tanVertical * (1f - 2f * topInset), ahead);

            var x = ClampSpan(Vector3.Dot(point, right), bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            var y = ClampSpan(Vector3.Dot(point, ahead), bounds.yMin - near, bounds.yMax - far);
            return right * x + ahead * y;
        }

        private static float ClampSpan(float value, float low, float high)
        {
            return low <= high ? Mathf.Clamp(value, low, high) : Mathf.Clamp(value, high, low);
        }

        /// <summary>
        /// How far along the ground away from the camera the middle of the view's top (positive tangent) or bottom edge
        /// lands from the point the camera looks at, at the present distance.
        /// </summary>
        private float Reach(float tangent, Vector3 ahead)
        {
            var direction = transform.forward + transform.up * tangent;
            if (direction.y >= -0.0001f) {
                return tangent > 0f ? Horizon : -Horizon;
            }

            var origin = -transform.forward * distance;
            var hit = origin + direction * (-origin.y / direction.y);
            return Vector3.Dot(hit, ahead);
        }

        private void GroundAxes(out Vector3 right, out Vector3 ahead)
        {
            ahead = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, ahead);
        }

        private Rect BoundsOf(IReadOnlyList<Vector3> points)
        {
            GroundAxes(out var right, out var ahead);
            var bounds = Rect.MinMaxRect(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            for (var i = 0; i < points.Count; i++) {
                var x = Vector3.Dot(points[i], right);
                var y = Vector3.Dot(points[i], ahead);
                bounds.xMin = Mathf.Min(bounds.xMin, x);
                bounds.xMax = Mathf.Max(bounds.xMax, x);
                bounds.yMin = Mathf.Min(bounds.yMin, y);
                bounds.yMax = Mathf.Max(bounds.yMax, y);
            }

            return bounds;
        }

        private static Rect Lerp(Rect from, Rect to, float share, float margin)
        {
            return Grow(Rect.MinMaxRect(
                Mathf.Lerp(from.xMin, to.xMin, share), Mathf.Lerp(from.yMin, to.yMin, share),
                Mathf.Lerp(from.xMax, to.xMax, share), Mathf.Lerp(from.yMax, to.yMax, share)), margin);
        }

        private static Rect Grow(Rect rect, float margin)
        {
            return Rect.MinMaxRect(rect.xMin - margin, rect.yMin - margin, rect.xMax + margin, rect.yMax + margin);
        }

        // Render settings belong to the active scene, and the scene that loads this one becomes active only after it has
        // loaded: the fog set on waking would be lost to the scene's own settings, so it is set again then.
        private void OnActiveSceneChanged(Scene previous, Scene next)
        {
            ApplyFog();
        }

        /// <summary>
        /// The fog is the background colour, so the world far below melts into the sky with no horizon. An arena without a
        /// world switches it off, because the scene's own settings may have it on.
        /// </summary>
        private void ApplyFog()
        {
            RenderSettings.fog = hasFog;
            if (hasFog) {
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = map.Theme.Background;
            }
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
            if (Ground.Raycast(ray, out var hit)) {
                point = ray.GetPoint(hit);
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
        private float FitDistance(IReadOnlyList<Vector3> points)
        {
            var rotation = transform.rotation;
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var tanVertical = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanHorizontal = tanVertical * view.aspect;

            var fit = 0f;
            for (var i = 0; i < points.Count; i++) {
                var depth = Vector3.Dot(points[i], forward);
                fit = Mathf.Max(fit,
                    Mathf.Abs(Vector3.Dot(points[i], right)) / tanHorizontal - depth,
                    Mathf.Abs(Vector3.Dot(points[i], up)) / tanVertical - depth);
            }

            return fit;
        }

        private float FitDistance(float radius)
        {
            var rotation = transform.rotation;
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var tanVertical = Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var tanHorizontal = tanVertical * view.aspect;

            var fit = 0f;
            for (var i = 0; i < RimSamples; i++) {
                var angle = i * Mathf.PI * 2f / RimSamples;
                var point = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                // From the camera at the given distance, the point is this deep: its depth from the centre plus it.
                var depth = Vector3.Dot(point, forward);
                fit = Mathf.Max(fit,
                    Mathf.Abs(Vector3.Dot(point, right)) / tanHorizontal - depth,
                    Mathf.Abs(Vector3.Dot(point, up)) / tanVertical - depth);
            }

            return fit;
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
