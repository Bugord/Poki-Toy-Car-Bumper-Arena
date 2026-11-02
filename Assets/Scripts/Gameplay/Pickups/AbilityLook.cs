using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Effects;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Pickups
{
    /// <summary>
    /// What a car shows while an ability runs, so the others can see it: a see-through bubble round a car with a shield
    /// (it blinks as it runs out), a dark pad pressing on the floor under a mega car, and flame puffs behind a car with a
    /// turbo. Flat stickers and a transparent bubble carry no outline of their own. The numbers are looks, not rules.
    /// Everything is built on the first car that needs it and freed with the look.
    /// </summary>
    internal sealed class AbilityLook
    {
        // Seconds before it ends that a shield starts to blink, and how fast, in blinks a second.
        private const float BlinkTime = 2f;
        private const float BlinkRate = 6f;
        private const float FlameInterval = 0.07f;
        private const float MinFlameSpeed = 2f;
        private const float PadHeight = 0.02f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        private sealed class Slot
        {
            public Transform Bubble;
            public Transform Pad;
            public MeshRenderer PadRenderer;
            public float Flame;
        }

        private readonly MechanicArt art;
        private readonly Transform parent;
        private readonly EffectPlayer effects;
        private readonly List<Slot> slots = new();
        private readonly List<Object> owned = new();
        private readonly MaterialPropertyBlock block = new();
        private Mesh bubbleMesh;
        private Mesh padMesh;
        private float time;

        public AbilityLook(MechanicArt art, Transform parent, EffectPlayer effects)
        {
            this.art = art;
            this.parent = parent;
            this.effects = effects;
        }

        public void Tick(IReadOnlyList<Car> cars, float deltaTime)
        {
            time += deltaTime;
            for (var i = 0; i < cars.Count; i++) {
                var car = cars[i];
                var isIn = !car.IsOut;
                var isShielded = isIn && car.IsRunning(ItemKind.Shield);
                var isMega = isIn && car.IsRunning(ItemKind.Mega);
                var isTurbo = isIn && car.IsRunning(ItemKind.Turbo);
                if (!isShielded && !isMega && !isTurbo && (i >= slots.Count || slots[i] == null)) {
                    continue;
                }

                var slot = SlotFor(i);
                ShowBubble(slot, car, isShielded);
                ShowPad(slot, car, isMega);
                if (isTurbo && effects != null) {
                    slot.Flame -= deltaTime;
                    if (slot.Flame <= 0f && car.Movement.Velocity.sqrMagnitude > MinFlameSpeed * MinFlameSpeed) {
                        slot.Flame = FlameInterval;
                        effects.PlayTurboFlame(car);
                    }
                }
            }
        }

        public void Dispose()
        {
            foreach (var slot in slots) {
                if (slot != null && slot.Bubble != null) {
                    Object.Destroy(slot.Bubble.gameObject);
                    Object.Destroy(slot.Pad.gameObject);
                }
            }

            slots.Clear();
            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        private void ShowBubble(Slot slot, Car car, bool isShown)
        {
            if (isShown && car.TimeLeft(ItemKind.Shield) < BlinkTime) {
                // Off for half of each blink, so a shield about to go is hard to miss.
                isShown = Mathf.Repeat(time * BlinkRate, 1f) < 0.5f;
            }

            if (slot.Bubble.gameObject.activeSelf != isShown) {
                slot.Bubble.gameObject.SetActive(isShown);
            }

            if (!isShown) {
                return;
            }

            var pulse = 1f + Mathf.Sin(time * 6f) * 0.04f;
            slot.Bubble.SetPositionAndRotation(car.Position + Vector3.up * 0.6f, Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f));
            slot.Bubble.localScale = Vector3.one * pulse;
        }

        private void ShowPad(Slot slot, Car car, bool isShown)
        {
            // Only while the car is over the floor: a pad does not follow it down a fall.
            isShown &= car.Position.y > -0.3f;
            if (slot.Pad.gameObject.activeSelf != isShown) {
                slot.Pad.gameObject.SetActive(isShown);
            }

            if (!isShown) {
                return;
            }

            var position = car.Position;
            var pulse = 1f + Mathf.Sin(time * 5f) * 0.07f;
            slot.Pad.SetPositionAndRotation(new Vector3(position.x, PadHeight, position.z),
                Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f));
            var size = 1.9f * pulse * car.Growth;
            slot.Pad.localScale = new Vector3(size, 1f, size);
        }

        private Slot SlotFor(int index)
        {
            while (slots.Count <= index) {
                slots.Add(null);
            }

            if (slots[index] != null) {
                return slots[index];
            }

            bubbleMesh ??= Own(ToyMeshes.Ellipsoid(new Vector3(1.55f, 1.15f, 2.1f), 20, 12));
            padMesh ??= Own(ToyMeshes.Disc(1f, 24));
            var slot = new Slot();

            var bubble = ToyMeshes.Part(parent, "Shield Bubble", bubbleMesh, art.Get("mound_ring"), Vector3.zero);
            var bubbleRenderer = bubble.GetComponent<MeshRenderer>();
            var colour = PickupArt.ColorOf(ItemKind.Shield);
            block.SetColor(ColorId, new Color(colour.r, colour.g, colour.b, 0.32f));
            bubbleRenderer.SetPropertyBlock(block);
            bubble.gameObject.SetActive(false);
            slot.Bubble = bubble;

            var pad = ToyMeshes.Decal(parent, "Mega Pad", padMesh, art.Get("mound_ring"), -5);
            block.SetColor(ColorId, new Color(0.114f, 0.169f, 0.31f, 0.4f));
            pad.SetPropertyBlock(block);
            pad.gameObject.SetActive(false);
            slot.Pad = pad.transform;
            slot.PadRenderer = pad;

            slots[index] = slot;
            return slot;
        }

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
