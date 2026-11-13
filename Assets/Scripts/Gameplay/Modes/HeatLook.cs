using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using ToyCarArena.Rendering;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// How hot an OVERHEAT car is, in the world, by steps the eye reads without a number: a warm car steams (white puffs
    /// rising off its roof, more and bigger as it heats), a red-hot one also wears a crown of toy flames that flicker, a
    /// red glow pulses on the floor under it and it leaves flame puffs behind as it drives. Solid pieces carry the navy
    /// outline of the other toys; no particle system and no new shader. The numbers are looks, not rules. Built once
    /// for every car of the round, freed with the scene.
    /// </summary>
    internal sealed class HeatLook
    {
        private const int Puffs = 3;
        private const float PuffCycle = 1.1f;
        private const float FlameInterval = 0.11f;
        private const float MinFlameSpeed = 2f;
        private const float GlowHeight = 0.02f;
        private const float RoofGap = 0.18f;
        // The crown seen from the arena's camera, high and looking down: big enough to read as flames on a phone.
        private const float FlameSize = 1.7f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color Glow = new(1f, 0.23f, 0.19f, 1f);

        private sealed class Slot
        {
            public Transform Steam;
            public Transform[] Puffs;
            public Transform Flame;
            public MeshRenderer Glow;
            public float Roof;
            public float FlameTimer;
            public float Phase;
        }

        private readonly MechanicArt art;
        private readonly Transform parent;
        private readonly EffectPlayer effects;
        private readonly GameTuning.OverheatSettings settings;
        private readonly List<Slot> slots = new();
        private readonly List<Object> owned = new();
        private readonly MaterialPropertyBlock block = new();
        private Transform root;
        private float time;

        public HeatLook(MechanicArt art, Transform parent, EffectPlayer effects, GameTuning.OverheatSettings settings)
        {
            this.art = art;
            this.parent = parent;
            this.effects = effects;
            this.settings = settings;
        }

        public void Build(IReadOnlyList<Car> cars)
        {
            root = new GameObject("Overheat").transform;
            root.SetParent(parent, false);
            var puff = Own(ToyMeshes.Ellipsoid(new Vector3(0.26f, 0.22f, 0.26f), 12, 8));
            var outer = Own(ToyMeshes.Revolved(FlameProfile(1f), 14));
            var inner = Own(ToyMeshes.Revolved(FlameProfile(0.55f), 12));
            var glow = Own(ToyMeshes.Disc(1f, 28));
            for (var i = 0; i < cars.Count; i++) {
                slots.Add(BuildSlot(cars[i], i, puff, outer, inner, glow));
            }

            Outline.Apply(root.gameObject);
            // The glow is a flat sticker under the car, and steam is soft: no outline of their own.
            foreach (var slot in slots) {
                slot.Glow.renderingLayerMask &= ~Outline.Layer;
                foreach (var steam in slot.Steam.GetComponentsInChildren<Renderer>(true)) {
                    steam.renderingLayerMask &= ~Outline.Layer;
                }
            }
        }

        /// <summary>The car went red-hot: a burst of sparks off it, so the moment is seen.</summary>
        public void Flare(int index)
        {
            if (effects == null || index < 0 || index >= slots.Count) {
                return;
            }

            var slot = slots[index];
            effects.PlayKnockoutCredit(slot.Flame.position);
        }

        public void Tick(IReadOnlyList<Car> cars, float[] heats, float deltaTime)
        {
            time += deltaTime;
            for (var i = 0; i < slots.Count && i < cars.Count; i++) {
                var car = cars[i];
                var slot = slots[i];
                var isShown = car != null && !car.IsOut && car.Position.y > -0.3f;
                var heat = isShown ? heats[i] : 0f;
                ShowSteam(slot, car, heat);
                ShowFlame(slot, car, heat, deltaTime);
            }
        }

        public void Dispose()
        {
            if (root != null) {
                Object.Destroy(root.gameObject);
            }

            slots.Clear();
            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        private void ShowSteam(Slot slot, Car car, float heat)
        {
            // A red-hot car wears its flames instead: the puffs would hide them.
            var isWarm = heat >= settings.Warm && heat < settings.Hot;
            if (slot.Steam.gameObject.activeSelf != isWarm) {
                slot.Steam.gameObject.SetActive(isWarm);
            }

            if (!isWarm) {
                return;
            }

            var share = Mathf.InverseLerp(settings.Warm, 1f, heat);
            var growth = car.Growth;
            slot.Steam.position = car.Position + Vector3.up * (slot.Roof * growth);
            // Faster and bigger as it heats: lazy wisps when warm, a boil near red-hot.
            var cycle = PuffCycle * Mathf.Lerp(1f, 0.6f, share);
            var size = Mathf.Lerp(0.6f, 1.1f, share) * growth;
            for (var p = 0; p < slot.Puffs.Length; p++) {
                var t = Mathf.Repeat((time + slot.Phase) / cycle + (float)p / slot.Puffs.Length, 1f);
                var drift = Mathf.Sin((p * 2.1f + slot.Phase) * 3f) * 0.35f;
                slot.Puffs[p].localPosition = new Vector3(drift * t, RoofGap + t * 1.4f * growth, drift * 0.5f * t);
                slot.Puffs[p].localScale = Vector3.one * (Mathf.Sin(t * Mathf.PI) * size);
            }
        }

        private void ShowFlame(Slot slot, Car car, float heat, float deltaTime)
        {
            var isHot = heat >= settings.Hot;
            var growth = isHot ? car.Growth : 1f;
            if (isHot) {
                var flicker = 1f + Mathf.Sin(time * 17f + slot.Phase * 5f) * 0.08f + Mathf.Sin(time * 29f) * 0.05f;
                var lean = Mathf.Sin(time * 9f + slot.Phase) * 6f;
                slot.Flame.SetPositionAndRotation(car.Position + Vector3.up * (slot.Roof * growth),
                    Quaternion.Euler(0f, 0f, lean));
                var size = FlameSize * growth;
                slot.Flame.localScale = new Vector3(size, flicker * size, size);
            }
            else if (slot.Flame.localScale != Vector3.zero) {
                // Scaled away rather than switched off: the card art finds the crown by its name.
                slot.Flame.localScale = Vector3.zero;
            }

            if (slot.Glow.enabled != isHot) {
                slot.Glow.enabled = isHot;
            }

            if (!isHot) {
                return;
            }

            var position = car.Position;
            var pulse = 0.5f + 0.5f * Mathf.Sin(time * 7f + slot.Phase);
            slot.Glow.transform.SetPositionAndRotation(new Vector3(position.x, GlowHeight, position.z), Quaternion.identity);
            slot.Glow.transform.localScale = new Vector3(1.7f, 1f, 1.7f) * (growth * (0.95f + pulse * 0.1f));
            block.SetColor(ColorId, new Color(Glow.r, Glow.g, Glow.b, Mathf.Lerp(0.28f, 0.5f, pulse)));
            slot.Glow.SetPropertyBlock(block);
            if (effects == null) {
                return;
            }

            slot.FlameTimer -= deltaTime;
            if (slot.FlameTimer <= 0f && car.Movement.Velocity.sqrMagnitude > MinFlameSpeed * MinFlameSpeed) {
                slot.FlameTimer = FlameInterval;
                effects.PlayTurboFlame(car);
            }
        }

        private Slot BuildSlot(Car car, int index, Mesh puff, Mesh outer, Mesh inner, Mesh glow)
        {
            var slot = new Slot { Roof = RoofOf(car), Phase = index * 0.37f };
            slot.Steam = new GameObject("Heat Steam").transform;
            slot.Steam.SetParent(root, false);
            slot.Puffs = new Transform[Puffs];
            for (var p = 0; p < Puffs; p++) {
                slot.Puffs[p] = ToyMeshes.Part(slot.Steam, "Puff", puff, art.Get("train_white"), Vector3.zero);
            }

            slot.Steam.gameObject.SetActive(false);

            // Three toy flames on the roof, red with a yellow heart, the middle one tallest.
            slot.Flame = new GameObject(OverheatMode.FlameName).transform;
            slot.Flame.SetParent(root, false);
            AddFlame(slot.Flame, outer, inner, new Vector3(0f, RoofGap, 0f), 0f, 1f);
            AddFlame(slot.Flame, outer, inner, new Vector3(0.32f, RoofGap - 0.04f, -0.05f), -18f, 0.68f);
            AddFlame(slot.Flame, outer, inner, new Vector3(-0.32f, RoofGap - 0.04f, -0.05f), 18f, 0.68f);
            slot.Flame.localScale = Vector3.zero;

            slot.Glow = ToyMeshes.Decal(root, "Heat Glow", glow, art.Get("mound_ring"), -5);
            slot.Glow.enabled = false;
            return slot;
        }

        private void AddFlame(Transform crown, Mesh outer, Mesh inner, Vector3 position, float tilt, float size)
        {
            // The crown faces the camera, which looks along +z: the yellow heart pokes out of the red on that side.
            var flame = ToyMeshes.Part(crown, "Flame", outer, art.Get("train_red"), position, new Vector3(0f, 0f, tilt),
                Vector3.one * size);
            ToyMeshes.Part(flame, "Heart", inner, art.Get("train_yellow"), new Vector3(0f, 0.02f, -0.13f));
        }

        /// <summary>Metres from the car's pivot to the top of its model: where the steam and the flames sit.</summary>
        private static float RoofOf(Car car)
        {
            var look = car.GetComponent<CarLook>();
            var model = look != null ? look.Model : null;
            if (model == null) {
                return 0.9f;
            }

            var top = float.NegativeInfinity;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) {
                top = Mathf.Max(top, renderer.bounds.max.y);
            }

            return float.IsInfinity(top) ? 0.9f : Mathf.Max(0.4f, top - car.Position.y);
        }

        /// <summary>A toy flame turned round its axis: a round belly and a pointed tip, <paramref name="scale"/> of the big one.</summary>
        private static List<Vector2> FlameProfile(float scale)
        {
            var profile = new List<Vector2> {
                new(0f, 0f), new(0.16f, 0.03f), new(0.24f, 0.12f), new(0.25f, 0.24f), new(0.21f, 0.38f),
                new(0.14f, 0.52f), new(0.07f, 0.66f), new(0.02f, 0.76f), new(0f, 0.8f),
            };
            for (var i = 0; i < profile.Count; i++) {
                profile[i] *= scale;
            }

            return profile;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
