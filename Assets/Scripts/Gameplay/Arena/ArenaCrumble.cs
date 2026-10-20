using ToyCarArena.Gameplay.Round;
using UnityEngine;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Arena
{
    /// <summary>
    /// The edge crumbling away over a round, so the scrum at the start tightens into a close duel at the end. From
    /// <see cref="GameTuning.ArenaSettings.CrumbleDelay"/> seconds into the round, every
    /// <see cref="GameTuning.ArenaSettings.CrumbleInterval"/> the outermost ring of the <see cref="ArenaFloor"/> starts to
    /// shake, and <see cref="GameTuning.ArenaSettings.CrumbleWarning"/> later it falls, until only the solid middle of
    /// <see cref="GameTuning.ArenaSettings.MinRadius"/> is left. It runs on the round's clock, so nothing crumbles before
    /// the round begins and no new ring starts once it is decided; a ring already shaking then still falls, so the arena
    /// does not freeze mid-shake under the result.
    /// </summary>
    public sealed class ArenaCrumble : ITickable
    {
        /// <summary>
        /// A ring of the floor, between two distances from the middle, in metres.
        /// </summary>
        public delegate void RingHandler(float innerRadius, float outerRadius);

        /// <summary>
        /// Raised when a ring starts to shake. From then on it no longer counts as arena: the bots already keep off it.
        /// </summary>
        public event RingHandler RingWarning;

        /// <summary>
        /// Raised when a ring falls away, taking any car still on it.
        /// </summary>
        public event RingHandler RingFell;

        private readonly GameTuning.ArenaSettings settings;
        private readonly ArenaRound round;
        private readonly ArenaFloor floor;

        private float time;
        private int warned;
        private int fallen;

        public ArenaCrumble(GameTuning tuning, ArenaRound round, ArenaFloor floor)
        {
            this.settings = tuning.Arena;
            this.round = round;
            this.floor = floor;
        }

        public void Tick()
        {
            if (round.IsRunning) {
                time = round.Elapsed;
                while (warned < floor.RingCount && time >= WarningTime(warned)) {
                    floor.Warn(warned);
                    RingWarning?.Invoke(floor.RingInnerRadius(warned), floor.RingOuterRadius(warned));
                    warned++;
                }
            }
            else if (fallen < warned) {
                // The round's clock stops with it; a shaking ring finishes on its own.
                time += Time.deltaTime;
            }
            else {
                return;
            }

            while (fallen < warned && time >= WarningTime(fallen) + settings.CrumbleWarning) {
                floor.Drop(fallen);
                RingFell?.Invoke(floor.RingInnerRadius(fallen), floor.RingOuterRadius(fallen));
                fallen++;
            }
        }

        /// <summary>
        /// Seconds into the round the given ring, counted from the outside in, starts to shake.
        /// </summary>
        private float WarningTime(int ring)
        {
            return settings.CrumbleDelay + ring * settings.CrumbleInterval;
        }
    }
}
