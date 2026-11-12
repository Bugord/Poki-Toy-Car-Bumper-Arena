using ToyCarArena.Gameplay.Arena;
using ToyCarArena.Gameplay.Bots;
using ToyCarArena.Gameplay.Round;
using VContainer.Unity;

namespace ToyCarArena.Gameplay.Modes
{
    /// <summary>
    /// The ITEM FRENZY event mode's own logic: boxes everywhere, items all the time. A stub so far: the mode's rules already play
    /// (<see cref="GameTuning.ItemFrenzySettings.Rules"/>), the bots play as usual and nothing of its own is built.
    /// <para>
    /// Registered in <see cref="GameplayScope"/> always, active only in a round of its kind
    /// (<see cref="SpecialRound.ItemFrenzy"/>), silent in every other, as <see cref="Pickups.HotPotato"/> is. The mode's work
    /// goes here, in its settings (<see cref="GameTuning.ItemFrenzy"/>), its HUD (<c>UI/Modes/ItemFrenzyLook</c>) and its tests
    /// (<c>Tests/PlayMode/Modes/ItemFrenzyModeTests</c>): see "How to add a mode" in CLAUDE.md.
    /// </para>
    /// </summary>
    public sealed class ItemFrenzyMode : IRoundMode, IBotSteering, ITickable
    {
        private readonly GameTuning.ItemFrenzySettings settings;
        private readonly ArenaRound round;

        public ItemFrenzyMode(GameTuning tuning, RoundRules rules, ArenaRound round)
        {
            this.settings = tuning.ItemFrenzy;
            this.round = round;
            this.IsActive = rules.Kind == SpecialRound.ItemFrenzy;
        }

        public SpecialRound Kind => SpecialRound.ItemFrenzy;

        public bool IsActive { get; }

        /// <summary>The mode's numbers.</summary>
        public GameTuning.ItemFrenzySettings Settings => settings;

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
