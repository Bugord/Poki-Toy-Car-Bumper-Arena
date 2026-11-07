using System;
using System.Collections.Generic;
using ToyCarArena.Audio;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The Jelly mechanic of the Candy arena: a few wobbly jelly pads lie on the chocolate, and a car that drives onto
    /// one is tossed forward and up and lands on the floor again some metres on. Pads are not a hazard (they hit nobody
    /// and the bots do not avoid them): they are a ramp, for the player to use or to be surprised by.
    /// <para>
    /// A toss never throws a car off the floor by design. The flight time follows from the upward speed and gravity, the
    /// landing place from the car's own speed (raised to the pad's range), and when that place is not on the floor with
    /// some margin the forward speed is shortened until it is; if even standing still would land badly the car is tossed
    /// straight up. The pad is a check of position in this class, with no collider, so the steering ray never sees it.
    /// A pad vanishes when the floor under it crumbles. Registered in every scope; does nothing off its variant. In a match
    /// whose cycles turn the variants the pads are laid afresh on the whole floor each cycle that plays them
    /// (<see cref="MechanicRun"/>).
    /// </para>
    /// </summary>
    public sealed class JellyPads : ITickable, IDisposable
    {
        private const int Slices = 40;
        private const float Height = 0.75f;

        // The jelly's side from the foot up to the middle of its top, in shares of the pad's radius and of its height: a
        // slight lip at the foot keeps the outline under it, a wide lower tier, a step and a rounded upper tier, so seen
        // from above it is two rings, a jelly mould's and a target's.
        private static readonly Vector2[] Profile =
        {
            new(0.94f, 0f), new(1f, 0.06f), new(1f, 0.2f), new(0.98f, 0.36f), new(0.93f, 0.47f), new(0.84f, 0.52f),
            new(0.66f, 0.54f), new(0.62f, 0.6f), new(0.6f, 0.78f), new(0.55f, 0.9f), new(0.45f, 0.97f), new(0.28f, 1f),
            new(0f, 1f),
        };

        // A car must be this close to the floor to be driving over a pad, rather than flying over it.
        private const float OnFloor = 0.6f;

        // Shares of the pad's radius within which a car is on it.
        private const float TriggerShare = 0.92f;

        // The floor's edge nearer than this share of the radius to a pad's middle takes the pad away.
        private const float VanishShare = 0.45f;
        private const float VanishSpeed = 4f;

        private sealed class Pad
        {
            public Transform Root;
            public Transform Body;
            public Vector3 Centre;
            public float ReadyAt;
            public float WobbleStart = -100f;
            public float Phase;
            public float Presence = 1f;
            public bool IsGone;
        }

        private readonly GameTuning.JellySettings settings;
        private readonly ArenaRound round;
        private readonly IArenaEdge edge;
        private readonly MechanicArt art;
        private readonly ArenaHazards hazards;
        private readonly IAudioService audioService;
        private readonly Transform parent;
        private readonly List<Pad> pads = new();
        private readonly List<Mesh> meshes = new();
        private readonly MechanicRun run;
        private GameObject root;
        private EffectPlayer effects;
        private float clock;

        public JellyPads(GameTuning tuning, ArenaRound round, IArenaEdge edge, MechanicArt art,
            ArenaHazards hazards, IAudioService audioService, ArenaVariants variants, Transform parent)
        {
            this.run = new MechanicRun(variants, GameTuning.ArenaMechanic.Jelly);
            this.settings = tuning.Jelly;
            this.round = round;
            this.edge = edge;
            this.art = art;
            this.hazards = hazards;
            this.audioService = audioService;
            this.parent = parent;
        }

        /// <summary>Whether the pads are laid out; false off the Jelly variant.</summary>
        public bool Exists => run.IsBuilt;

        /// <summary>How many pads have been laid, whole or not.</summary>
        public int Count => pads.Count;

        /// <summary>The middle of pad <paramref name="index"/> on the floor.</summary>
        public Vector3 CentreOf(int index) => pads[index].Centre;

        /// <summary>Whether pad <paramref name="index"/> is still on the floor, so that it tosses.</summary>
        public bool IsStanding(int index) => !pads[index].IsGone;

        /// <summary>How many cars the pads have tossed this round.</summary>
        public int Tosses { get; private set; }

        public void Tick()
        {
            switch (run.Next(round.IsPlaced, Time.deltaTime)) {
                case MechanicStep.Idle:
                    return;
                case MechanicStep.Leave:
                    run.Shrink(root != null ? root.transform : null);
                    return;
                case MechanicStep.Clear:
                    Clear();
                    return;
                case MechanicStep.Start:
                    Clear();
                    Build();
                    break;
            }

            var delta = Time.deltaTime;
            clock += delta;
            for (var i = 0; i < pads.Count; i++) {
                Animate(pads[i], delta);
            }

            if (!round.IsRunning || round.IsFinished) {
                return;
            }

            var cars = round.Cars;
            for (var c = 0; c < cars.Count; c++) {
                var car = cars[c];
                if (car.IsOut || car.Position.y > OnFloor || !car.Movement.IsGrounded) {
                    continue;
                }

                for (var i = 0; i < pads.Count; i++) {
                    var pad = pads[i];
                    if (pad.IsGone || clock < pad.ReadyAt) {
                        continue;
                    }

                    var flat = new Vector3(car.Position.x - pad.Centre.x, 0f, car.Position.z - pad.Centre.z);
                    if (flat.sqrMagnitude > settings.PadRadius * settings.PadRadius * TriggerShare * TriggerShare) {
                        continue;
                    }

                    pad.ReadyAt = clock + settings.Cooldown;
                    pad.WobbleStart = clock;
                    Toss(car, pad);
                    break;
                }
            }
        }

        public void Dispose()
        {
            Clear();
        }

        /// <summary>Takes the pads off the floor, so a later cycle can lay new ones.</summary>
        private void Clear()
        {
            if (root != null) {
                UnityEngine.Object.Destroy(root);
                root = null;
            }

            foreach (var mesh in meshes) {
                if (mesh != null) {
                    UnityEngine.Object.Destroy(mesh);
                }
            }

            meshes.Clear();
            pads.Clear();
            clock = 0f;
        }

        /// <summary>
        /// Gives the car the pad's upward speed and a forward speed that lands it on the floor, or straight up when no
        /// forward throw can.
        /// </summary>
        private void Toss(Car car, Pad pad)
        {
            var velocity = car.Movement.Velocity;
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            var up = settings.JumpSpeed;
            var flight = 2f * up / Mathf.Abs(Physics.gravity.y);

            var thrown = flat;
            if (flat.magnitude >= settings.MinSpeed) {
                var heading = flat.normalized;
                var speed = Mathf.Max(flat.magnitude, settings.Range / flight);
                var start = car.Position;
                // Shortens the throw by quarters until the landing place is on the floor with room to spare.
                var share = 1f;
                for (; share > 0f; share -= 0.25f) {
                    var landing = start + heading * (speed * share * flight);
                    if (edge.DistanceToEdge(landing, settings.LandingMargin) >= 0f) {
                        break;
                    }
                }

                thrown = share > 0f ? heading * (speed * share) : Vector3.zero;
            }
            else {
                // Too slow for a forward throw: straight up, keeping whatever drift it has if that lands safely.
                var landing = car.Position + flat * flight;
                if (edge.DistanceToEdge(landing, settings.LandingMargin) < 0f) {
                    thrown = Vector3.zero;
                }
            }

            car.Movement.Push(new Vector3(thrown.x - flat.x, up - velocity.y, thrown.z - flat.z));
            Tosses++;

            effects ??= UnityEngine.Object.FindAnyObjectByType<EffectPlayer>();
            if (effects != null) {
                effects.PlayHit(pad.Centre, thrown.sqrMagnitude > 0.01f ? thrown.normalized : Vector3.up, 0.4f, false,
                    false);
            }

            if (audioService != null) {
                audioService.Play(audioService.Library.Boost, car.IsPlayer ? 0.6f : 0.3f);
            }
        }

        private void Animate(Pad pad, float delta)
        {
            if (!pad.IsGone) {
                if (edge.DistanceToEdge(pad.Centre) < settings.PadRadius * VanishShare) {
                    pad.IsGone = true;
                }
            }

            pad.Presence = Mathf.MoveTowards(pad.Presence, pad.IsGone ? 0f : 1f, VanishSpeed * delta);
            pad.Root.gameObject.SetActive(pad.Presence > 0.01f);
            if (pad.Presence <= 0.01f) {
                return;
            }

            // A little breathing all the time; after a toss a squash that springs back past its size and settles.
            var breath = 0.035f * Mathf.Sin(clock * 3.1f + pad.Phase);
            var since = clock - pad.WobbleStart;
            var spring = since < 1.2f ? -0.5f * Mathf.Exp(-4f * since) * Mathf.Cos(24f * since) : 0f;
            var height = 1f + breath + spring;
            var width = 1f - 0.5f * (breath + spring);
            var presence = Mathf.SmoothStep(0f, 1f, pad.Presence);
            pad.Body.localScale = new Vector3(width * presence, height * presence, width * presence);
        }

        // ---- Look: built once, from code. ----

        private void Build()
        {
            var random = new System.Random(run.SeedOf(unchecked(hazards.Seed ^ 0x4A31E11)));
            var places = PropPlacement.Scatter(random, edge, round.Cars, settings.PadCount, settings.EdgeClearance,
                settings.PadSpacing, settings.SpawnClearance);

            root = new GameObject("Jelly Pads");
            root.transform.SetParent(parent, false);
            var radius = settings.PadRadius;

            // One jelly for every pad, so a pink pudding on the chocolate always means a bounce.
            var profile = new Vector2[Profile.Length];
            for (var i = 0; i < Profile.Length; i++) {
                profile[i] = new Vector2(Profile[i].x * radius, Profile[i].y * Height);
            }

            var bodyMesh = ToyMeshes.Revolved(profile, Slices);
            var shineMesh = ToyMeshes.Ellipsoid(new Vector3(radius * 0.2f, 0.035f, radius * 0.075f), 14, 4);
            meshes.Add(bodyMesh);
            meshes.Add(shineMesh);

            var body = art.Get("jelly_body");
            var shine = art.Get("jelly_shine");
            for (var i = 0; i < places.Count; i++) {
                var pad = new Pad { Centre = places[i], Phase = i * 1.7f };
                pad.Root = new GameObject("Pad " + i).transform;
                pad.Root.SetParent(root.transform, false);
                pad.Root.position = new Vector3(pad.Centre.x, 0.004f, pad.Centre.z);
                pad.Body = ToyMeshes.Part(pad.Root, "Body", bodyMesh, body, Vector3.zero);
                // Outlined before the gloss is added, so the gloss stays a plain white stroke.
                ToyMeshes.Outlined(pad.Body.gameObject);
                ToyMeshes.Part(pad.Body, "Shine", shineMesh, shine, new Vector3(-radius * 0.24f, Height * 0.95f, radius * 0.24f),
                    new Vector3(-8f, -45f, 8f));
                pads.Add(pad);
            }
        }
    }
}
