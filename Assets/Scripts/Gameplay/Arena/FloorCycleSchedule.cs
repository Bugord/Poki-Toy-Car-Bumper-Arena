using UnityEngine;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// Where a cycle of the floor is.
    /// </summary>
    public enum FloorPhase
    {
        /// <summary>The whole floor.</summary>
        Open,

        /// <summary>The waves shake and fall, one after another.</summary>
        Shrink,

        /// <summary>Only the core is left.</summary>
        Peak,

        /// <summary>The tiles hang over their places and drop back into them.</summary>
        Return,
    }

    /// <summary>
    /// When each wave of the floor shakes, falls and comes back in the cycles of <see cref="GameTuning.FloorCycleSettings"/>,
    /// as a plain function of the round's clock: the floor at any moment is well defined, a frame that skips a step still
    /// lands on the right floor, and a test can ask for any time. Every wave of the arena falls within the shrink phase,
    /// however many there are, and they come back in the reverse order, the innermost first: a round floor holds a car up
    /// only over the unbroken run of rings from the middle, so an outer ring landing first would be drawn but hold nothing.
    /// After the last cycle the floor stays as the last cycle leaves it, whole.
    /// </summary>
    public sealed class FloorCycleSchedule
    {
        private readonly int cycles;
        private readonly float open;
        private readonly float shrink;
        private readonly float peak;
        private readonly float returnTime;
        private readonly float returnWarning;
        private readonly int waveCount;
        private readonly float step;
        private readonly float first;

        /// <param name="cycles">Cycles the floor goes through; 0 for ever.</param>
        /// <param name="warning">Seconds a wave shakes before it falls; cut to the shrink phase if longer.</param>
        /// <param name="waveCount">Waves the arena's floor crumbles in.</param>
        public FloorCycleSchedule(int cycles, float open, float shrink, float peak, float returnTime,
            float returnWarning, float warning, int waveCount)
        {
            this.cycles = Mathf.Max(0, cycles);
            this.open = Mathf.Max(0f, open);
            this.shrink = Mathf.Max(0.01f, shrink);
            this.peak = Mathf.Max(0f, peak);
            this.returnTime = Mathf.Max(0.01f, returnTime);
            this.returnWarning = Mathf.Clamp(returnWarning, 0f, this.returnTime);
            this.waveCount = Mathf.Max(0, waveCount);
            this.Warning = Mathf.Clamp(warning, 0f, this.shrink);
            // The first wave shakes as the shrink begins and the last one falls as it ends; a lone wave falls as it ends,
            // so the peak is as long as it says.
            this.step = this.waveCount > 1 ? (this.shrink - this.Warning) / (this.waveCount - 1) : 0f;
            this.first = this.waveCount > 1 ? 0f : this.shrink - this.Warning;
        }

        public static FloorCycleSchedule From(GameTuning.FloorCycleSettings settings, float warning, int waveCount)
        {
            return new FloorCycleSchedule(settings.Cycles, settings.Open, settings.Shrink, settings.Peak,
                settings.ReturnTime, settings.ReturnWarning, warning, waveCount);
        }

        /// <summary>Seconds a wave shakes before it falls.</summary>
        public float Warning { get; }

        /// <summary>Seconds of one whole cycle.</summary>
        public float Length => open + shrink + peak + returnTime;

        /// <summary>Cycles the floor goes through; 0 for ever.</summary>
        public int Cycles => cycles;

        /// <summary>Seconds into a cycle the shrink begins.</summary>
        public float ShrinkAt => open;

        /// <summary>Seconds into a cycle the last wave has fallen and only the core is left.</summary>
        public float PeakAt => open + shrink;

        /// <summary>Seconds into a cycle the tiles appear over their places.</summary>
        public float ReturnAt => open + shrink + peak;

        /// <summary>Seconds into a cycle the given wave starts to shake.</summary>
        public float WarnAt(int wave)
        {
            return open + first + wave * step;
        }

        /// <summary>Seconds into a cycle the given wave falls.</summary>
        public float FallAt(int wave)
        {
            return WarnAt(wave) + Warning;
        }

        /// <summary>
        /// Seconds into a cycle the given wave lands back in place: the innermost (last to fall) first, as the warning
        /// ends, the rest evenly after it, all before the cycle ends.
        /// </summary>
        public float LandAt(int wave)
        {
            var order = waveCount - 1 - wave;
            var spread = returnTime - returnWarning;
            return ReturnAt + returnWarning + (waveCount > 0 ? spread * order / waveCount : 0f);
        }

        /// <summary>
        /// The cycle a moment of the round is in and how far into it, in seconds. Past the last cycle it is the end of
        /// the last one, so the floor stays as that left it.
        /// </summary>
        public float CycleTime(float time, out int cycle)
        {
            time = Mathf.Max(0f, time);
            var length = Length;
            if (cycles > 0 && time >= cycles * length) {
                cycle = cycles - 1;
                return length;
            }

            cycle = Mathf.FloorToInt(time / length);
            return time - cycle * length;
        }

        /// <summary>The phase of the floor at a moment of the round.</summary>
        public FloorPhase PhaseAt(float time)
        {
            var into = CycleTime(time, out _);
            if (into < ShrinkAt) {
                return FloorPhase.Open;
            }

            if (into < PeakAt) {
                return FloorPhase.Shrink;
            }

            return into < ReturnAt ? FloorPhase.Peak : FloorPhase.Return;
        }

        /// <summary>
        /// Where a wave should be at a moment of the round. A fallen wave is <see cref="WaveState.Gone"/>, whether its
        /// tiles are still tumbling or not.
        /// </summary>
        public WaveState StateAt(int wave, float time)
        {
            var into = CycleTime(time, out _);
            if (into < WarnAt(wave)) {
                return WaveState.Standing;
            }

            if (into < FallAt(wave)) {
                return WaveState.Warning;
            }

            if (into < ReturnAt) {
                return WaveState.Gone;
            }

            return into < LandAt(wave) ? WaveState.Returning : WaveState.Standing;
        }

        /// <summary>The moment of the round the given wave lands in the given cycle.</summary>
        public float LandTime(int cycle, int wave)
        {
            return cycle * Length + LandAt(wave);
        }
    }
}
