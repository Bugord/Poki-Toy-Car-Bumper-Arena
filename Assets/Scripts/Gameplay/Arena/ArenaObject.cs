using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The shared life of an arena mechanic that is an object on the floor (a crab, mounds, a top, a pencil): registered in
    /// every scope, it does nothing and builds nothing unless the variant playing names its mechanic
    /// (<see cref="ArenaVariants"/>); otherwise it builds its look when the cars are placed (a simulation sets the bots'
    /// seed after the scene is loaded, and the chance comes from it), and steps every frame after. In a match whose cycles
    /// turn the variants it stops when its cycle ends (no longer a hazard, its look shrinking away), is cleared, and is
    /// built afresh, with its state reset (<see cref="ResetState"/>), if a later cycle plays it again. What it builds goes
    /// under one root and is freed with it.
    /// </summary>
    public abstract class ArenaObject : ITickable, IDisposable
    {
        private readonly MechanicRun run;
        private readonly Transform parent;
        private readonly List<Object> owned = new();

        protected ArenaObject(GameTuning.ArenaMechanic mechanic, ArenaMap map, ArenaRound round, IArenaEdge edge,
            ArenaHazards hazards, ArenaVariants variants, Transform parent)
        {
            this.run = new MechanicRun(variants, mechanic);
            this.parent = parent;
            Map = map;
            Round = round;
            Edge = edge;
            Hazards = hazards;
        }

        protected ArenaMap Map { get; }
        protected ArenaRound Round { get; }
        protected IArenaEdge Edge { get; }
        protected ArenaHazards Hazards { get; }

        /// <summary>The object everything of the mechanic hangs under; null until built.</summary>
        protected Transform Root { get; private set; }

        /// <summary>Whether anything of the mechanic is on the floor: built, acting or shrinking away.</summary>
        public bool IsBuilt => run.IsBuilt;

        /// <summary>Whether the mechanic acts: built for the variant playing and its cycle not over.</summary>
        protected bool IsRunning => run.IsRunning;

        /// <summary>The round clock runs: things may start. It stops with the round, and nothing new starts after.</summary>
        protected bool IsClockRunning => Round.IsRunning && !Round.IsFinished;

        /// <summary>
        /// Seconds since the mechanic's cycle began, which its delays count from: the round's clock in a round.
        /// </summary>
        protected float Clock => run.Clock;

        /// <summary>The mechanic's own seed turned for its cycle; the seed itself in a round and the first cycle.</summary>
        protected int SeedOf(int seed) => run.SeedOf(seed);

        public void Tick()
        {
            switch (run.Next(Round.IsPlaced, Time.deltaTime)) {
                case MechanicStep.Idle:
                    return;
                case MechanicStep.Leave:
                    // Out of the bots' way at once; only the look lingers while it shrinks.
                    if (this is IArenaHazard hazard) {
                        Hazards.Remove(hazard);
                    }

                    run.Shrink(Root);
                    return;
                case MechanicStep.Clear:
                    Clear();
                    return;
                case MechanicStep.Start:
                    Clear();
                    Root = new GameObject(GetType().Name).transform;
                    Root.SetParent(parent, false);
                    Build();
                    break;
            }

            Step(Time.deltaTime);
        }

        public void Dispose()
        {
            Clear();
        }

        /// <summary>
        /// Takes everything of the mechanic off the floor and out of the bots' way, and puts its state back to how it
        /// was before it was ever built.
        /// </summary>
        private void Clear()
        {
            if (this is IArenaHazard hazard) {
                Hazards.Remove(hazard);
            }

            if (Root != null) {
                Object.Destroy(Root.gameObject);
                Root = null;
            }

            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
            ResetState();
        }

        /// <summary>
        /// Puts the mechanic's state back to how it was before it was ever built, so it can be built again for a later
        /// cycle. Its look is already gone; a counter the bots tell threats apart by keeps counting.
        /// </summary>
        protected abstract void ResetState();

        /// <summary>Builds the look and the state, when the cars are placed and again for each cycle that plays it.</summary>
        protected abstract void Build();

        /// <summary>One frame of the mechanic.</summary>
        protected abstract void Step(float deltaTime);

        /// <summary>Remembers a mesh or material instance made for the mechanic, to free it with it.</summary>
        protected T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        protected static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
