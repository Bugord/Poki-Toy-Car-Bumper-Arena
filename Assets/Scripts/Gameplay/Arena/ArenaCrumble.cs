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
    /// </summary>
    public sealed class ArenaCrumble : ITickable
    {
        /// <summary>
        /// A wave of the floor.
        /// </summary>
        public delegate void WaveHandler(CrumbleWave wave);

        /// <summary>
        /// Raised when a wave starts to shake. From then on it no longer counts as arena: the bots already keep off it.
        /// </summary>
        public event WaveHandler WaveWarning;

        /// <summary>
        /// Raised when a wave falls away, taking any car still on it.
        /// </summary>
        public event WaveHandler WaveFell;

        private readonly GameTuning.ArenaSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaFloor floor;

        private float time;
        private int warned;
        private int fallen;

        public ArenaCrumble(GameTuning.ArenaSettings settings, ArenaRound round, ArenaFloor floor)
        {
            this.settings = settings;
            this.round = round;
            this.floor = floor;
        }

        public void Tick()
        {
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
                floor.Drop(fallen);
                foreach (var car in round.Cars) {
                    if (car.IsOut) {
                        continue;
                    }

                    if (floor.Contains(fallen, car.Position)) {
                        car.NoteRingFall();
                    }
                }

                WaveFell?.Invoke(floor.Wave(fallen));
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
    }
}
