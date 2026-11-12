using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The GOLD RUSH event mode's own logic: coins fall on the floor; the car with the most when time runs out wins. A stub so far: the mode's rules already play
    /// (<see cref="GameTuning.GoldRushSettings.Rules"/>), the bots play as usual and nothing of its own is built.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.GoldRush"/>), silent in every other, as <see cref="Pickups.HotPotato"/> is. The mode's work
    /// goes here, in its settings (<see cref="GameTuning.GoldRush"/>), its HUD (<c>UI/Modes/GoldRushLook</c>) and its tests
    /// (<c>Tests/PlayMode/Modes/GoldRushModeTests</c>): see "How to add a mode" in CLAUDE.md.
    /// </para>
    /// </summary>
    public sealed class GoldRushMode : IRoundMode, IBotSteering, ITickable
    {
        private readonly GameTuning.GoldRushSettings settings;
        private readonly ArenaRound round;

        public GoldRushMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.GoldRush;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.GoldRush;
        }

        public SpecialRound Kind => SpecialRound.GoldRush;

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.GoldRushSettings Settings => settings;

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
