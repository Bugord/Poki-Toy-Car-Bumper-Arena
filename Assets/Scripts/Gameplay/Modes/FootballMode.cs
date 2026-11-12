using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The FOOTBALL event mode's own logic: two teams push a ball into the other team's goal. A stub so far: the mode's rules already play
    /// (<see cref="GameTuning.FootballSettings.Rules"/>), the bots play as usual and nothing of its own is built.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.Football"/>), silent in every other, as <see cref="Pickups.HotPotato"/> is. The mode's work
    /// goes here, in its settings (<see cref="GameTuning.Football"/>), its HUD (<c>UI/Modes/FootballLook</c>) and its tests
    /// (<c>Tests/PlayMode/Modes/FootballModeTests</c>): see "How to add a mode" in CLAUDE.md.
    /// </para>
    /// </summary>
    public sealed class FootballMode : IRoundMode, IBotSteering, ITickable
    {
        private readonly GameTuning.FootballSettings settings;
        private readonly ArenaRound round;

        public FootballMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.Football;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.Football;
        }

        public SpecialRound Kind => SpecialRound.Football;

        /// <summary>A placeholder card: the car alone, until the mode has a prop of its own to ride on it.</summary>
        public ModeCard Card { get; } = new("suv", 0);

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.FootballSettings Settings => settings;

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
