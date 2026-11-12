using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The CROWN event mode's own logic: the car that holds the crown the longest wins; a bump steals it. A stub so far: the mode's rules already play
    /// (<see cref="GameTuning.CrownSettings.Rules"/>), the bots play as usual and nothing of its own is built.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.Crown"/>), silent in every other, as <see cref="Pickups.HotPotato"/> is. The mode's work
    /// goes here, in its settings (<see cref="GameTuning.Crown"/>), its HUD (<c>UI/Modes/CrownLook</c>) and its tests
    /// (<c>Tests/PlayMode/Modes/CrownModeTests</c>): see "How to add a mode" in CLAUDE.md.
    /// </para>
    /// </summary>
    public sealed class CrownMode : IRoundMode, IBotSteering, ITickable
    {
        private readonly GameTuning.CrownSettings settings;
        private readonly ArenaRound round;

        public CrownMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.Crown;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.Crown;
        }

        public SpecialRound Kind => SpecialRound.Crown;

        /// <summary>A placeholder card: the car alone, until the mode has a prop of its own to ride on it.</summary>
        public ModeCard Card { get; } = new("racer", 4);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.CrownSettings Settings => settings;

        public void Tick()
        {
            if (!IsActive || !round.IsRunning) {
                return;
            }

            // The mode's own play goes here: points by ArenaRound.AddPoints in a timed mode.
        }

        /// <summary>The bots play the usual round until the mode's own bot play is written.</summary>
        public bool SteerBot(Bot bot, IArenaEdge edge, float deltaTime) => false;
    }
}
