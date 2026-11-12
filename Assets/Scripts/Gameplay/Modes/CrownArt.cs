using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Rendering;
using UnityEngine;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The look of the CROWN mode's crown in the arena: the crown hat's own model (<see cref="TopperModels"/>, on the
    /// cars' atlas and outlined), made big enough to read from a phone's camera, and a gold ring on the floor under it.
    /// It drops from the sky onto its spot at GO, bobs and turns there while loose, then floats over its holder's roof
    /// (above any hat the car wears), blinking while the holder cannot lose it yet, with the ring following the holder.
    /// <para>
    /// The crown's root is named <see cref="PropName"/> and carries its size below it, so the mode card's picture
    /// (<c>ModeCardArt</c>) copies it as it shows; the ring lies apart from it.
    /// </para>
    /// </summary>
    internal sealed class CrownArt
    {
        /// <summary>The name of the crown's root in the scene.</summary>
        public const string PropName = "Mode Crown";

        // Metres across the crown is drawn, held and loose: the hat's own crown is a third of that.
        private const float HeldWidth = 1.5f;
        private const float LooseScale = 1.35f;
        // Metres above the holder's middle the crown's bottom floats: over the roof and the tallest hat.
        private const float HeldHeight = 1.95f;
        private const float LooseHeight = 0.9f;
        private const float DropHeight = 16f;
        private const float HeldBob = 0.1f;
        private const float LooseBob = 0.25f;
        // Degrees a second the crown turns, held and loose.
        private const float HeldTurn = 70f;
        private const float LooseTurn = 120f;
        // The ring on the floor: its radius under the holder and on the spot, and its height over the floor.
        private const float HolderRing = 1.9f;
        private const float SpotRing = 2.2f;
        private const float DecalHeight = 0.013f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Gold = new(1f, 0.79f, 0.24f);
        private static readonly Color White = new(1f, 0.97f, 0.85f);

        private readonly List<Object> owned = new();
        private readonly MaterialPropertyBlock block = new();
        private readonly Transform root;
        private readonly Transform size;
        private readonly Transform ring;
        private readonly MeshRenderer ringFill;
        private readonly MeshRenderer ringEdge;
        private float turn;

        public CrownArt(MechanicArt art, CarLook look, Transform parent)
        {
            root = new GameObject(PropName).transform;
            root.SetParent(parent, false);
            size = new GameObject("Size").transform;
            size.SetParent(root, false);
            if (look != null) {
                var crown = CarTopper.Build("crown", size, look.ModelMaterial, look.Toppers, out _);
                if (crown != null) {
                    Fit(crown);
                    Outline.Apply(root.gameObject);
                }
            }

            ring = new GameObject("Crown Ring").transform;
            ring.SetParent(parent, false);
            var decal = art.Get("mound_ring");
            ringFill = ToyMeshes.Decal(ring, "Fill", Own(ToyMeshes.Disc(1f, 48)), decal, -4);
            ringFill.transform.localPosition = Vector3.up * DecalHeight;
            ringEdge = ToyMeshes.Decal(ring, "Edge", Own(ToyMeshes.Ring(0.84f, 1f, 56)), decal, -3);
            ringEdge.transform.localPosition = Vector3.up * (DecalHeight * 1.5f);
            Tint(0.22f, 0.95f);
            root.gameObject.SetActive(false);
            ring.gameObject.SetActive(false);
        }

        /// <summary>Where the crown is now: its bottom's middle.</summary>
        public Vector3 CrownPosition => root.position;

        /// <summary>
        /// Moves the crown and its ring for this frame. Nothing shows before the round begins.
        /// </summary>
        /// <param name="dropShare">How far through its drop the crown is, 0 at GO and 1 on the floor.</param>
        /// <param name="isProtected">Whether the holder cannot lose it yet: the crown blinks.</param>
        public void Animate(CrownMode.CrownPhase phase, bool hasBegun, Car holder, Vector3 spot, float dropShare,
            bool isProtected)
        {
            var isShown = hasBegun && (phase != CrownMode.CrownPhase.Held || holder != null);
            root.gameObject.SetActive(isShown);
            ring.gameObject.SetActive(isShown);
            if (!isShown) {
                return;
            }

            var time = Time.time;
            var isHeld = phase == CrownMode.CrownPhase.Held;
            turn += Time.deltaTime * (isHeld ? HeldTurn : LooseTurn);
            if (isHeld) {
                var growth = Mathf.Max(1f, holder.Growth);
                var position = holder.Position;
                var blink = isProtected && Mathf.Repeat(time * 8f, 1f) < 0.5f;
                root.position = position + Vector3.up * (HeldHeight * growth + Mathf.Sin(time * 5f) * HeldBob);
                root.rotation = Quaternion.Euler(0f, turn, 0f);
                size.localScale = Vector3.one * (blink ? 1.12f : 1f);
                ring.position = new Vector3(position.x, 0f, position.z);
                var pulse = HolderRing * (1f + 0.06f * Mathf.Sin(time * 6f));
                ring.localScale = new Vector3(pulse, 1f, pulse);
                Tint(blink ? 0.32f : 0.2f, 0.95f);
                return;
            }

            float height;
            if (phase == CrownMode.CrownPhase.Dropping) {
                // Falls faster and faster, as a dropped thing does, and lands at its floating height.
                height = Mathf.Lerp(DropHeight, LooseHeight, dropShare * dropShare);
            }
            else {
                height = LooseHeight + (0.5f + 0.5f * Mathf.Sin(time * 3f)) * LooseBob;
            }

            root.position = spot + Vector3.up * height;
            root.rotation = Quaternion.Euler(0f, turn, 0f);
            size.localScale = Vector3.one * LooseScale;
            ring.position = new Vector3(spot.x, 0f, spot.z);
            // While it drops, the ring draws in to where it lands; loose, it breathes.
            var radius = phase == CrownMode.CrownPhase.Dropping
                ? Mathf.Lerp(SpotRing * 1.8f, SpotRing, dropShare)
                : SpotRing * (1f + 0.08f * Mathf.Sin(time * 4f));
            ring.localScale = new Vector3(radius, 1f, radius);
            Tint(phase == CrownMode.CrownPhase.Dropping ? 0.12f : 0.28f, 0.95f);
        }

        /// <summary>Hides the crown hat a car may wear: the round's crown is the only one.</summary>
        public static void HideCrownHat(Car car)
        {
            foreach (var part in car.GetComponentsInChildren<Transform>(true)) {
                if (part.name == "Topper crown") {
                    part.gameObject.SetActive(false);
                }
            }
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

        // The model scaled to the crown's width, its bottom on the root and its middle over it.
        private static void Fit(Transform crown)
        {
            var renderers = crown.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) {
                return;
            }

            var toRoot = crown.parent.worldToLocalMatrix;
            var bounds = new Bounds(toRoot.MultiplyPoint3x4(renderers[0].bounds.center), Vector3.zero);
            foreach (var renderer in renderers) {
                var b = renderer.bounds;
                for (var i = 0; i < 8; i++) {
                    var corner = new Vector3(i % 2 == 0 ? b.min.x : b.max.x, i / 2 % 2 == 0 ? b.min.y : b.max.y,
                        i / 4 == 0 ? b.min.z : b.max.z);
                    bounds.Encapsulate(toRoot.MultiplyPoint3x4(corner));
                }
            }

            var width = Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            var scale = HeldWidth / width;
            crown.localScale *= scale;
            crown.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) * scale;
        }

        private void Tint(float fill, float edge)
        {
            block.SetColor(ColorId, new Color(Gold.r, Gold.g, Gold.b, fill));
            ringFill.SetPropertyBlock(block);
            block.SetColor(ColorId, new Color(White.r * Gold.r, White.g * Gold.g, White.b * Gold.b, edge));
            ringEdge.SetPropertyBlock(block);
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
