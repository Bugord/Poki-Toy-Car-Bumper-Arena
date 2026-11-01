using System;
using ToyCarArena.Gameplay.Cars;
using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The edge crumbling away over a round, so the scrum at the start tightens into a close duel at the end. From
    /// <see cref="GameTuning.ArenaSettings.CrumbleDelay"/> seconds into the round, every
    /// <see cref="GameTuning.ArenaSettings.CrumbleInterval"/> the outermost wave of the <see cref="ArenaFloor"/> (a ring of
    /// a round floor) starts to shake, and <see cref="GameTuning.ArenaSettings.CrumbleWarning"/> later it falls, until
    /// only the part that never crumbles is left. It runs on the round's clock, so nothing crumbles before the round
    /// begins and no new wave starts once it is decided; a wave already shaking then still falls, so the arena does not
    /// freeze mid-shake under the result.
    /// <para>
    /// In a match, or with <see cref="GameTuning.FloorCycleSettings.Enabled"/>, the floor goes in cycles instead
    /// (<see cref="FloorCycleSchedule"/>): open, shrink (the waves fall: every one in a round, the outer share of them in a
    /// match, <see cref="GameTuning.FloorCycleSettings.MatchFallShare"/>), peak (what is left) and return (the tiles
    /// drop back from above), again and again, so a long match tenses and eases in waves. The cycle and its phase, and an
    /// event at the start of each cycle and of each return, are here for what changes with the cycles. A tile lands only
    /// where no car is caught: a car still in the slab it fills is lifted onto it, keeping its way across the floor.
    /// </para>
    /// </summary>
    public sealed class ArenaCrumble : ITickable
    {
        // A car this far below the top of the floor or less is standing on it, not caught in a tile that lands.
        private const float StandingDepth = 0.05f;

        /// <summary>
        /// A wave of the floor.
        /// </summary>
        public delegate void WaveHandler(CrumbleWave wave);

        /// <summary>
        /// A wave of the floor coming back, landing in <paramref name="landIn"/> seconds.
        /// </summary>
        public delegate void WaveReturnHandler(CrumbleWave wave, float landIn);

        /// <summary>
        /// Raised when a wave starts to shake. From then on it no longer counts as arena: the bots already keep off it.
        /// </summary>
        public event WaveHandler WaveWarning;

        /// <summary>
        /// Raised when a wave falls away, taking any car still on it.
        /// </summary>
        public event WaveHandler WaveFell;

        /// <summary>
        /// Raised when a fallen wave appears over its place, before it drops into it: the warning of the floor coming
        /// back. Only when the floor goes in cycles.
        /// </summary>
        public event WaveReturnHandler WaveReturning;

        /// <summary>
        /// Raised when a wave lands back in its place and holds the cars up again. Only when the floor goes in cycles.
        /// </summary>
        public event WaveHandler WaveLanded;

        /// <summary>
        /// Raised as a cycle of the floor begins, the first as the round begins, with the cycle's number from 0. The
        /// floor is whole. Only when the floor goes in cycles.
        /// </summary>
        public event Action<int> CycleStarted;

        /// <summary>
        /// Raised as the floor of a cycle starts to come back, with the cycle's number, just before its waves appear over
        /// their places. Only when the floor goes in cycles.
        /// </summary>
        public event Action<int> FloorReturning;

        private readonly GameTuning tuning;
        private readonly GameTuning.ArenaSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaFloor floor;
        private readonly bool isCycling;
        private FloorCycleSchedule schedule;

        // What each wave was last told, when the floor goes in cycles: Standing, Warning, Gone or Returning.
        private WaveState[] applied;
        private int returningCycle = -1;

        private float time;
        private int warned;
        private int fallen;

        public ArenaCrumble(GameTuning tuning, GameTuning.ArenaSettings settings, ArenaRound round, ArenaFloor floor)
        {
            this.tuning = tuning;
            this.settings = settings;
            this.round = round;
            this.floor = floor;
            // The one place the switch is read: off, nothing of the cycles runs and the edge crumbles once. A match
            // always cycles, since respawned cars need the floor back; the switch also lets a round cycle for testing.
            this.isCycling = tuning.IsMatch || tuning.FloorCycles.Enabled;
        }

        /// <summary>Whether the floor goes in cycles and comes back, instead of crumbling once.</summary>
        public bool IsCycling => isCycling;

        /// <summary>
        /// When each wave shakes, falls and comes back; null unless the floor goes in cycles, and until the first tick:
        /// it counts the floor's waves, and in the player the scene's components are built only after the entry points.
        /// </summary>
        public FloorCycleSchedule Schedule => schedule;

        /// <summary>The cycle of the floor, from 0; -1 before the round begins or when the floor does not cycle.</summary>
        public int Cycle { get; private set; } = -1;

        /// <summary>The phase of the floor's cycle; open before the round begins and when the floor does not cycle.</summary>
        public FloorPhase Phase { get; private set; }

        /// <summary>Seconds a wave shakes before it falls.</summary>
        public float Warning => schedule?.Warning ?? settings.CrumbleWarning;

        public void Tick()
        {
            if (isCycling) {
                TickCycles();
                return;
            }

            if (round.IsRunning) {
                time = round.Elapsed;
                while (warned < floor.WaveCount && time >= WarningTime(warned)) {
                    floor.Warn(warned);
                    WaveWarning?.Invoke(floor.Wave(warned));
                    warned++;
                }
            }
            else if (fallen < warned) {
                // The round's clock stops with it; a shaking wave finishes on its own.
                time += Time.deltaTime;
            }
            else {
                return;
            }

            while (fallen < warned && time >= WarningTime(fallen) + settings.CrumbleWarning) {
                Fall(fallen);
                fallen++;
            }
        }

        /// <summary>
        /// Seconds into the round the given wave, counted from the outside in, starts to shake.
        /// </summary>
        private float WarningTime(int wave)
        {
            return settings.CrumbleDelay + wave * settings.CrumbleInterval;
        }

        /// <summary>
        /// Brings every wave to where the schedule has it now, step by step, so a long frame still shakes a wave before
        /// it falls and shows it before it lands. Once the round is decided nothing new starts, but a wave shaking still
        /// falls and a wave coming back still lands, so nothing freezes halfway under the result.
        /// </summary>
        private void TickCycles()
        {
            // A match keeps part of the floor standing at the peak, so there is room to come back on; a round cycling for
            // a test still crumbles to the core.
            schedule ??= FloorCycleSchedule.From(tuning.FloorCycles, settings.CrumbleWarning, floor.WaveCount,
                tuning.IsMatch ? tuning.FloorCycles.MatchFallingWaves(floor.WaveCount) : -1);
            applied ??= new WaveState[floor.WaveCount];
            var isRunning = round.IsRunning;
            if (isRunning) {
                time = round.Elapsed;
            }
            else if (IsMoving()) {
                time += Time.deltaTime;
            }
            else {
                return;
            }

            if (isRunning) {
                schedule.CycleTime(time, out var cycle);
                Phase = schedule.PhaseAt(time);
                if (cycle != Cycle) {
                    Cycle = cycle;
                    CycleStarted?.Invoke(cycle);
                }

                if (Phase == FloorPhase.Return && returningCycle != cycle) {
                    returningCycle = cycle;
                    FloorReturning?.Invoke(cycle);
                }
            }

            // The innermost wave first: it is the first to come back, and a round floor needs it under the next one.
            for (var wave = applied.Length - 1; wave >= 0; wave--) {
                Follow(wave, schedule.StateAt(wave, time), isRunning);
            }
        }

        private bool IsMoving()
        {
            foreach (var state in applied) {
                if (state is WaveState.Warning or WaveState.Returning) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takes a wave through its steps until it is where the schedule has it: standing, shaking, fallen, coming back
        /// and standing again.
        /// </summary>
        private void Follow(int wave, WaveState target, bool isRunning)
        {
            for (var steps = 0; steps < 4 && applied[wave] != target; steps++) {
                switch (applied[wave]) {
                    case WaveState.Standing when isRunning:
                        floor.Warn(wave, schedule.Warning);
                        applied[wave] = WaveState.Warning;
                        WaveWarning?.Invoke(floor.Wave(wave));
                        break;
                    case WaveState.Warning:
                        Fall(wave);
                        applied[wave] = WaveState.Gone;
                        break;
                    case WaveState.Gone when isRunning:
                        var landIn = Mathf.Max(0f, schedule.LandTime(Mathf.Max(0, Cycle), wave) - time);
                        floor.Raise(wave, landIn);
                        applied[wave] = WaveState.Returning;
                        WaveReturning?.Invoke(floor.Wave(wave), landIn);
                        break;
                    case WaveState.Returning:
                        Land(wave);
                        applied[wave] = WaveState.Standing;
                        break;
                    default:
                        // Once the round is decided a standing wave stays up and a fallen one stays down.
                        return;
                }
            }
        }

        private void Fall(int wave)
        {
            floor.Drop(wave);
            foreach (var car in round.Cars) {
                if (car.IsOut) {
                    continue;
                }

                if (floor.Contains(wave, car.Position)) {
                    car.NoteRingFall();
                }
            }

            WaveFell?.Invoke(floor.Wave(wave));
        }

        /// <summary>
        /// Puts a wave back and lifts out of it any car caught in the slab it fills: one that slipped into the gap and has
        /// not yet dropped clear of it. The tiles have no collider until this moment, so nothing else can be inside them;
        /// a car above simply lands on them, and one deeper than the slab falls on.
        /// </summary>
        private void Land(int wave)
        {
            floor.Restore(wave);
            foreach (var car in round.Cars) {
                if (car == null || car.IsOut || !IsCaught(car, wave)) {
                    continue;
                }

                var velocity = car.Movement.Velocity;
                velocity.y = 0f;
                var forward = car.transform.forward;
                forward.y = 0f;
                var heading = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;
                var position = car.Position;
                car.Movement.Place(new Vector3(position.x, settings.SpawnHeight, position.z), heading);
                car.Movement.Push(velocity);
            }

            WaveLanded?.Invoke(floor.Wave(wave));
        }

        /// <summary>
        /// Whether a car is in the slab of a wave: below the top of the floor but not so deep that its body is clear of
        /// the slab, with its middle or a corner of its footprint over the wave's tiles.
        /// </summary>
        private bool IsCaught(Car car, int wave)
        {
            var body = car.GetComponent<BoxCollider>();
            var height = body != null ? body.center.y + body.size.y * 0.5f : 1.5f;
            var position = car.Position;
            if (position.y > -StandingDepth || position.y < -(ArenaLayout.SlabThickness + height)) {
                return false;
            }

            if (floor.Contains(wave, position)) {
                return true;
            }

            if (body == null) {
                return false;
            }

            var half = body.size * 0.5f;
            for (var x = -1; x <= 1; x += 2) {
                for (var z = -1; z <= 1; z += 2) {
                    var corner = car.transform.TransformPoint(body.center + new Vector3(x * half.x, -half.y, z * half.z));
                    if (floor.Contains(wave, corner)) {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
