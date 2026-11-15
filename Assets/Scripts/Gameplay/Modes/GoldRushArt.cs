using System.Collections.Generic;
using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Rendering;
using UnityEngine;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// One gold coin of the GOLD RUSH round, and where it is in its life: free in the pool, flying (dropped from the sky,
    /// shaken out of a car or spilled by a fall: nobody takes it in the air), lying on the floor to be taken, or leaving
    /// (taken by a car, or faded). <see cref="GoldRushMode"/> owns and reuses it; <see cref="GoldRushArt"/> draws it.
    /// </summary>
    public sealed class GoldCoin
    {
        internal enum Stage
        {
            Free,
            Flying,
            Live,
            Leaving,
        }

        internal Stage State;

        /// <summary>The spot on the floor under it (where it will land, while it flies).</summary>
        public Vector3 Position { get; internal set; }

        /// <summary>Whether it lies on the floor to be taken.</summary>
        public bool IsLive => State == Stage.Live;

        /// <summary>Whether it is on its way down: dropped or thrown, not yet to be taken.</summary>
        public bool IsFlying => State == Stage.Flying;

        /// <summary>Whether it came out of a car (a bump or a fall), not from a burst.</summary>
        public bool IsSpilled { get; internal set; }

        /// <summary>Seconds it has lain on the floor.</summary>
        public float Age { get; internal set; }

        // Seconds it lies there before it fades.
        internal float Lifetime;

        // Where it flies from, how long and how high.
        internal Vector3 From;
        internal float FlightTime;
        internal float FlightHeight;

        // The car that may not take it back for a moment, and until when (the clock of Time.time).
        internal Car Locked;
        internal float LockedUntil;

        // The car that took it, which it flies to while it leaves, and whether it was taken rather than faded.
        internal Car TakenBy;
        internal bool WasTaken;

        // Seconds into the flight or the leaving, and a phase so the coins do not all spin and bob together.
        internal float StageTime;
        internal float Phase;

        // The coin's look, built once by the art and reused.
        internal Transform Root;
        internal Transform Spin;
    }

    /// <summary>
    /// How the GOLD RUSH coins look: a pool of chunky gold coins built in code from the toys' meshes (a thick gold disc
    /// with a paler face on both sides, outlined in navy like the other toys), standing on their edge over the floor,
    /// spinning and bobbing; dropped coins fall in from the sky, shaken ones fly in an arc, taken ones shoot up into the
    /// car and shrink, fading ones blink and shrink. The car with the most coins wears a pile of them on its roof
    /// (<see cref="PileName"/>, the card's prop). No physics, no colliders, no shadows; nothing made per frame.
    /// </summary>
    internal sealed class GoldRushArt
    {
        /// <summary>The name of the coin pile the leader wears: the card's prop.</summary>
        public const string PileName = "Coin Pile";

        // Metres: the coin's radius and thickness, its face's radius, and how high its middle hovers over the floor.
        public const float CoinRadius = 0.55f;
        private const float CoinThickness = 0.17f;
        private const float FaceRadius = 0.37f;
        private const float Hover = 0.75f;
        private const float SpinSpeed = 200f;
        private const float BobHeight = 0.1f;
        private const float PopIn = 0.22f;
        private const float DropHeight = 8f;
        private const float TakeTime = 0.18f;
        private const float FadeTime = 0.25f;
        private const float RoofGap = 0.12f;
        private const float PileSpin = 50f;

        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color FaceGold = new(1f, 0.91f, 0.56f, 1f);

        private readonly MechanicArt art;
        private readonly Transform parent;
        private readonly List<Object> owned = new();
        private Transform root;
        private Transform pile;
        private Car pileCar;
        private float pileRoof;
        private float time;

        public GoldRushArt(MechanicArt art, Transform parent)
        {
            this.art = art;
            this.parent = parent;
        }

        /// <summary>The pile on the leader's roof, for the tests.</summary>
        public Transform Pile => pile;

        /// <summary>Builds the pool's looks into <paramref name="coins"/> and the leader's pile.</summary>
        public void Build(IReadOnlyList<GoldCoin> coins)
        {
            root = new GameObject("Gold Rush").transform;
            root.SetParent(parent, false);
            var gold = art.Get("train_yellow");
            // The face a paler gold: a copy of the toys' yellow, owned and destroyed with the round.
            var face = Own(new Material(gold) { name = "Coin Face" });
            face.SetColor(ColorId, FaceGold);
            var rim = Own(ToyMeshes.Cylinder(CoinRadius, CoinRadius, CoinThickness, 20));
            var faceMesh = Own(ToyMeshes.Cylinder(FaceRadius, FaceRadius, CoinThickness + 0.05f, 16));

            foreach (var coin in coins) {
                coin.Root = new GameObject("Coin").transform;
                coin.Root.SetParent(root, false);
                coin.Spin = new GameObject("Spin").transform;
                coin.Spin.SetParent(coin.Root, false);
                // Standing on its edge: the cylinder's axis turned to lie flat, the coin spun round the up axis.
                var body = ToyMeshes.Part(coin.Spin, "Rim", rim, gold, Vector3.zero, new Vector3(90f, 0f, 0f));
                ToyMeshes.Part(body, "Face", faceMesh, face, Vector3.zero);
                coin.Root.gameObject.SetActive(false);
            }

            pile = new GameObject(PileName).transform;
            pile.SetParent(root, false);
            // Three coins lying flat in a crooked stack, one standing on them: a toy heap of gold.
            var flat = ToyMeshes.Part(pile, "Coin", rim, gold, new Vector3(0f, CoinThickness * 0.5f, 0f));
            ToyMeshes.Part(flat, "Face", faceMesh, face, Vector3.zero);
            flat = ToyMeshes.Part(pile, "Coin", rim, gold, new Vector3(0.12f, CoinThickness * 1.5f, -0.06f),
                new Vector3(0f, 0f, 4f));
            ToyMeshes.Part(flat, "Face", faceMesh, face, Vector3.zero);
            flat = ToyMeshes.Part(pile, "Coin", rim, gold, new Vector3(-0.04f, CoinThickness * 2.5f, 0.05f),
                new Vector3(-5f, 0f, -3f));
            ToyMeshes.Part(flat, "Face", faceMesh, face, Vector3.zero);
            var standing = ToyMeshes.Part(pile, "Coin", rim, gold,
                new Vector3(0.05f, CoinThickness * 3f + CoinRadius * 0.95f, 0f), new Vector3(90f, 0f, -12f));
            ToyMeshes.Part(standing, "Face", faceMesh, face, Vector3.zero);
            // A second one across it: the pile spins, and a lone standing coin seen on its edge read as a pin.
            standing = ToyMeshes.Part(pile, "Coin", rim, gold,
                new Vector3(-0.06f, CoinThickness * 3f + CoinRadius * 0.85f, 0.03f), new Vector3(90f, 90f, 10f));
            ToyMeshes.Part(standing, "Face", faceMesh, face, Vector3.zero);
            // Scaled away rather than switched off: the card art finds the pile by its name.
            pile.localScale = Vector3.zero;

            Outline.Apply(root.gameObject);
        }

        /// <summary>Draws every coin where its life has it, and the pile on <paramref name="leader"/>'s roof (none on null).</summary>
        public void Tick(IReadOnlyList<GoldCoin> coins, Car leader, float deltaTime, float blinkTime)
        {
            time += deltaTime;
            for (var i = 0; i < coins.Count; i++) {
                Show(coins[i], blinkTime);
            }

            ShowPile(leader);
        }

        public void Dispose()
        {
            if (root != null) {
                Object.Destroy(root.gameObject);
            }

            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        private void Show(GoldCoin coin, float blinkTime)
        {
            var isShown = coin.State != GoldCoin.Stage.Free;
            if (coin.Root.gameObject.activeSelf != isShown) {
                coin.Root.gameObject.SetActive(isShown);
            }

            if (!isShown) {
                return;
            }

            var spin = (time * SpinSpeed + coin.Phase * 360f) % 360f;
            var scale = 1f;
            Vector3 position;
            switch (coin.State) {
                case GoldCoin.Stage.Flying: {
                    var t = Mathf.Clamp01(coin.StageTime / Mathf.Max(0.01f, coin.FlightTime));
                    position = Vector3.Lerp(coin.From, coin.Position, t) + Vector3.up * (Hover
                        + 4f * coin.FlightHeight * t * (1f - t));
                    // A thrown coin tumbles faster than one lying there.
                    spin = (spin + coin.StageTime * 900f) % 360f;
                    break;
                }
                case GoldCoin.Stage.Live: {
                    var bob = Mathf.Sin((time + coin.Phase * 3f) * 3.2f) * BobHeight;
                    position = coin.Position + Vector3.up * (Hover + bob);
                    if (coin.Age < PopIn) {
                        // Lands with a squash past full size, then settles.
                        var t = coin.Age / PopIn;
                        scale = 1f + 0.3f * Mathf.Sin(t * Mathf.PI);
                    }

                    var left = coin.Lifetime - coin.Age;
                    if (left < blinkTime) {
                        // Blinks faster as it is about to go: hidden, not faded (an outline under a fade reads dirty).
                        var rate = Mathf.Lerp(9f, 3f, left / Mathf.Max(0.01f, blinkTime));
                        if (Mathf.Repeat(time * rate, 1f) > 0.62f) {
                            scale = 0f;
                        }
                    }

                    break;
                }
                default: {
                    if (coin.WasTaken && coin.TakenBy != null) {
                        // Shoots up into the car that took it and shrinks into it.
                        var t = Mathf.Clamp01(coin.StageTime / TakeTime);
                        var to = coin.TakenBy.Position + Vector3.up * 1.2f;
                        position = Vector3.Lerp(coin.Position + Vector3.up * Hover, to, t) + Vector3.up * (t * 0.8f);
                        scale = 1f - t * 0.85f;
                    }
                    else {
                        var t = Mathf.Clamp01(coin.StageTime / FadeTime);
                        position = coin.Position + Vector3.up * Hover;
                        scale = 1f - t;
                    }

                    break;
                }
            }

            coin.Root.localPosition = position;
            coin.Spin.localRotation = Quaternion.Euler(0f, spin, 0f);
            coin.Spin.localScale = Vector3.one * scale;
        }

        private void ShowPile(Car leader)
        {
            var isShown = leader != null && !leader.IsOut && leader.Position.y > -0.3f;
            if (!isShown) {
                if (pile.localScale != Vector3.zero) {
                    pile.localScale = Vector3.zero;
                }

                return;
            }

            if (leader != pileCar) {
                pileCar = leader;
                pileRoof = RoofOf(leader);
            }

            var growth = leader.Growth;
            var bob = Mathf.Sin(time * 4f) * 0.06f;
            pile.SetPositionAndRotation(leader.Position + Vector3.up * ((pileRoof + RoofGap + bob) * growth),
                Quaternion.Euler(0f, time * PileSpin, 0f));
            pile.localScale = Vector3.one * growth;
        }

        /// <summary>Metres from the car's pivot to the top of its model: where the pile sits.</summary>
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

        private T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }
    }
}
