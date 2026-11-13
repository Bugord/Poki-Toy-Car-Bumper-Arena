using System;
using System.Collections.Generic;
using ToyCarArena.Gameplay.Round;
using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The variant of the arena playing now: its floor and its mechanic. A round plays the variant the rotation picked
    /// (<see cref="ArenaMap.Variant"/>) from start to end. A long match turns through the arena's variants with the floor's
    /// cycles, so the long match keeps changing under the cars: cycle <c>k</c> plays the variant <c>(start + k) mod n</c> in
    /// the arena's order. The mechanic of a cycle stops as the floor starts to come back
    /// (<see cref="ArenaCrumble.FloorReturning"/>), the tiles that come back already wear the next variant's floor and the
    /// rest of the floor takes it as the last of them lands, and the next mechanic starts with the next cycle
    /// (<see cref="ArenaCrumble.CycleStarted"/>), its delays counted from there (<see cref="Clock"/>). The last cycle keeps
    /// its variant to the end of the match. The mechanics ask <see cref="IsLive"/> and <see cref="Cycle"/> each frame
    /// (<see cref="MechanicRun"/>) rather than hear the events, so the order the entry points tick in does not matter.
    /// </summary>
    public sealed class ArenaVariants : IDisposable
    {
        // Mixes the cycle into a mechanic's seed: nothing for the first cycle, so a round draws what it always drew.
        private const int CycleSalt = 0x2F6B1D3;

        private readonly ArenaMap map;
        private readonly ArenaRound round;
        private readonly ArenaCrumble crumble;
        private readonly ArenaFloor floor;
        private readonly List<GameTuning.VariantSettings> variants = new();
        private readonly int start;
        private readonly int cycles;
        private readonly bool isRotating;

        // The floor the kept part takes as the last returning wave lands; null when nothing waits.
        private Material pendingFloor;

        public ArenaVariants(GameTuning tuning, ArenaMap map, ArenaRound round, ArenaCrumble crumble, ArenaFloor floor,
            RoundRules rules)
        {
            this.map = map;
            this.round = round;
            this.crumble = crumble;
            this.floor = floor;
            this.cycles = tuning.FloorCycles.Cycles;
            foreach (var settings in tuning.Maps) {
                if (settings.Id == map.Id) {
                    variants.AddRange(settings.Variants);
                    break;
                }
            }

            this.start = Mathf.Max(0, variants.FindIndex(variant => variant.Id == map.Variant));
            Variant = map.Variant;
            // A mode may play without mechanics (HOT POTATO has one rule, the bomb): no mechanic of the variant builds or
            // acts, only its floor shows.
            Mechanic = rules.Mode.HasMechanics ? map.Mechanic : GameTuning.ArenaMechanic.None;
            // Only a round whose floor goes in cycles of its own (a match) turns the variants: a round, even one cycling
            // its floor for a test, keeps its variant.
            this.isRotating = rules.Mode.Floor == FloorRule.Cycles && crumble.IsCycling && variants.Count > 1;
            if (isRotating) {
                crumble.CycleStarted += OnCycleStarted;
                crumble.FloorReturning += OnFloorReturning;
                crumble.WaveLanded += OnWaveLanded;
            }
        }

        /// <summary>
        /// Raised with the new variant's id as a cycle of a match starts on another variant; never in a round.
        /// </summary>
        public event Action<string> VariantChanged;

        /// <summary>Whether the variants turn with the cycles: a match on an arena of more than one variant.</summary>
        public bool IsRotating => isRotating;

        /// <summary>The variant playing now.</summary>
        public string Variant { get; private set; }

        /// <summary>The mechanic of the variant playing now.</summary>
        public GameTuning.ArenaMechanic Mechanic { get; private set; }

        /// <summary>
        /// The cycle the variant plays in, from 0; it stays 0 in a round. A mechanic built for another cycle than this
        /// one builds again.
        /// </summary>
        public int Cycle { get; private set; }

        /// <summary>
        /// Whether the mechanic of the variant acts: from the scene's start, and in a match from the start of its cycle
        /// to the floor starting to come back.
        /// </summary>
        public bool IsLive { get; private set; } = true;

        /// <summary>Seconds into the round the variant's cycle began; 0 in a round and in the first cycle.</summary>
        public float StartTime { get; private set; }

        /// <summary>
        /// Seconds of the round since the variant's cycle began: what a mechanic counts its delays from. The round's
        /// own clock in a round.
        /// </summary>
        public float Clock => round.Elapsed - StartTime;

        /// <summary>Whether <paramref name="mechanic"/> is the one acting now.</summary>
        public bool IsLiveFor(GameTuning.ArenaMechanic mechanic) => IsLive && Mechanic == mechanic;

        /// <summary>
        /// Whether some cycle of this scene plays <paramref name="mechanic"/>: a mechanic nobody plays never builds, nor
        /// keeps anything of itself switched on.
        /// </summary>
        public bool Uses(GameTuning.ArenaMechanic mechanic)
        {
            if (!isRotating) {
                return Mechanic == mechanic;
            }

            var played = cycles > 0 ? Mathf.Min(cycles, variants.Count) : variants.Count;
            for (var k = 0; k < played; k++) {
                if (variants[(start + k) % variants.Count].Mechanic == mechanic) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A seed of the cycle from a mechanic's own seed: the seed itself in the first cycle, so a round and the first
        /// cycle of a match draw what they always drew, and another for every later cycle, so a simulation repeats them.
        /// </summary>
        public int SeedOf(int seed) => unchecked(seed ^ (Cycle * CycleSalt));

        public void Dispose()
        {
            if (isRotating) {
                crumble.CycleStarted -= OnCycleStarted;
                crumble.FloorReturning -= OnFloorReturning;
                crumble.WaveLanded -= OnWaveLanded;
            }
        }

        private GameTuning.VariantSettings VariantOf(int cycle) => variants[(start + cycle) % variants.Count];

        private bool HasCycleAfter(int cycle) => cycles <= 0 || cycle + 1 < cycles;

        private Material FloorOf(GameTuning.VariantSettings variant)
        {
            return map.HasTheme ? map.Theme.FloorMaterialOf(variant.Id) : null;
        }

        private void OnCycleStarted(int cycle)
        {
            // The first cycle is the one the scene was built for, and its mechanic runs already.
            if (cycle <= 0 || cycle == Cycle) {
                return;
            }

            var variant = VariantOf(cycle);
            Cycle = cycle;
            Variant = variant.Id;
            Mechanic = variant.Mechanic;
            StartTime = cycle * crumble.Schedule.Length;
            IsLive = true;
            VariantChanged?.Invoke(Variant);
        }

        private void OnFloorReturning(int cycle)
        {
            // The last cycle has no next variant: it plays on to the end of the match.
            if (!HasCycleAfter(cycle)) {
                return;
            }

            IsLive = false;
            var material = FloorOf(VariantOf(cycle + 1));
            if (material == null) {
                return;
            }

            // The fallen tiles come back already in the next look; the rest takes it as the last of them lands.
            floor.SetMaterial(material, true);
            pendingFloor = material;
        }

        private void OnWaveLanded(CrumbleWave wave)
        {
            // Wave 0, the outermost, lands last.
            if (pendingFloor == null || wave.Index != 0) {
                return;
            }

            floor.SetMaterial(pendingFloor, false);
            pendingFloor = null;
        }
    }
}
