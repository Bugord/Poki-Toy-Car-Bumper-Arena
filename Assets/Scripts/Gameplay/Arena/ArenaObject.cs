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
    /// every scope, it does nothing and builds nothing unless the map's variant names its mechanic; otherwise it builds its
    /// look once, when the cars are placed (a simulation sets the bots' seed after the scene is loaded, and the chance
    /// comes from it), and steps every frame after. What it builds goes under one root and is freed with it.
    /// </summary>
    public abstract class ArenaObject : ITickable, IDisposable
    {
        private readonly GameTuning.ArenaMechanic mechanic;
        private readonly Transform parent;
        private readonly List<Object> owned = new();
        private bool isBuilt;

        protected ArenaObject(GameTuning.ArenaMechanic mechanic, ArenaMap map, ArenaRound round, IArenaEdge edge,
            ArenaHazards hazards, Transform parent)
        {
            this.mechanic = mechanic;
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

        /// <summary>This map's variant has the mechanic.</summary>
        protected bool IsOurs => Map.Mechanic == mechanic;

        /// <summary>The round clock runs: things may start. It stops with the round, and nothing new starts after.</summary>
        protected bool IsClockRunning => Round.IsRunning && !Round.IsFinished;

        public void Tick()
        {
            if (!IsOurs) {
                return;
            }

            if (!isBuilt) {
                if (!Round.IsPlaced) {
                    return;
                }

                isBuilt = true;
                Root = new GameObject(GetType().Name).transform;
                Root.SetParent(parent, false);
                Build();
            }

            Step(Time.deltaTime);
        }

        public virtual void Dispose()
        {
            if (Root != null) {
                Object.Destroy(Root.gameObject);
            }

            foreach (var item in owned) {
                if (item != null) {
                    Object.Destroy(item);
                }
            }

            owned.Clear();
        }

        /// <summary>Builds the look and the state; once, when the cars are placed.</summary>
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
