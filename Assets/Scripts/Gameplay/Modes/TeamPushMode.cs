using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The TEAM PUSH event mode's own logic: two teams push each other off; the last team on the floor wins. A stub so far: the mode's rules already play
    /// (<see cref="GameTuning.TeamPushSettings.Rules"/>), the bots play as usual and nothing of its own is built.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.TeamPush"/>), silent in every other, as <see cref="Pickups.HotPotato"/> is. The mode's work
    /// goes here, in its settings (<see cref="GameTuning.TeamPush"/>), its HUD (<c>UI/Modes/TeamPushLook</c>) and its tests
    /// (<c>Tests/PlayMode/Modes/TeamPushModeTests</c>): see "How to add a mode" in CLAUDE.md.
    /// </para>
    /// </summary>
    public sealed class TeamPushMode : IRoundMode, IBotSteering, ITickable
    {
        private readonly GameTuning.TeamPushSettings settings;
        private readonly ArenaRound round;

        public TeamPushMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.TeamPush;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.TeamPush;
        }

        public SpecialRound Kind => SpecialRound.TeamPush;

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.TeamPushSettings Settings => settings;

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
