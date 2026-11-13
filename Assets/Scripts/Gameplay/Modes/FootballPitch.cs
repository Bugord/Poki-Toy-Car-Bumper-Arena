using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Round;
using ToyCarArena.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// What FOOTBALL builds on its pitch: the two goals (posts and crossbar in the colour of the team that defends them,
    /// white side nets and roof, all solid), the bumpers that run from each post to the touchline so no corner of the
    /// pitch can trap the ball, and the ball. The fence of the mode (<see cref="ArenaFence"/>) is the back of each goal.
    /// Also answers where the goals are and whether a point is on the part of the pitch play happens on.
    /// <para>
    /// Blue defends the goal at the near end (−Z, towards the camera) and attacks away from it; red the other way round.
    /// </para>
    /// </summary>
    public sealed class FootballPitch : IDisposable
    {
        /// <summary>The name of the ball's look, which the mode card copies onto its car.</summary>
        public const string BallLookName = "Football Look";

        private const float PostRadius = 0.2f;
        private const float NetThickness = 0.14f;
        private const float BumperThickness = 0.45f;
        private const float BumperHeight = 0.6f;
        private const float BumperWall = 4f;
        private const float StripeLength = 1.5f;

        /// <summary>The touchline point each bumper runs to, from the corner of the pitch along Z, in metres.</summary>
        private const float BumperReach = 5.5f;

        private readonly GameTuning.FootballSettings settings;
        private readonly MechanicArt art;
        private readonly List<Mesh> meshes = new();
        private PhysicsMaterial wallPhysics;
        private PhysicsMaterial ballPhysics;

        public FootballPitch(GameTuning.FootballSettings settings, MechanicArt art)
        {
            this.settings = settings;
            this.art = art;
        }

        public Transform Root { get; private set; }

        public FootballBall Ball { get; private set; }

        /// <summary>Where each bumper meets its post (x &gt; 0, z &gt; 0; the other three are mirrored).</summary>
        public Vector2 BumperPost => new(settings.GoalHalfWidth + PostRadius, settings.GoalLine);

        /// <summary>Where each bumper meets the touchline (x &gt; 0, z &gt; 0; the other three are mirrored).</summary>
        public Vector2 BumperTouch => new(settings.PitchHalfSize.x, settings.PitchHalfSize.y - BumperReach);

        /// <summary>The sign of Z at the end <paramref name="team"/> defends: blue the near end, red the far one.</summary>
        public static float EndOf(int team) => team == RoundTeams.Red ? 1f : -1f;

        /// <summary>The middle of the goal line <paramref name="team"/> defends.</summary>
        public Vector3 GoalOf(int team) => new(0f, 0f, EndOf(team) * settings.GoalLine);

        /// <summary>The middle of the goal line <paramref name="team"/> attacks.</summary>
        public Vector3 TargetOf(int team) => GoalOf(team == RoundTeams.Red ? RoundTeams.Blue : RoundTeams.Red);

        /// <summary>
        /// Whether a point is on the part of the pitch play happens on, at least <paramref name="margin"/> in from its
        /// walls: inside the touchlines and the ends, in front of the bumpers, or inside a goal.
        /// </summary>
        public bool IsInPlay(Vector3 point, float margin)
        {
            var x = Mathf.Abs(point.x);
            var z = Mathf.Abs(point.z);
            var half = settings.PitchHalfSize;
            if (x > half.x - margin || z > half.y - margin) {
                return false;
            }

            if (x < settings.GoalHalfWidth) {
                return true;
            }

            // In front of the bumper: on the middle's side of the line from the post to the touchline.
            var post = BumperPost;
            var touch = BumperTouch;
            var along = (touch - post).normalized;
            var normal = new Vector2(along.y, -along.x);
            if (Vector2.Dot(normal, Vector2.zero - post) < 0f) {
                normal = -normal;
            }

            return Vector2.Dot(normal, new Vector2(x, z) - post) >= margin;
        }

        public void Build(Transform parent)
        {
            Root = new GameObject("FootballPitch").transform;
            Root.SetParent(parent, false);
            wallPhysics = new PhysicsMaterial("Football Wall") {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Average,
            };
            ballPhysics = new PhysicsMaterial("Football") {
                bounciness = settings.BallBounce,
                dynamicFriction = 0.4f,
                staticFriction = 0.4f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Average,
            };

            BuildGoal(RoundTeams.Blue);
            BuildGoal(RoundTeams.Red);
            foreach (var sx in new[] { 1f, -1f }) {
                foreach (var sz in new[] { 1f, -1f }) {
                    BuildBumper(sx, sz);
                }
            }

            Outline.Apply(Root.gameObject);
            BuildBall();
        }

        /// <summary>Puts the ball on the centre spot, still, and holds it there until <see cref="Release"/>.</summary>
        public void Hold()
        {
            var body = Ball.Body;
            body.isKinematic = true;
            body.position = Vector3.up * settings.BallRadius;
            body.rotation = Quaternion.identity;
            Ball.transform.SetPositionAndRotation(body.position, body.rotation);
            Ball.Forget();
        }

        /// <summary>Lets the ball go.</summary>
        public void Release()
        {
            var body = Ball.Body;
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        public void Dispose()
        {
            foreach (var mesh in meshes) {
                if (mesh != null) {
                    Object.Destroy(mesh);
                }
            }

            meshes.Clear();
            if (wallPhysics != null) {
                Object.Destroy(wallPhysics);
            }

            if (ballPhysics != null) {
                Object.Destroy(ballPhysics);
            }
        }

        private void BuildGoal(int team)
        {
            var end = EndOf(team);
            var goal = new GameObject(team == RoundTeams.Red ? "Red Goal" : "Blue Goal").transform;
            goal.SetParent(Root, false);
            var frame = art.Get(team == RoundTeams.Red ? "train_red" : "train_blue");
            var net = art.Get("train_white");
            var width = settings.GoalHalfWidth;
            var height = settings.GoalHeight;
            var line = settings.GoalLine;
            var back = settings.PitchHalfSize.y;
            var depth = back - line;
            var middle = end * (line + depth * 0.5f);

            var post = Own(ToyMeshes.Cylinder(PostRadius, PostRadius, height, 12));
            var side = Own(ToyMeshes.Box(new Vector3(NetThickness, height, depth)));
            var bar = Own(ToyMeshes.Box(new Vector3((width + PostRadius) * 2f, PostRadius * 2f, PostRadius * 2f)));
            var roof = Own(ToyMeshes.Box(new Vector3(width * 2f, NetThickness, depth)));
            foreach (var sx in new[] { 1f, -1f }) {
                var x = sx * (width + PostRadius);
                ToyMeshes.Part(goal, "Post", post, frame, new Vector3(x, height * 0.5f, end * line));
                ToyMeshes.Part(goal, "Side Net", side, net, new Vector3(x, height * 0.5f, middle));
                Solid(goal, new Vector3(x, height * 0.5f, middle), new Vector3(PostRadius * 2f, height, depth));
            }

            ToyMeshes.Part(goal, "Crossbar", bar, frame, new Vector3(0f, height, end * line));
            ToyMeshes.Part(goal, "Roof Net", roof, net, new Vector3(0f, height, middle));
            // The roof solid as deep as the bar: a ball that bounces on top of the goal rolls off it.
            Solid(goal, new Vector3(0f, height + PostRadius, middle),
                new Vector3((width + PostRadius) * 2f, PostRadius * 2f, depth));
        }

        private void BuildBumper(float sx, float sz)
        {
            var post = BumperPost;
            var touch = BumperTouch;
            var a = new Vector3(sx * post.x, 0f, sz * post.y);
            var b = new Vector3(sx * touch.x, 0f, sz * touch.y);
            var along = b - a;
            var length = along.magnitude;
            var rotation = Quaternion.LookRotation(along / length);
            // Pushed back by half its thickness, so its face is on the line from the post to the touchline.
            var away = Vector3.Cross(Vector3.up, along / length);
            if (Vector3.Dot(away, a) < 0f) {
                away = -away;
            }

            var bumper = new GameObject("Bumper").transform;
            bumper.SetParent(Root, false);
            bumper.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var team = sz > 0f ? RoundTeams.Red : RoundTeams.Blue;
            var colour = art.Get(team == RoundTeams.Red ? "train_red" : "train_blue");
            var white = art.Get("train_white");
            var stripes = Mathf.Max(1, Mathf.RoundToInt(length / StripeLength));
            var piece = Own(ToyMeshes.Box(new Vector3(BumperThickness, BumperHeight, length / stripes)));
            for (var i = 0; i < stripes; i++) {
                var centre = Vector3.Lerp(a, b, (i + 0.5f) / stripes) + away * (BumperThickness * 0.5f);
                ToyMeshes.Part(bumper, "Stripe", piece, i % 2 == 0 ? colour : white,
                    centre + Vector3.up * (BumperHeight * 0.5f), rotation.eulerAngles);
            }

            // The wall runs on into the fence, so the ball cannot slip between them.
            var wall = Solid(bumper, (a + b) * 0.5f + along / length * 0.5f + away * (BumperThickness * 0.5f)
                + Vector3.up * (BumperWall * 0.5f - 0.5f), new Vector3(BumperThickness, BumperWall, length + 1f));
            wall.transform.rotation = rotation;
        }

        private void BuildBall()
        {
            var radius = settings.BallRadius;
            var ball = new GameObject("Football");
            ball.transform.SetParent(Root, false);
            ball.transform.localPosition = Vector3.up * radius;
            var body = ball.AddComponent<Rigidbody>();
            body.mass = settings.BallMass;
            body.linearDamping = settings.BallDrag;
            body.angularDamping = 0.6f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.isKinematic = true;
            var collider = ball.AddComponent<SphereCollider>();
            collider.radius = radius;
            collider.sharedMaterial = ballPhysics;

            var look = new GameObject(BallLookName).transform;
            look.SetParent(ball.transform, false);
            var white = art.Get("train_white");
            var dark = art.Get("train_dark");
            ToyMeshes.Part(look, "Leather", Own(ToyMeshes.Ellipsoid(Vector3.one * radius, 24, 14)), white, Vector3.zero);
            // Twelve dark patches at the corners of an icosahedron: the ball reads as a football and its roll shows.
            var patch = Own(ToyMeshes.Ellipsoid(new Vector3(0.34f, 0.34f, 0.1f) * radius, 10, 6));
            var phi = (1f + Mathf.Sqrt(5f)) * 0.5f;
            foreach (var corner in new[] {
                         new Vector3(-1f, phi, 0f), new Vector3(1f, phi, 0f), new Vector3(-1f, -phi, 0f),
                         new Vector3(1f, -phi, 0f), new Vector3(0f, -1f, phi), new Vector3(0f, 1f, phi),
                         new Vector3(0f, -1f, -phi), new Vector3(0f, 1f, -phi), new Vector3(phi, 0f, -1f),
                         new Vector3(phi, 0f, 1f), new Vector3(-phi, 0f, -1f), new Vector3(-phi, 0f, 1f),
                     }) {
                var normal = corner.normalized;
                var part = ToyMeshes.Part(look, "Patch", patch, dark, normal * (radius * 0.97f));
                part.localRotation = Quaternion.LookRotation(normal);
            }

            Outline.Apply(look.gameObject);
            Ball = ball.AddComponent<FootballBall>();
            Ball.Setup(settings, body);
        }

        private GameObject Solid(Transform parent, Vector3 position, Vector3 size)
        {
            var solid = new GameObject("Solid");
            solid.transform.SetParent(parent, false);
            solid.transform.position = position;
            var collider = solid.AddComponent<BoxCollider>();
            collider.size = size;
            collider.sharedMaterial = wallPhysics;
            return solid;
        }

        private Mesh Own(Mesh mesh)
        {
            meshes.Add(mesh);
            return mesh;
        }
    }
}
