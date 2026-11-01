using System.Collections.Generic;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// What a mechanic does this frame, as <see cref="MechanicRun.Next"/> sees it.
    /// </summary>
    public enum MechanicStep
    {
        /// <summary>Nothing of it is on the floor, nor should be.</summary>
        Idle,

        /// <summary>Its cycle has begun: clear what is left of it, build it afresh, then run it.</summary>
        Start,

        /// <summary>It acts.</summary>
        Run,

        /// <summary>Its cycle is over: it acts no more, harms nobody, and its look shrinks away.</summary>
        Leave,

        /// <summary>It has shrunk away: clear it.</summary>
        Clear,
    }

    /// <summary>
    /// The life of one variant mechanic over the cycles of <see cref="ArenaVariants"/>, the same for all of them: it
    /// starts when its variant is live and the cars stand (in a round once, as the cars are placed), starts afresh when a
    /// later cycle plays it again, and when its cycle ends it stops at once, shrinks its look away in
    /// <see cref="LeaveTime"/> and is cleared. The mechanic keeps its own state and look; this only says which of those
    /// to do, and shrinks the look.
    /// </summary>
    public sealed class MechanicRun
    {
        /// <summary>Seconds a mechanic's look takes to shrink away once its cycle is over.</summary>
        public const float LeaveTime = 0.3f;

        private readonly ArenaVariants variants;
        private readonly GameTuning.ArenaMechanic mechanic;
        private readonly List<Transform> shrinking = new();
        private readonly List<Vector3> scales = new();
        private float leaving = -1f;

        public MechanicRun(ArenaVariants variants, GameTuning.ArenaMechanic mechanic)
        {
            this.variants = variants;
            this.mechanic = mechanic;
        }

        /// <summary>The cycle the mechanic was built for, -1 when nothing of it is built.</summary>
        public int Cycle { get; private set; } = -1;

        /// <summary>Whether it is built, acting or shrinking away.</summary>
        public bool IsBuilt => Cycle >= 0;

        /// <summary>
        /// Whether it acts: built and its cycle not over. Off, it pushes, slows and threatens nobody. Asked of the variants
        /// too, so it is off the moment the cycle ends, whichever entry point ticks first.
        /// </summary>
        public bool IsRunning => Cycle >= 0 && leaving < 0f && Cycle == variants.Cycle && variants.IsLiveFor(mechanic);

        /// <summary>Whether some cycle of the scene plays the mechanic at all.</summary>
        public bool IsUsed => variants.Uses(mechanic);

        /// <summary>Seconds since its cycle began; the round's clock in a round.</summary>
        public float Clock => variants.Clock;

        /// <summary>The mechanic's own seed turned for the cycle it is built for; the seed itself in the first.</summary>
        public int SeedOf(int seed) => variants.SeedOf(seed);

        /// <summary>
        /// What to do this frame. <paramref name="canStart"/> says whether the mechanic may be built now (the cars stand).
        /// </summary>
        public MechanicStep Next(bool canStart, float deltaTime)
        {
            if (variants.IsLiveFor(mechanic)) {
                if (Cycle == variants.Cycle && leaving < 0f) {
                    return MechanicStep.Run;
                }

                if (!canStart && Cycle < 0) {
                    return MechanicStep.Idle;
                }

                Cycle = variants.Cycle;
                leaving = -1f;
                shrinking.Clear();
                scales.Clear();
                return MechanicStep.Start;
            }

            if (Cycle < 0) {
                return MechanicStep.Idle;
            }

            if (leaving < 0f) {
                leaving = 0f;
                return MechanicStep.Leave;
            }

            leaving += deltaTime;
            if (leaving < LeaveTime) {
                return MechanicStep.Leave;
            }

            Cycle = -1;
            leaving = -1f;
            shrinking.Clear();
            scales.Clear();
            return MechanicStep.Clear;
        }

        /// <summary>
        /// Shrinks the look under <paramref name="root"/> as the mechanic leaves: each object right under it about its own
        /// middle, eased, like the Candy patches peeling off. Called on every frame of <see cref="MechanicStep.Leave"/>.
        /// </summary>
        public void Shrink(Transform root)
        {
            if (root == null || leaving < 0f) {
                return;
            }

            if (shrinking.Count == 0) {
                for (var i = 0; i < root.childCount; i++) {
                    shrinking.Add(root.GetChild(i));
                    scales.Add(root.GetChild(i).localScale);
                }
            }

            var share = 1f - Mathf.Clamp01(leaving / LeaveTime);
            share = share * share * (3f - 2f * share);
            for (var i = 0; i < shrinking.Count; i++) {
                if (shrinking[i] != null) {
                    shrinking[i].localScale = scales[i] * share;
                }
            }
        }
    }
}
