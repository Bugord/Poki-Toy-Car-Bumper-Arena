using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// The look of the pickups, built once in code like the arena objects' (<see cref="ToyMeshes"/>): a chunky token for
    /// each kind, flat unlit colours with the navy outline of the other toys, and the soft marker on the floor under it.
    /// Nothing is imported: the materials are copies of ones <see cref="MechanicArt"/> already holds, tinted here, so the
    /// build carries no new asset. The colours are also the HUD's (orange heavy, purple shockwave, blue shield, gold
    /// turbo). What it builds is freed with it.
    /// </summary>
    internal sealed class PickupArt
    {
        /// <summary>Metres above the floor the tokens float, before they bob.</summary>
        public const float HoverHeight = 1.7f;

        private const float MarkerRadius = 1.5f;
        private const float DecalHeight = 0.012f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Navy = new(0.114f, 0.169f, 0.31f, 1f);

        private readonly List<Object> owned = new();
        private readonly Material[] kindMaterials = new Material[5];
        private readonly MaterialPropertyBlock block = new();
        private readonly Material white;
        private readonly Material navy;
        private readonly Material markerMaterial;
        private readonly Mesh box;
        private readonly Mesh sphere;
        private readonly Mesh prism;
        private readonly Mesh cylinder;
        private readonly Mesh cone;
        private readonly Mesh hexagon;
        private readonly Mesh disc;
        private readonly Mesh ring;

        public PickupArt(MechanicArt art)
        {
            white = Own(new Material(art.Get("jelly_shine")) { name = "pickup_white" });
            white.SetColor(ColorId, Color.white);
            navy = Own(new Material(art.Get("jelly_shine")) { name = "pickup_navy" });
            navy.SetColor(ColorId, Navy);
            foreach (var kind in new[] { PickupKind.Heavy, PickupKind.Shockwave, PickupKind.Shield, PickupKind.Turbo }) {
                var material = Own(new Material(art.Get("jelly_shine")) { name = $"pickup_{kind}" });
                material.SetColor(ColorId, ColorOf(kind));
                kindMaterials[(int)kind] = material;
            }

            markerMaterial = art.Get("mound_ring");
            box = Own(ToyMeshes.Box(Vector3.one));
            sphere = Own(ToyMeshes.Ellipsoid(Vector3.one, 16, 10));
            cylinder = Own(ToyMeshes.Cylinder(1f, 1f, 1f, 12));
            hexagon = Own(ToyMeshes.Cylinder(1f, 1f, 1f, 6));
            cone = Own(ToyMeshes.Cylinder(1f, 0f, 1f, 10));
            prism = box;
            disc = Own(ToyMeshes.Disc(1f, 28));
            ring = Own(ToyMeshes.Ring(0.8f, 1f, 28));
        }

        /// <summary>The kind's colour: the token's, the marker's and the HUD badge's.</summary>
        public static Color ColorOf(PickupKind kind)
        {
            return kind switch
            {
                PickupKind.Heavy => new Color(1f, 0.62f, 0.11f),
                PickupKind.Shockwave => new Color(0.69f, 0.42f, 1f),
                PickupKind.Shield => new Color(0.22f, 0.71f, 1f),
                PickupKind.Turbo => new Color(1f, 0.79f, 0.24f),
                _ => Color.white,
            };
        }

        /// <summary>
        /// Builds a token of every kind under <paramref name="hover"/>, all switched off, indexed by the kind's number
        /// (slot 0 is empty). A token is as tall as a car's roof is high, and spins about its own vertical axis.
        /// </summary>
        public Transform[] BuildTokens(Transform hover)
        {
            var tokens = new Transform[5];
            tokens[(int)PickupKind.Heavy] = BuildHeavy(hover);
            tokens[(int)PickupKind.Shockwave] = BuildShockwave(hover);
            tokens[(int)PickupKind.Shield] = BuildShield(hover);
            tokens[(int)PickupKind.Turbo] = BuildTurbo(hover);
            foreach (var token in tokens) {
                if (token != null) {
                    ToyMeshes.Outlined(token.gameObject);
                    token.gameObject.SetActive(false);
                }
            }

            return tokens;
        }

        /// <summary>The soft marker on the floor: a pale disc and a ring round it, tinted for the kind with <see cref="Tint"/>.</summary>
        public void BuildMarker(FloorPickup slot)
        {
            slot.Marker = new GameObject("Marker").transform;
            slot.Marker.SetParent(slot.Root, false);
            var discPart = ToyMeshes.Decal(slot.Marker, "Disc", disc, markerMaterial, -4);
            discPart.transform.localPosition = Vector3.up * DecalHeight;
            discPart.transform.localScale = new Vector3(MarkerRadius, 1f, MarkerRadius);
            var ringPart = ToyMeshes.Decal(slot.Marker, "Ring", ring, markerMaterial, -3);
            ringPart.transform.localPosition = Vector3.up * (DecalHeight * 1.5f);
            ringPart.transform.localScale = new Vector3(MarkerRadius, 1f, MarkerRadius);
            slot.MarkerDisc = discPart;
            slot.MarkerRing = ringPart;
        }

        /// <summary>Colours the marker for a kind; <paramref name="pulse"/> 0 to 1 brightens the ring.</summary>
        public void Tint(FloorPickup slot, PickupKind kind, float pulse)
        {
            var colour = ColorOf(kind);
            block.SetColor(ColorId, new Color(colour.r, colour.g, colour.b, 0.22f));
            slot.MarkerDisc.SetPropertyBlock(block);
            block.SetColor(ColorId, new Color(colour.r, colour.g, colour.b, Mathf.Lerp(0.5f, 0.95f, pulse)));
            slot.MarkerRing.SetPropertyBlock(block);
        }

        public void Dispose()
        {
            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        // A kettlebell: a fat ball with a handle, the weight in the word.
        private Transform BuildHeavy(Transform parent)
        {
            var root = NewToken(parent, "Heavy");
            var main = kindMaterials[(int)PickupKind.Heavy];
            ToyMeshes.Part(root, "Body", sphere, main, new Vector3(0f, -0.1f, 0f), default, new Vector3(0.78f, 0.66f, 0.78f));
            ToyMeshes.Part(root, "PostLeft", box, navy, new Vector3(-0.3f, 0.7f, 0f), default, new Vector3(0.17f, 0.5f, 0.17f));
            ToyMeshes.Part(root, "PostRight", box, navy, new Vector3(0.3f, 0.7f, 0f), default, new Vector3(0.17f, 0.5f, 0.17f));
            ToyMeshes.Part(root, "Bar", box, navy, new Vector3(0f, 1f, 0f), default, new Vector3(0.78f, 0.17f, 0.17f));
            ToyMeshes.Part(root, "Shine", sphere, white, new Vector3(-0.28f, 0.1f, 0.5f), default, Vector3.one * 0.15f);
            return root;
        }

        // A burst: a ball with spikes in every direction, the ring that goes out from it.
        private Transform BuildShockwave(Transform parent)
        {
            var root = NewToken(parent, "Shockwave");
            var main = kindMaterials[(int)PickupKind.Shockwave];
            ToyMeshes.Part(root, "Core", sphere, main, Vector3.zero, default, Vector3.one * 0.58f);
            // Each cone's base sits on the core and its tip points out along the axis the rotation turns +Y to.
            var reach = 0.82f;
            var spikes = new (Vector3 direction, Vector3 euler)[]
            {
                (Vector3.right, new Vector3(0f, 0f, -90f)),
                (Vector3.left, new Vector3(0f, 0f, 90f)),
                (Vector3.forward, new Vector3(90f, 0f, 0f)),
                (Vector3.back, new Vector3(-90f, 0f, 0f)),
                (Vector3.up, Vector3.zero),
                (Vector3.down, new Vector3(180f, 0f, 0f)),
            };
            foreach (var (direction, euler) in spikes) {
                ToyMeshes.Part(root, "Spike", cone, white, direction * reach, euler, new Vector3(0.26f, 0.6f, 0.26f));
            }

            return root;
        }

        // A shield: a blue hexagonal coin standing on edge with a white one proud of each face.
        private Transform BuildShield(Transform parent)
        {
            var root = NewToken(parent, "Shield");
            var main = kindMaterials[(int)PickupKind.Shield];
            var upright = new Vector3(90f, 0f, 0f);
            ToyMeshes.Part(root, "Coin", hexagon, main, Vector3.zero, upright, new Vector3(0.9f, 0.32f, 0.9f));
            ToyMeshes.Part(root, "FaceFront", hexagon, white, new Vector3(0f, 0f, 0.19f), upright, new Vector3(0.55f, 0.1f, 0.55f));
            ToyMeshes.Part(root, "FaceBack", hexagon, white, new Vector3(0f, 0f, -0.19f), upright, new Vector3(0.55f, 0.1f, 0.55f));
            return root;
        }

        // A rocket: a body with a nose, three fins and a flame under it.
        private Transform BuildTurbo(Transform parent)
        {
            var root = NewToken(parent, "Turbo");
            var main = kindMaterials[(int)PickupKind.Turbo];
            var flame = kindMaterials[(int)PickupKind.Heavy];
            ToyMeshes.Part(root, "Body", cylinder, main, new Vector3(0f, 0.05f, 0f), default, new Vector3(0.4f, 0.85f, 0.4f));
            ToyMeshes.Part(root, "Nose", cone, navy, new Vector3(0f, 0.72f, 0f), default, new Vector3(0.4f, 0.6f, 0.4f));
            ToyMeshes.Part(root, "Band", cylinder, navy, new Vector3(0f, -0.18f, 0f), default, new Vector3(0.43f, 0.14f, 0.43f));
            for (var i = 0; i < 3; i++) {
                var turn = Quaternion.Euler(0f, i * 120f, 0f);
                ToyMeshes.Part(root, "Fin", prism, white, turn * new Vector3(0.34f, -0.34f, 0f), new Vector3(0f, i * 120f, 0f),
                    new Vector3(0.34f, 0.36f, 0.07f));
            }

            ToyMeshes.Part(root, "Flame", cone, flame, new Vector3(0f, -0.72f, 0f), new Vector3(180f, 0f, 0f),
                new Vector3(0.28f, 0.55f, 0.28f));
            return root;
        }

        private static Transform NewToken(Transform parent, string name)
        {
            var token = new GameObject(name).transform;
            token.SetParent(parent, false);
            return token;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
