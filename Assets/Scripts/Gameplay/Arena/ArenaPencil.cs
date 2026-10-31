using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Pencil mechanic of the tabletop's notebook variant: from <see cref="GameTuning.PencilSettings.Delay"/> seconds in,
    /// and every <see cref="GameTuning.PencilSettings.Interval"/> after, a long yellow pencil is announced and rolls across the
    /// page like a rolling pin. It lies across a line of the floor (its length is a share of that line, at a random offset,
    /// so there is a clear end to step round) and rolls perpendicular to its length. For
    /// <see cref="GameTuning.PencilSettings.Warning"/> seconds a dashed band shows on the floor, the area it will sweep, and
    /// the pencil wobbles at the near edge; then it rolls the whole way across and off the far side. Each car in the band is
    /// knocked along the way it rolls, once per pass, by nobody (<see cref="HazardBlow"/>). The bots leave the band by its
    /// nearer end (<see cref="HazardBand"/>). The lines and offsets come from the bots' seed.
    /// </summary>
    public sealed class ArenaPencil : ArenaObject, IArenaHazard
    {
        private enum Stage
        {
            Idle,
            Warning,
            Rolling,
        }

        // Metres the pencil stops beyond the far edge, and how far inside the near edge it lies while it is announced, so it
        // shows from the camera.
        private const float EdgeSlack = 1.2f;
        private const float StartInside = 0.5f;
        // Half a car's length as the pencil sees it, in metres, on top of its radius.
        private const float CarReach = 0.9f;
        // How far past its end a car is still caught, in metres.
        private const float EndReach = 0.4f;
        private const float DecalHeight = 0.012f;
        private const float DashSpacing = 1.3f;
        private const float DashLength = 0.75f;
        private const float DashWidth = 0.17f;
        private const float ChevronSpacing = 3.2f;

        private readonly GameTuning.PencilSettings settings;
        private readonly GameTuning tuning;
        private readonly MechanicArt art;
        private readonly RoundDifficulty difficulty;
        private readonly ArenaCamera arenaCamera;
        private readonly IAudioService audioService;
        private readonly List<Car> hitCars = new();
        private readonly List<Vector3> dashVertices = new();
        private readonly List<int> dashTriangles = new();
        private readonly int colorId = Shader.PropertyToID("_BaseColor");
        private readonly Color bandDim = new(1f, 0.85f, 0.2f, 0.16f);
        private readonly Color bandBright = new(1f, 0.85f, 0.2f, 0.4f);

        private HazardBlow blow;
        private System.Random random;
        private Transform pencil;
        private Transform roll;
        private Transform shadow;
        private Transform band;
        private Transform bandQuad;
        private MeshRenderer bandRenderer;
        private MaterialPropertyBlock bandBlock;
        private Mesh dashMesh;
        private Transform body;
        private Transform eraser;
        private Transform ferrule;
        private Transform cone;
        private Transform lead;

        private Stage stage;
        private float stageTime;
        private float nextLaunch = -1f;
        private Vector3 along = Vector3.forward;
        private Vector3 axis = Vector3.right;
        private float centerAxis;
        private float halfLength;
        private float travel;
        private float travelStart;
        private float travelEnd;
        private float angle;
        private int version;

        public ArenaPencil(GameTuning tuning, ArenaMap map, ArenaRound round, IArenaEdge edge, ArenaHazards hazards,
            MechanicArt art, RoundDifficulty difficulty, ArenaCamera arenaCamera, IAudioService audioService, Transform parent)
            : base(GameTuning.ArenaMechanic.Pencil, map, round, edge, hazards, parent)
        {
            this.tuning = tuning;
            this.settings = tuning.Pencil;
            this.art = art;
            this.difficulty = difficulty;
            this.arenaCamera = arenaCamera;
            this.audioService = audioService;
        }

        public int Version => version;

        /// <summary>Whether a pencil is announced or rolling.</summary>
        public bool IsActive => stage != Stage.Idle;

        public bool IsRolling => stage == Stage.Rolling;

        /// <summary>The way the pencil rolls, flat and of length 1.</summary>
        public Vector3 Direction => along;

        /// <summary>The pencil's length axis, flat and of length 1; the band lies along it.</summary>
        public Vector3 Axis => axis;

        /// <summary>The middle of the pencil, flat.</summary>
        public Vector3 Center => axis * centerAxis + along * travel;

        public float HalfLength => halfLength;

        /// <summary>Where along its length axis the pencil's middle lies.</summary>
        public float CenterAxis => centerAxis;

        /// <summary>The distances along the way it rolls, from the middle line's foot, where the pencil starts and stops.</summary>
        public float TravelStart => travelStart;

        public float TravelEnd => travelEnd;

        public bool TryAvoid(Vector3 position, float margin, IArenaEdge edge, float edgeMargin, out Vector3 heading)
        {
            heading = Vector3.zero;
            if (stage == Stage.Idle) {
                return false;
            }

            return HazardBand.TryAvoid(position, Center, along, axis, halfLength, margin,
                -(settings.Radius + CarReach + 0.3f), float.MaxValue, edge, edgeMargin, out heading);
        }

        public override void Dispose()
        {
            Hazards.Remove(this);
            base.Dispose();
        }

        protected override void Build()
        {
            random = new System.Random(Hazards.Seed ^ 0x2C4B);
            blow = new HazardBlow(tuning, difficulty, arenaCamera, audioService);
            Hazards.Add(this);
            BuildLook();
        }

        protected override void Step(float deltaTime)
        {
            if (stage == Stage.Idle) {
                TryLaunch();
                return;
            }

            stageTime += deltaTime;
            if (stage == Stage.Warning) {
                TickWarning();
            }
            else {
                TickRolling(deltaTime);
            }
        }

        private void TryLaunch()
        {
            if (!IsClockRunning) {
                return;
            }

            if (nextLaunch < 0f) {
                nextLaunch = settings.Delay;
            }

            if (Round.Elapsed >= nextLaunch) {
                Launch();
            }
        }

        /// <summary>
        /// Picks the line, the pencil's length and offset and the way it rolls, and shows the band and the pencil at the near
        /// edge.
        /// </summary>
        private void Launch()
        {
            var turn = (float)random.NextDouble() * Mathf.PI * 2f;
            along = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
            axis = new Vector3(-along.z, 0f, along.x);

            // The pencil lies along its line of the floor: a share of its width, with its middle off the line's middle by a
            // random amount, never so far that an end hangs over the edge.
            Edge.SolidChord(Vector3.zero, axis, out var behind, out var ahead);
            var width = behind + ahead;
            var mid = (ahead - behind) * 0.5f;
            halfLength = Mathf.Max(1f, width * settings.LengthShare * 0.5f);
            var slack = Mathf.Max(0f, width * 0.5f - halfLength);
            var offset = Mathf.Clamp(((float)random.NextDouble() * 2f - 1f) * settings.MaxOffset * width, -slack, slack);
            centerAxis = mid + offset;

            // It rolls from just beyond the near edge of the floor along its middle line to just beyond the far one.
            Edge.SolidChord(axis * centerAxis, along, out var near, out var far);
            travelStart = -near + StartInside;
            travelEnd = far + EdgeSlack;
            travel = travelStart;
            angle = 0f;
            stage = Stage.Warning;
            stageTime = 0f;
            hitCars.Clear();
            version++;
            nextLaunch = Round.Elapsed + Mathf.Max(settings.Warning + 3f,
                settings.Interval + ((float)random.NextDouble() * 2f - 1f) * settings.Jitter);

            ShowBand();
            PlacePencil(0f);
            pencil.gameObject.SetActive(true);
            shadow.gameObject.SetActive(true);
            band.gameObject.SetActive(true);
        }

        private void TickWarning()
        {
            PulseBand(0.5f + 0.5f * Mathf.Sin(stageTime * Mathf.PI * 2f / 0.35f));
            // It rocks on the spot and hops, as a pin held back.
            var rock = Mathf.Sin(stageTime * 22f) * 0.12f;
            PlacePencil(rock, Mathf.Abs(Mathf.Sin(stageTime * 11f)) * 0.18f);
            if (stageTime >= settings.Warning) {
                stage = Stage.Rolling;
                stageTime = 0f;
                if (audioService != null) {
                    audioService.Play(audioService.Library.HitLight, 0.35f);
                }
            }
        }

        private void TickRolling(float deltaTime)
        {
            travel += settings.Speed * deltaTime;
            angle += settings.Speed * deltaTime / settings.Radius * Mathf.Rad2Deg;
            PlacePencil(0f);
            PulseBand(1f);
            if (IsClockRunning) {
                HitCars();
            }

            if (travel >= travelEnd) {
                Finish();
            }
        }

        private void Finish()
        {
            stage = Stage.Idle;
            pencil.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            band.gameObject.SetActive(false);
        }

        private void HitCars()
        {
            var center = Center;
            var reachAlong = settings.Radius + CarReach;
            var reachAxis = halfLength + EndReach;
            for (var i = 0; i < Round.Cars.Count; i++) {
                var car = Round.Cars[i];
                if (car.IsOut || car.Position.y > settings.HitHeight || hitCars.Contains(car)) {
                    continue;
                }

                var offset = Flat(car.Position) - center;
                if (Mathf.Abs(Vector3.Dot(offset, along)) > reachAlong || Mathf.Abs(Vector3.Dot(offset, axis)) > reachAxis) {
                    continue;
                }

                hitCars.Add(car);
                blow.Apply(car, along, settings.Push, 0f, settings.Stagger, settings.EasyPlayerShare);
            }
        }

        // ---- Look. ----

        private void PlacePencil(float rock, float hop = 0f)
        {
            var center = Center + along * rock;
            pencil.position = new Vector3(center.x, settings.Radius + hop, center.z);
            pencil.rotation = Quaternion.LookRotation(axis, Vector3.up);
            // Rolling along the way it travels is a turn about the flat axis across that way.
            roll.localRotation = Quaternion.Euler(0f, 0f, -angle + rock * 90f);
            shadow.position = new Vector3(center.x, 0.02f, center.z);
            shadow.rotation = Quaternion.LookRotation(axis, Vector3.up);
            shadow.localScale = new Vector3(settings.Radius * 2.8f, 1f, halfLength * 2f);
        }

        private void PulseBand(float mix)
        {
            // Brighter while announced and pulsing, steadier once the pencil rolls.
            bandBlock.SetColor(colorId, Color.Lerp(bandDim, bandBright, mix));
            bandRenderer.SetPropertyBlock(bandBlock);
        }

        /// <summary>
        /// Lays the band on the floor between where the pencil starts and stops: a translucent strip with a dashed line down
        /// each side and chevrons along the middle pointing the way it rolls.
        /// </summary>
        private void ShowBand()
        {
            var length = travelEnd - travelStart;
            var middle = axis * centerAxis + along * ((travelStart + travelEnd) * 0.5f);
            band.SetPositionAndRotation(middle + Vector3.up * DecalHeight, Quaternion.LookRotation(along, Vector3.up));
            bandQuad.localScale = new Vector3(halfLength * 2f, 1f, length);

            dashVertices.Clear();
            dashTriangles.Clear();
            // The band's own frame: +X across (the pencil's length), +Z the way it rolls.
            var count = Mathf.Max(1, Mathf.FloorToInt(length / DashSpacing));
            for (var i = 0; i < count; i++) {
                var z = -length * 0.5f + DashSpacing * (i + 0.5f) + (length - count * DashSpacing) * 0.5f;
                AddQuad(halfLength - DashWidth, z - DashLength * 0.5f, halfLength, z + DashLength * 0.5f);
                AddQuad(-halfLength, z - DashLength * 0.5f, -halfLength + DashWidth, z + DashLength * 0.5f);
            }

            var chevrons = Mathf.Max(1, Mathf.FloorToInt((length - 1f) / ChevronSpacing));
            for (var i = 0; i < chevrons; i++) {
                var z = -length * 0.5f + ChevronSpacing * (i + 0.5f) + (length - chevrons * ChevronSpacing) * 0.5f;
                AddChevron(z);
            }

            dashMesh.Clear();
            dashMesh.SetVertices(dashVertices);
            dashMesh.SetTriangles(dashTriangles, 0);
            dashMesh.RecalculateBounds();
            PulseBand(0.5f);

            // The pencil's parts, laid out along its length.
            var z0 = -halfLength;
            var bodyLength = Mathf.Max(0.5f, halfLength * 2f - 2f);
            Place(eraser, z0 + 0.25f, 0.5f);
            Place(ferrule, z0 + 0.7f, 0.4f);
            Place(body, z0 + 0.9f + bodyLength * 0.5f, bodyLength);
            Place(cone, z0 + 0.9f + bodyLength + 0.55f, 1.1f);
            Place(lead, z0 + 0.9f + bodyLength + 1.1f + 0.1f, 0.3f);
        }

        private static void Place(Transform part, float z, float length)
        {
            part.localPosition = new Vector3(0f, 0f, z);
            // The part's own length is its Y, turned to Z by its rotation.
            part.localScale = new Vector3(1f, length, 1f);
        }

        private void AddChevron(float z)
        {
            const float Width = 0.75f;
            const float HalfLength = 0.55f;
            const float Thick = 0.4f;
            var tip = new Vector3(0f, 0f, z + HalfLength);
            var right = new Vector3(Width, 0f, z - HalfLength);
            var left = new Vector3(-Width, 0f, z - HalfLength);
            var drop = new Vector3(0f, 0f, -Thick);
            AddTri(tip, right, right + drop, tip + drop);
            AddTri(left, tip, tip + drop, left + drop);
        }

        private void AddQuad(float x0, float z0, float x1, float z1)
        {
            AddTri(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0));
        }

        // Four corners, clockwise from above, as two triangles.
        private void AddTri(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = dashVertices.Count;
            dashVertices.Add(a);
            dashVertices.Add(b);
            dashVertices.Add(c);
            dashVertices.Add(d);
            dashTriangles.Add(start); dashTriangles.Add(start + 1); dashTriangles.Add(start + 2);
            dashTriangles.Add(start); dashTriangles.Add(start + 2); dashTriangles.Add(start + 3);
        }

        private void BuildLook()
        {
            var radius = settings.Radius;
            pencil = new GameObject("Pencil").transform;
            pencil.SetParent(Root, false);
            roll = new GameObject("Roll").transform;
            roll.SetParent(pencil, false);

            // Each part is a unit-length cylinder standing on Y, turned to lie along Z and stretched to length when laid out.
            var lie = new Vector3(90f, 0f, 0f);
            body = ToyMeshes.Part(roll, "Body", Own(ToyMeshes.Cylinder(radius, radius, 1f, 6)), art.Get("pencil_body"), Vector3.zero, lie);
            eraser = ToyMeshes.Part(roll, "Eraser", Own(ToyMeshes.Cylinder(radius * 0.95f, radius * 0.95f, 1f, 12)), art.Get("pencil_eraser"), Vector3.zero, lie);
            ferrule = ToyMeshes.Part(roll, "Ferrule", Own(ToyMeshes.Cylinder(radius * 1.04f, radius * 1.04f, 1f, 12)), art.Get("pencil_metal"), Vector3.zero, lie);
            cone = ToyMeshes.Part(roll, "Wood", Own(ToyMeshes.Cylinder(radius, radius * 0.28f, 1f, 6)), art.Get("pencil_wood"), Vector3.zero, lie);
            lead = ToyMeshes.Part(roll, "Lead", Own(ToyMeshes.Cylinder(radius * 0.28f, 0f, 1f, 6)), art.Get("pencil_lead"), Vector3.zero, lie);
            ToyMeshes.Outlined(pencil.gameObject);

            shadow = ToyMeshes.Part(Root, "Shadow", Own(ToyMeshes.Quad()), art.Get("pencil_shadow"), Vector3.zero);
            shadow.GetComponent<MeshRenderer>().sortingOrder = -2;

            bandBlock = new MaterialPropertyBlock();
            band = new GameObject("Band").transform;
            band.SetParent(Root, false);
            bandRenderer = ToyMeshes.Decal(band, "Strip", Own(ToyMeshes.Quad()), art.Get("pencil_band"), -4);
            bandQuad = bandRenderer.transform;
            dashMesh = Own(new Mesh { name = "PencilDashes" });
            dashMesh.MarkDynamic();
            ToyMeshes.Decal(band, "Dashes", dashMesh, art.Get("pencil_dash"), -3);

            pencil.gameObject.SetActive(false);
            shadow.gameObject.SetActive(false);
            band.gameObject.SetActive(false);
        }
    }
}
